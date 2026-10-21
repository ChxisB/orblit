namespace Orblit.Asset;

/// <summary>
/// A content store held in memory, for tests and short-lived tools. Copies on the way in and
/// out, so bytes changed by whoever put them or got them cannot end up under a hash they no
/// longer match.
/// </summary>
public sealed class MemoryContentStore : IContentStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<ContentHash, byte[]> _entries = [];

    /// <inheritdoc />
    public Task<byte[]?> GetAsync(ContentHash hash)
    {
        lock (_gate)
        {
            return Task.FromResult(_entries.TryGetValue(hash, out var bytes) ? (byte[]?)bytes.ToArray() : null);
        }
    }

    /// <inheritdoc />
    public Task<ContentHash> PutAsync(ReadOnlyMemory<byte> bytes)
    {
        var hash = ContentHash.Of(bytes.Span);
        lock (_gate)
        {
            _entries.TryAdd(hash, bytes.ToArray());
        }

        return Task.FromResult(hash);
    }

    /// <inheritdoc />
    public Task<bool> ContainsAsync(ContentHash hash)
    {
        lock (_gate)
        {
            return Task.FromResult(_entries.ContainsKey(hash));
        }
    }

    /// <inheritdoc />
    public Task RemoveAsync(ContentHash hash)
    {
        lock (_gate)
        {
            _entries.Remove(hash);
        }

        return Task.CompletedTask;
    }
}
