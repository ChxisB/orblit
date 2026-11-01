namespace Orblit.Asset;

/// <summary>
/// Somewhere assets can be read from, by id. Two questions and nothing more, so a directory, a
/// bundle and a map in a test can all stand behind the same code. There is no listing: anything
/// that needs to know which assets exist should be reading a manifest.
/// </summary>
public interface IAssetSource
{
    /// <summary>
    /// The bytes of <paramref name="id"/>. Throws <see cref="AssetNotFoundException"/> when this
    /// source does not have it, because an asset asked for by name and missing is nearly always a mistake.
    /// </summary>
    Task<byte[]> ReadAsync(AssetId id);

    /// <summary>
    /// Whether <paramref name="id"/> can be read from here. A hint rather than a promise: an asset
    /// can go away between the asking and the reading.
    /// </summary>
    Task<bool> ExistsAsync(AssetId id);
}
