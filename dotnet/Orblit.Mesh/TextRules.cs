using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Orblit.Json;

namespace Orblit.Mesh;

/// <summary>
/// A whole number or a fraction, as a JSON number holds one. A reader that is handed
/// <c>5</c> and <c>5.0</c> keeps them apart, and a count written as <c>1e999</c> is not a count.
/// </summary>
internal readonly record struct WholeOrFraction(bool IsWhole, long Whole, double Fraction)
{
    internal static WholeOrFraction Of(long whole) => new(true, whole, 0);

    internal static WholeOrFraction Of(double fraction) => new(false, 0, fraction);

    internal double ToDouble() => IsWhole ? Whole : Fraction;
}

/// <summary>
/// Text rules where .NET does something a file format can see.
/// Whitespace is not the same set in a trim and in a pattern. A number reads with a space on
/// either side and a hexadecimal whole number but never a hexadecimal fraction. A byte order mark
/// is dropped from decoded text. A malformed escape in a name is an error, not a pass-through.
/// </summary>
internal static class TextRules
{
    private static readonly UTF8Encoding Strict = new(false, true);

    /// <summary><c>num.tryParse</c>: a whole number when it reads as one, otherwise a fraction.</summary>
    internal static WholeOrFraction? ParseNum(string text)
    {
        if (PlainText.ParseInt(text) is { } whole)
        {
            return WholeOrFraction.Of(whole);
        }

        return PlainText.ParseDouble(text) is { } fraction ? WholeOrFraction.Of(fraction) : null;
    }

    /// <summary>
    /// <c>utf8.decode(bytes, allowMalformed: true)</c>: each bad stretch is one U+FFFD and one
    /// leading byte order mark is dropped.
    /// </summary>
    internal static string DecodeLenient(ReadOnlySpan<byte> bytes) => DropMark(Encoding.UTF8.GetString(bytes));

    /// <summary><c>utf8.decode(bytes)</c>. Throws <see cref="FormatException"/> on a malformed byte.</summary>
    internal static string Decode(ReadOnlySpan<byte> bytes)
    {
        try
        {
            return DropMark(Strict.GetString(bytes));
        }
        catch (DecoderFallbackException error)
        {
            throw new FormatException(error.Message, error);
        }
    }

    private static string DropMark(string text) => text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;

    /// <summary>
    /// Base64 decoding, or null for bad text. Either alphabet, in any mix. The length is a
    /// multiple of four with the padding written, and the unused bits are nought.
    /// </summary>
    internal static byte[]? DecodeBase64(string text)
    {
        if (text.Length % 4 != 0)
        {
            return null;
        }

        var padding = text.EndsWith("==", StringComparison.Ordinal) ? 2 : text.EndsWith('=') ? 1 : 0;
        var values = new List<int>(text.Length);
        foreach (var c in text.AsSpan(0, text.Length - padding))
        {
            var value = Base64Value(c);
            if (value < 0)
            {
                return null;
            }

            values.Add(value);
        }

        return FromSextets(values, padding);
    }

    private static int Base64Value(char c) => c switch
    {
        >= 'A' and <= 'Z' => c - 'A',
        >= 'a' and <= 'z' => c - 'a' + 26,
        >= '0' and <= '9' => c - '0' + 52,
        '+' or '-' => 62,
        '/' or '_' => 63,
        _ => -1,
    };

    private static byte[]? FromSextets(List<int> values, int padding)
    {
        var unusedBits = padding == 0 ? 0 : padding == 1 ? 2 : 4;
        if (padding > 0 && values.Count > 0 && (values[^1] & ((1 << unusedBits) - 1)) != 0)
        {
            return null;
        }

        var bytes = new List<byte>(values.Count * 3 / 4);
        var buffer = 0;
        var bits = 0;
        foreach (var value in values)
        {
            buffer = (buffer << 6) | value;
            bits += 6;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)((buffer >> bits) & 0xFF));
            }
        }

        return [.. bytes];
    }

    /// <summary>
    /// <c>Uri.decodeComponent</c>. A plus stays a plus. A character past ASCII, a percent sign
    /// without two hexadecimal digits after it and bytes that are not UTF-8 are all errors.
    /// </summary>
    internal static string DecodeUriComponent(string text)
    {
        var bytes = new List<byte>(text.Length);
        for (var at = 0; at < text.Length; at++)
        {
            var c = text[at];
            if (c > 127)
            {
                throw new ArgumentException("Illegal percent encoding in URI", nameof(text));
            }

            if (c != '%')
            {
                bytes.Add((byte)c);
                continue;
            }

            if (at + 3 > text.Length)
            {
                throw new ArgumentException("Truncated URI", nameof(text));
            }

            if (!byte.TryParse(text.AsSpan(at + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var one)
                || !IsHex(text[at + 1]) || !IsHex(text[at + 2]))
            {
                throw new ArgumentException("Invalid URL encoding", nameof(text));
            }

            bytes.Add(one);
            at += 2;
        }

        return Decode(bytes.ToArray());
    }

    private static bool IsHex(char c) => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
}
