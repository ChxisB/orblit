namespace Orblit.Asset;

/// <summary>
/// The reads a glTF rewrite makes on a document's JSON, as Dart's casts make them: a number that
/// is not a whole number is not an <c>int</c>, and asking for one is an error.
/// </summary>
internal static class GltfJson
{
    /// <summary>The value at <paramref name="key"/>, or null when there is none.</summary>
    internal static object? Get(GltfMap map, string key) => map.TryGetValue(key, out var value) ? value : null;

    /// <summary><c>value as int?</c>. JSON reads whole numbers as <c>long</c>, so one is narrowed.</summary>
    internal static int? AsInt(object? value) => value switch
    {
        null => null,
        int whole => whole,
        long whole => checked((int)whole),
        _ => throw new InvalidCastException($"{value.GetType().Name} is not an int."),
    };

    /// <summary><c>map[key] as int?</c>.</summary>
    internal static int? IntAt(GltfMap map, string key) => AsInt(Get(map, key));

    /// <summary><c>(map[key] as int?) ?? fallback</c>.</summary>
    internal static int IntOr(GltfMap map, string key, int fallback) => IntAt(map, key) ?? fallback;

    /// <summary><c>map[key] as int</c>.</summary>
    internal static int RequiredInt(GltfMap map, string key) =>
        IntAt(map, key) ?? throw new InvalidCastException($"\"{key}\" is not an int.");

    /// <summary><c>value is int</c>.</summary>
    internal static bool IsInt(object? value) => value is int or long;
}
