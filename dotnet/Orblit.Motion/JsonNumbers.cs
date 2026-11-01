namespace Orblit.Motion;

internal static class JsonNumbers
{
    /// <summary>A JSON number that is finite, or null for anything else.</summary>
    internal static double? Finite(object? raw) => raw switch
    {
        long whole => whole,
        double fraction when double.IsFinite(fraction) => fraction,
        _ => null,
    };
}
