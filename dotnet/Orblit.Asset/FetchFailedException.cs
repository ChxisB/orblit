namespace Orblit.Asset;

/// <summary>A fetch that ran out of attempts, carrying the last thing that went wrong.</summary>
public sealed class FetchFailedException(WebUrl url, int attempts, object cause)
    : Exception(Describe(url, attempts, cause), cause as Exception)
{
    public WebUrl Url { get; } = url;

    public int Attempts { get; } = attempts;

    /// <summary>
    /// The last failure: an exception thrown by the transport, or an <see cref="int"/> status code the server
    /// answered with.
    /// </summary>
    public object Cause { get; } = cause;

    private static string Describe(WebUrl url, int attempts, object cause)
    {
        var why = cause is int status ? $"the server answered {status}" : cause is Exception error ? error.Message : $"{cause}";
        return $"Could not fetch {url} after {attempts} {(attempts == 1 ? "attempt" : "attempts")}: {why}.";
    }
}
