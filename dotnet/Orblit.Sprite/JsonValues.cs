using Orblit.VectorMath;

namespace Orblit.Sprite;

/// <summary>The two reads every file reader here does on a parsed JSON value.</summary>
internal static class JsonValues
{
    private static readonly IReadOnlyDictionary<string, object?> NoBox = new Dictionary<string, object?>();

    /// <summary>The map <paramref name="value"/> is, or an empty one when it is anything else.</summary>
    internal static IReadOnlyDictionary<string, object?> Box(object? value) =>
        value as IReadOnlyDictionary<string, object?> ?? NoBox;

    /// <summary>
    /// A whole number as it is, a fraction rounded half away from zero, anything else nought.
    /// Infinity throws, because Dart's <c>round()</c> does.
    /// </summary>
    internal static long Int(object? value) => value switch
    {
        long whole => whole,
        double fraction => Doubles.Round(fraction),
        _ => 0,
    };
}
