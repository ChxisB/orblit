namespace Orblit.Asset;

/// <summary>
/// Assets read through a function, with null meaning there is no such asset. How a host with its
/// own idea of a bundle gets in. Anything else the function throws is passed on untouched, because
/// a failure to read is not the same thing as the asset not being there.
/// </summary>
public sealed class CallbackAssetSource(Func<AssetId, Task<byte[]?>> load) : IAssetSource
{
    /// <inheritdoc />
    public async Task<byte[]> ReadAsync(AssetId id) =>
        await load(id).ConfigureAwait(false) ?? throw new AssetNotFoundException(id);

    /// <summary>
    /// Whether the function gives anything for <paramref name="id"/>. This loads the asset to
    /// find out, since a bare function has no cheaper way to be asked.
    /// </summary>
    public async Task<bool> ExistsAsync(AssetId id) => await load(id).ConfigureAwait(false) is not null;
}
