using System.Globalization;
using System.Text;

namespace Orblit.Json;

internal static class JsonWriter
{
    internal static string Write(object? value, string? indent)
    {
        var builder = new StringBuilder();
        WriteValue(builder, value, indent, 0);
        return builder.ToString();
    }

    private static void WriteValue(StringBuilder builder, object? value, string? indent, int level)
    {
        switch (value)
        {
            case null:
                builder.Append("null");
                break;
            case bool flag:
                builder.Append(flag ? "true" : "false");
                break;
            case int or long:
                builder.Append(Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
                break;
            case string text:
                Quote(builder, text);
                break;
            case IReadOnlyDictionary<string, object?> map:
                WriteMap(builder, map, indent, level);
                break;
            case IReadOnlyList<object?> list:
                WriteList(builder, list, indent, level);
                break;
            default:
                // shortcut: no doubles, because Dart's double.toString has no .NET twin yet, add one when a package writes a double.
                throw new ArgumentException($"{value.GetType().Name} is not written.", nameof(value));
        }
    }

    private static void WriteMap(StringBuilder builder, IReadOnlyDictionary<string, object?> map, string? indent, int level)
    {
        if (map.Count == 0)
        {
            builder.Append("{}");
            return;
        }

        builder.Append('{');
        var first = true;
        foreach (var (key, item) in map)
        {
            Separate(builder, first, indent, level + 1);
            first = false;
            Quote(builder, key);
            builder.Append(indent is null ? ":" : ": ");
            WriteValue(builder, item, indent, level + 1);
        }

        Newline(builder, indent, level).Append('}');
    }

    private static void WriteList(StringBuilder builder, IReadOnlyList<object?> list, string? indent, int level)
    {
        if (list.Count == 0)
        {
            builder.Append("[]");
            return;
        }

        builder.Append('[');
        for (var i = 0; i < list.Count; i++)
        {
            Separate(builder, i == 0, indent, level + 1);
            WriteValue(builder, list[i], indent, level + 1);
        }

        Newline(builder, indent, level).Append(']');
    }

    private static void Separate(StringBuilder builder, bool first, string? indent, int level)
    {
        if (!first)
        {
            builder.Append(',');
        }

        Newline(builder, indent, level);
    }

    private static StringBuilder Newline(StringBuilder builder, string? indent, int level)
    {
        if (indent is null)
        {
            return builder;
        }

        builder.Append('\n');
        for (var i = 0; i < level; i++)
        {
            builder.Append(indent);
        }

        return builder;
    }

    // Dart escapes the quote, the backslash, control characters and a lone surrogate, and nothing else.
    private static void Quote(StringBuilder builder, string text)
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
