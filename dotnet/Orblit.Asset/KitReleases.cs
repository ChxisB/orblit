using System.Collections.ObjectModel;

namespace Orblit.Asset;

/// <summary>
/// <c>kits/&lt;publisher&gt;/&lt;slug&gt;/releases.json</c>: every release of one kit, newest first, and the
/// names of the preview images beside it.
/// </summary>
public sealed class KitReleases
{
    /// <summary>
    /// Throws a <see cref="FormatException"/> when a version is listed twice, a preview name is not
    /// <c>preview-&lt;0 to 5&gt;.&lt;png, jpg, jpeg or webp&gt;</c>, or a list is too long.
    /// </summary>
    public KitReleases(IReadOnlyList<string> previews, IReadOnlyList<PublishedRelease> releases)
    {
        var versions = releases.Select(item => item.Release.Version).Distinct().Count();
        if (releases.Count is 0 or > KitStoreRules.ReleaseLimit || versions != releases.Count)
        {
            throw new FormatException($"A release list holds 1 to {KitStoreRules.ReleaseLimit} releases and no version twice.");
        }

        if (previews.Count > KitManifest.PreviewLimit || previews.Distinct().Count() != previews.Count)
        {
            throw new FormatException($"A release list names up to {KitManifest.PreviewLimit} previews and no preview twice.");
        }

        var bad = previews.FirstOrDefault(name => !KitStoreRules.IsPreviewName(name));
        if (bad is not null)
        {
            throw new FormatException($"The preview \"{KitText.Show(bad)}\" is not named preview-0.png to preview-5.webp.");
        }

        var newestFirst = releases.ToList();
        newestFirst.Sort((a, b) => KitVersion.Compare(b.Release.Version, a.Release.Version));
        Previews = new ReadOnlyCollection<string>([.. previews]);
        Releases = new ReadOnlyCollection<PublishedRelease>(newestFirst);
    }

    /// <summary>File names beside <c>releases.json</c>, such as <c>preview-0.png</c>.</summary>
    public IReadOnlyList<string> Previews { get; }

    /// <summary>Newest version first.</summary>
    public IReadOnlyList<PublishedRelease> Releases { get; }

    /// <summary>Reads <c>releases.json</c>. Keys it does not know are ignored, and a newer format is refused.</summary>
    public static KitReleases Parse(byte[] bytes)
    {
        var json = KitJsonObject.Parse(bytes, "releases.json", KitStoreRules.ReleasesLimit);
        json.CheckFormat(KitStoreRules.Format);
        return new KitReleases(json.Strings("previews"), [.. json.Objects("releases").Select(PublishedRelease.FromJson)]);
    }

    public byte[] Encode() =>
        KitJsonObject.Encode(
            "releases.json",
            KitStoreRules.ReleasesLimit,
            new OrderedDictionary<string, object?>
            {
                ["formatVersion"] = (long)KitStoreRules.Format,
                ["previews"] = new List<object?>(Previews),
                ["releases"] = new List<object?>(Releases.Select(item => item.ToJson())),
            });
}
