namespace Orblit.Mesh;

/// <summary>The reads every <c>FromJson</c> here does on a parsed JSON value.</summary>
internal static class JsonValues
{
    /// <summary>The map <paramref name="value"/> is, or null when it is anything else.</summary>
    internal static IReadOnlyDictionary<string, object?>? Map(object? value) =>
        value as IReadOnlyDictionary<string, object?>;

    internal static IReadOnlyList<object?>? List(object? value) => value as IReadOnlyList<object?>;

    /// <summary>A JSON number as a double. Null for anything that is not one.</summary>
    internal static double? Num(object? value) => value switch
    {
        long whole => whole,
        double fraction => fraction,
        _ => null,
    };

    /// <summary>
    /// A number, nought for null and a throw for anything else.
    /// </summary>
    internal static double NumOrZero(object? value) =>
        value is null ? 0 : Num(value) ?? throw new InvalidCastException($"{value.GetType()} is not a number.");

    /// <summary>
    /// A JSON integer, held in 32 bits. A larger one keeps its sign and its distance from every
    /// index a mesh could hold, so a reader that bounds-checks still refuses it.
    /// </summary>
    internal static int? Whole(object? value) =>
        value is long whole ? (int)Math.Clamp(whole, int.MinValue, int.MaxValue) : null;

    internal static bool IsTrue(object? value) => value is true;

    /// <summary>Sets <paramref name="key"/> when <paramref name="when"/> holds, so a file only names what differs from the default.</summary>
    internal static void Put(OrderedDictionary<string, object?> json, bool when, string key, object? value)
    {
        if (when)
        {
            json[key] = value;
        }
    }
}
