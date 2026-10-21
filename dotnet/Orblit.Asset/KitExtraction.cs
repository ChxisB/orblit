namespace Orblit.Asset;

/// <summary>Writes the files of a kit into a project folder.</summary>
public static class KitExtraction
{
    private enum Place
    {
        NotFound,
        File,
        Directory,
        Link,
    }

    /// <summary>
    /// Writes the files of <paramref name="kit"/> into the folder <paramref name="projectPath"/> and answers
    /// which it wrote, as the kit names them.
    /// </summary>
    /// <remarks>
    /// With <c>Into</c>, the files go under that folder of the project, which has to pass
    /// <see cref="KitRules.PlaceProblem"/>. With <c>Only</c>, just those files are written, and a file the
    /// person left out cannot conflict. Both are settled before the first file is written.
    ///
    /// The kit is checked first, by the rules of <see cref="KitRules.Problems"/> and by the hash of every
    /// blob, so a pack built by hand cannot write a name that packing would have refused. A file the project
    /// already has with other bytes stops the install with a <see cref="KitConflictsException"/>, unless
    /// <c>Overwrite</c> is set. A file that is there and identical is left alone. The manifest is not written.
    /// A link or a plain file where a folder must go, or a folder or link where a file must go, stops the
    /// install too. Each blob is hashed again on the way out and written beside its place under a new name,
    /// then renamed, so a file is whole or not there.
    ///
    /// A file that <c>Overwrite</c> replaces gets the default permissions, so an executable flag on it is lost.
    ///
    /// shortcut: files are written one at a time, so a disk that fills half way leaves the earlier ones.
    /// Running the install again is safe, because identical files are skipped. Stage the whole kit and swap
    /// it in if that matters. A person who swaps a folder for a link while the install runs can still
    /// redirect a later file, so run it in a folder only you write to.
    /// </remarks>
    public static async Task<IReadOnlyList<AssetId>> ExtractAsync(
        Kit kit, string projectPath, KitExtractionOptions? options = null)
    {
        options ??= new KitExtractionOptions();
        if (!Directory.Exists(projectPath))
        {
            throw new ArgumentException($"\"{projectPath}\" is not a directory.", nameof(projectPath));
        }

        var picked = kit.Chosen(options.Only);
        if (KitRules.PlaceProblem(options.Into, picked) is { } misplaced)
        {
            throw new FormatException(misplaced);
        }

        var problems = await kit.CheckAsync().ConfigureAwait(false);
        if (problems.Count > 0)
        {
            throw new FormatException(string.Join('\n', problems));
        }

        var root = RealPath.Of(projectPath);
        var conflicts = await kit.ConflictsInAsync(new DirectoryAssetSource(root), options.Into, options.Only)
            .ConfigureAwait(false);
        if (conflicts.Count > 0 && !options.Overwrite)
        {
            throw new KitConflictsException(conflicts);
        }

        var places = picked.Select(id => (Id: id, At: KitRules.Place(id, options.Into))).ToList();
        foreach (var (_, at) in places)
        {
            CheckPlace(root, at);
        }

        var written = new List<AssetId>();
        foreach (var (id, at) in places)
        {
            if (await PutAsync(kit, id, PathOf(root, at)).ConfigureAwait(false))
            {
                written.Add(id);
            }
        }

        return written;
    }

    // Writes id of kit to path unless it is there already. Answers whether it wrote.
    private static async Task<bool> PutAsync(Kit kit, AssetId id, string path)
    {
        var hash = kit.Pack.Manifest.Entries[id].Hash;
        var bytes = await kit.Pack.ReadAsync(id).ConfigureAwait(false);
        if (ContentHash.Of(bytes) != hash)
        {
            throw new FormatException($"\"{id}\" does not match its hash in the kit.");
        }

        if (File.Exists(path) && ContentHash.Of(await File.ReadAllBytesAsync(path).ConfigureAwait(false)) == hash)
        {
            return false;
        }

        await AtomicFile.WriteBesideAsync(path, bytes).ConfigureAwait(false);
        return true;
    }

    // Throws when id cannot be written under root: a folder it needs is a link or a file, or its own place
    // is a folder or a link. Writes nothing.
    private static void CheckPlace(string root, AssetId id)
    {
        var path = root;
        var parts = id.ToString().Split('/');
        foreach (var part in parts[..^1])
        {
            path = Path.Combine(path, part);
            var type = TypeOf(path);
            if (type == Place.NotFound)
            {
                return;
            }

            if (type != Place.Directory)
            {
                throw new FormatException($"\"{path}\" is in the way of \"{id}\" and is not a plain folder.");
            }
        }

        if (TypeOf(PathOf(root, id)) is not (Place.NotFound or Place.File))
        {
            throw new FormatException($"\"{id}\" is a folder or a link in the project.");
        }
    }

    // What is at path, without following a link at the end of it.
    private static Place TypeOf(string path)
    {
        if (new FileInfo(path).LinkTarget is not null)
        {
            return Place.Link;
        }

        if (Directory.Exists(path))
        {
            return Place.Directory;
        }

        return File.Exists(path) ? Place.File : Place.NotFound;
    }

    private static string PathOf(string root, AssetId id) => Path.Combine([root, .. id.ToString().Split('/')]);
}
