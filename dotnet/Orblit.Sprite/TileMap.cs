using Orblit.Json;
using Orblit.VectorMath;

namespace Orblit.Sprite;

/// <summary>
/// A map of tiles, as an editor wrote it. Tiled is what people already use and its format is
/// stable, so this reads the subset that matters and says plainly what it ignores.
/// </summary>
/// <param name="Width">The map's width in tiles.</param>
/// <param name="Height">The map's height in tiles.</param>
/// <param name="TileWidth">One tile's width in pixels.</param>
/// <param name="TileHeight">One tile's height in pixels.</param>
/// <param name="Layers">The tile layers, bottom first.</param>
/// <param name="Tilesets">Every set a tile number may come from.</param>
/// <param name="Properties">Whatever the map carried, unchanged.</param>
// F2: seven parameters, and the last is optional and called by name.
public sealed record TileMap(
    long Width,
    long Height,
    long TileWidth,
    long TileHeight,
    IReadOnlyList<TileLayer> Layers,
    IReadOnlyList<Tileset> Tilesets,
    IReadOnlyDictionary<string, object?>? Properties = null)
{
    // shortcut: an infinite layer is one dense grid over all its chunks, so chunks far apart cost the empty space between them. Upgrade to keyed chunks when a map needs more than this.
    private const long MaxCells = 1 << 22;

    /// <summary>Whatever the map carried, unchanged.</summary>
    public IReadOnlyDictionary<string, object?> Properties { get; } = Properties ?? new Dictionary<string, object?>();

    public TileLayer? Layer(string name) => Layers.FirstOrDefault(found => found.Name == name);

    /// <summary>Which set tile <paramref name="id"/> comes from.</summary>
    public Tileset? SetFor(long id)
    {
        if (id <= 0)
        {
            return null;
        }

        Tileset? best = null;
        foreach (var set in Tilesets)
        {
            if (set.FirstId <= id && (best is null || set.FirstId > best.FirstId))
            {
                best = set;
            }
        }

        return best;
    }

    /// <summary>
    /// Where tile <paramref name="id"/> is drawn when it sits in cell (<paramref name="x"/>,
    /// <paramref name="y"/>), in world pixels, or null when no set holds it. A tile is anchored to
    /// the bottom left of its cell. One taller or wider than the grid grows up and to the right.
    /// </summary>
    public (long X, long Y, long Width, long Height)? PlaceOf(long id, long x, long y)
    {
        if (SetFor(id)?.RectOf(id) is not { } source)
        {
            return null;
        }

        return (x * TileWidth, (((y + 1) * TileHeight) - source.Height), source.Width, source.Height);
    }

    /// <summary>The cell a point in world pixels falls in.</summary>
    public (long X, long Y) CellAt(double x, double y) => (Doubles.Floor(x / TileWidth), Doubles.Floor(y / TileHeight));

    /// <summary>Whether any visible layer has something at a cell, among <paramref name="only"/> when it is given.</summary>
    public bool IsSolidAt(long x, long y, IReadOnlySet<string>? only = null) =>
        Layers.Any(layer => (only is null || only.Contains(layer.Name)) && layer.Visible && layer.At(x, y) != 0);

    /// <summary>
    /// Reads a Tiled map saved as JSON: the orthogonal, uncompressed subset, which is what the
    /// editor writes by default. Isometric and hexagonal maps, base64 or zlib layer data and
    /// external tileset files come back as null, so a caller knows to look rather than wondering
    /// why the map is empty. So does a layer with fewer tiles than its size, and an infinite map
    /// whose chunks span more than four million cells. An infinite map's chunks are joined into one
    /// grid per layer, so <see cref="Width"/> and <see cref="Height"/> are only what the file says.
    /// </summary>
    public static TileMap? Read(string text)
    {
        object? parsed;
        try
        {
            parsed = JsonText.Decode(text);
        }
        catch (FormatException)
        {
            return null;
        }

        // Anything but a plain grid is a different reader, and pretending otherwise draws a map at the wrong angle.
        if (parsed is not IReadOnlyDictionary<string, object?> root
            || (root.GetValueOrDefault("orientation") is string orientation && orientation != "orthogonal")
            || ReadLayers(root.GetValueOrDefault("layers")) is not { } layers
            || ReadTilesets(root.GetValueOrDefault("tilesets")) is not { } tilesets)
        {
            return null;
        }

        return new TileMap(
            JsonValues.Int(root.GetValueOrDefault("width")),
            JsonValues.Int(root.GetValueOrDefault("height")),
            JsonValues.Int(root.GetValueOrDefault("tilewidth")),
            JsonValues.Int(root.GetValueOrDefault("tileheight")),
            layers,
            tilesets,
            root.GetValueOrDefault("properties") as IReadOnlyDictionary<string, object?>);
    }

    private static List<TileLayer>? ReadLayers(object? raw)
    {
        if (raw is not IReadOnlyList<object?> entries)
        {
            return null;
        }

        var layers = new List<TileLayer>();
        foreach (var map in entries.OfType<IReadOnlyDictionary<string, object?>>())
        {
            if (map.GetValueOrDefault("type") is not "tilelayer")
            {
                continue;
            }

            if (ReadLayer(map) is not { } layer)
            {
                return null;
            }

            layers.Add(layer);
        }

        return layers;
    }

    private static List<Tileset>? ReadTilesets(object? raw)
    {
        var tilesets = new List<Tileset>();
        if (raw is not IReadOnlyList<object?> entries)
        {
            return tilesets;
        }

        foreach (var map in entries.OfType<IReadOnlyDictionary<string, object?>>())
        {
            // An external tileset is a second file this was not given.
            if (map.GetValueOrDefault("source") is not null)
            {
                return null;
            }

            tilesets.Add(new Tileset(
                map.GetValueOrDefault("name") as string ?? "",
                map.GetValueOrDefault("image") as string ?? "",
                JsonValues.Int(map.GetValueOrDefault("firstgid")),
                JsonValues.Int(map.GetValueOrDefault("tilewidth")),
                JsonValues.Int(map.GetValueOrDefault("tileheight")),
                JsonValues.Int(map.GetValueOrDefault("columns")),
                Count: JsonValues.Int(map.GetValueOrDefault("tilecount")),
                Spacing: JsonValues.Int(map.GetValueOrDefault("spacing")),
                Margin: JsonValues.Int(map.GetValueOrDefault("margin"))));
        }

        return tilesets;
    }

    // Null when the layer is in a form this does not read.
    private static TileLayer? ReadLayer(IReadOnlyDictionary<string, object?> map)
    {
        // Compressed or base64 data is a decoder this does not have. An empty layer would be a map that silently lost a floor.
        if (map.GetValueOrDefault("encoding") is { } encoding && encoding is not "csv")
        {
            return null;
        }

        if (map.GetValueOrDefault("compression") is { } compression && compression is not "")
        {
            return null;
        }

        var cells = map.GetValueOrDefault("chunks") is IReadOnlyList<object?> chunks ? ReadChunks(chunks) : ReadCells(map);
        if (cells is not { } found)
        {
            return null;
        }

        return new TileLayer(
            map.GetValueOrDefault("name") as string ?? "",
            found.Width,
            found.Height,
            found.Tiles,
            Left: found.Left,
            Top: found.Top,
            Visible: map.GetValueOrDefault("visible") is not false,
            Opacity: map.GetValueOrDefault("opacity") switch
            {
                long whole => whole,
                double fraction => fraction,
                _ => 1.0,
            });
    }

    private readonly record struct Cells(long Left, long Top, long Width, long Height, IReadOnlyList<long> Tiles);

    // The grid a layer or a chunk holds, or null when its data is missing or shorter than its size.
    private static Cells? ReadCells(IReadOnlyDictionary<string, object?> map)
    {
        var width = JsonValues.Int(map.GetValueOrDefault("width"));
        var height = JsonValues.Int(map.GetValueOrDefault("height"));
        if (map.GetValueOrDefault("data") is not IReadOnlyList<object?> data || width < 0 || height < 0 || data.Count < unchecked(width * height))
        {
            return null;
        }

        return new Cells(
            JsonValues.Int(map.GetValueOrDefault("x")),
            JsonValues.Int(map.GetValueOrDefault("y")),
            width,
            height,
            [.. data.Select(JsonValues.Int)]);
    }

    // One grid over the chunks of an infinite layer. Chunks may sit at negative cells, and the cells between them are empty.
    private static Cells? ReadChunks(IReadOnlyList<object?> raw)
    {
        var chunks = new List<Cells>();
        foreach (var entry in raw)
        {
            if (entry is not IReadOnlyDictionary<string, object?> map || ReadCells(map) is not { } chunk)
            {
                return null;
            }

            chunks.Add(chunk);
        }

        if (chunks.Count == 0)
        {
            return new Cells(0, 0, 0, 0, []);
        }

        var left = chunks.Min(chunk => chunk.Left);
        var top = chunks.Min(chunk => chunk.Top);
        var width = unchecked(chunks.Aggregate(chunks[0].Left, (right, chunk) => Math.Max(right, chunk.Left + chunk.Width)) - left);
        var height = unchecked(chunks.Aggregate(chunks[0].Top, (bottom, chunk) => Math.Max(bottom, chunk.Top + chunk.Height)) - top);

        // The product wraps, so a span past 64 bits can slip under the limit and fail at the copy.
        var cells = unchecked(width * height);
        if (cells > MaxCells)
        {
            return null;
        }

        var tiles = new long[checked((int)cells)];
        foreach (var chunk in chunks)
        {
            Paste(chunk, tiles, left, top, width);
        }

        return new Cells(left, top, width, height, tiles);
    }

    // Writes each row of a chunk into the grid. A tile is stored as an unsigned 32-bit number, so a wider one is cut to fit.
    private static void Paste(Cells chunk, long[] tiles, long left, long top, long width)
    {
        for (long row = 0; row < chunk.Height; row++)
        {
            var start = unchecked((((chunk.Top - top) + row) * width) + chunk.Left - left);
            var from = unchecked(row * chunk.Width);
            if (start < 0 || start + chunk.Width > tiles.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(chunk), "A chunk lies outside the grid.");
            }

            if (from + chunk.Width > chunk.Tiles.Count)
            {
                throw new InvalidOperationException("A chunk has fewer tiles than its size.");
            }

            for (var i = 0; i < chunk.Width; i++)
            {
                tiles[start + i] = (uint)chunk.Tiles[(int)(from + i)];
            }
        }
    }
}
