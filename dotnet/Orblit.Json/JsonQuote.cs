using System.Globalization;
using System.Text;

namespace Orblit.Json;

/// <summary>
/// A string as JSON text. Linked into Orblit.Codegen, which targets netstandard2.0, so it keeps to what
/// that target has.
/// </summary>
internal static class JsonQuote
{
    // The format escapes the quote, the backslash, control characters and a lone surrogate, and nothing else.
    internal static void Append(StringBuilder builder, string text)
    {
        builder.Append('"');
        for (var i = 0; i < text.Length; i++)
        {
            builder.Append(Escape(text, i) ?? text[i].ToString());
        }

        builder.Append('"');
    }

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
}
