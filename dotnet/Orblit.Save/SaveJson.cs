using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Orblit.Save;

/// <summary>
/// The JSON the headers and history entries are written in. It writes what Dart's
/// <c>jsonEncode</c> writes, so a Dart file and a C# file of one save are the same bytes.
/// </summary>
internal static class SaveJson
{
    /// <summary>
    /// <paramref name="text"/> as a JSON string. Dart escapes the quote, the backslash,
    /// control characters and a lone surrogate, and nothing else, where System.Text.Json
    /// also escapes <c>&lt;</c>, <c>&gt;</c>, <c>&amp;</c>, <c>+</c> and every non-ASCII character.
    /// </summary>
    internal static string Quote(string text)
    {
        var builder = new StringBuilder(text.Length + 2).Append('"');
        for (var i = 0; i < text.Length; i++)
        {
            builder.Append(Escape(text, i) ?? text[i].ToString());
        }

        return builder.Append('"').ToString();
    }

    [SuppressMessage("Maintainability", "CA1502:Avoid excessive complexity", Justification = "F2: a list of cases is data.")]
    private static string? Escape(string text, int i) => text[i] switch
    {
        '"' => "\\\"",
        '\\' => "\\\\",
        '\b' => "\\b",
        '\t' => "\\t",
        '\n' => "\\n",
        '\f' => "\\f",
        '\r' => "\\r",
        < ' ' => Hex(text[i]),
        >= '\ud800' and <= '\udfff' when IsLoneSurrogate(text, i) => Hex(text[i]),
        _ => null,
    };

    private static string Hex(char c) => "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture);

    private static bool IsLoneSurrogate(string text, int i) =>
        char.IsHighSurrogate(text[i])
            ? !(i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            : !(i > 0 && char.IsHighSurrogate(text[i - 1]));

    /// <summary>Parses JSON text. Throws <see cref="FormatException"/> when it is not JSON.</summary>
    internal static JsonElement Parse(ReadOnlyMemory<byte> utf8)
    {
        try
        {
            using var document = JsonDocument.Parse(utf8);
            return document.RootElement.Clone();
        }
        catch (JsonException e)
        {
            throw new FormatException(e.Message, e);
        }
    }

    /// <summary>
    /// True when <paramref name="map"/> is an object whose <paramref name="name"/> is a
    /// number equal to <paramref name="expected"/>. Dart compares with <c>==</c>, so 1.0
    /// matches 1.
    /// </summary>
    internal static bool HasNumber(JsonElement map, string name, int expected) =>
        map.ValueKind == JsonValueKind.Object
        && map.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDouble(out var number)
        && number == expected;

    /// <summary>True when <paramref name="name"/> is a string in <paramref name="map"/>.</summary>
    internal static bool TryString(JsonElement map, string name, [NotNullWhen(true)] out string? value)
    {
        value = null;
        if (map.TryGetProperty(name, out var found) && found.ValueKind == JsonValueKind.String)
        {
            value = Unescape(found.GetRawText());
        }

        return value is not null;
    }

    // GetString refuses a lone surrogate, which Dart's decoder keeps. The parser has
    // already checked the escapes, so each backslash is followed by a valid one.
    private static string Unescape(string quoted)
    {
        const string escaped = "\"\\/bfnrt";
        const string plain = "\"\\/\b\f\n\r\t";
        var text = quoted.AsSpan(1, quoted.Length - 2);
        var output = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\\')
            {
                output.Append(text[i]);
            }
            else if (text[++i] == 'u')
            {
                output.Append((char)ushort.Parse(text.Slice(i + 1, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
                i += 4;
            }
            else
            {
                output.Append(plain[escaped.IndexOf(text[i], StringComparison.Ordinal)]);
            }
        }

        return output.ToString();
    }

    /// <summary>
    /// True when <paramref name="name"/> is a whole number in <paramref name="map"/>.
    /// A number written with a fraction or an exponent is a double in Dart, so it is
    /// not one.
    /// </summary>
    internal static bool TryWhole(JsonElement map, string name, out long value)
    {
        value = 0;
        return map.TryGetProperty(name, out var found)
            && found.ValueKind == JsonValueKind.Number
            && found.TryGetInt64(out value);
    }
}
