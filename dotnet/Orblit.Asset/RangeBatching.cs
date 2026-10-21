namespace Orblit.Asset;

/// <summary>How a range queue groups wants into requests.</summary>
/// <remarks>
/// The defaults are a guess and have not been timed on a real host. A host with a long round trip wants a
/// larger <see cref="MergeGap"/> and <see cref="MaxBatch"/>. One that is quick to answer and slow to send
/// wants smaller ones.
/// </remarks>
public sealed record RangeBatching
{
    /// <summary>
    /// Two wants with at most this many bytes between them are one request, and the bytes between are
    /// fetched too.
    /// </summary>
    public long MergeGap { get; init; } = 16 * 1024;

    /// <summary>The most bytes a joined request grows to. One want larger than this is still sent whole.</summary>
    public long MaxBatch { get; init; } = 1024 * 1024;

    /// <summary>How many requests may be out at once.</summary>
    public int Parallel { get; init; } = 4;

    /// <summary>
    /// How long a want waits for others to join it before it is sent. Wants made within this time of the
    /// first join it, so a delay of nothing sends a want as soon as a thread is free to.
    /// </summary>
    public TimeSpan Delay { get; init; } = TimeSpan.FromMilliseconds(2);
}
