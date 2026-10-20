using static Orblit.Sprite.Tests.Tree;

namespace Orblit.Sprite.Tests;

/// <summary>Tiled's JSON as the Dart tests wrote it, on a 16 pixel grid.</summary>
internal static class TiledFixture
{
    /// <summary>A tile set as Tiled writes it into a map, four tiles to a row.</summary>
    public static OrderedDictionary<string, object?> SetOf(string name, long firstId, long tileWidth = 16, long tileHeight = 16) => Map(
        ("firstgid", firstId),
        ("name", name),
        ("image", $"{name}.png"),
        ("imagewidth", tileWidth * 4),
        ("imageheight", tileHeight * 2),
        ("tilewidth", tileWidth),
        ("tileheight", tileHeight),
        ("columns", 4L),
        ("tilecount", 8L),
        ("margin", 0L),
        ("spacing", 0L));

    /// <summary>An orthogonal map in the layout Tiled saves as JSON.</summary>
    public static string MapOf(object?[] layers, object?[] tilesets, bool infinite = false) => Text(Map(
        ("type", "map"),
        ("version", "1.10"),
        ("orientation", "orthogonal"),
        ("renderorder", "right-down"),
        ("infinite", infinite),
        ("width", 4L),
        ("height", 4L),
        ("tilewidth", 16L),
        ("tileheight", 16L),
        ("layers", layers),
        ("tilesets", tilesets)));

    /// <summary>A tile layer of a map of fixed size.</summary>
    public static OrderedDictionary<string, object?> LayerOf(string name, long width, params long[] data) => Map(
        ("type", "tilelayer"),
        ("name", name),
        ("x", 0L),
        ("y", 0L),
        ("width", width),
        ("height", data.Length / width),
        ("opacity", 1L),
        ("visible", true),
        ("data", Cells(data)));

    /// <summary>
    /// A tile layer of an infinite map, as chunks of <paramref name="size"/> by <paramref name="size"/>
    /// cells. Each chunk is its corner cell and its tiles.
    /// </summary>
    public static OrderedDictionary<string, object?> ChunksOf(string name, long size, params (long X, long Y, long[] Data)[] chunks) => Map(
        ("type", "tilelayer"),
        ("name", name),
        ("x", 0L),
        ("y", 0L),
        ("opacity", 1L),
        ("visible", true),
        ("chunks", chunks.Select(chunk => (object?)Map(
            ("x", chunk.X),
            ("y", chunk.Y),
            ("width", size),
            ("height", size),
            ("data", Cells(chunk.Data)))).ToArray()));

    private static object?[] Cells(long[] data) => [.. data.Select(value => (object?)value)];
}
