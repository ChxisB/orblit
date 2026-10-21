using System.Globalization;
using Orblit.VectorMath;

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

    /// <summary>
    /// A whole JSON number as an <c>int</c>, with one past 32 bits held at the end of the range. Dart's
    /// <c>int</c> has 64 bits, so a range check on the number still refuses it where a narrowing would
    /// throw or wrap into range.
    /// </summary>
    internal static int Saturated(object? value) => value switch
    {
        int whole => whole,
        long whole => (int)Math.Clamp(whole, int.MinValue, int.MaxValue),
        _ => throw new InvalidCastException($"{value?.GetType().Name ?? "null"} is not an int."),
    };

    /// <summary>
    /// <c>map[key] is int ? map[key] as int : null</c>: the whole number at <paramref name="key"/>, held at the
    /// end of the 32-bit range as <see cref="Saturated"/> holds it, or null for anything else, including a
    /// fraction, where <see cref="IntAt"/> would throw.
    /// </summary>
    internal static int? WholeAt(GltfMap map, string key) =>
        Get(map, key) is { } value && IsInt(value) ? Saturated(value) : null;

    /// <summary>A JSON number as a double, or null for anything that is not a number: <c>value is num</c>.</summary>
    internal static double? AsNumber(object? value) => value switch
    {
        int whole => whole,
        long whole => whole,
        double number => number,
        _ => null,
    };

    /// <summary>
    /// What Dart's string interpolation makes of a JSON value, so a key built from one tells <c>1</c> from
    /// <c>1.0</c> as Dart's does.
    /// </summary>
    internal static string Display(object? value) => value switch
    {
        null => "null",
        string text => text,
        bool flag => flag ? "true" : "false",
        int or long => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty,
        double number => Doubles.ToDartString(number),
        List<object?> list => $"[{string.Join(", ", list.Select(Display))}]",
        GltfMap map => $"{{{string.Join(", ", map.Select(entry => $"{entry.Key}: {Display(entry.Value)}"))}}}",
        _ => throw new ArgumentException($"{value.GetType().Name} is not a JSON value.", nameof(value)),
    };
}
