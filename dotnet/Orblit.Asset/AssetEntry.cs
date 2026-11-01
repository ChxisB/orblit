namespace Orblit.Asset;

/// <summary>
/// Which bytes one asset currently is. The size travels beside the hash, which pins the bytes
/// down on its own, because a size can be used before a single byte has arrived.
/// </summary>
public sealed record AssetEntry(ContentHash Hash, long Bytes)
{
    /// <summary>How many bytes that is.</summary>
    public long Bytes { get; } = Bytes >= 0
        ? Bytes
        : throw new ArgumentOutOfRangeException(nameof(Bytes), Bytes, "A size cannot be negative.");
}
