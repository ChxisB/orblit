namespace Orblit.Asset;

/// <summary>
/// Assets held in memory, for tests and for tools that build them on the fly. The bytes are
/// copied on the way in and on the way out, so code is free to decode in place.
/// </summary>
public sealed class MemoryAssetSource : IAssetSource
{
    private readonly Dictionary<AssetId, byte[]> _assets;

    public MemoryAssetSource(IReadOnlyDictionary<AssetId, byte[]>? assets = null) =>
        _assets = (assets ?? new Dictionary<AssetId, byte[]>()).ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());

    /// <inheritdoc />
    public Task<byte[]> ReadAsync(AssetId id) =>
        _assets.TryGetValue(id, out var bytes)
            ? Task.FromResult(bytes.ToArray())
            : Task.FromException<byte[]>(new AssetNotFoundException(id));

    /// <inheritdoc />
    public Task<bool> ExistsAsync(AssetId id) => Task.FromResult(_assets.ContainsKey(id));
}
