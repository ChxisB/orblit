using System.Collections.ObjectModel;
using Orblit.Json;

namespace Orblit.Asset;

/// <summary>
/// A manifest read back from text, with anything that could not be read. Problems are returned
/// rather than thrown, so one broken entry does not cost every other asset.
/// </summary>
public sealed record AssetManifestLoad(AssetManifest Manifest, IReadOnlyList<string> Problems)
{
    public bool HasProblems => Problems.Count > 0;
}

/// <summary>
/// Which content each of a project's assets currently is. The one place an <see cref="AssetId"/>
/// and a <see cref="ContentHash"/> meet. Written as JSON with a stable order, because a manifest
/// lives in somebody's repository.
/// </summary>
public sealed class AssetManifest
{
    /// <summary>Bumped when the shape changes in a way an older reader could misread.</summary>
    public const int FormatVersion = 1;

    private sealed record Outcome(AssetId? Id, AssetEntry? Entry, string? Problem);

    public AssetManifest(IReadOnlyDictionary<AssetId, AssetEntry>? entries = null)
    {
        var copy = new OrderedDictionary<AssetId, AssetEntry>();
        foreach (var (id, entry) in entries ?? new Dictionary<AssetId, AssetEntry>())
        {
            copy[id] = entry;
        }

        Entries = new ReadOnlyDictionary<AssetId, AssetEntry>(copy);
    }

    /// <summary>Every asset and what it is. A changed manifest is a new one.</summary>
    public IReadOnlyDictionary<AssetId, AssetEntry> Entries { get; }

    /// <summary>
    /// The manifest as JSON, the same bytes every time for the same entries: assets in order of
    /// id however the map was built, each entry's keys in a fixed order, and a trailing newline.
    /// </summary>
    public string Encode()
    {
        var assets = new OrderedDictionary<string, object?>();
        foreach (var id in Entries.Keys.OrderBy(id => id.ToString(), StringComparer.Ordinal))
        {
            var entry = Entries[id];
            assets[id.ToString()] = new OrderedDictionary<string, object?> { ["bytes"] = entry.Bytes, ["hash"] = entry.Hash.Hex };
        }

        var json = new OrderedDictionary<string, object?> { ["formatVersion"] = (long)FormatVersion, ["assets"] = assets };
        return DartJson.Encode(json, "  ") + "\n";
    }

    /// <summary>
    /// Reads what <see cref="Encode"/> writes. Throws <see cref="AssetFormatException"/> for text that
    /// cannot be a manifest at all. Any single entry that is wrong is left out and named in
    /// <see cref="AssetManifestLoad.Problems"/> instead.
    /// </summary>
    public static AssetManifestLoad Read(string text)
    {
        var entries = new OrderedDictionary<AssetId, AssetEntry>();
        var problems = new List<string>();
        foreach (var (key, value) in AssetsIn(text))
        {
            var outcome = ReadEntry(key, value);
            if (outcome.Problem is not null)
            {
                problems.Add(outcome.Problem);
            }
            else
            {
                entries[outcome.Id!] = outcome.Entry!; // no problem means both were read
            }
        }

        return new AssetManifestLoad(new AssetManifest(entries), problems);
    }

    private static IReadOnlyDictionary<string, object?> AssetsIn(string text)
    {
        object? parsed;
        try
        {
            parsed = DartJson.Decode(text);
        }
        catch (FormatException error)
        {
            throw new AssetFormatException($"This is not an asset manifest, because it is not JSON: {error.Message}");
        }

        if (parsed is not IReadOnlyDictionary<string, object?> map)
        {
            throw new AssetFormatException("An asset manifest has to be a JSON object, and this is not one.");
        }

        CheckVersion(map.GetValueOrDefault("formatVersion"));

        // Refused rather than read as empty, so another Orblit file opened by mistake says so.
        return map.GetValueOrDefault("assets") as IReadOnlyDictionary<string, object?>
            ?? throw new AssetFormatException("This manifest has no \"assets\" object in it.");
    }

    private static void CheckVersion(object? version)
    {
        if (version is not long number)
        {
            throw new AssetFormatException(
                "This manifest does not say what format version it is, so it cannot be read safely.");
        }

        // Refused rather than half-read: a manifest misread is assets quietly resolved to the wrong bytes.
        if (number > FormatVersion)
        {
            throw new AssetFormatException(
                $"This manifest was written by a newer Orblit (format {number}; this one reads up to {FormatVersion}).");
        }

        if (number < 1)
        {
            throw new AssetFormatException($"This manifest says it is format {number}, which no Orblit has ever written.");
        }
    }

    private static Outcome ReadEntry(string key, object? value)
    {
        AssetId id;
        try
        {
            id = AssetId.Parse(key);
        }
        catch (AssetFormatException error)
        {
            return Left($"{error.Message} Its entry was left out.");
        }

        if (value is not IReadOnlyDictionary<string, object?> map)
        {
            return Left($"The entry for \"{id}\" was left out, because it is not an object.");
        }

        if (map.GetValueOrDefault("hash") is not string hashText)
        {
            return Left($"The entry for \"{id}\" was left out, because it has no hash.");
        }

        ContentHash hash;
        try
        {
            hash = ContentHash.Parse(hashText);
        }
        catch (AssetFormatException error)
        {
            return Left($"The entry for \"{id}\" was left out. {error.Message}");
        }

        if (map.GetValueOrDefault("bytes") is not long bytes)
        {
            return Left($"The entry for \"{id}\" was left out, because it does not give its size as a whole number of bytes.");
        }

        return bytes < 0
            ? Left($"The entry for \"{id}\" was left out, because its size is {bytes} bytes, and a size cannot be negative.")
            : new Outcome(id, new AssetEntry(hash, bytes), null);
    }

    private static Outcome Left(string problem) => new(null, null, problem);
}
