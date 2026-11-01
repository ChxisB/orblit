using System.Globalization;

namespace Orblit.Dialogue;

/// <summary>A decoded JSON value as an error message prints it.</summary>
internal static class Printed
{
    internal static string Text(object? value) => value switch
    {
        null => "null",
        bool flag => flag ? "true" : "false",
        double number => Number(number),
        IReadOnlyDictionary<string, object?> map => "{" + string.Join(", ", map.Select(e => $"{e.Key}: {Text(e.Value)}")) + "}",
        IReadOnlyList<object?> list => "[" + string.Join(", ", list.Select(Text)) + "]",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
    };

    // shortcut: no exponent form, so 1e21 prints as C# does. Upgrade when a message must show one.
    private static string Number(double number)
    {
        var text = number.ToString("R", CultureInfo.InvariantCulture);
        return double.IsFinite(number) && !text.Contains('E') && !text.Contains('.') ? text + ".0" : text;
    }
}
