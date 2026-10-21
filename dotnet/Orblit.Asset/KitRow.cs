namespace Orblit.Asset;

/// <summary>
/// One kit in <c>index.json</c>: what a browser lists before anyone opens it. The words come from the kit's
/// own manifest, so they were checked once when the kit was packed and are checked again here, because a
/// reader gets this file from a server and not from the pack.
/// </summary>
public sealed class KitRow
{
    /// <summary>
    /// Throws a <see cref="FormatException"/> when a field is wrong or <paramref name="thumbnail"/> is not
    /// one of this kit's preview files.
    /// </summary>
    public KitRow(KitManifest manifest, string updated, string? thumbnail = null)
    {
        KitText.CheckDate("updated", updated);
        if (thumbnail is not null && !IsPreviewOf(manifest, thumbnail))
        {
            throw new FormatException(
                $"The thumbnail \"{KitText.Show(thumbnail)}\" is not a preview file of {manifest.Publisher}/{manifest.Slug}.");
        }

        Manifest = manifest;
        Updated = updated;
        Thumbnail = thumbnail;
    }

    /// <summary>The latest release's manifest, without its previews.</summary>
    public KitManifest Manifest { get; }

    /// <summary>The day the latest release came out.</summary>
    public string Updated { get; }

    /// <summary>A path under the index, such as <c>kits/brass-lamp/crates/preview-0.png</c>.</summary>
    public string? Thumbnail { get; }

    internal static KitRow FromJson(KitJsonObject json)
    {
        var licence = json.Section("licence");
        var engine = json.Section("engine");
        return json.Read(() => new KitRow(
            manifest: new KitManifest(
                slug: json.Text("slug"),
                publisher: json.Text("publisher"),
                name: json.Text("name"),
                summary: json.Text("summary"),
                version: json.Text("version"),
                licence: new KitLicence(licence.Text("spdx"), licence.Text("url")),
                engine: new KitEngine(engine.Text("from"), engine.Text("to")),
                containsScripts: json.Flag("containsScripts"),
                tags: json.Strings("tags")),
            updated: json.Text("updated"),
            thumbnail: json.OptionalText("thumbnail")));
    }

    internal OrderedDictionary<string, object?> ToJson()
    {
        var json = new OrderedDictionary<string, object?>
        {
            ["containsScripts"] = Manifest.ContainsScripts,
            ["engine"] = new OrderedDictionary<string, object?> { ["from"] = Manifest.Engine.From, ["to"] = Manifest.Engine.To },
            ["licence"] = new OrderedDictionary<string, object?> { ["spdx"] = Manifest.Licence.Spdx, ["url"] = Manifest.Licence.Url },
            ["name"] = Manifest.Name,
            ["publisher"] = Manifest.Publisher,
            ["slug"] = Manifest.Slug,
            ["summary"] = Manifest.Summary,
            ["tags"] = new List<object?>(Manifest.Tags),
        };
        if (Thumbnail is not null)
        {
            json["thumbnail"] = Thumbnail;
        }

        json["updated"] = Updated;
        json["version"] = Manifest.Version;
        return json;
    }

    private static bool IsPreviewOf(KitManifest manifest, string thumbnail)
    {
        var folder = $"kits/{manifest.Publisher}/{manifest.Slug}/";
        return thumbnail.StartsWith(folder, StringComparison.Ordinal) && KitStoreRules.IsPreviewName(thumbnail[folder.Length..]);
    }
}
