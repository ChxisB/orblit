namespace Orblit.Sprite;

/// <summary>
/// How a tile is turned. Swap the x and y axes first when <paramref name="Diagonal"/> is set,
/// then mirror across <paramref name="Horizontal"/> and <paramref name="Vertical"/>.
/// </summary>
public readonly record struct TileFlips(bool Horizontal, bool Vertical, bool Diagonal);

/// <summary>One layer of a tile map.</summary>
/// <param name="Name">What the editor called the layer.</param>
/// <param name="Width">How many cells across.</param>
/// <param name="Height">How many cells down.</param>
/// <param name="Tiles">
/// The tile at each cell, in reading order. Zero is nothing. A number may carry the flip bits of
/// <see cref="FlipsAt"/>, so read it with <see cref="At"/>.
/// </param>
/// <param name="Left">
/// The cell the first tile sits in. Zero for a map of fixed size. An infinite map's layer starts
/// wherever its first chunk does, which can be left of or above the origin.
/// </param>
/// <param name="Top">The row the first tile sits in.</param>
/// <param name="Visible">Whether the editor showed the layer.</param>
/// <param name="Opacity">How opaque the editor drew it.</param>
// F2: eight parameters, and every one after the tiles is optional and called by name.
public sealed record TileLayer(
    string Name,
    long Width,
    long Height,
    IReadOnlyList<long> Tiles,
    long Left = 0,
    long Top = 0,
    bool Visible = true,
    double Opacity = 1)
{
    // The top three bits of a tile number say how the tile is turned. The rest is the number.
    private const long FlipHorizontal = 0x80000000;
    private const long FlipVertical = 0x40000000;
    private const long FlipDiagonal = 0x20000000;
    private const long IdMask = 0x1FFFFFFF;

    /// <summary>
    /// The tile at a cell, or zero outside the map, with the flip bits left off. Outside is empty
    /// rather than an error, because a map is walked by things that wander off the edge of it.
    /// </summary>
    public long At(long x, long y) => Raw(x, y) & IdMask;

    public bool IsEmptyAt(long x, long y) => At(x, y) == 0;

    /// <summary>How the tile at a cell is turned. Not turned outside the map.</summary>
    public TileFlips FlipsAt(long x, long y)
    {
        var raw = Raw(x, y);
        return new TileFlips((raw & FlipHorizontal) != 0, (raw & FlipVertical) != 0, (raw & FlipDiagonal) != 0);
    }

    private long Raw(long x, long y)
    {
        var column = x - Left;
        var row = y - Top;
        if (column < 0 || row < 0 || column >= Width || row >= Height)
        {
            return 0;
        }

        return Tiles[checked((int)((row * Width) + column))];
    }
}
