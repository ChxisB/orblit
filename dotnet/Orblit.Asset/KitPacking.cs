using System.Globalization;

namespace Orblit.Asset;

/// <summary>Packs a folder as a kit file, and publishes one with the entry that lists it.</summary>
public static class KitPacking
{
    // Every file is listed, hidden ones too, so a stray .DS_Store is named by the rules and not skipped.
    private static readonly EnumerationOptions Everything = new() { AttributesToSkip = 0 };

    /// <summary>
    /// Packs <paramref name="folder"/> as one kit file at <paramref name="outPath"/>.
    /// </summary>
    /// <remarks>
    /// The folder holds the files a person should get and a <c>kit.json</c> the author wrote. The tool fills
    /// in <c>containsScripts</c> itself, from the files. A folder that breaks a rule of
    /// <see cref="KitRules.Problems"/>, or holds a link, is refused before any file is read for its bytes. A
    /// kit file left inside the folder by an earlier run is not packed into the next. Throws a
    /// <see cref="FormatException"/> that lists every problem.
    /// </remarks>
    public static async Task<PackSummary> PackAsync(string folder, string outPath)
    {
        if (!Directory.Exists(folder))
        {
            throw new ArgumentException($"\"{folder}\" is not a directory.", nameof(folder));
        }

        var sizes = Walk(folder, Outputs(outPath));
        var authored = Path.Combine(folder, KitManifest.ManifestId.ToString());
        if (!File.Exists(authored))
        {
            throw new FormatException($"\"{folder}\" has no kit.json.");
        }

        var manifest = KitManifest.ReadAuthored(
            await File.ReadAllBytesAsync(authored).ConfigureAwait(false), KitRules.CarriesScripts(sizes.Keys));
        var problems = KitRules.Problems(manifest, sizes);
        if (problems.Count > 0)
        {
            throw new FormatException(string.Join('\n', problems));
        }

        return await WriteAsync(folder, outPath, manifest, sizes).ConfigureAwait(false);
    }

    /// <summary>
    /// Packs the folder of <paramref name="request"/> as the kit file at its <c>OutPath</c> and answers the
    /// entry that lists it, hosted at its <c>Url</c>.
    /// </summary>
    /// <remarks>
    /// The file is opened again and checked, as CI will check it, so a file that does not read back is
    /// refused here. The release takes its version from the kit's own manifest and its hash and size from
    /// the file.
    ///
    /// With an <c>EntriesPath</c>, the folder of entries <c>kit index</c> reads, the entry file
    /// <c>&lt;publisher&gt;/&lt;slug&gt;.json</c> is written there, with the release added to the ones it
    /// listed. A version already listed is refused, because a listed release is not edited. The entry is
    /// written whole by rename. Without it, only the kit file is written. Nothing is uploaded.
    ///
    /// The kit file is packed beside the out path and moved into place only when every check has passed, so
    /// a refusal leaves it as it was. Throws a <see cref="FormatException"/> for a kit, a url, a note or an
    /// entry that is refused, and for an out path inside the folder, where the next pack would take it in.
    /// </remarks>
    public static async Task<KitPublished> PublishAsync(KitPublishRequest request)
    {
        if (request.EntriesPath is { } entries && !Directory.Exists(entries))
        {
            throw new ArgumentException($"\"{entries}\" is not a directory.", nameof(request));
        }

        RefuseInside(request.Folder, request.OutPath);
        var staged = request.OutPath + ".staged";
        try
        {
            var summary = await PackAsync(request.Folder, staged).ConfigureAwait(false);
            var manifest = await ReadBackAsync(staged, summary).ConfigureAwait(false);
            var release = new KitRelease(
                manifest.Version,
                request.Url,
                summary.Hash,
                summary.Bytes,
                request.Released.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                request.Notes);
            var path = request.EntriesPath is null ? null : EntryFile(request.EntriesPath, manifest);
            var entry = await EntryWithAsync(path, manifest, release).ConfigureAwait(false);
            File.Move(staged, request.OutPath, overwrite: true);
            if (path is not null)
            {
                await AtomicFile.WriteBesideAsync(path, entry.Encode()).ConfigureAwait(false);
            }

            return new KitPublished { Summary = summary, Release = release, Entry = entry, EntryPath = path };
        }
        finally
        {
            if (File.Exists(staged))
            {
                File.Delete(staged);
            }
        }
    }

    private static async Task<PackSummary> WriteAsync(
        string folder, string outPath, KitManifest manifest, IReadOnlyDictionary<AssetId, long> sizes)
    {
        var packed = manifest.Encode();

        async Task<byte[]> Read(AssetId id) =>
            id == KitManifest.ManifestId
                ? packed
                : await File.ReadAllBytesAsync(PathOf(folder, id)).ConfigureAwait(false);

        var entries = new Dictionary<AssetId, AssetEntry>();
        foreach (var (id, bytes) in sizes)
        {
            entries[id] = new AssetEntry(ContentHash.Of(await Read(id).ConfigureAwait(false)), bytes);
        }

        entries[KitManifest.ManifestId] = new AssetEntry(ContentHash.Of(packed), packed.Length);
        try
        {
            return await PackWriter.WriteAsync(new AssetManifest(entries), outPath, KitManifest.Target, Read)
                .ConfigureAwait(false);
        }
        catch (FormatException error)
        {
            throw new FormatException(
                $"{error.Message} Something in \"{folder}\" changed while the kit was written. Pack it again.", error);
        }
    }

    // Where the entry of the manifest lives in the entries folder.
    private static string EntryFile(string root, KitManifest manifest) =>
        Path.Combine(root, manifest.Publisher, $"{manifest.Slug}.json");

    // Throws when a kit file at outPath would sit inside folder.
    private static void RefuseInside(string folder, string outPath)
    {
        if (!Directory.Exists(folder))
        {
            return; // PackAsync names this problem.
        }

        var root = RealPath.Of(folder);
        var parent = RealParent(outPath);
        var inside = parent == root || parent.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        if (inside)
        {
            throw new FormatException(
                $"The kit file \"{outPath}\" would be inside \"{folder}\", and the next pack would take it in. "
                + "Put it in another folder.");
        }
    }

    // The manifest of the kit file at path, after every blob in it has been hashed again. Throws when the
    // file is not the one written describes.
    private static async Task<KitManifest> ReadBackAsync(string path, PackSummary written)
    {
        var source = new FilePackBytes(path);
        await using (source.ConfigureAwait(false))
        {
            var pack = await AssetPack.OpenAsync(source).ConfigureAwait(false);
            if (pack.Hash != written.Hash || new FileInfo(path).Length != written.Bytes)
            {
                throw new FormatException($"\"{path}\" is not the file that was written.");
            }

            var kit = await Kit.OpenAsync(pack).ConfigureAwait(false);
            var problems = await kit.CheckAsync().ConfigureAwait(false);
            return problems.Count > 0 ? throw new FormatException(string.Join('\n', problems)) : kit.Manifest;
        }
    }

    // The entry at path, or a new one when it is not there, with release added. Throws when release is listed
    // already or the entry would be over the size kit index reads.
    private static async Task<KitEntry> EntryWithAsync(string? path, KitManifest manifest, KitRelease release)
    {
        IReadOnlyList<KitRelease> listed = path is null || !File.Exists(path)
            ? []
            : KitEntry.Parse(
                await File.ReadAllBytesAsync(path).ConfigureAwait(false), manifest.Publisher, manifest.Slug).Releases;
        if (listed.Any(held => held.Version == release.Version))
        {
            throw new FormatException(
                $"{manifest.Publisher}/{manifest.Slug} lists {release.Version} already. "
                + "A listed release is not edited. Raise \"version\" in kit.json.");
        }

        var entry = new KitEntry(manifest.Publisher, manifest.Slug, [.. listed, release]);
        return entry.Encode().Length > KitStoreRules.EntryLimit
            ? throw new FormatException(
                $"{manifest.Publisher}/{manifest.Slug} would be over {KitStoreRules.EntryLimit} bytes, "
                + "which is more than kit index reads.")
            : entry;
    }

    // The paths of the kit file and its temporary file, so a run does not pack its own output.
    // shortcut: compared as the file system spells them, so an output named with other letter case than it
    // has on a case insensitive disk is packed. Fold the case when that bites.
    private static HashSet<string> Outputs(string outPath)
    {
        var path = Path.Combine(RealParent(outPath), Path.GetFileName(outPath));
        return [path, path + ".partial"];
    }

    private static string RealParent(string outPath) =>
        RealPath.Of(
            Path.GetDirectoryName(Path.GetFullPath(outPath))
            ?? throw new ArgumentException($"\"{outPath}\" is not a file path.", nameof(outPath)));

    // Every file under the folder with its size, in id order, except the manifest and what skip names.
    private static OrderedDictionary<AssetId, long> Walk(string folder, HashSet<string> skip)
    {
        var real = RealPath.Of(folder);
        var found = new Dictionary<AssetId, long>();
        Collect(new DirectoryInfo(real), real, skip, found);
        return Ordered.Copy(Ordered.ById(found.Keys).Select(id => KeyValuePair.Create(id, found[id])));
    }

    private static void Collect(DirectoryInfo folder, string root, HashSet<string> skip, Dictionary<AssetId, long> found)
    {
        foreach (var entry in folder.EnumerateFileSystemInfos("*", Everything))
        {
            if (entry.LinkTarget is not null)
            {
                throw new FormatException($"\"{entry.FullName}\" is a link. A kit holds files.");
            }

            if (entry is DirectoryInfo inner)
            {
                Collect(inner, root, skip, found);
                continue;
            }

            var relative = Path.GetRelativePath(root, entry.FullName).Replace(Path.DirectorySeparatorChar, '/');
            if (!skip.Contains(entry.FullName) && DartText.ToLower(relative) != KitManifest.ManifestId.ToString())
            {
                found[AssetId.Parse(relative)] = ((FileInfo)entry).Length;
            }
        }
    }

    private static string PathOf(string root, AssetId id) => Path.Combine([root, .. id.ToString().Split('/')]);
}
