namespace Orblit.Asset;

/// <summary>
/// A folder of packs downloaded after install. Every pack in it is named by its hash,
/// <c>&lt;hash&gt;.opack</c>, and a download in progress is <c>&lt;hash&gt;.part</c>. A file only gets
/// a pack's name once it has been checked, so a crash or a power cut part way through leaves a file
/// nothing will mount.
/// </summary>
/// <remarks>
/// The folder holds packs the game fetched. A mods folder a player drops files into is a different
/// thing: open those with <see cref="FilePackBytes"/> and <see cref="AssetPack.OpenAsync"/>, whatever
/// they are called.
/// </remarks>
public sealed class PackFolder(string path)
{
    private const string PackSuffix = ".opack";
    private const string PartSuffix = ".part";

    /// <summary>The folder. It is made when the first pack arrives.</summary>
    public string Path { get; } = path;

    /// <summary>
    /// Every pack here, and everything here that is not one. A file whose contents do not hash to its
    /// name is reported and not opened, since the name is what makes the folder safe to trust.
    /// </summary>
    public async Task<PackListing> ListAsync()
    {
        var files = Files();
        var packs = new List<AssetPack>();
        var unreadable = new List<UnreadablePack>();
        foreach (var file in files.Where(file => NameOf(file).EndsWith(PackSuffix, StringComparison.Ordinal)))
        {
            try
            {
                packs.Add(await OpenNamedAsync(file).ConfigureAwait(false));
            }
            catch (Exception error) when (error is FormatException or IOException or UnauthorizedAccessException)
            {
                unreadable.Add(new UnreadablePack(file, error.Message));
            }
        }

        return new PackListing
        {
            Packs = packs,
            Unreadable = unreadable,
            Unfinished = [.. files.Select(file => HashOf(file, PartSuffix)).OfType<ContentHash>()],
        };
    }

    /// <summary>
    /// Deletes the pack with <paramref name="hash"/> and any half-finished download of it, and says
    /// whether either was there. Call it before the pack is mounted. A mounted pack holds its file open.
    /// </summary>
    public Task<bool> RemoveAsync(ContentHash hash)
    {
        var packGone = Delete(PackFile(hash));
        var partGone = Delete(PartFile(hash));
        return Task.FromResult(packGone || partGone);
    }

    private static bool Delete(string file)
    {
        if (!File.Exists(file))
        {
            return false;
        }

        File.Delete(file);
        return true;
    }

    private static string NameOf(string file) => System.IO.Path.GetFileName(file);

    private static ContentHash? HashOf(string file, string suffix)
    {
        var name = NameOf(file);
        if (!name.EndsWith(suffix, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            return ContentHash.Parse(name[..^suffix.Length]);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    // Opens file, which has to be named for the hash of what is in it.
    private static async Task<AssetPack> OpenNamedAsync(string file)
    {
        var pack = await OpenFileAsync(file).ConfigureAwait(false);
        return NameOf(file) != pack.Hash.Hex + PackSuffix
            ? throw new FormatException($"its name is not the hash of what is in it, which is {pack.Hash.Hex}")
            : pack;
    }

    // Opens the pack in file and lets go of the file, which a read opens again. A folder with a dozen
    // packs in it does not hold a dozen handles for the ones that are never read.
    private static async Task<AssetPack> OpenFileAsync(string file)
    {
        var bytes = new FilePackBytes(file);
        try
        {
            return await AssetPack.OpenAsync(bytes).ConfigureAwait(false);
        }
        finally
        {
            await bytes.CloseAsync().ConfigureAwait(false);
        }
    }

    // The files in the folder, by name, or none when it is not there yet.
    private string[] Files()
    {
        if (!Directory.Exists(Path))
        {
            return [];
        }

        var files = Directory.GetFiles(Path);
        Array.Sort(files, StringComparer.Ordinal);
        return files;
    }

    private string PackFile(ContentHash hash) => System.IO.Path.Join(Path, hash.Hex + PackSuffix);

    private string PartFile(ContentHash hash) => System.IO.Path.Join(Path, hash.Hex + PartSuffix);
}
