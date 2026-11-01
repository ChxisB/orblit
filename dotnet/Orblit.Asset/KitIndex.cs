using System.Collections.ObjectModel;

namespace Orblit.Asset;

/// <summary><c>index.json</c>: a row for every kit in the store, sorted by publisher and slug.</summary>
public sealed class KitIndex
{
    private const int MaxRows = 50_000;

    /// <summary>Throws a <see cref="FormatException"/> when a kit is listed twice or there are too many.</summary>
    public KitIndex(IReadOnlyList<KitRow> rows)
    {
        var names = rows.Select(row => $"{row.Manifest.Publisher}/{row.Manifest.Slug}").Distinct().Count();
        if (names != rows.Count || rows.Count > MaxRows)
        {
            throw new FormatException($"An index lists up to {MaxRows} kits and no kit twice.");
        }

        var sorted = rows.ToList();
        sorted.Sort(ByName);
        Rows = new ReadOnlyCollection<KitRow>(sorted);
    }

    public IReadOnlyList<KitRow> Rows { get; }

    /// <summary>Reads <c>index.json</c>. Keys it does not know are ignored, and a newer format is refused.</summary>
    public static KitIndex Parse(byte[] bytes)
    {
        var json = KitJsonObject.Parse(bytes, "index.json", KitStoreRules.IndexLimit);
        json.CheckFormat(KitStoreRules.Format);
        return new KitIndex([.. json.Objects("kits").Select(KitRow.FromJson)]);
    }

    public byte[] Encode() =>
        KitJsonObject.Encode(
            "index.json",
            KitStoreRules.IndexLimit,
            new OrderedDictionary<string, object?>
            {
                ["formatVersion"] = (long)KitStoreRules.Format,
                ["kits"] = new List<object?>(Rows.Select(row => row.ToJson())),
            });

    private static int ByName(KitRow a, KitRow b)
    {
        var publishers = string.CompareOrdinal(a.Manifest.Publisher, b.Manifest.Publisher);
        return publishers != 0 ? publishers : string.CompareOrdinal(a.Manifest.Slug, b.Manifest.Slug);
    }
}
