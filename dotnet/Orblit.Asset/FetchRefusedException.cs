namespace Orblit.Asset;

/// <summary>
/// A fetch that was not allowed to happen, and why. Thrown before anything is sent rather than returned
/// as a failed reply, because a refusal here is never about the network. It means the program asked for
/// something it is not configured to ask for, such as a host nobody listed, a plain-text URL or a path
/// climbing out of its own directory. That is a mistake in the program or an attempt on it, not a
/// connection to retry.
/// </summary>
public sealed class FetchRefusedException(string url, string reason)
    : Exception($"Refused to fetch {url}: {reason}.")
{
    /// <summary>What was asked for. Text rather than a <see cref="WebUrl"/> because some of what is refused is not one.</summary>
    public string Url { get; } = url;

    /// <summary>One sentence, written to be read by whoever has to fix it.</summary>
    public string Reason { get; } = reason;
}
