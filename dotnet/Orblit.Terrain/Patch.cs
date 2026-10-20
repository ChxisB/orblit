using System.Collections.Frozen;

namespace Orblit.Terrain;

/// <summary>Which of a region's maps an edit changes.</summary>
public enum TerrainLayer
{
    Height,
    Cover,
    Colour,
}

/// <summary>
/// What an edit changed: the tiles it touched, as they were before and as they were after.
/// A tile is a square of <see cref="TileSize"/> texels inside one region, and only the maps in
/// <see cref="Layers"/> are kept for it. So a stroke across one hillside costs a few kilobytes,
/// where a copy of the regions it crossed would cost megabytes, and an undo history of those would be
/// a leak with a menu item.
/// Made by a <see cref="TerrainRecorder"/>. <see cref="Apply"/> and <see cref="Revert"/> write the
/// tiles back into whatever terrain they are handed, into the regions that exist there; a tile whose
/// region has since gone is skipped rather than made.
/// </summary>
public sealed class TerrainPatch
{
    /// <summary>
    /// Texels a tile is across, unless the regions are smaller: 32, so a tile of heights is 4 KB and
    /// a small brush touches one to four of them.
    /// </summary>
    public const int DefaultTileSize = 32;

    private readonly Dictionary<TileKey, Tile> _before;
    private readonly Dictionary<TileKey, Tile> _after;

    // F2: private, and every caller names all five.
    internal TerrainPatch(
        int tileSize,
        int regionSize,
        IReadOnlySet<TerrainLayer> layers,
        Dictionary<TileKey, Tile> before,
        Dictionary<TileKey, Tile> after)
    {
        TileSize = tileSize;
        RegionSize = regionSize;
        Layers = layers;
        _before = before;
        _after = after;
    }

    /// <summary>Texels a tile is across.</summary>
    public int TileSize { get; }

    /// <summary>Texels a region is across in the terrain this was recorded on.</summary>
    public int RegionSize { get; }

    public IReadOnlySet<TerrainLayer> Layers { get; }

    public bool IsEmpty => _before.Count == 0;

    public int TileCount => _before.Count;

    /// <summary>How much it holds, before and after together.</summary>
    public long ByteCount => _before.Values.Concat(_after.Values).Sum(tile => (long)tile.ByteCount);

    /// <summary>The regions it changes.</summary>
    public IReadOnlySet<RegionKey> Regions =>
        _before.Keys.Select(key => RegionKey.Containing(key.X * TileSize, key.Z * TileSize, RegionSize)).ToHashSet();

    /// <summary>Nothing changed.</summary>
    public static TerrainPatch Empty(int tileSize = DefaultTileSize, int regionSize = 256) =>
        new(tileSize, regionSize, FrozenSet<TerrainLayer>.Empty, [], []);

    /// <summary>Makes <paramref name="terrain"/> how it was after the edit. Says which regions changed.</summary>
    public IReadOnlySet<RegionKey> Apply(Terrain terrain) => Write(terrain, _after);

    /// <summary>Makes <paramref name="terrain"/> how it was before the edit. Says which regions changed.</summary>
    public IReadOnlySet<RegionKey> Revert(Terrain terrain) => Write(terrain, _before);

    /// <summary>
    /// This edit and then <paramref name="later"/>, as one: the earlier of the two befores and the
    /// later of the two afters for each tile. How a stroke recorded a move at a time becomes one step
    /// to undo.
    /// </summary>
    public TerrainPatch FollowedBy(TerrainPatch later)
    {
        if (IsEmpty)
        {
            return later;
        }

        if (later.IsEmpty)
        {
            return this;
        }

        if (later.TileSize != TileSize || later.RegionSize != RegionSize)
        {
            throw new ArgumentException("Was recorded with other tiles or regions than this one.", nameof(later));
        }

        return new TerrainPatch(
            tileSize: TileSize,
            regionSize: RegionSize,
            layers: Layers.Union(later.Layers).ToFrozenSet(),
            before: Over(_before, later._before),
            after: Over(later._after, _after));
    }

    /// <summary>
    /// A copy of <paramref name="under"/> with <paramref name="top"/>'s tiles on it. A tile in both
    /// keeps the maps of the top one and takes from the other any it lacks. Two strokes of different
    /// tools can touch one tile: the heights one kept and the cover the other did are both part of it.
    /// </summary>
    private static Dictionary<TileKey, Tile> Over(
        Dictionary<TileKey, Tile> top,
        Dictionary<TileKey, Tile> under)
    {
        var joined = new Dictionary<TileKey, Tile>(under);
        foreach (var (key, tile) in top)
        {
            joined[key] = under.TryGetValue(key, out var had) ? tile.Joined(had) : tile;
        }

        return joined;
    }

    private HashSet<RegionKey> Write(Terrain terrain, Dictionary<TileKey, Tile> tiles)
    {
        if (terrain.RegionSize != RegionSize)
        {
            throw new ArgumentException(
                $"Its regions are {terrain.RegionSize} across; this was recorded on regions {RegionSize} across.",
                nameof(terrain));
        }

        var changed = new HashSet<TerrainRegion>();
        foreach (var (key, tile) in tiles)
        {
            if (terrain.RegionAt(key.Region(TileSize, RegionSize)) is not { } region)
            {
                continue;
            }

            tile.WriteInto(region, key.CornerIn(region, TileSize), TileSize);
            changed.Add(region);
        }

        foreach (var region in changed)
        {
            region.Touch();
        }

        return changed.Select(region => region.Key).ToHashSet();
    }
}

/// <summary>
/// Keeps the tiles an edit is about to write, so that what it did can be undone.
/// Tell it which texels are about to change with <see cref="Keep"/> before writing them; each tile is
/// copied the first time, and only the first. <see cref="Finish"/> copies the same tiles again as
/// they are then, and hands both over as a <see cref="TerrainPatch"/>.
/// </summary>
public sealed class TerrainRecorder
{
    private readonly Dictionary<TileKey, Tile> _before = [];

    public TerrainRecorder(
        Terrain terrain,
        IEnumerable<TerrainLayer> layers,
        int tileSize = TerrainPatch.DefaultTileSize)
    {
        Terrain = terrain;
        Layers = layers.ToFrozenSet();
        TileSize = Math.Min(tileSize, terrain.RegionSize);
        if (!TerrainRegion.ValidSize(TileSize))
        {
            throw new ArgumentOutOfRangeException(nameof(tileSize), tileSize, "A tile is a power of two texels across.");
        }
    }

    public Terrain Terrain { get; }

    public IReadOnlySet<TerrainLayer> Layers { get; }

    public int TileSize { get; }

    /// <summary>
    /// Copies every tile holding a texel from (<paramref name="i0"/>, <paramref name="j0"/>) to
    /// (<paramref name="i1"/>, <paramref name="j1"/>) of the whole terrain, both corners included,
    /// that is not copied already. Tiles where there is no region are left out: nothing can be
    /// written there.
    /// </summary>
    public void Keep(long i0, long j0, long i1, long j1)
    {
        for (var z = TerrainMath.FloorDiv(j0, TileSize); z <= TerrainMath.FloorDiv(j1, TileSize); z++)
        {
            for (var x = TerrainMath.FloorDiv(i0, TileSize); x <= TerrainMath.FloorDiv(i1, TileSize); x++)
            {
                var key = new TileKey(x, z);
                if (!_before.ContainsKey(key) && RegionOf(key) is { } region)
                {
                    _before[key] = Tile.Copy(region, key.CornerIn(region, TileSize), TileSize, Layers);
                }
            }
        }
    }

    /// <summary>What was kept, before and as it is now.</summary>
    public TerrainPatch Finish()
    {
        var before = new Dictionary<TileKey, Tile>();
        var after = new Dictionary<TileKey, Tile>();
        foreach (var (key, tile) in _before)
        {
            // A region taken out between keeping and finishing leaves nothing to compare, so its
            // tiles are not part of the edit.
            if (RegionOf(key) is not { } region)
            {
                continue;
            }

            before[key] = tile;
            after[key] = Tile.Copy(region, key.CornerIn(region, TileSize), TileSize, Layers);
        }

        return new TerrainPatch(
            tileSize: TileSize,
            regionSize: Terrain.RegionSize,
            layers: Layers,
            before: before,
            after: after);
    }

    private TerrainRegion? RegionOf(TileKey key) => Terrain.RegionAt(key.Region(TileSize, Terrain.RegionSize));
}

/// <summary>Which tile, counted in tiles across the whole terrain.</summary>
internal readonly record struct TileKey(long X, long Z)
{
    public RegionKey Region(int tileSize, int regionSize) =>
        RegionKey.Containing(X * tileSize, Z * tileSize, regionSize);

    /// <summary>The texel within <paramref name="region"/> where this tile starts.</summary>
    public (int I, int J) CornerIn(TerrainRegion region, int tileSize) => (
        (int)((X * tileSize) - (region.Key.X * region.Size)),
        (int)((Z * tileSize) - (region.Key.Z * region.Size)));
}

/// <summary>One tile's maps, the ones an edit touched.</summary>
internal sealed class Tile(float[]? heights, uint[]? cover, byte[]? colour)
{
    public float[]? Heights { get; } = heights;

    public uint[]? Cover { get; } = cover;

    public byte[]? Colour { get; } = colour;

    public int ByteCount => (Heights?.Length * sizeof(float) ?? 0) + (Cover?.Length * sizeof(uint) ?? 0) + (Colour?.Length ?? 0);

    public static Tile Copy(TerrainRegion region, (int I, int J) corner, int size, IReadOnlySet<TerrainLayer> layers)
    {
        var heights = layers.Contains(TerrainLayer.Height) ? new float[size * size] : null;
        var cover = layers.Contains(TerrainLayer.Cover) ? new uint[size * size] : null;
        var colour = layers.Contains(TerrainLayer.Colour) ? new byte[size * size * 4] : null;
        for (var row = 0; row < size; row++)
        {
            var from = ((corner.J + row) * region.Size) + corner.I;
            var to = row * size;
            CopyRow(region.Heights, from, heights, to, size);
            CopyRow(region.Cover, from, cover, to, size);
            CopyRow(region.Colour, from * 4, colour, to * 4, size * 4);
        }

        return new Tile(heights, cover, colour);
    }

    /// <summary>
    /// This tile's maps, and <paramref name="other"/>'s for any this one lacks.
    /// </summary>
    public Tile Joined(Tile other) => new(Heights ?? other.Heights, Cover ?? other.Cover, Colour ?? other.Colour);

    public void WriteInto(TerrainRegion region, (int I, int J) corner, int size)
    {
        for (var row = 0; row < size; row++)
        {
            var to = ((corner.J + row) * region.Size) + corner.I;
            var from = row * size;
            CopyRow(Heights, from, region.Heights, to, size);
            CopyRow(Cover, from, region.Cover, to, size);
            CopyRow(Colour, from * 4, region.Colour, to * 4, size * 4);
        }
    }

    private static void CopyRow<T>(T[]? source, int from, T[]? destination, int to, int length)
    {
        if (source is not null && destination is not null)
        {
            Array.Copy(source, from, destination, to, length);
        }
    }
}
