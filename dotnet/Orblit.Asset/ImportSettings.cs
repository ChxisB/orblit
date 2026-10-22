using System.Collections.ObjectModel;
using Orblit.Json;

namespace Orblit.Asset;

/// <summary>
/// How one asset should be cooked, as read from the project. A file does not say what it is: a
/// PNG can be a colour map, a normal map or a sprite sheet. Settings are where the project says
/// so outright, in a file that sits next to the asset.
/// </summary>
public sealed class ImportSettings
{
    /// <summary>The suffix that makes a file a settings file rather than an asset.</summary>
    private const string Suffix = ".import.json";

    /// <summary>Bumped when the shape changes in a way an older reader could misread.</summary>
    public const int FormatVersion = 1;

    public ImportSettings(string? importer = null, IReadOnlyDictionary<string, object?>? values = null)
    {
        Importer = importer;
        var copy = new OrderedDictionary<string, object?>();
        foreach (var (key, value) in values ?? new Dictionary<string, object?>())
        {
            copy[key] = value;
        }

        Values = new ReadOnlyDictionary<string, object?>(copy);
    }

    /// <summary>The importer the project asked for by name, or null to let the file's kind decide.</summary>
    public string? Importer { get; }

    /// <summary>
    /// What that importer was asked for, by its own names. Untyped here, so adding an importer
    /// does not mean editing this class.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Values { get; }

    /// <summary>The id of the settings file for <paramref name="id"/>: <c>textures/wall.png.import.json</c>.</summary>
    public static AssetId FileFor(AssetId id) => AssetId.Parse(id + Suffix);

    /// <summary>The id of the settings file that applies to everything in <paramref name="directory"/>.</summary>
    public static AssetId FolderFileFor(string directory) =>
        AssetId.Parse(directory.Length == 0 ? Suffix : $"{directory}/{Suffix}");

    /// <summary>Whether <paramref name="id"/> is a settings file rather than something to cook.</summary>
    public static bool IsSettingsFile(AssetId id) => id.Name.EndsWith(Suffix, StringComparison.Ordinal);

    /// <summary>The asset a per-asset settings file belongs to, or null when <paramref name="id"/> is not one.</summary>
    public static AssetId? AssetFor(AssetId id)
    {
        var text = id.ToString();
        if (!text.EndsWith(Suffix, StringComparison.Ordinal) || text.Length == Suffix.Length)
        {
            return null;
        }

        var without = text[..^Suffix.Length];
        return without.EndsWith('/') ? null : AssetId.TryParse(without);
    }

    /// <summary>
    /// Reads what a settings file says. Throws <see cref="AssetFormatException"/> naming the file
    /// when it does not say anything readable.
    /// </summary>
    public static ImportSettings Parse(string text, AssetId from)
    {
        var map = ObjectIn(text, from);
        var version = map.GetValueOrDefault("formatVersion");
        if (version is not (null or long))
        {
            throw new AssetFormatException(
                $"\"{from}\" says its formatVersion is {TextRules.Print(version)}, and a format version is a whole number.");
        }

        if ((long?)version > FormatVersion)
        {
            throw new AssetFormatException(
                $"\"{from}\" is format version {version}, and this build reads up to {FormatVersion}. Cooking it with an older reader would silently drop whatever the newer version added.");
        }

        var importer = ImporterIn(map, from);
        return map.GetValueOrDefault("settings") switch
        {
            null => new ImportSettings(importer),
            IReadOnlyDictionary<string, object?> settings => new ImportSettings(importer, settings),
            _ => throw new AssetFormatException(
                $"\"{from}\" has a \"settings\" that is not a JSON object, so there is no way to tell which setting is which."),
        };
    }

    /// <summary>These settings with <paramref name="other"/> laid over them, key by key. Shallow on purpose.</summary>
    public ImportSettings MergedWith(ImportSettings other)
    {
        var merged = new OrderedDictionary<string, object?>();
        foreach (var (key, value) in Values.Concat(other.Values))
        {
            merged[key] = value;
        }

        return new ImportSettings(other.Importer ?? Importer, merged);
    }

    /// <summary>The settings as the JSON a project keeps, with a trailing newline.</summary>
    public string Encode()
    {
        var settings = new OrderedDictionary<string, object?>();
        foreach (var key in Values.Keys.Order(StringComparer.Ordinal))
        {
            settings[key] = Values[key];
        }

        var json = new OrderedDictionary<string, object?> { ["formatVersion"] = (long)FormatVersion };
        if (Importer is not null)
        {
            json["importer"] = Importer;
        }

        json["settings"] = settings;
        return JsonText.Encode(json, "  ") + "\n";
    }

    public override string ToString() => $"ImportSettings(importer: {Importer ?? "null"}, values: {TextRules.Print(Values)})";

    private static IReadOnlyDictionary<string, object?> ObjectIn(string text, AssetId from)
    {
        object? parsed;
        try
        {
            parsed = JsonText.Decode(text);
        }
        catch (FormatException error)
        {
            throw new AssetFormatException(
                $"\"{from}\" is not readable as import settings, because it is not JSON: {error.Message}");
        }

        return parsed as IReadOnlyDictionary<string, object?>
            ?? throw new AssetFormatException(
                $"\"{from}\" has to be a JSON object — {{\"importer\": …, \"settings\": …}} — and it is not one.");
    }

    private static string? ImporterIn(IReadOnlyDictionary<string, object?> map, AssetId from)
    {
        var importer = map.GetValueOrDefault("importer");
        if (importer is not (null or string))
        {
            throw new AssetFormatException(
                $"\"{from}\" says its importer is {TextRules.Print(importer)}, and an importer is named with a string.");
        }

        return importer is "" // an importer named as an empty string
            ? throw new AssetFormatException(
                $"\"{from}\" names its importer as an empty string. Leave the field out to let the file kind decide.")
            : (string?)importer;
    }
}
