using Orblit.Json;

namespace Orblit.Asset;

/// <summary>
/// Reads and writes a whole set of records as one JSON object. Shared by every store that keeps them in a
/// single blob, because the encoding should not be written twice and then differ by a field.
/// </summary>
public static class FetchRecordsCodec
{
    public const int FormatVersion = 1;

    public static string Encode(IReadOnlyDictionary<WebUrl, FetchRecord> records)
    {
        // Sorted by URL so the file is the same every time for the same records.
        var entries = new OrderedDictionary<string, object?>();
        foreach (var (url, record) in records.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            entries[url.ToString()] = record.ToJson();
        }

        return DartJson.Encode(new OrderedDictionary<string, object?>
        {
            ["formatVersion"] = (long)FormatVersion,
            ["records"] = entries,
        });
    }

    /// <summary>
    /// Reads back what <see cref="Encode"/> writes, dropping anything unreadable. An empty map for text that is
    /// not a record set at all, including one written by a newer version. Everything here can be fetched
    /// again. Losing it costs a download, and guessing at a format that has changed costs correctness.
    /// </summary>
    public static Dictionary<WebUrl, FetchRecord> Decode(string text)
    {
        object? parsed;
        try
        {
            parsed = DartJson.Decode(text);
        }
        catch (FormatException)
        {
            return [];
        }

        if (parsed is not IReadOnlyDictionary<string, object?> map
            || !IsFormat(map.GetValueOrDefault("formatVersion"))
            || map.GetValueOrDefault("records") is not IReadOnlyDictionary<string, object?> records)
        {
            return [];
        }

        var found = new Dictionary<WebUrl, FetchRecord>();
        foreach (var (key, value) in records)
        {
            if (WebUrl.TryParse(key, out var url) && FetchRecord.FromJson(value) is { } record)
            {
                found[url] = record;
            }
        }

        return found;
    }

    // A number equals 1 whether it was written 1 or 1.0, as in Dart.
    private static bool IsFormat(object? version) => version switch
    {
        long whole => whole == FormatVersion,
        double number => number == FormatVersion,
        _ => false,
    };
}
