namespace Orblit.Asset;

/// <summary>
/// Assets fetched from a server. The <see cref="IAssetSource"/> face of an <see cref="AssetFetcher"/>, so
/// that everything already reading assets works unchanged against a server, including a layered source,
/// which is how a network source is usually wanted: what shipped in the app first, what has been downloaded
/// second, and the server last. The bytes are handed over as bytes. Nothing here writes a file, so an asset
/// never exists in a half-written state something else could pick up.
/// </summary>
/// <param name="fetcher">The fetcher that does the work.</param>
/// <param name="urgency">
/// How urgently anything read through this source is wanted. One setting for the whole source, because
/// <see cref="IAssetSource.ReadAsync"/> has nowhere to put a second argument and should not grow one. Code
/// that cares about the order things arrive in should be holding the fetcher, where it can also cancel and
/// watch progress. Two sources over one fetcher, one urgent and one not, is the cheap way to have both.
/// </param>
public sealed class NetworkAssetSource(AssetFetcher fetcher, FetchUrgency urgency = FetchUrgency.Soon) : IAssetSource
{
    public AssetFetcher Fetcher { get; } = fetcher;

    public FetchUrgency Urgency { get; } = urgency;

    /// <summary>
    /// The bytes of <paramref name="id"/>. A 404 or a 410 becomes <see cref="AssetNotFoundException"/>, which
    /// is what makes a network source work as a layer: a layered source moves to the next source on that and
    /// on nothing else. Every other failure, such as a refused host, a hash that does not match the manifest
    /// or a connection that never came back, is passed on untouched, because "the server has not got it" and
    /// "the server could not be reached" must not lead to the same place. One is a missing asset and the
    /// other is a broken connection, and quietly serving something older for the second hides an outage.
    /// </summary>
    public async Task<byte[]> ReadAsync(AssetId id)
    {
        try
        {
            return await Fetcher.ReadAsync(id, Urgency).ConfigureAwait(false);
        }
        catch (FetchFailedException failure) when (failure.Cause is 404 or 410)
        {
            throw new AssetNotFoundException(id, $"the server answered {failure.Cause}");
        }
    }

    /// <summary>
    /// Whether the manifest says this server has <paramref name="id"/>. Only as good as the manifest. Asking
    /// a server whether it has something costs a request, and answering that question by fetching the asset,
    /// which is what a source without a manifest would have to do, turns a cheap question into the most
    /// expensive one in the package. So with a manifest this is exact, and without one it answers for what
    /// has already been fetched and is therefore certain, and under-reports everything else. Code choosing
    /// between sources should read and catch <see cref="AssetNotFoundException"/> instead, which costs one
    /// fetch rather than two.
    /// </summary>
    public async Task<bool> ExistsAsync(AssetId id)
    {
        if (Fetcher.Manifest is { } manifest)
        {
            return manifest.Entries.ContainsKey(id);
        }

        var record = await Fetcher.Records.GetAsync(Fetcher.Origin.UrlOf(id)).ConfigureAwait(false);
        return record is not null && await Fetcher.Store.ContainsAsync(record.Hash).ConfigureAwait(false);
    }
}
