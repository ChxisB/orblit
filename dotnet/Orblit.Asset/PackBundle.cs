namespace Orblit.Asset;

/// <summary>Packs a cooked bundle.</summary>
public static class PackBundle
{
    /// <summary>
    /// Writes the cooked bundle in <paramref name="bundlePath"/> as one pack at
    /// <paramref name="outPath"/>. Each blob is copied and hashed on the way, and a file that is not
    /// what the bundle's manifest says it is refuses the whole pack. That check is how a stale bundle
    /// is caught. Assets with the same bytes share one blob. The pack is written beside
    /// <paramref name="outPath"/> and renamed into place, so a failure leaves no half of one behind.
    /// </summary>
    /// <remarks>
    /// With <paramref name="base"/>, the packs already shipped, in the order they were mounted, the
    /// result is a patch. It holds the assets whose hash differs from what the chain serves or that the
    /// chain lacks, and names the ids the bundle has dropped. Nothing in <paramref name="base"/> is read
    /// for its bytes. The comparison is by hash. The patch's own base is the hash of the last pack in
    /// <paramref name="base"/>.
    /// </remarks>
    public static async Task<PackSummary> PackAsync(
        string bundlePath, string outPath, CookTarget target, IReadOnlyList<AssetPack>? @base = null)
    {
        var bundle = await ReadManifestAsync(bundlePath).ConfigureAwait(false);
        var chain = Mount(target, @base ?? []);
        var manifest = ChangedFrom(bundle, chain);
        try
        {
            return await PackWriter.WriteAsync(
                manifest,
                outPath,
                target.Name,
                read: id => ReadBundleFileAsync(bundlePath, id),
                @base: chain?.ChainHash,
                removed: DroppedFrom(bundle, chain),
                added: NewTo(manifest, chain)).ConfigureAwait(false);
        }
        catch (FormatException error)
        {
            throw new FormatException(
                $"{error.Message} The bundle \"{bundlePath}\" is stale or was changed by hand. Cook the project again.");
        }
    }

    private static MountedPacks? Mount(CookTarget target, IReadOnlyList<AssetPack> @base)
    {
        if (@base.Count == 0)
        {
            return null;
        }

        var chain = new MountedPacks(target);
        chain.Start(@base[0]);
        foreach (var pack in @base.Skip(1))
        {
            chain.Patch(pack);
        }

        return chain;
    }

    // The bundle's assets that the chain does not already serve, by hash.
    private static AssetManifest ChangedFrom(AssetManifest bundle, MountedPacks? chain) => chain is null
        ? bundle
        : new AssetManifest(Ordered.Copy(
            bundle.Entries.Where(e => chain.Manifest.Entries.GetValueOrDefault(e.Key)?.Hash != e.Value.Hash)));

    // How many of the held assets the chain has no answer for at all.
    private static int NewTo(AssetManifest held, MountedPacks? chain) => chain is null
        ? held.Entries.Count
        : held.Entries.Keys.Count(id => !chain.Manifest.Entries.ContainsKey(id));

    // What the chain serves that the bundle no longer has, in id order.
    private static List<AssetId> DroppedFrom(AssetManifest bundle, MountedPacks? chain) => chain is null
        ? []
        : Ordered.ById(chain.Manifest.Entries.Keys.Where(id => !bundle.Entries.ContainsKey(id)));

    private static async Task<AssetManifest> ReadManifestAsync(string bundlePath)
    {
        string text;
        try
        {
            text = TextRules.Decode(await File.ReadAllBytesAsync(Inside(bundlePath, "manifest.json")).ConfigureAwait(false));
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new FormatException(
                $"\"{bundlePath}\" has no manifest.json. It is not a cooked bundle. Cook the project first.");
        }

        var load = AssetManifest.Read(text);
        return load.HasProblems
            ? throw new FormatException(
                $"The manifest in \"{bundlePath}\" has problems, so the bundle cannot be packed: {string.Join(' ', load.Problems)}")
            : load.Manifest;
    }

    private static async Task<byte[]> ReadBundleFileAsync(string bundlePath, AssetId id)
    {
        try
        {
            return await File.ReadAllBytesAsync(Inside(bundlePath, id.ToString())).ConfigureAwait(false);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            throw new FormatException($"The manifest lists \"{id}\" and \"{bundlePath}\" has no such file.");
        }
    }

    private static string Inside(string root, string id) => string.Join(Path.DirectorySeparatorChar, [root, .. id.Split('/')]);
}
