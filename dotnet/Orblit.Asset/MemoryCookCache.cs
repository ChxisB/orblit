namespace Orblit.Asset;

/// <summary>
/// A cook cache held in memory, for tests and for a single short-lived run. Nothing is evicted: a
/// run that outgrows memory has a size problem this would only hide.
/// </summary>
public sealed class MemoryCookCache : ICookCache
{
    private readonly MemoryContentStore _content = new();
    private readonly Dictionary<ContentHash, CookedAsset> _entries = [];
    private readonly Dictionary<ContentHash, CookFailure> _failures = [];
    private readonly Lock _gate = new();

    public async Task<CookedAsset?> LookUpAsync(CookKey key)
    {
        CookedAsset? found;
        lock (_gate)
        {
            found = _entries.GetValueOrDefault(key.Hash);
        }

        if (found is null)
        {
            return null;
        }

        foreach (var output in found.Outputs)
        {
            if (!await _content.ContainsAsync(output.Hash).ConfigureAwait(false))
            {
                return null;
            }
        }

        return found;
    }

    public async Task<CookedAsset> StoreAsync(CookKey key, IReadOnlyDictionary<string, byte[]> outputs)
    {
        var stored = new List<CookOutput>();
        foreach (var (name, bytes) in outputs)
        {
            var hash = await _content.PutAsync(bytes).ConfigureAwait(false);
            stored.Add(new CookOutput(name, hash, bytes.Length));
        }

        var asset = new CookedAsset(stored);
        lock (_gate)
        {
            // A key that cooks is a key that no longer failed: the tool that was missing has been
            // installed, or the disk has been cleared.
            _failures.Remove(key.Hash);
            _entries[key.Hash] = asset;
        }

        return asset;
    }

    public Task<byte[]?> ReadAsync(ContentHash hash) => _content.GetAsync(hash);

    public Task<CookFailure?> LookUpFailureAsync(CookKey key)
    {
        lock (_gate)
        {
            return Task.FromResult(_failures.GetValueOrDefault(key.Hash));
        }
    }

    public Task RecordFailureAsync(CookKey key, string reason)
    {
        lock (_gate)
        {
            _failures[key.Hash] = new CookFailure(reason, DateTimeOffset.UtcNow);
        }

        return Task.CompletedTask;
    }
}
