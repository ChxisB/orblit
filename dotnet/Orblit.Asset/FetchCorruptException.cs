namespace Orblit.Asset;

/// <summary>
/// An asset that was fetched and is not what it should be. Its own failure rather than a general one, because
/// the answer is different: a connection failure is retried and a wrong hash is not. Bytes that do not match
/// the manifest are either a corrupted transfer or somebody else's bytes, and both of those are reasons to
/// stop rather than to try again more slowly.
/// </summary>
public sealed class FetchCorruptException(WebUrl url, ContentHash wanted, ContentHash got)
    : Exception($"What came back from {url} is not what the manifest says it should be. Expected {wanted}, got {got}.")
{
    public WebUrl Url { get; } = url;

    public ContentHash Wanted { get; } = wanted;

    public ContentHash Got { get; } = got;
}
