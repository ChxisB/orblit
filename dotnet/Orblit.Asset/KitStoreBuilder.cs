namespace Orblit.Asset;

/// <summary>How many kits and releases <see cref="KitStoreBuilder.BuildAsync"/> wrote.</summary>
public sealed record KitStoreBuilt(int Kits, int Releases);

/// <summary>Builds the files a browser reads for a store of kits from the entries that list them.</summary>
public static class KitStoreBuilder
{
    /// <summary>
    /// Reads the entries in <paramref name="entriesPath"/>, downloads and checks every release they list,
    /// and writes the files a browser reads into the new folder <paramref name="outPath"/>: <c>index.json</c>,
    /// and <c>kits/&lt;publisher&gt;/&lt;slug&gt;/</c> with a <c>releases.json</c> and the latest release's
    /// previews.
    /// </summary>
    /// <remarks>
    /// <paramref name="entriesPath"/> holds <c>&lt;publisher&gt;/&lt;slug&gt;.json</c>. A file name that starts
    /// with a dot at the top, such as <c>.gitkeep</c>, is left alone. Anything else that is not an entry is a
    /// problem, and so is a link or an empty file.
    ///
    /// A release is fetched over https from its url, up to the number of bytes the entry gives and for no
    /// longer than <paramref name="timeout"/>, which is ten minutes when left out. The pack has to hash to
    /// the entry's hash and be the size it says, every blob has to match its hash, the kit has to pass
    /// <see cref="Kit.CheckAsync"/>, and its manifest has to name the same publisher, slug and version as the
    /// entry. Nothing is written until every release of every kit has passed, and
    /// <paramref name="outPath"/> must not exist. The files are written beside it and renamed into place, so
    /// what is published is only what was checked this time. Throws a <see cref="FormatException"/> that
    /// lists every problem.
    ///
    /// shortcut: every release is downloaded on every run, one after another, and the previews of every kit
    /// are held in memory until the last release has passed. Keep packs by hash between runs, fetch several
    /// at once and write each kit as it passes when the store grows past a few hundred kits.
    /// </remarks>
    public static async Task<KitStoreBuilt> BuildAsync(
        string entriesPath, string outPath, IAssetTransport transport, TimeSpan? timeout = null)
    {
        if (!Directory.Exists(entriesPath))
        {
            throw new ArgumentException($"\"{entriesPath}\" is not a directory.", nameof(entriesPath));
        }

        if (Exists(outPath))
        {
            throw new ArgumentException(
                $"\"{outPath}\" is there already. Name a folder that does not exist yet.", nameof(outPath));
        }

        var scratch = Directory.CreateTempSubdirectory("orblit_kit_store_");
        try
        {
            var build = new KitStoreBuild(scratch.FullName, transport, timeout ?? TimeSpan.FromMinutes(10));
            var kits = new List<BuiltKit>();
            foreach (var entry in await ReadEntriesAsync(entriesPath, build.Problems).ConfigureAwait(false))
            {
                if (await build.KitAsync(entry).ConfigureAwait(false) is { } built)
                {
                    kits.Add(built);
                }
            }

            if (build.Problems.Count > 0)
            {
                throw new FormatException(string.Join('\n', build.Problems));
            }

            await WriteAsync(outPath, kits).ConfigureAwait(false);
            return new KitStoreBuilt(kits.Count, kits.Sum(kit => kit.Releases.Releases.Count));
        }
        finally
        {
            scratch.Delete(recursive: true);
        }
    }

    // Anything at path, a link that leads nowhere included.
    private static bool Exists(string path) =>
        File.Exists(path) || Directory.Exists(path) || new FileInfo(path).LinkTarget is not null;

    private static List<FileSystemInfo> Sorted(DirectoryInfo folder) =>
        [.. folder.EnumerateFileSystemInfos().OrderBy(entry => entry.Name, StringComparer.Ordinal)];

    private static async Task<List<KitEntry>> ReadEntriesAsync(string root, List<string> problems)
    {
        var entries = new List<KitEntry>();
        foreach (var publisher in Sorted(new DirectoryInfo(root)))
        {
            if (publisher.LinkTarget is null && publisher is FileInfo && publisher.Name.StartsWith('.'))
            {
                continue;
            }

            if (publisher is DirectoryInfo { LinkTarget: null } folder)
            {
                entries.AddRange(await ReadPublisherAsync(folder, problems).ConfigureAwait(false));
            }
            else
            {
                problems.Add($"\"{KitText.Show(publisher.Name)}\" is not a folder of entries.");
            }
        }

        return entries;
    }

    private static async Task<List<KitEntry>> ReadPublisherAsync(DirectoryInfo folder, List<string> problems)
    {
        var entries = new List<KitEntry>();
        foreach (var file in Sorted(folder))
        {
            if (Unreadable(file, $"{folder.Name}/{file.Name}") is { } unreadable)
            {
                problems.Add(unreadable);
                continue;
            }

            try
            {
                var bytes = await File.ReadAllBytesAsync(file.FullName).ConfigureAwait(false);
                entries.Add(KitEntry.Parse(bytes, folder.Name, file.Name[..^".json".Length]));
            }
            catch (FormatException error)
            {
                problems.Add(error.Message);
            }
        }

        return entries;
    }

    // Why file is not read as an entry, or null when it is one. Looks before any byte is read, so a pipe or a
    // huge file cannot stall the build. shortcut: a pipe shows as an empty file, which no entry is, so every
    // empty file is refused as "not an entry" and none is read.
    private static string? Unreadable(FileSystemInfo file, string path)
    {
        var shown = KitText.Show(path);
        if (file is not FileInfo { LinkTarget: null, Length: > 0 } real || !file.Name.EndsWith(".json", StringComparison.Ordinal))
        {
            return $"\"{shown}\" is not an entry. An entry is a .json file.";
        }

        return real.Length > KitStoreRules.EntryLimit ? $"\"{shown}\" is over {KitStoreRules.EntryLimit} bytes." : null;
    }

    private static async Task WriteAsync(string outPath, List<BuiltKit> kits)
    {
        // Staged beside the final place and renamed, so a folder that appeared while the build ran is never
        // written into or deleted.
        var place = outPath.TrimEnd('/', '\\');
        var staging = $"{place}.{Random.Shared.NextInt64(1L << 32):x}.partial";
        Directory.CreateDirectory(staging);
        try
        {
            await WriteFilesAsync(staging, kits).ConfigureAwait(false);
            Directory.Move(staging, place);
        }
        catch
        {
            Directory.Delete(staging, recursive: true);
            throw;
        }
    }

    private static async Task WriteFilesAsync(string root, List<BuiltKit> kits)
    {
        foreach (var kit in kits)
        {
            var manifest = kit.Row.Manifest;
            var folder = Path.Combine(root, "kits", manifest.Publisher, manifest.Slug);
            Directory.CreateDirectory(folder);
            await File.WriteAllBytesAsync(Path.Combine(folder, "releases.json"), kit.Releases.Encode())
                .ConfigureAwait(false);
            foreach (var (name, bytes) in kit.Previews)
            {
                await File.WriteAllBytesAsync(Path.Combine(folder, name), bytes).ConfigureAwait(false);
            }
        }

        await File.WriteAllBytesAsync(
            Path.Combine(root, "index.json"), new KitIndex([.. kits.Select(kit => kit.Row)]).Encode())
            .ConfigureAwait(false);
    }
}
