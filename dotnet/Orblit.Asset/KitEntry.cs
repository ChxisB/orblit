using System.Collections.ObjectModel;
using System.Text;
using Orblit.Json;

namespace Orblit.Asset;

/// <summary>
/// The releases of one kit, as the file <c>kits/&lt;publisher&gt;/&lt;slug&gt;.json</c> lists them. The path
/// names the kit, so the file does not.
/// </summary>
public sealed class KitEntry
{
    private static readonly HashSet<string> ReleasesKey = ["releases"];

    private static readonly HashSet<string> ReleaseKeys = ["version", "url", "hash", "bytes", "released", "notes"];

    /// <summary>Throws a <see cref="FormatException"/> when the kit has no release or none twice.</summary>
    public KitEntry(string publisher, string slug, IReadOnlyList<KitRelease> releases)
    {
        KitText.CheckWord("publisher", publisher, 40);
        KitText.CheckWord("slug", slug, 40);
        if (releases.Count is 0 or > KitStoreRules.ReleaseLimit)
        {
            throw new FormatException($"An entry lists 1 to {KitStoreRules.ReleaseLimit} releases.");
        }

        if (releases.Select(release => release.Version).Distinct().Count() != releases.Count)
        {
            throw new FormatException("An entry lists a version twice.");
        }

        var newestFirst = releases.ToList();
        newestFirst.Sort((a, b) => KitVersion.Compare(b.Version, a.Version));
        Publisher = publisher;
        Slug = slug;
        Releases = new ReadOnlyCollection<KitRelease>(newestFirst);
    }

    public string Publisher { get; }

    public string Slug { get; }

    /// <summary>Newest version first.</summary>
    public IReadOnlyList<KitRelease> Releases { get; }

    public KitRelease Latest => Releases[0];

    /// <summary>
    /// Reads the entry file for <paramref name="publisher"/> and <paramref name="slug"/>. Throws a
    /// <see cref="FormatException"/> that names the file, and refuses a key it does not know.
    /// </summary>
    public static KitEntry Parse(byte[] bytes, string publisher, string slug)
    {
        var name = $"The entry \"{KitText.Show($"{publisher}/{slug}")}\"";
        var json = KitJsonObject.Parse(bytes, name, KitStoreRules.EntryLimit);
        json.Only(ReleasesKey);
        return json.Read(() => new KitEntry(publisher, slug, [.. json.Objects("releases").Select(ReleaseOf)]));
    }

    /// <summary>
    /// The entry file, indented so a pull request shows each release as lines, and ending in a new line.
    /// <see cref="Parse"/> reads it back.
    /// </summary>
    public byte[] Encode()
    {
        var json = new OrderedDictionary<string, object?> { ["releases"] = new List<object?>(Releases.Select(release => release.ToJson())) };
        return Encoding.UTF8.GetBytes(DartJson.Encode(json, "  ") + "\n");
    }

    private static KitRelease ReleaseOf(KitJsonObject item)
    {
        item.Only(ReleaseKeys);
        return KitRelease.FromJson(item);
    }
}
