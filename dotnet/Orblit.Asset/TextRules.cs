using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Orblit.Json;

namespace Orblit.Asset;

/// <summary>Text rules where .NET does something a file name can see.</summary>
internal static class TextRules
{
    // DateTime.MaxValue.Ticks, written out because a pattern needs a constant.
    private const long LastTick = 3_155_378_975_999_999_999;

    private static readonly UTF8Encoding Strict = new(false, true);

    // The shape DateTime.tryParse reads. Each part is loose: no dashes, a space for the T,
    // an hour on its own, an offset of any size.
    private static readonly Regex TimeShape = new(
        "^([+-]?[0-9]{4,6})-?([0-9]{2})-?([0-9]{2})(?:[ T]([0-9]{2})(?::?([0-9]{2})(?::?([0-9]{2})(?:[.,]([0-9]+))?)?)?"
        + "( ?[zZ]| ?([-+])([0-9]{2})(?::?([0-9]{2}))?)?)?\\z",
        RegexOptions.CultureInvariant);

    /// <summary>
    /// <c>num.tryParse</c>: a boxed <see cref="long"/> when the text reads as a whole number, otherwise a boxed
    /// <see cref="double"/>, or null when it reads as neither.
    /// </summary>
    internal static object? ParseNum(string text)
    {
        if (PlainText.ParseInt(text) is { } whole)
        {
            return whole;
        }

        return PlainText.ParseDouble(text);
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

            if (!IsHex(text[at + 1]) || !IsHex(text[at + 2]))
            {
                throw new ArgumentException("Invalid URL encoding", nameof(text));
            }

            bytes.Add(Convert.ToByte(text.Substring(at + 1, 2), 16));
            at += 2;
        }

        return Decode(bytes.ToArray());
    }

    /// <summary>
    /// <see cref="DecodeUriComponent"/>, or <paramref name="text"/> as given when it is not valid percent
    /// encoding. A glTF URI that is not encoded at all is common, and a file beside the model by that name
    /// is the best guess left. Bytes that are not UTF-8 still throw <see cref="FormatException"/>.
    /// </summary>
    internal static string PercentDecoded(string text)
    {
        try
        {
            return DecodeUriComponent(text);
        }
        catch (ArgumentException)
        {
            return text;
        }
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

    /// <summary><c>Uri.encodeComponent</c>: everything but <c>A-Za-z0-9-_.!~*'()</c> as upper-case percent-escaped UTF-8.</summary>
    internal static string EncodeComponent(string text)
    {
        var encoded = new StringBuilder(text.Length);
        foreach (var b in Encoding.UTF8.GetBytes(text))
        {
            if (b < 128 && (char.IsAsciiLetterOrDigit((char)b) || "-_.!~*'()".Contains((char)b)))
            {
                encoded.Append((char)b);
            }
            else
            {
                encoded.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return encoded.ToString();
    }

    /// <summary>
    /// <c>DateTime.tryParse</c>, for the instants the fetch records hold. A month, day or hour past its range
    /// carries into the next one. A time without a zone is read as UTC, not as
    /// local time. An instant outside the years 1 to 9999 is refused, because a <see cref="DateTimeOffset"/>
    /// cannot hold it.
    /// </summary>
    internal static bool TryParseTime(string text, out DateTimeOffset at)
    {
        at = default;
        var match = TimeShape.Match(text);
        if (!match.Success)
        {
            return false;
        }

        if (TimeTicks(match) is not { } ticks)
        {
            return false;
        }

        at = new DateTimeOffset(ticks, TimeSpan.Zero);
        return true;
    }

    private static long? TimeTicks(Match match)
    {
        var months = TimePart(match, 2, 1) - 1;
        var carry = months < 0 ? -1 : months / 12;
        var year = TimePart(match, 1, 0) + carry;
        if (year is < 1 or > 9999)
        {
            return null;
        }

        var first = new DateTime((int)year, (int)(months - carry * 12) + 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks;
        var seconds = ((TimePart(match, 3, 1) - 1) * 86_400)
            + (TimePart(match, 4, 0) * 3_600)
            + (TimePart(match, 5, 0) * 60)
            + TimePart(match, 6, 0)
            - OffsetSeconds(match);
        var ticks = first + (seconds * TimeSpan.TicksPerSecond) + (Microseconds(match) * 10);
        return ticks is >= 0 and <= LastTick ? ticks : null;
    }

    private static long TimePart(Match match, int group, long absent) =>
        match.Groups[group].Success ? long.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture) : absent;

    private static long OffsetSeconds(Match match)
    {
        var sign = match.Groups[9].Value == "-" ? -1 : 1;
        return sign * ((TimePart(match, 10, 0) * 3_600) + (TimePart(match, 11, 0) * 60));
    }

    // The first six digits of the fraction are kept and the rest dropped.
    private static long Microseconds(Match match) =>
        match.Groups[7].Success ? long.Parse((match.Groups[7].Value + "000000")[..6], CultureInfo.InvariantCulture) : 0;

    // Three fraction digits, or six when the instant has microseconds.
    internal static string Iso(DateTimeOffset at)
    {
        var utc = at.UtcDateTime;
        return utc.ToString(
            utc.Ticks % 10_000 == 0 ? "yyyy-MM-dd'T'HH:mm:ss.fff'Z'" : "yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'",
            CultureInfo.InvariantCulture);
    }

    /// <summary>A decoded JSON value as an error message prints it.</summary>
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
