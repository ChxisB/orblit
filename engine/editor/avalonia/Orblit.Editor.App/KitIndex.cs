using System.Text.Json;

namespace Orblit.Editor.App;

/// <summary>One kit of the store, as the list shows it.</summary>
internal sealed record KitEntry(string Name, string Publisher, string Slug, string Version, string Summary);

/// <summary>Reads <c>index.json</c> of the kit store: a format number and a row for every kit.</summary>
internal static class KitIndex
{
    // The newest format this reader knows. A newer file may mean something else by the same keys.
    const int Format = 1;

    /// <summary>The kits in the order the file lists them. Keys it does not know are ignored. Throws <see cref="FormatException"/> for a file it cannot read.</summary>
    public static IReadOnlyList<KitEntry> Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("kits", out var kits) || kits.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException("The index has no list of kits.");
            }

            if (!root.TryGetProperty("formatVersion", out var format) || format.ValueKind != JsonValueKind.Number || !format.TryGetInt32(out var number) || number is < 1 or > Format)
            {
                throw new FormatException("The index is in a format this editor does not read.");
            }

            return [.. kits.EnumerateArray().Select(Row)];
        }
        catch (JsonException error)
        {
            throw new FormatException("The index is not valid JSON.", error);
        }
    }

    static KitEntry Row(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException("A kit in the index is not an object.");
        }

        return new KitEntry(Text(row, "name"), Text(row, "publisher"), Text(row, "slug"), Text(row, "version"), Optional(row, "summary"));
    }

    static string Text(JsonElement row, string key) =>
        Optional(row, key) is { Length: > 0 } text ? text : throw new FormatException($"A kit in the index has no {key}.");

    static string Optional(JsonElement row, string key) =>
        row.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
}
