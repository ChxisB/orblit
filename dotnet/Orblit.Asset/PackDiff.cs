using Orblit.Json;

namespace Orblit.Asset;

/// <summary>An asset with the same name in both packs and other bytes.</summary>
/// <param name="Id">The asset.</param>
/// <param name="Before">Its size in the older pack.</param>
/// <param name="After">Its size in the newer pack.</param>
public sealed record ChangedAsset(AssetId Id, long Before, long After);

/// <summary>A document that names an asset the newer pack no longer holds.</summary>
/// <param name="Document">The scene, prefab, clip, material or other document.</param>
/// <param name="Missing">The asset it names.</param>
public sealed record DanglingReference(AssetId Document, AssetId Missing);

/// <summary>
/// What differs between two packs, by asset name. Added, removed and changed come from comparing what
/// each side serves, so no blob is read for them. When something was removed, the documents of the
/// newer side are read for its name, because a removed texture is a blank model and nothing else says
/// so before a player does.
/// </summary>
public sealed class PackDiff
{
    // The extensions of the text documents Orblit writes. Each is JSON.
    private static readonly HashSet<string> Documents =
        ["oscene", "oprefab", "oclip", "oblend", "ocutscene", "obonemap", "omat", "oterrain", "oui", "opath"];

    // Read-only wrappers, so a caller cannot change the lists.
    private PackDiff(List<AssetId> added, List<AssetId> removed, List<ChangedAsset> changed, Scan scan)
    {
        Added = added.AsReadOnly();
        Removed = removed.AsReadOnly();
        Changed = changed.AsReadOnly();
        Dangling = scan.Dangling.AsReadOnly();
        Unreadable = scan.Unreadable.AsReadOnly();
    }

    public IReadOnlyList<AssetId> Added { get; }

    public IReadOnlyList<AssetId> Removed { get; }

    public IReadOnlyList<ChangedAsset> Changed { get; }

    /// <summary>
    /// Every removed name that a document of the newer side still uses, by document and then by name.
    /// A name that was never in the older pack is not here. Cooking refuses that when the new pack is
    /// built, and counting it would make the first stray string in a scene fail every later diff.
    /// </summary>
    public IReadOnlyList<DanglingReference> Dangling { get; }

    /// <summary>Documents that are not JSON, so what they name is not known. Only read when something was removed.</summary>
    public IReadOnlyList<AssetId> Unreadable { get; }

    /// <summary>Whether a build should stop on this: a removed name is still used or a document could not be read.</summary>
    public bool IsBroken => Dangling.Count > 0 || Unreadable.Count > 0;

    /// <summary>
    /// Compares what <paramref name="before"/> and <paramref name="after"/> each serve. Throws
    /// <see cref="PackMountRefusedException"/> when they are for different targets, because every cooked
    /// asset would then show as changed.
    /// </summary>
    public static async Task<PackDiff> BetweenAsync(MountedPacks before, MountedPacks after)
    {
        if (before.Target != after.Target)
        {
            throw new PackMountRefusedException(
                $"The old pack is for {before.Target} and the new one is for {after.Target}, so every cooked asset would show as changed.");
        }

        var was = before.Manifest.Entries;
        var now = after.Manifest.Entries;
        var removed = Ordered.ById(was.Keys.Where(id => !now.ContainsKey(id)));
        var scan = await ScanAsync(after, [.. removed]).ConfigureAwait(false);
        var changed = now
            .Where(e => was.TryGetValue(e.Key, out var old) && old.Hash != e.Value.Hash)
            .Select(e => new ChangedAsset(e.Key, was[e.Key].Bytes, e.Value.Bytes))
            .OrderBy(c => c.Id.ToString(), StringComparer.Ordinal)
            .ToList();
        return new PackDiff(Ordered.ById(now.Keys.Where(id => !was.ContainsKey(id))), removed, changed, scan);
    }

    // Reads each document of the newer side for the names in gone.
    private static async Task<Scan> ScanAsync(MountedPacks after, HashSet<AssetId> gone)
    {
        var scan = new Scan([], []);
        if (gone.Count == 0)
        {
            return scan;
        }

        var documents = after.Manifest.Entries.Keys.Where(id => Documents.Contains(id.Extension));
        foreach (var document in Ordered.ById(documents))
        {
            object? json;
            try
            {
                json = JsonText.Decode(TextRules.Decode(await after.ReadAsync(document).ConfigureAwait(false)));
            }
            catch (FormatException)
            {
                scan.Unreadable.Add(document);
                continue;
            }

            var named = new HashSet<AssetId>();
            Collect(document, json, gone, named);
            scan.Dangling.AddRange(Ordered.ById(named).Select(missing => new DanglingReference(document, missing)));
        }

        return scan;
    }

    // Adds to into every name in gone that appears in node, as a value or as a key. Any string is tried.
    // A name has to be exactly an asset that was removed to count, so free text and entity names are
    // never mistaken for one, and a field added to a format next year is found without being told.
    private static void Collect(AssetId document, object? node, HashSet<AssetId> gone, HashSet<AssetId> into)
    {
        switch (node)
        {
            case List<object?> list:
                foreach (var child in list)
                {
                    Collect(document, child, gone, into);
                }

                break;
            case OrderedDictionary<string, object?> map:
                foreach (var (key, value) in map)
                {
                    Collect(document, key, gone, into);
                    Collect(document, value, gone, into);
                }

                break;
            case string text:
                // Ids are read from the project root. Only a leading "./" or "../" means "near this
                // document", as in a scene.
                var id = text.StartsWith("./", StringComparison.Ordinal) || text.StartsWith("../", StringComparison.Ordinal)
                    ? document.Resolve(text)
                    : AssetId.TryParse(text);
                if (id is not null && gone.Contains(id))
                {
                    into.Add(id);
                }

                break;
        }
    }

    private sealed record Scan(List<DanglingReference> Dangling, List<AssetId> Unreadable);
}
