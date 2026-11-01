using Orblit.Json;

namespace Orblit.Motion;

/// <summary>
/// Decoded JSON as the text of a blend file: anything that fits on a line is written on one, and the rest is
/// indented, so a state, a point and a change each read as one line.
/// </summary>
internal static class BlendText
{
    private const int LineLength = 80;

    internal static string Of(JsonMap json) => $"{Write(json, string.Empty)}\n";

    private static string Write(object? value, string indent)
    {
        var flat = JsonText.Encode(value);
        if (indent.Length > 0 && indent.Length + flat.Length <= LineLength)
        {
            return flat;
        }

        var inner = $"{indent}  ";
        return value switch
        {
            JsonMap { Count: 0 } => "{}",
            JsonMap map => $"{{\n{string.Join(",\n", map.Select(entry => $"{inner}{JsonText.Encode(entry.Key)}: {Write(entry.Value, inner)}"))}\n{indent}}}",
            JsonList { Count: 0 } => "[]",
            JsonList list => $"[\n{string.Join(",\n", list.Select(one => $"{inner}{Write(one, inner)}"))}\n{indent}]",
            _ => flat,
        };
    }
}
