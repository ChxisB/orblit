using System.Collections.ObjectModel;
using System.Text;
using Orblit.Json;

namespace Orblit.Asset;

/// <summary>
/// What a kit says about itself, as <c>kit.json</c> in the pack. The constructor refuses a field no
/// index or install dialog could show safely, so a manifest that exists is one that reads.
/// </summary>
/// <remarks>
/// shortcut: a version is three plain numbers and a licence is any id or expression of the right
/// letters. Add pre-release tags and a check against the SPDX list when a publisher needs them.
/// </remarks>
public sealed class KitManifest
{
    /// <summary>The <c>kind</c> marker in a kit's manifest.</summary>
    public const string Kind = "orblit.kit";

    /// <summary>The format this code writes, and the newest it reads.</summary>
    public const int Format = 1;

    /// <summary>The cook target of a kit. No game runs on it, so a game refuses to mount a kit.</summary>
    public const string Target = "source";

    /// <summary>The largest <c>kit.json</c> read.</summary>
    public const int ManifestLimit = 64 * 1024;

    /// <summary>The most previews a kit names.</summary>
    public const int PreviewLimit = 6;

    private const int MaxUrl = 200;
    private const int MaxTags = 8;

    private static readonly HashSet<string> PreviewExtensions = ["png", "jpg", "jpeg", "webp"];

    /// <summary>
    /// Throws a <see cref="FormatException"/> naming the field that is wrong.
    /// </summary>
    // F2: ten parameters, all named at each call.
    public KitManifest(
        string slug,
        string publisher,
        string name,
        string summary,
        string version,
        KitLicence licence,
        KitEngine engine,
        bool containsScripts,
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<AssetId>? previews = null)
    {
        KitText.CheckWord("slug", slug, 40);
        KitText.CheckWord("publisher", publisher, 40);
        CheckLine("name", name, 60);
        CheckLine("summary", summary, 200);
        KitVersion.Check("version", version);
        CheckLicence(licence);
        engine.Check();
        CheckTags(tags ??= []);
        CheckPreviews(previews ??= []);
        Slug = slug;
        Publisher = publisher;
        Name = name;
        Summary = summary;
        Version = version;
        Licence = licence;
        Engine = engine;
        Tags = new ReadOnlyCollection<string>([.. tags]);
        Previews = new ReadOnlyCollection<AssetId>([.. previews]);
        ContainsScripts = containsScripts;
    }

    /// <summary>The id the manifest is stored under, at the root of every kit.</summary>
    public static AssetId ManifestId { get; } = AssetId.Parse("kit.json");

    public string Slug { get; }

    public string Publisher { get; }

    public string Name { get; }

    public string Summary { get; }

    /// <summary>Three whole numbers, such as <c>1.2.0</c>.</summary>
    public string Version { get; }

    /// <summary>An SPDX id or expression, and where its text can be read.</summary>
    public KitLicence Licence { get; }

    /// <summary>The lowest and highest <c>orblit</c> package versions the kit was tried with.</summary>
    public KitEngine Engine { get; }

    public IReadOnlyList<string> Tags { get; }

    /// <summary>Ids of images in the pack, shown before the kit is installed.</summary>
    public IReadOnlyList<AssetId> Previews { get; }

    /// <summary>True when the kit holds a file a person could run or compile.</summary>
    public bool ContainsScripts { get; }

    /// <summary>Reads a manifest out of a pack, which says whether it holds scripts.</summary>
    public static KitManifest Read(byte[] bytes)
    {
        var json = ParseObject(bytes);
        var kind = json.GetValueOrDefault("kind");
        if (kind is not string text || text != Kind)
        {
            throw new FormatException($"This manifest says it is \"{KitText.Show(TextRules.Print(kind))}\", not \"{Kind}\".");
        }

        CheckFormat(json.GetValueOrDefault("formatVersion"));
        return FromFields(json, FlagAt(json, "containsScripts"));
    }

    /// <summary>
    /// Reads the <c>kit.json</c> an author wrote in a folder. The tool counts the scripts, so a
    /// <c>containsScripts</c> typed there is ignored.
    /// </summary>
    public static KitManifest ReadAuthored(byte[] bytes, bool containsScripts) =>
        FromFields(ParseObject(bytes), containsScripts);

    /// <summary>The manifest as JSON, the same bytes every time for the same content.</summary>
    public byte[] Encode()
    {
        var json = new OrderedDictionary<string, object?>
        {
            ["containsScripts"] = ContainsScripts,
            ["engine"] = new OrderedDictionary<string, object?> { ["from"] = Engine.From, ["to"] = Engine.To },
            ["formatVersion"] = (long)Format,
            ["kind"] = Kind,
            ["licence"] = new OrderedDictionary<string, object?> { ["spdx"] = Licence.Spdx, ["url"] = Licence.Url },
            ["name"] = Name,
            ["previews"] = new List<object?>(Previews.Select(id => id.ToString())),
            ["publisher"] = Publisher,
            ["slug"] = Slug,
            ["summary"] = Summary,
            ["tags"] = new List<object?>(Tags),
            ["version"] = Version,
        };
        return Encoding.UTF8.GetBytes(JsonText.Encode(json));
    }

    private static void CheckFormat(object? format)
    {
        // A number compares by value, so a format of 1.0 is the format 1.
        if (format is long newer && newer > Format)
        {
            throw new FormatException(
                $"This kit was written by a newer Orblit (format {newer}; this one reads up to {Format}).");
        }

        if (!IsFormatOne(format))
        {
            throw new FormatException($"This manifest says format {KitText.Show(TextRules.Print(format))}.");
        }
    }

    private static bool IsFormatOne(object? format) =>
        format is long whole && whole == Format || format is double real && real == Format;

    private static KitManifest FromFields(IReadOnlyDictionary<string, object?> json, bool containsScripts)
    {
        var licence = SectionAt(json, "licence");
        var engine = SectionAt(json, "engine");
        return new KitManifest(
            slug: TextAt(json, "slug"),
            publisher: TextAt(json, "publisher"),
            name: TextAt(json, "name"),
            summary: TextAt(json, "summary"),
            version: TextAt(json, "version"),
            licence: new KitLicence(TextAt(licence, "spdx"), TextAt(licence, "url")),
            engine: new KitEngine(TextAt(engine, "from"), TextAt(engine, "to")),
            containsScripts: containsScripts,
            tags: [.. ListAt(json, "tags").Select(tag => Item(tag, "tags"))],
            previews: [.. ListAt(json, "previews").Select(id => AssetId.Parse(Item(id, "previews")))]);
    }

    private static void CheckLine(string field, string value, int longest)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > longest || KitText.IsUnsafe(value))
        {
            throw new FormatException($"\"{field}\" must be one line of 1 to {longest} characters with no control characters.");
        }
    }

    private static void CheckLicence(KitLicence licence)
    {
        var spdx = licence.Spdx;
        var none = spdx.Equals("NONE", StringComparison.OrdinalIgnoreCase)
            || spdx.Equals("NOASSERTION", StringComparison.OrdinalIgnoreCase);
        if (spdx.Length > 80 || none || !KitText.IsLicence(spdx))
        {
            throw new FormatException(
                $"The licence \"{KitText.Show(spdx)}\" is not an SPDX id or expression, such as MIT or CC-BY-4.0. A kit needs a licence.");
        }

        if (!IsWebAddress(licence.Url))
        {
            throw new FormatException(
                $"The licence URL \"{KitText.Show(licence.Url)}\" is not an http or https address of up to {MaxUrl} characters with no user name in it.");
        }
    }

    private static bool IsWebAddress(string text) =>
        WebUrl.TryParse(text, out var url)
        && url.Scheme is "https" or "http"
        && url.Host.Length > 0
        && url.UserInfo.Length == 0
        && text.Length <= MaxUrl
        && !text.Contains(' ', StringComparison.Ordinal)
        && !KitText.IsUnsafe(text);

    private static void CheckTags(IReadOnlyList<string> tags)
    {
        if (tags.Count > MaxTags || tags.Distinct().Count() != tags.Count)
        {
            throw new FormatException($"A kit has up to {MaxTags} tags and no tag twice.");
        }

        foreach (var tag in tags)
        {
            KitText.CheckWord("tag", tag, 24);
        }
    }

    private static void CheckPreviews(IReadOnlyList<AssetId> previews)
    {
        if (previews.Count > PreviewLimit || previews.Distinct().Count() != previews.Count)
        {
            throw new FormatException($"A kit has up to {PreviewLimit} previews and no preview twice.");
        }

        var bad = previews.FirstOrDefault(id => !PreviewExtensions.Contains(id.Extension));
        if (bad is not null)
        {
            throw new FormatException($"The preview \"{KitText.Show(bad.ToString())}\" is not a png, jpg or webp image.");
        }
    }

    private static IReadOnlyDictionary<string, object?> ParseObject(byte[] bytes)
    {
        if (bytes.Length > ManifestLimit)
        {
            throw new FormatException($"The manifest is over {ManifestLimit} bytes.");
        }

        object? parsed;
        try
        {
            parsed = JsonText.Decode(TextRules.Decode(bytes));
        }
        catch (FormatException error)
        {
            throw new FormatException($"The manifest is not JSON: {error.Message}");
        }

        return parsed as IReadOnlyDictionary<string, object?> ?? throw new FormatException("The manifest is not a JSON object.");
    }

    private static IReadOnlyDictionary<string, object?> SectionAt(IReadOnlyDictionary<string, object?> json, string key) =>
        json.GetValueOrDefault(key) as IReadOnlyDictionary<string, object?>
            ?? throw new FormatException($"The manifest has no \"{key}\" object.");

    private static string TextAt(IReadOnlyDictionary<string, object?> json, string key) =>
        json.GetValueOrDefault(key) as string ?? throw new FormatException($"The manifest has no \"{key}\" text.");

    private static bool FlagAt(IReadOnlyDictionary<string, object?> json, string key) =>
        json.GetValueOrDefault(key) is bool flag ? flag : throw new FormatException($"The manifest has no \"{key}\" true or false.");

    // A missing list, or a null one, is an empty one.
    private static IReadOnlyList<object?> ListAt(IReadOnlyDictionary<string, object?> json, string key) =>
        (json.GetValueOrDefault(key) ?? Array.Empty<object?>()) as IReadOnlyList<object?>
            ?? throw new FormatException($"\"{key}\" in the manifest is not a list.");

    private static string Item(object? value, string where) =>
        value as string
            ?? throw new FormatException($"\"{where}\" in the manifest holds {KitText.Show(TextRules.Print(value))}, not text.");
}
