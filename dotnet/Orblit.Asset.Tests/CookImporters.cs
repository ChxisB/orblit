using System.Collections.Concurrent;

namespace Orblit.Asset.Tests;

// An importer that records what it was asked, so a test can assert that the cook did not ask, which is the
// whole claim the cache makes. The queues are concurrent because the cook runs workers on threads.
internal sealed class RecordingImporter(string name = "recording", int version = 1, string extension = "txt") : Importer
{
    private static readonly IReadOnlySet<AssetId> NoDependencies = new HashSet<AssetId>();

    public override string Name { get; } = name;

    public override int Version { get; } = version;

    public override IReadOnlySet<string> Extensions { get; } = new HashSet<string> { extension };

    /// <summary>Dependencies to report for an asset, by the asset's id.</summary>
    public IReadOnlyDictionary<AssetId, IReadOnlySet<AssetId>> Discovers { get; init; } =
        new Dictionary<AssetId, IReadOnlySet<AssetId>>();

    /// <summary>What to write, given the request. The default echoes the source.</summary>
    public Func<ImportRequest, byte[]>? Transform { get; init; }

    public IReadOnlyList<string> Notes { get; init; } = [];

    public ConcurrentQueue<AssetId> Imported { get; } = new();

    public ConcurrentQueue<AssetId> Asked { get; } = new();

    public override IReadOnlyDictionary<string, object?> ResolveSettings(ImportSettings settings)
    {
        var resolved = new Dictionary<string, object?> { ["mode"] = "normal" };
        foreach (var (key, value) in settings.Values)
        {
            if (resolved.ContainsKey(key))
            {
                resolved[key] = value;
            }
        }

        return resolved;
    }

    public override Task<IReadOnlySet<AssetId>> DependenciesOfAsync(
        AssetId id, byte[] bytes, IReadOnlyDictionary<string, object?> settings)
    {
        Asked.Enqueue(id);
        return Task.FromResult(Discovers.GetValueOrDefault(id) ?? NoDependencies);
    }

    public override Task<ImportResult> ImportAsync(ImportRequest request)
    {
        Imported.Enqueue(request.Id);
        var bytes = Transform?.Invoke(request) ?? request.Bytes;
        return Task.FromResult(new ImportResult { Outputs = new Dictionary<string, byte[]> { ["out"] = bytes }, Notes = Notes });
    }
}

// An importer that parks in ImportAsync until it is released, so a test can see how many run at once.
internal sealed class GatedImporter : Importer
{
    private readonly ConcurrentQueue<TaskCompletionSource> _parked = new();
    private int _running;
    private int _peak;

    public override string Name => "gated";

    public override int Version => 1;

    public override IReadOnlySet<string> Extensions { get; } = new HashSet<string> { "txt" };

    public int Peak => Volatile.Read(ref _peak);

    public int Parked => _parked.Count;

    public override IReadOnlyDictionary<string, object?> ResolveSettings(ImportSettings settings) =>
        new Dictionary<string, object?>();

    public override async Task<ImportResult> ImportAsync(ImportRequest request)
    {
        RaisePeak(Interlocked.Increment(ref _running));
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _parked.Enqueue(released);
        await released.Task.ConfigureAwait(false);
        Interlocked.Decrement(ref _running);
        return new ImportResult { Outputs = new Dictionary<string, byte[]> { ["out"] = request.Bytes } };
    }

    /// <summary>Lets the longest parked importer go, and says whether there was one.</summary>
    public bool ReleaseOne()
    {
        if (!_parked.TryDequeue(out var released))
        {
            return false;
        }

        released.SetResult();
        return true;
    }

    private void RaisePeak(int running)
    {
        int seen;
        do
        {
            seen = Volatile.Read(ref _peak);
        }
        while (running > seen && Interlocked.CompareExchange(ref _peak, running, seen) != seen);
    }
}

// An importer that claims an asset and then cannot make sense of it, the way a real one meets a .png that is
// not a PNG.
internal sealed class UnreadableImporter : Importer
{
    public override string Name => "unreadable";

    public override int Version => 1;

    public override IReadOnlySet<string> Extensions { get; } = new HashSet<string> { "txt" };

    public override IReadOnlyDictionary<string, object?> ResolveSettings(ImportSettings settings) =>
        new Dictionary<string, object?>();

    public override Task<IReadOnlySet<AssetId>> DependenciesOfAsync(
        AssetId id, byte[] bytes, IReadOnlyDictionary<string, object?> settings) =>
        Task.FromException<IReadOnlySet<AssetId>>(new ImportFailureException(id, "not a txt at all"));

    public override Task<ImportResult> ImportAsync(ImportRequest request) =>
        Task.FromException<ImportResult>(new ImportFailureException(request.Id, "not a txt at all"));
}

// A cache that cooks and looks up as usual and refuses to write a failure down, the way a full disk would.
internal sealed class FailureRefusingCache(MemoryCookCache inner) : ICookCache
{
    public Task<CookedAsset?> LookUpAsync(CookKey key) => inner.LookUpAsync(key);

    public Task<CookedAsset> StoreAsync(CookKey key, IReadOnlyDictionary<string, byte[]> outputs) =>
        inner.StoreAsync(key, outputs);

    public Task<byte[]?> ReadAsync(ContentHash hash) => inner.ReadAsync(hash);

    public Task<CookFailure?> LookUpFailureAsync(CookKey key) => inner.LookUpFailureAsync(key);

    public Task RecordFailureAsync(CookKey key, string reason) =>
        Task.FromException(new IOException("no space left on device"));
}
