using Orblit.Json;

namespace Orblit.Motion.Tests;

/// <summary>Builds the JSON documents the ported tests wrote as map and list literals.</summary>
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

    public static OrderedDictionary<string, object?> With(
        IReadOnlyDictionary<string, object?> map,
        params (string Key, object? Value)[] changes)
    {
        var copy = new OrderedDictionary<string, object?>();
        foreach (var (key, value) in map)
        {
            copy[key] = value;
        }

        foreach (var (key, value) in changes)
        {
            copy[key] = value;
        }

        return copy;
    }

    public static object?[] List(params object?[] items) => items;

    /// <summary>
    /// Asserts two trees are equal the way the file format sees them: key order is ignored, and
    /// a whole number is not a fractional one, so <c>2</c> and <c>2.0</c> differ.
    /// </summary>
    public static void Same(object? expected, object? actual) =>
        Assert.Equal(JsonText.Encode(Canonical(expected)), JsonText.Encode(Canonical(actual)));

    public static T Some<T>(T? value)
        where T : class
    {
        Assert.NotNull(value);
        return value;
    }

    private static object? Canonical(object? node) => node switch
    {
        IReadOnlyDictionary<string, object?> map => Sorted(map),
        IEnumerable<object?> list => list.Select(Canonical).ToList(),
        _ => node,
    };

    private static OrderedDictionary<string, object?> Sorted(IReadOnlyDictionary<string, object?> map)
    {
        var sorted = new OrderedDictionary<string, object?>();
        foreach (var key in map.Keys.Order(StringComparer.Ordinal))
        {
            sorted[key] = Canonical(map[key]);
        }

        return sorted;
    }
}
