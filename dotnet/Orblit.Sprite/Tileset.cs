namespace Orblit.Sprite;

/// <summary>Which image a tile number comes from, and where in it.</summary>
/// <param name="Name">What the editor called the set.</param>
/// <param name="Image">The file the tiles are cut from.</param>
/// <param name="FirstId">
/// The number the first tile of this set has in the map. A map with several tilesets numbers them
/// end to end, so which set a tile belongs to is the last set whose first id is not above it.
/// </param>
/// <param name="TileWidth">One tile's width in pixels.</param>
/// <param name="TileHeight">One tile's height in pixels.</param>
/// <param name="Columns">How many tiles lie across the image.</param>
/// <param name="Count">How many tiles the set holds. Zero means it did not say.</param>
/// <param name="Spacing">The gap between tiles, in pixels.</param>
/// <param name="Margin">The border around the tiles, in pixels.</param>
// F2: nine parameters, and every one after the column count is optional and called by name.
public sealed record Tileset(
    string Name,
    string Image,
    long FirstId,
    long TileWidth,
    long TileHeight,
    long Columns,
    long Count = 0,
    long Spacing = 0,
    long Margin = 0)
{
    public bool Has(long id) => id >= FirstId && (Count == 0 || id < FirstId + Count);

    /// <summary>Where tile <paramref name="id"/> sits in this set's image, in pixels.</summary>
    public (long X, long Y, long Width, long Height)? RectOf(long id)
    {
        if (!Has(id) || Columns <= 0)
        {
            return null;
        }

        var local = id - FirstId;

        // The remainder is never negative, and the difference of two far-apart ids wraps below zero.
        var column = local % Columns;
        if (column < 0)
        {
            column += Columns;
        }

        var row = local / Columns;
        return (Margin + (column * (TileWidth + Spacing)), Margin + (row * (TileHeight + Spacing)), TileWidth, TileHeight);
    }
}
