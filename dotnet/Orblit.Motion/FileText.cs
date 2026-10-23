using System.Collections.Frozen;
using Orblit.Json;

namespace Orblit.Motion;

/// <summary>
/// Decoded JSON as the text of a clip or a cutscene file: indented like a scene, with one key, mark,
/// shot or sound to a line.
/// </summary>
/// <remarks>
/// A scene's writer would put every number of every key on a line of its own, and a two-second walk
/// would be ten thousand lines that nobody could review. A key to a line makes the diff of two takes
/// a diff of the keys that changed.
/// </remarks>
internal static class FileText
{
    /// <summary>The lists written an item to a line.</summary>
    private static readonly FrozenSet<string> FlatLists = new[] { "keys", "marks", "shots", "sounds" }.ToFrozenSet();

    internal static string Of(JsonMap json) => $"{Write(json, string.Empty, flat: false)}\n";

    private static string Write(object? value, string indent, bool flat) => value switch
    {
        JsonMap map => WriteMap(map, indent),
        JsonList list => WriteList(list, indent, flat),
        _ => JsonText.Encode(value),
    };

    private static string WriteMap(JsonMap map, string indent)
    {
        if (map.Count == 0)
        {
            return "{}";
        }

        var inner = $"{indent}  ";
        var lines = map.Select(entry =>
            $"{inner}{JsonText.Encode(entry.Key)}: {Write(entry.Value, inner, FlatLists.Contains(entry.Key))}");
        return $"{{\n{string.Join(",\n", lines)}\n{indent}}}";
    }

    private static string WriteList(JsonList list, string indent, bool flat)
    {
        if (list.Count == 0)
        {
            return "[]";
        }

        if (list.All(static one => one is not (JsonMap or JsonList)))
        {
            return JsonText.Encode(list);
        }

        var inner = $"{indent}  ";
        var lines = list.Select(one => $"{inner}{(flat ? JsonText.Encode(one) : Write(one, inner, flat: false))}");
        return $"[\n{string.Join(",\n", lines)}\n{indent}]";
    }
}
