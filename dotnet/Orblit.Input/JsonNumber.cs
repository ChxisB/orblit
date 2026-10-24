namespace Orblit.Input;

internal static class JsonNumber
{
    /// <summary>
    /// A decoded JSON number as a double, or null for anything else. Decoded JSON holds a whole
    /// number as an int or a long and any other number as a double.
    /// </summary>
    internal static double? Of(object? value) => value switch
    {
        long whole => whole,
        int whole => whole,
        double number => number,
        _ => null,
    };
}
