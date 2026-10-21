using System.Globalization;
using System.Text;

namespace Orblit.Asset;

/// <summary>What Dart's core library does with text where .NET does something a file name can see.</summary>
internal static class DartText
{
    private static readonly UTF8Encoding Strict = new(false, true);

    /// <summary><c>String.toLowerCase</c>, which also folds the Kelvin sign and the dotted capital I to ASCII.</summary>
    internal static string ToLower(string text) =>
        text.ToLowerInvariant().Replace('İ', 'i').Replace('K', 'k');

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

            if (!IsHex(text[at + 1]) || !IsHex(text[at + 2]))
            {
                throw new ArgumentException("Invalid URL encoding", nameof(text));
            }

            bytes.Add(Convert.ToByte(text.Substring(at + 1, 2), 16));
            at += 2;
        }

        return Decode(bytes.ToArray());
    }

    /// <summary><c>utf8.decode(bytes)</c>. Throws <see cref="FormatException"/> on a malformed byte. One leading byte order mark is dropped.</summary>
    internal static string Decode(ReadOnlySpan<byte> bytes)
    {
        string text;
        try
        {
            text = Strict.GetString(bytes);
        }
        catch (DecoderFallbackException error)
        {
            throw new FormatException(error.Message, error);
        }

        return text.Length > 0 && text[0] == '﻿' ? text[1..] : text;
    }

    // Dart writes three fraction digits, or six when the instant has microseconds.
    internal static string Iso(DateTimeOffset at)
    {
        var utc = at.UtcDateTime;
        return utc.ToString(
            utc.Ticks % 10_000 == 0 ? "yyyy-MM-dd'T'HH:mm:ss.fff'Z'" : "yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'",
            CultureInfo.InvariantCulture);
    }

    /// <summary>A decoded JSON value as Dart prints it, so a message reads the same in both languages.</summary>
    internal static string Print(object? value) => value switch
    {
        null => "null",
        bool flag => flag ? "true" : "false",
        double number => PrintNumber(number),
        IReadOnlyDictionary<string, object?> map => "{" + string.Join(", ", map.Select(e => $"{e.Key}: {Print(e.Value)}")) + "}",
        IReadOnlyList<object?> list => "[" + string.Join(", ", list.Select(Print)) + "]",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "",
    };

    // shortcut: no exponent form, so 1e21 prints as C# does. Upgrade when a message must show one.
    private static string PrintNumber(double number)
    {
        var text = number.ToString("R", CultureInfo.InvariantCulture);
        return double.IsFinite(number) && !text.Contains('E') && !text.Contains('.') ? text + ".0" : text;
    }

    private static bool IsHex(char c) => c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
}
