namespace Orblit.Asset;

/// <summary>
/// An opened kit: a pack made for the target <c>source</c>, with a <c>kit.json</c>. A kit holds files
/// for a project's own folders. It is not mounted by a game, so no game reads it by accident, and
/// installing one copies its files out.
/// </summary>
public sealed class Kit
{
    private Kit(AssetPack pack, KitManifest manifest)
    {
        Pack = pack;
        Manifest = manifest;
    }

    public AssetPack Pack { get; }

    public KitManifest Manifest { get; }

    /// <summary>Each file a person gets, with its size. The manifest is not one.</summary>
    public IReadOnlyDictionary<AssetId, long> Files =>
        Ordered.Copy(Pack.Manifest.Entries.Where(e => e.Key != KitManifest.ManifestId).Select(e => KeyValuePair.Create(e.Key, e.Value.Bytes)));

    /// <summary>
    /// The files that a person could run, compile or load as code, in the order <see cref="Files"/> has
    /// them. The same test as <c>containsScripts</c>. Installing a kit never runs one.
    /// </summary>
    public IReadOnlyList<AssetId> Scripts => [.. Files.Keys.Where(KitRules.IsScript)];

    /// <summary>
    /// Throws a <see cref="FormatException"/> when <paramref name="pack"/> is not a kit: it was made for
    /// another target, it is a patch, or its <c>kit.json</c> is missing, too big, not the bytes the pack
    /// lists for it, or wrong.
    /// </summary>
    public static async Task<Kit> OpenAsync(AssetPack pack)
    {
        if (pack.Target != KitManifest.Target)
        {
            throw new FormatException($"This pack is for \"{pack.Target}\", not \"{KitManifest.Target}\", so it is not a kit.");
        }

        if (pack.Base is not null || pack.Removed.Count > 0)
        {
            throw new FormatException("A kit is a whole pack, not a patch.");
        }

        var id = KitManifest.ManifestId;
        if (!pack.Manifest.Entries.TryGetValue(id, out var entry))
        {
            throw new FormatException($"This pack has no \"{id}\".");
        }

        if (entry.Bytes > KitManifest.ManifestLimit)
        {
            throw new FormatException($"\"{id}\" is over {KitManifest.ManifestLimit} bytes.");
        }

        var bytes = await pack.ReadAsync(id).ConfigureAwait(false);
        return ContentHash.Of(bytes) != entry.Hash
            ? throw new FormatException($"\"{id}\" does not match its hash.")
            : new Kit(pack, KitManifest.Read(bytes));
    }

    /// <summary>
    /// What is wrong with the kit, in words. Empty when nothing is. Reads and hashes every blob, so it is
    /// the place a flipped byte in a download shows up.
    /// </summary>
    public async Task<IReadOnlyList<string>> CheckAsync()
    {
        var problems = KitRules.Problems(Manifest, Files);
        problems.AddRange((await Pack.FindCorruptAsync().ConfigureAwait(false))
            .Select(id => $"\"{KitText.Show(id.ToString())}\" does not match its hash."));
        return problems;
    }

    /// <summary>
    /// The files of the kit, or those in <paramref name="only"/>, as the kit names them. Throws an
    /// <see cref="ArgumentException"/> for a name the kit does not hold, so a typo installs nothing instead
    /// of a part. The manifest is not a file a person gets, so it cannot be chosen.
    /// </summary>
    public IReadOnlyList<AssetId> Chosen(IReadOnlySet<AssetId>? only)
    {
        var all = Files;
        foreach (var id in only ?? new HashSet<AssetId>())
        {
            if (!all.ContainsKey(id))
            {
                throw new ArgumentException($"\"{KitText.Show(id.ToString())}\" is not a file of the kit.", nameof(only));
            }
        }

        return [.. all.Keys.Where(id => only is null || only.Contains(id))];
    }

    /// <summary>
    /// The files <paramref name="project"/> already has with other bytes than the kit's, as the kit names
    /// them. With <paramref name="into"/>, each file is looked for under that folder, and with
    /// <paramref name="only"/>, the others are not looked at. A file that is there and identical is not a
    /// conflict, because writing it would change nothing. Only the project is read, never a blob of the kit.
    /// </summary>
    /// <remarks>shortcut: every file the project has is read and hashed. Compare sizes first when kits get large.</remarks>
    public async Task<IReadOnlyList<AssetId>> ConflictsInAsync(
        IAssetSource project, string into = "", IReadOnlySet<AssetId>? only = null)
    {
        var conflicts = new List<AssetId>();
        foreach (var id in Chosen(only))
        {
            var place = KitRules.Place(id, into);
            if (!await project.ExistsAsync(place).ConfigureAwait(false))
            {
                continue;
            }

            var held = ContentHash.Of(await project.ReadAsync(place).ConfigureAwait(false));
            if (held != Pack.Manifest.Entries[id].Hash)
            {
                conflicts.Add(id);
            }
        }

        return conflicts;
    }
}
