namespace Orblit.Asset;

/// <summary>
/// Reads a store of kits over an <see cref="IAssetTransport"/>: the index, the releases of one kit, its
/// previews and the file list of a release.
/// </summary>
/// <remarks>
/// Every file is read to the limit the format gives it and no further, so a store cannot make a reader hold
/// more than that. A reply that is not a 200 is refused. Nothing here writes, and no file of a kit is
/// fetched except the manifest.
/// </remarks>
public sealed class KitStoreClient
{
    // The most OpenAsync reads in one piece. A header says how long its directory is, and a store must not
    // pick how much a reader holds to list the files. A kit of 5000 files has a directory of about a megabyte.
    private const long OpenLimit = 4 * 1024 * 1024;

    private readonly IAssetTransport _transport;

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> when <see cref="AddressProblem"/> has something to say about
    /// <paramref name="baseUrl"/>. A path with no trailing slash names a folder.
    /// </summary>
    public KitStoreClient(WebUrl baseUrl, IAssetTransport transport, TimeSpan? timeout = null)
    {
        if (AddressProblem(baseUrl) is { } problem)
        {
            throw new ArgumentException(problem, nameof(baseUrl));
        }

        _transport = transport;
        Base = baseUrl.Path.EndsWith('/') ? baseUrl : baseUrl.WithPath($"{baseUrl.Path}/");
        Timeout = timeout ?? TimeSpan.FromSeconds(30);
    }

    /// <summary>The folder of the store, which holds <c>index.json</c>. Ends in a slash.</summary>
    public WebUrl Base { get; }

    /// <summary>
    /// How long one file, or the file list of one release, may take.
    /// </summary>
    /// <remarks>
    /// shortcut: this stops the wait and not the request. The caller disposes the transport to stop the
    /// request. Pass a token through <see cref="IAssetTransport"/> when a request must be cancelled alone.
    /// </remarks>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Why <paramref name="baseUrl"/> cannot be the address of a store, or null. A store is read over
    /// https, from a host, with no user, query or fragment.
    /// </summary>
    public static string? AddressProblem(WebUrl baseUrl) =>
        baseUrl.Scheme == "https" && baseUrl.Host.Length > 0 && baseUrl.UserInfo.Length == 0
            && baseUrl.Query is null && baseUrl.Fragment is null
            ? null
            : "An address of a store is https, with a host and no user, query or fragment.";

    public async Task<KitIndex> IndexAsync() =>
        KitIndex.Parse(await GetAsync("index.json", KitStoreRules.IndexLimit).ConfigureAwait(false));

    public async Task<KitReleases> ReleasesAsync(KitRow row) =>
        KitReleases.Parse(
            await GetAsync($"{FolderOf(row)}releases.json", KitStoreRules.ReleasesLimit).ConfigureAwait(false));

    /// <summary>The bytes of the preview image <paramref name="name"/>, one of <see cref="KitReleases.Previews"/>.</summary>
    public async Task<byte[]> PreviewAsync(KitRow row, string name)
    {
        if (!KitStoreRules.IsPreviewName(name))
        {
            throw new ArgumentException($"\"{name}\" is not a preview file.", nameof(name));
        }

        return await GetAsync($"{FolderOf(row)}{name}", KitStoreRules.PreviewBytes).ConfigureAwait(false);
    }

    /// <summary>The image a card shows, or null for a kit that lists none.</summary>
    public async Task<byte[]?> ThumbnailAsync(KitRow row) =>
        row.Thumbnail is { } path ? await PreviewAsync(row, path[(path.LastIndexOf('/') + 1)..]).ConfigureAwait(false) : null;

    /// <summary>
    /// <paramref name="release"/> opened without its blobs: the file list, the sizes and the manifest, by
    /// ranged requests.
    /// </summary>
    /// <exception cref="PackRangeFailedException">The server does not answer a range.</exception>
    /// <exception cref="FormatException">
    /// The file is not a kit, or asks to read more than a kit's directory needs.
    /// </exception>
    public Task<Kit> OpenAsync(KitRelease release)
    {
        var url = WebUrl.Parse(release.Url);
        return WithinAsync(url, async () =>
        {
            var bytes = new Bounded(new RangePackBytes(_transport, url));
            return await Kit.OpenAsync(await AssetPack.OpenAsync(bytes).ConfigureAwait(false)).ConfigureAwait(false);
        });
    }

    private static string FolderOf(KitRow row) => $"kits/{row.Manifest.Publisher}/{row.Manifest.Slug}/";

    // A timeout from the wait is the store's. One the read itself throws is passed on as it is.
    private async Task<T> WithinAsync<T>(WebUrl url, Func<Task<T>> read)
    {
        var task = read();
        try
        {
            return await task.WaitAsync(Timeout).ConfigureAwait(false);
        }
        catch (TimeoutException) when (!task.IsCompleted)
        {
            throw new KitStoreFailedException(url, "it was not answered in time");
        }
    }

    private Task<byte[]> GetAsync(string path, int limit)
    {
        var url = Base.Resolve(WebUrl.Parse(path));
        return WithinAsync(url, () => ReadAsync(url, limit));
    }

    private async Task<byte[]> ReadAsync(WebUrl url, int limit)
    {
        var reply = await _transport.SendAsync(new FetchRequest(url)).ConfigureAwait(false);
        if (reply.Status != 200)
        {
            await reply.DiscardAsync().ConfigureAwait(false);
            throw new KitStoreFailedException(url, $"the server answered {reply.Status}");
        }

        if (reply.Length is { } length && length > limit)
        {
            await reply.DiscardAsync().ConfigureAwait(false);
            throw new KitStoreFailedException(url, $"it is {length} bytes and the most is {limit}");
        }

        using var body = new MemoryStream();
        await foreach (var chunk in reply.Body.ConfigureAwait(false))
        {
            body.Write(chunk);
            if (body.Length > limit)
            {
                throw new KitStoreFailedException(url, $"it passed the {limit} bytes allowed");
            }
        }

        return body.ToArray();
    }

    // Refuses a read of more than OpenLimit before it is made.
    private sealed class Bounded(IPackBytes inner) : IPackBytes
    {
        public Task<long> LengthAsync() => inner.LengthAsync();

        public Task<byte[]> ReadAsync(long at, long bytes) =>
            bytes > OpenLimit
                ? Task.FromException<byte[]>(new FormatException(
                    $"The file asks for {bytes} bytes to list its files, and the most is {OpenLimit}."))
                : inner.ReadAsync(at, bytes);
    }
}
