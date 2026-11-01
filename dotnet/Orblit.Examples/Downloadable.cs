namespace Orblit.Examples;

/// <summary>
/// Something an example needs, and how to get it.
/// </summary>
/// <remarks>
/// A description rather than a download. The example says what it wants and
/// which command brings it, and whatever is showing the example decides whether
/// that is a button, a line in a script or nothing at all. An example that ran
/// a download itself could not be run on a machine where downloading is
/// somebody else's decision.
/// </remarks>
public sealed record Downloadable
{
    /// <summary>What is missing, as somebody would say it: "the Bistro interior".</summary>
    public required string What { get; init; }

    /// <summary>
    /// Roughly how much, so nobody starts a seven-hundred-megabyte download
    /// wondering whether it is seven hundred kilobytes.
    /// </summary>
    public required string Size { get; init; }

    /// <summary>Where it comes from.</summary>
    public required string From { get; init; }

    /// <summary>
    /// Under what terms. Shown rather than buried, because a download button
    /// that does not say what it is fetching is one nobody should press.
    /// </summary>
    public required string Licence { get; init; }

    /// <summary>The command that fetches it, and its arguments. Run from the engine repository's root.</summary>
    public required IReadOnlyList<string> Command { get; init; }
}
