namespace Orblit.Asset;

/// <summary>
/// Several sources searched in order, the first one that has an asset winning. Whatever is in an
/// earlier layer hides the same id further down.
/// </summary>
public sealed class LayeredAssetSource(IReadOnlyList<IAssetSource> layers) : IAssetSource
{
    /// <summary>The sources, searched from the first.</summary>
    public IReadOnlyList<IAssetSource> Layers { get; } = [.. layers];

    /// <summary>
    /// The bytes from the first layer that has <paramref name="id"/>. Each layer is read rather
    /// than asked first, because for some sources asking is reading. Only
    /// <see cref="AssetNotFoundException"/> moves on to the next layer: any other failure is a layer
    /// that has the asset and could not read it.
    /// </summary>
    public async Task<byte[]> ReadAsync(AssetId id)
    {
        foreach (var layer in Layers)
        {
            try
            {
                return await layer.ReadAsync(id).ConfigureAwait(false);
            }
            catch (AssetNotFoundException)
            {
                // Not in this layer; a later one may have it.
            }
        }

        throw new AssetNotFoundException(
            id,
            Layers.Count == 0 ? "there are no sources to look in" : $"none of the {Layers.Count} sources has it");
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(AssetId id)
    {
        foreach (var layer in Layers)
        {
            if (await layer.ExistsAsync(id).ConfigureAwait(false))
            {
                return true;
            }
        }

        return false;
    }
}
