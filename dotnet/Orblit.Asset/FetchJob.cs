namespace Orblit.Asset;

/// <summary>
/// One caller's hold on a download. Several callers asking for the same asset share one download and each
/// get one of these. <see cref="Cancel"/> gives up this caller's interest in it; the download itself stops
/// only when the last interested caller has gone, because cancelling a fetch somebody else is still waiting
/// for is not a saving.
/// </summary>
public sealed class FetchJob
{
    private readonly FetchDownload _download;
    private readonly TaskCompletionSource<byte[]> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _given;

    internal FetchJob(FetchDownload download, Action<FetchProgress>? onProgress, Action<byte[]>? onRough)
    {
        _download = download;
        OnProgress = onProgress;
        OnRough = onRough;
    }

    /// <summary>Called as bytes arrive, for this caller only.</summary>
    public Action<FetchProgress>? OnProgress { get; }

    /// <summary>
    /// Called once, part way through, with a smaller version of the asset. Only for a texture with a mip chain,
    /// and only when the download is long enough for it to matter. The bytes are a whole KTX2 file in their own
    /// right, holding the coarse levels and nothing else, so whatever would have drawn the finished texture can
    /// draw this instead and be replaced when <see cref="Bytes"/> completes.
    /// </summary>
    public Action<byte[]>? OnRough { get; }

    /// <summary>Where the asset is being fetched from.</summary>
    public WebUrl Url => _download.Url;

    /// <summary>The bytes, when they have all arrived and been checked.</summary>
    public Task<byte[]> Bytes => _completion.Task;

    /// <summary>
    /// Gives up waiting. <see cref="Bytes"/> faults with a <see cref="FetchCancelledException"/>. Awaiting it
    /// afterwards is safe; ignoring it is too, since the error is delivered to whoever asked and nowhere else.
    /// </summary>
    public void Cancel()
    {
        if (Interlocked.Exchange(ref _given, 1) == 1)
        {
            return;
        }

        _download.Drop(this);
        GiveUp();
    }

    internal void Complete(byte[] bytes) => _completion.TrySetResult(bytes);

    internal void Fail(Exception error) => _completion.TrySetException(error);

    internal void GiveUp() => Fail(new FetchCancelledException(Url));
}
