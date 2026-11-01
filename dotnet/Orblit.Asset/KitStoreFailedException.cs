namespace Orblit.Asset;

/// <summary>A file of a store could not be read.</summary>
public sealed class KitStoreFailedException(WebUrl url, string reason)
    : Exception($"Could not read {url}: {reason}.")
{
    public WebUrl Url { get; } = url;

    public string Reason { get; } = reason;
}
