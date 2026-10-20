using System.Globalization;
using System.Text;
using Orblit.VectorMath;

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
            case int or long or double:
                WriteNumber(builder, value);
                break;
            case string text:
                DartQuote.Append(builder, text);
                break;
            case IReadOnlyDictionary<string, object?> map:
                WriteMap(builder, map, indent, level);
                break;
            case IReadOnlyList<object?> list:
                WriteList(builder, list, indent, level);
                break;
            default:
                throw new ArgumentException($"{value.GetType().Name} is not written.", nameof(value));
        }
    }

    private static void WriteNumber(StringBuilder builder, object value)
    {
        if (value is not double number)
        {
            builder.Append(Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture));
            return;
        }

        // Dart's jsonEncode throws for these, because JSON has no word for them.
        if (!double.IsFinite(number))
        {
            throw new ArgumentOutOfRangeException(nameof(value), number, "JSON has no word for this number.");
        }

        builder.Append(Doubles.ToDartString(number));
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
            DartQuote.Append(builder, key);
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
}
