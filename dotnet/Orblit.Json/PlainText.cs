using System.Globalization;
using System.Text.RegularExpressions;

namespace Orblit.Json;

/// <summary>
/// Text rules where .NET does something a file format can see. A trim and a pattern do not take
/// the same whitespace. A number reads with a space on either side, and a whole number may be
/// hexadecimal but a fraction never is. Lower-casing folds two characters past ASCII.
/// </summary>
public static partial class PlainText
{
    /// <summary>The characters a trim takes off.</summary>
    public static bool IsSpace(char c) =>
        c is (>= '\t' and <= '\r') or ' ' or '\u0085' or '\u00A0' or '\u1680' or (>= '\u2000' and <= '\u200A')
            or '\u2028' or '\u2029' or '\u202F' or '\u205F' or '\u3000' or '\uFEFF';

    /// <summary>What <c>\s</c> matches in a file format's pattern. The same as a trim, less the next-line character.</summary>
    public static bool IsPatternSpace(char c) => c != '\u0085' && IsSpace(c);

    /// <summary>Takes <see cref="IsSpace"/> characters off both ends.</summary>
    public static string Trim(string text) => TrimStart(TrimEnd(text));

    /// <summary>Takes <see cref="IsSpace"/> characters off the end.</summary>
    public static string TrimEnd(string text)
    {
        var end = text.Length;
        while (end > 0 && IsSpace(text[end - 1]))
        {
            end--;
        }

        return text[..end];
    }

    private static string TrimStart(string text)
    {
        var start = 0;
        while (start < text.Length && IsSpace(text[start]))
        {
            start++;
        }

        return text[start..];
    }

    /// <summary>
    /// Splits on runs of <see cref="IsPatternSpace"/> characters. A run at either end leaves an
    /// empty piece, and no text at all is one empty piece.
    /// </summary>
    public static List<string> SplitSpace(string text)
    {
        var pieces = new List<string>();
        var start = 0;
        var at = 0;
        while (at < text.Length)
        {
            if (!IsPatternSpace(text[at]))
            {
                at++;
                continue;
            }

            pieces.Add(text[start..at]);
            while (at < text.Length && IsPatternSpace(text[at]))
            {
                at++;
            }

            start = at;
        }

        pieces.Add(text[start..]);
        return pieces;
    }

    /// <summary>Lower-cases the text. The Kelvin sign and the dotted capital I fold to ASCII.</summary>
    public static string ToLower(string text) =>
        text.ToLowerInvariant().Replace('\u0130', 'i').Replace('\u212A', 'k');

    [GeneratedRegex(@"^[+-]?(?:NaN|Infinity|(?:[0-9]+\.?[0-9]*|\.[0-9]+)(?:[eE][+-]?[0-9]+)?)\z", RegexOptions.CultureInvariant)]
    private static partial Regex FractionShape();

    [GeneratedRegex(@"^([+-]?)(?:0[xX]([0-9a-fA-F]+)|([0-9]+))\z", RegexOptions.CultureInvariant)]
    private static partial Regex WholeShape();

    /// <summary>Reads a fraction, with a space allowed on either side. Null when it is not a number.</summary>
    public static double? ParseDouble(string text)
    {
        var word = Trim(text);
        if (!FractionShape().IsMatch(word))
        {
            return null;
        }

        var unsigned = word.TrimStart('+', '-');
        var negative = word.StartsWith('-');
        return unsigned switch
        {
            "NaN" => double.NaN,
            "Infinity" => negative ? double.NegativeInfinity : double.PositiveInfinity,
            _ => double.Parse(word, NumberStyles.Float, CultureInfo.InvariantCulture),
        };
    }

    /// <summary>
    /// Reads a whole number. A decimal that does not fit 64 bits is not a number. A hexadecimal one
    /// of up to sixteen digits is, and one with its top bit set wraps negative.
    /// </summary>
    public static long? ParseInt(string text)
    {
        var shape = WholeShape().Match(Trim(text));
        if (!shape.Success)
        {
            return null;
        }

        var negative = shape.Groups[1].Value == "-";
        if (shape.Groups[3].Success)
        {
            var digits = (negative ? "-" : string.Empty) + shape.Groups[3].Value;
            return long.TryParse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var whole)
                ? whole
                : null;
        }

        var hex = shape.Groups[2].Value.TrimStart('0');
        if (hex.Length > 16)
        {
            return null;
        }

        var magnitude = hex.Length == 0 ? 0UL : ulong.Parse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        if (negative && magnitude > 1UL << 63)
        {
            return null;
        }

        return unchecked(negative ? -(long)magnitude : (long)magnitude);
    }
}
