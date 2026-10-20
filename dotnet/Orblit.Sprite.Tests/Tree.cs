using Orblit.Json;

namespace Orblit.Sprite.Tests;

/// <summary>Builds the JSON documents the Dart tests wrote as map and list literals.</summary>
internal static class Tree
{
    public static OrderedDictionary<string, object?> Map(params (string Key, object? Value)[] pairs)
    {
        var map = new OrderedDictionary<string, object?>();
        foreach (var (key, value) in pairs)
        {
            map[key] = value;
        }

        return map;
    }

    public static OrderedDictionary<string, object?> With(OrderedDictionary<string, object?> map, params (string Key, object? Value)[] changes)
    {
        var copy = new OrderedDictionary<string, object?>(map);
        foreach (var (key, value) in changes)
        {
            copy[key] = value;
        }

        return copy;
    }

    public static object?[] List(params object?[] items) => items;

    public static T Some<T>(T? value)
        where T : class
    {
        Assert.NotNull(value);
        return value;
    }

    public static string Text(object? document) => DartJson.Encode(document);

    public static OrderedDictionary<string, object?> Box(long x, long y, long w, long h) =>
        Map(("x", x), ("y", y), ("w", w), ("h", h));
}
