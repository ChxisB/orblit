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

    private readonly Lock _gate = new();
    private readonly Dictionary<ContentHash, Task<AssetPack>> _running = [];

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

    /// <summary>
    /// Fetches the pack <paramref name="offer"/> names and answers it opened, with every blob checked.
    /// Returns at once when the pack is already here. A download that was cut off, in this run or an earlier
    /// one, resumes from the bytes held. The pack has to hash to <c>offer.Hash</c> and every blob in it has
    /// to match the directory. A failure of either throws and leaves no file with a pack's name.
    /// </summary>
    /// <remarks>
    /// A connection that failed, or a server that answered 5xx, 408, 425 or 429, is tried again up to
    /// <paramref name="attempts"/> times in all, waiting <paramref name="backoff"/> (one second) and then
    /// twice as long each time, give or take a fifth. A resumed attempt costs only the bytes still missing,
    /// so more attempts are cheap. A 404, a host <paramref name="origin"/> does not allow, a pack over the
    /// policy's <c>MaxBytes</c> and a pack that is not <paramref name="offer"/>'s throw at once.
    /// <para>
    /// A second call for the same hash while one is running shares it, and its
    /// <paramref name="onProgress"/> is not called.
    /// </para>
    /// </remarks>
    // F2: a download is described by what it fetches and how, all named at the call as Dart names them.
    public Task<AssetPack> DownloadAsync(
        PackOffer offer,
        IAssetTransport transport,
        AssetOrigin origin,
        int attempts = 5,
        TimeSpan? backoff = null,
        Random? jitter = null,
        Action<FetchProgress>? onProgress = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attempts);
        lock (_gate)
        {
            if (_running.TryGetValue(offer.Hash, out var running))
            {
                return running;
            }

            var download = new PackDownload
            {
                Offer = offer,
                Transport = transport,
                Origin = origin,
                Part = PartFile(offer.Hash),
                Attempts = attempts,
                Backoff = backoff ?? TimeSpan.FromSeconds(1),
                Jitter = jitter ?? Random.Shared,
                OnProgress = onProgress,
            };
            return _running[offer.Hash] = FetchAsync(offer.Hash, download);
        }
    }

    private async Task<AssetPack> FetchAsync(ContentHash hash, PackDownload download)
    {
        // The task is made under the gate, so it must not finish, and take itself out of the running list,
        // before the gate has put it in.
        await Task.Yield();
        try
        {
            Directory.CreateDirectory(Path);
            if (await HeldCopyAsync(hash).ConfigureAwait(false) is { } held)
            {
                return held;
            }

            // A file with the pack's name and other contents is in the way of the rename that finishes a download.
            Delete(PackFile(hash));
            return await download.RunAsync().ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                _running.Remove(hash);
            }
        }
    }

    // The pack already filed under hash, or null when there is none or what has its name is not that pack.
    private async Task<AssetPack?> HeldCopyAsync(ContentHash hash)
    {
        var file = PackFile(hash);
        if (!File.Exists(file))
        {
            return null;
        }

        try
        {
            var pack = await OpenFileAsync(file).ConfigureAwait(false);
            return pack.Hash == hash ? pack : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    internal static bool Delete(string file)
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
    internal static async Task<AssetPack> OpenFileAsync(string file)
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
