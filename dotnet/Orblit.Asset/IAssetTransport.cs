namespace Orblit.Asset;

/// <summary>
/// The thing that actually talks to a server. A seam rather than a hard dependency on one client, for three
/// reasons that each came up on their own: a test wants to answer without a network, an app may already have
/// a client carrying authentication or a certificate pin, and a platform may have something better than the
/// default, such as an HTTP/3 stack or a game console's own downloader, that this package should not have to
/// know about.
/// </summary>
public interface IAssetTransport : IDisposable
{
    /// <summary>
    /// Sends <paramref name="request"/> and answers with the headers, before the body arrives. Throwing
    /// means the request never got an answer: no connection, no route, a timeout. A status code is an answer,
    /// however unwelcome, and comes back as a reply. Sending after <see cref="IDisposable.Dispose"/> is a mistake.
    /// </summary>
    Task<FetchReply> SendAsync(FetchRequest request, CancellationToken cancellation = default);
}
