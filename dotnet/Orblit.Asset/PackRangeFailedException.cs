namespace Orblit.Asset;

/// <summary>
/// The pack at <see cref="Url"/> could not be read in parts. Says what the server did, because the usual
/// cause is a host that does not answer a range and sends the whole file instead, and that is a setting to
/// change and not a bug to look for.
/// </summary>
public sealed class PackRangeFailedException(WebUrl url, string reason)
    : Exception($"Could not read part of the pack at {url}: {reason}.")
{
    public WebUrl Url { get; } = url;

    public string Reason { get; } = reason;
}
