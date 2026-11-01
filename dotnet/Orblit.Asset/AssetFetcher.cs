namespace Orblit.Asset;

/// <summary>
/// Fetches assets over the network, once each, in the order they are wanted. Everything here exists because a
/// download is slow, unreliable and somebody else's: it is <b>deduplicated</b>, so two parts of a scene asking
/// for the same texture cost one download; <b>queued</b>, so what is on screen goes first; <b>revalidated</b>,
/// so a warm start costs a round trip rather than the bytes; <b>resumed</b>, so a connection that drops at
/// ninety per cent does not start again; <b>verified</b> against the manifest, so a corrupted or substituted
/// file is caught before anything decodes it; and <b>cached</b>, so the second launch and the offline launch
/// both work.
/// </summary>
/// <remarks>
/// It never writes a temporary file. Bytes go from the socket to memory to the content store, and an asset is
/// handed over as the bytes it is. That is partly because the web has no files to write, and partly because a
/// half-written temporary that outlives the process is the oldest way to serve a truncated asset and swear it
/// is fine. Safe to call from any thread: one lock guards the queue and every download in it.
/// </remarks>
public sealed class AssetFetcher : IDisposable
{
    // A doubling wait passes what Task.Delay takes after about 25 attempts and what a TimeSpan holds after about 40.
    internal static readonly TimeSpan LongestWait = TimeSpan.FromDays(1);

    private readonly Random _jitter;
    private readonly Dictionary<WebUrl, FetchDownload> _live = [];
    private readonly List<FetchDownload> _waiting = [];
    private int _running;
    private long _arrived;
    private bool _closed;

    // F2: only the origin and the transport are required. The rest are named at the call.
    public AssetFetcher(
        AssetOrigin origin,
        IAssetTransport transport,
        IContentStore? store = null,
        IFetchRecords? records = null,
        AssetManifest? manifest = null,
        int concurrent = 4,
        int attempts = 3,
        TimeSpan? backoff = null,
        Random? jitter = null,
        TimeProvider? clock = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(concurrent);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attempts);
        Origin = origin;
        Transport = transport;
        Store = store ?? new MemoryContentStore();
        Records = records ?? new MemoryFetchRecords();
        Manifest = manifest;
        Concurrent = concurrent;
        Attempts = attempts;
        Backoff = backoff ?? TimeSpan.FromMilliseconds(300);
        _jitter = jitter ?? Random.Shared;
        Clock = clock ?? TimeProvider.System;
    }

    /// <summary>Where assets are served from, and what is allowed.</summary>
    public AssetOrigin Origin { get; }

    public IAssetTransport Transport { get; }

    /// <summary>Where fetched bytes are kept, by hash.</summary>
    public IContentStore Store { get; }

    /// <summary>What each URL was, last time it was fetched.</summary>
    public IFetchRecords Records { get; }

    /// <summary>
    /// What each asset should hash to. Without one, nothing is verified, and the fetcher says so rather than
    /// pretending the bytes are vouched for.
    /// </summary>
    public AssetManifest? Manifest { get; }

    /// <summary>
    /// How many downloads run at once. Four. More connections do not make a link faster once it is full, and
    /// they do make the first thing finish later, which on a loading screen is the number anybody actually sees.
    /// </summary>
    public int Concurrent { get; }

    /// <summary>How many times a failure that might pass is tried.</summary>
    public int Attempts { get; }

    /// <summary>The wait before the second attempt, doubling after that.</summary>
    public TimeSpan Backoff { get; }

    internal TimeProvider Clock { get; }

    internal Lock Gate { get; } = new();

    /// <summary>How many downloads are queued but not started.</summary>
    public int Waiting
    {
        get
        {
            lock (Gate)
            {
                return _waiting.Count;
            }
        }
    }

    /// <summary>How many are running.</summary>
    public int Running
    {
        get
        {
            lock (Gate)
            {
                return _running;
            }
        }
    }

    /// <summary>Fetches <paramref name="id"/>, returning a handle that can be cancelled.</summary>
    public FetchJob Fetch(
        AssetId id,
        FetchUrgency urgency = FetchUrgency.Soon,
        Action<FetchProgress>? onProgress = null,
        Action<byte[]>? onRough = null,
        int roughSize = 256) =>
        FetchUrl(Origin.UrlOf(id), id, urgency, onProgress, onRough, roughSize);

    /// <summary>
    /// Fetches a URL directly, for a file named from inside another one. <paramref name="id"/> is what the
    /// manifest would call it, when there is such a name; a buffer a glTF names beside itself usually has none,
    /// and then nothing verifies it beyond its length. Refuses anything <see cref="Origin"/> would not allow,
    /// before a byte is sent.
    /// </summary>
    // F2: a fetch is described by what the caller wants and how it wants to hear about it, all named at the call.
    public FetchJob FetchUrl(
        WebUrl url,
        AssetId? id = null,
        FetchUrgency urgency = FetchUrgency.Soon,
        Action<FetchProgress>? onProgress = null,
        Action<byte[]>? onRough = null,
        int roughSize = 256)
    {
        lock (Gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            Origin.RequireAllowed(url);

            if (!_live.TryGetValue(url, out var download))
            {
                download = _live[url] = new FetchDownload(this, url, id, _arrived++);
            }

            // The caller is recorded before the download is queued, because the queue skips whatever nobody is
            // waiting for, and until this job is on the list that is every download, including this one.
            var job = new FetchJob(download, onProgress, onRough);
            download.Join(job, onRough is null ? null : roughSize);

            // Something already queued that is suddenly on screen jumps the queue rather than waiting its turn
            // behind work nobody is looking at.
            download.WantedBy(urgency);
            return job;
        }
    }

    /// <summary>
    /// The bytes of <paramref name="id"/>, waiting for them. The plain form, for code that has nothing to
    /// cancel with and no progress to show, which is most code and is what <see cref="NetworkAssetSource"/> is
    /// built on. Cancelling <paramref name="cancellation"/> gives up this one caller, as
    /// <see cref="FetchJob.Cancel"/> does.
    /// </summary>
    public async Task<byte[]> ReadAsync(
        AssetId id, FetchUrgency urgency = FetchUrgency.Soon, CancellationToken cancellation = default)
    {
        var job = Fetch(id, urgency);
        using var registration = cancellation.Register(job.Cancel);
        return await job.Bytes.ConfigureAwait(false);
    }

    /// <summary>
    /// Stops everything and lets go of the transport. Every job in flight fails with
    /// <see cref="FetchCancelledException"/>. Fetching afterwards throws rather than hangs.
    /// </summary>
    public void Dispose()
    {
        List<FetchDownload> live;
        lock (Gate)
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            live = [.. _live.Values];
            _live.Clear();
            _waiting.Clear();
        }

        foreach (var download in live)
        {
            download.Abandon();
        }

        Transport.Dispose();
    }

    // By urgency, then by the order they were first asked for. A plain list sorted on demand rather than a
    // heap: the queue is short, since it is bounded by what a scene is loading, and this way the ordering rule
    // is one line that can be read. Every arrival number is different, so no two entries ever tie.
    internal void Sort() =>
        _waiting.Sort((a, b) => a.Urgency != b.Urgency ? a.Urgency.CompareTo(b.Urgency) : a.Arrived.CompareTo(b.Arrived));

    internal void Queue(FetchDownload download)
    {
        download.MarkQueued();
        _waiting.Add(download);
        Sort();
        Pump();
    }

    internal void Pump()
    {
        while (_running < Concurrent && _waiting.Count > 0)
        {
            var next = _waiting[0];
            _waiting.RemoveAt(0);
            if (!next.TryStart())
            {
                next.Dispose();
                continue;
            }

            _running++;
            _ = RunAsync(next);
        }
    }

    /// <summary>
    /// Takes a download out of the live map, if it is still the one there. Guarded on identity because a later
    /// caller may already have started a fresh download for the same URL, and that one is not ours to forget.
    /// </summary>
    internal void ForgetLocked(FetchDownload download)
    {
        if (_live.TryGetValue(download.Url, out var current) && ReferenceEquals(current, download))
        {
            _live.Remove(download.Url);
        }
    }

    /// <summary>
    /// Waits, longer each time, with a little noise. The noise matters more than the doubling. A hundred
    /// devices that lost the same server come back at the same moment without it, and knock it over again the
    /// instant it recovers.
    /// </summary>
    internal TimeSpan WaitBefore(int attempt)
    {
        var grown = Math.Min(Backoff.TotalMicroseconds * Math.Pow(2, attempt - 1), LongestWait.TotalMicroseconds);
        var noise = 1 + ((_jitter.NextDouble() - 0.5) * 0.4);
        return TimeSpan.FromMicroseconds(Math.Round(grown * noise, MidpointRounding.AwayFromZero));
    }

    private async Task RunAsync(FetchDownload download)
    {
        try
        {
            await download.RunAsync().ConfigureAwait(false);
        }
        finally
        {
            lock (Gate)
            {
                _running--;
                ForgetLocked(download);
                Pump();
            }

            download.Dispose();
        }
    }
}
