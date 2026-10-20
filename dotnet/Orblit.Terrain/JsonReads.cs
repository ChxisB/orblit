using Orblit.VectorMath;

namespace Orblit.Terrain;

/// <summary>The three reads every settings reader here does on a parsed JSON map.</summary>
internal static class JsonReads
{
    /// <summary>The map <paramref name="value"/> is, or null when it is anything else.</summary>
    internal static IReadOnlyDictionary<string, object?>? Map(object? value) =>
        value as IReadOnlyDictionary<string, object?>;

    /// <summary>A finite number as a double, or null.</summary>
    internal static double? Number(IReadOnlyDictionary<string, object?> json, string key) => json.GetValueOrDefault(key) switch
    {
        long whole => whole,
        double fraction when double.IsFinite(fraction) => fraction,
        _ => null,
    };

    /// <summary>A whole number as it is, a finite double with no fraction as the number it is, or null.</summary>
    internal static long? Integer(IReadOnlyDictionary<string, object?> json, string key) => json.GetValueOrDefault(key) switch
    {
        long whole => whole,
        double fraction when double.IsFinite(fraction) && fraction == Math.Round(fraction) => Doubles.Floor(fraction),
        _ => null,
    };

    /// <summary>
    /// What Dart's string interpolation makes of a parsed JSON value: <c>2.0</c> for a double,
    /// strings bare, and <c>[1, a]</c> and <c>{k: v}</c> for the collections.
    /// </summary>
    internal static string Describe(object? value) => value switch
    {
        null => "null",
        bool flag => flag ? "true" : "false",
        long whole => whole.ToString(System.Globalization.CultureInfo.InvariantCulture),
        double fraction => Doubles.ToDartString(fraction),
        IReadOnlyDictionary<string, object?> map =>
            "{" + string.Join(", ", map.Select(pair => $"{pair.Key}: {Describe(pair.Value)}")) + "}",
        IReadOnlyList<object?> list => "[" + string.Join(", ", list.Select(Describe)) + "]",
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>A string that is not empty, or null.</summary>
    internal static string? Text(IReadOnlyDictionary<string, object?> json, string key) =>
        json.GetValueOrDefault(key) is string { Length: > 0 } text ? text : null;
}
