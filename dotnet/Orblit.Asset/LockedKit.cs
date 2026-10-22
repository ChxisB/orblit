using System.Collections.ObjectModel;

namespace Orblit.Asset;

/// <summary>
/// One kit an install put in a project, and what it wrote. <see cref="Files"/> are the files the person kept,
/// at their place in the project, each with the hash of the bytes the kit gave. A later update compares that
/// hash with what is on disk to tell a file the person edited from one they did not. A file the person left
/// out is not listed, so an update never writes it.
/// </summary>
public sealed class LockedKit
{
    /// <summary>
    /// Throws a <see cref="FormatException"/> when a name, the version or <paramref name="into"/> is wrong or
    /// <paramref name="files"/> holds too many files.
    /// </summary>
    // F2: six parameters, all named at each call.
    public LockedKit(
        string publisher,
        string slug,
        string version,
        ContentHash hash,
        IReadOnlyDictionary<AssetId, ContentHash> files,
        string into = "")
    {
        KitText.CheckWord("publisher", publisher, 40);
        KitText.CheckWord("slug", slug, 40);
        KitVersion.Check("version", version);
        var misplaced = KitRules.PlaceProblem(into, []);
        if (misplaced is not null)
        {
            throw new FormatException(misplaced);
        }

        if (files.Count > KitRules.CountLimit)
        {
            throw new FormatException($"{publisher}/{slug} lists {files.Count} files, over the limit of {KitRules.CountLimit}.");
        }

        Publisher = publisher;
        Slug = slug;
        Version = version;
        Hash = hash;
        Into = into;
        Files = new ReadOnlyDictionary<AssetId, ContentHash>(Ordered.Copy(files));
    }

    public string Publisher { get; }

    public string Slug { get; }

    public string Version { get; }

    /// <summary>The hash of the kit file the files came from.</summary>
    public ContentHash Hash { get; }

    /// <summary>The folder of the project the kit was written under, or empty.</summary>
    public string Into { get; }

    public IReadOnlyDictionary<AssetId, ContentHash> Files { get; }

    internal static LockedKit FromJson(KitJsonObject json)
    {
        var files = json.Section("files");
        return json.Read(() => new LockedKit(
            publisher: json.Text("publisher"),
            slug: json.Text("slug"),
            version: json.Text("version"),
            hash: ContentHash.Parse(json.Text("hash")),
            into: json.Text("into"),
            files: HashesIn(json, files)));
    }

    internal OrderedDictionary<string, object?> ToJson()
    {
        var files = new OrderedDictionary<string, object?>();
        foreach (var id in Ordered.ById(Files.Keys))
        {
            files[id.ToString()] = Files[id].Hex;
        }

        return new OrderedDictionary<string, object?>
        {
            ["files"] = files,
            ["hash"] = Hash.Hex,
            ["into"] = Into,
            ["publisher"] = Publisher,
            ["slug"] = Slug,
            ["version"] = Version,
        };
    }

    private static OrderedDictionary<AssetId, ContentHash> HashesIn(KitJsonObject json, KitJsonObject files)
    {
        var hashes = new OrderedDictionary<AssetId, ContentHash>();
        foreach (var (path, value) in files.Map)
        {
            hashes[AssetId.Parse(path)] = ContentHash.Parse(value as string ?? throw json.Problem("has a file hash that is not text"));
        }

        return hashes;
    }
}
