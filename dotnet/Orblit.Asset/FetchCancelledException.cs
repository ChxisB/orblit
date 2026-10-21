namespace Orblit.Asset;

/// <summary>A fetch somebody gave up on.</summary>
public sealed class FetchCancelledException(WebUrl url) : Exception($"The fetch of {url} was cancelled.")
{
    public WebUrl Url { get; } = url;
}
