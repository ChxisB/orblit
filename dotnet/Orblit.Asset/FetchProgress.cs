namespace Orblit.Asset;

/// <summary>How much of an asset has arrived.</summary>
/// <param name="Received">Bytes so far.</param>
/// <param name="Total">
/// The whole size, when the server says. Null when it does not, which no progress bar likes and which happens
/// anyway on a chunked reply.
/// </param>
public sealed record FetchProgress(long Received, long? Total = null)
{
    /// <summary>Between nought and one, or null when the total is unknown.</summary>
    public double? Fraction => Total is null or 0 ? null : Math.Clamp((double)Received / Total.Value, 0.0, 1.0);
}
