using System.Text.RegularExpressions;
using Orblit.Json;
using Orblit.VectorMath;

namespace Orblit.Ui;

/// <summary>A colour as 32 bits with the alpha first: <c>0xAARRGGBB</c>.</summary>
public readonly partial record struct UiColour(uint Argb)
{
    [GeneratedRegex(@"^rgba?\(([^)]*)\)\z", RegexOptions.CultureInvariant)]
    private static partial Regex Call();

    /// <summary>
    /// Reads <c>#rgb</c>, <c>#rrggbb</c>, <c>#rrggbbaa</c>, <c>rgb(...)</c> and <c>rgba(...)</c>. Null
    /// when the text is none of those. A channel that is not a number reads as zero.
    /// </summary>
    public static UiColour? FromText(string value)
    {
        var text = PlainText.Trim(value);
        return text.StartsWith('#') ? FromHash(text[1..]) : FromCall(text);
    }

    private static UiColour? FromHash(string digits)
    {
        var expanded = digits.Length switch
        {
            3 => $"FF{digits[0]}{digits[0]}{digits[1]}{digits[1]}{digits[2]}{digits[2]}",
            6 => "FF" + digits,
            // CSS puts the alpha last and the colour puts it first.
            8 => digits[6..] + digits[..6],
            _ => null,
        };
        return expanded is not null && PlainText.ParseHex(expanded) is { } number
            ? new UiColour(unchecked((uint)number))
            : null;
    }

    private static UiColour? FromCall(string text)
    {
        var call = Call().Match(text);
        if (!call.Success)
        {
            return null;
        }

        var parts = PlainText.SplitSpace(call.Groups[1].Value.Replace(',', ' ').Replace('/', ' '))
            .Where(part => part.Length > 0)
            .ToList();
        if (parts.Count < 3)
        {
            return null;
        }

        var alpha = parts.Count > 3 ? Clamp((PlainText.ParseDouble(parts[3]) ?? 1) * 255) : 255;
        return new UiColour(unchecked((uint)((alpha << 24) | (Channel(parts[0]) << 16) | (Channel(parts[1]) << 8) | Channel(parts[2]))));
    }

    private static long Channel(string part)
    {
        if (!part.EndsWith('%'))
        {
            return Clamp(PlainText.ParseDouble(part) ?? 0);
        }

        return PlainText.ParseDouble(part[..^1]) is { } percent ? Clamp(percent * 2.55) : 0;
    }

    private static long Clamp(double value) => Math.Clamp(Doubles.Round(value), 0, 255);
}
