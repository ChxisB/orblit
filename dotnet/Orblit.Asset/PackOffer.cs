namespace Orblit.Asset;

/// <summary>
/// A pack a game's server says is there to download. A URL and the hash the pack must have. The hash is
/// the pack's own, the one <see cref="AssetPack.Hash"/> answers and <c>opack build</c> prints, which is
/// also what a patch built on it names as its base. The game's server is the one place that can vouch for
/// it, so it comes from there with the URL.
/// </summary>
public sealed class PackOffer(WebUrl url, ContentHash hash)
{
    public WebUrl Url { get; } = url;

    /// <summary>The hash of the pack's directory, which stands for every blob in it.</summary>
    public ContentHash Hash { get; } = hash;

    public override string ToString() => $"PackOffer({Url}, {Hash.Short})";
}
