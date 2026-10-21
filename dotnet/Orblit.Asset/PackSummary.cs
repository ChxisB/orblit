namespace Orblit.Asset;

/// <summary>What writing a pack produced.</summary>
public sealed record PackSummary
{
    /// <summary>The pack's hash, which is the hash of its directory.</summary>
    public required ContentHash Hash { get; init; }

    /// <summary>The size of the file.</summary>
    public required long Bytes { get; init; }

    /// <summary>How many assets it holds.</summary>
    public required int Assets { get; init; }

    /// <summary>How many of <see cref="Assets"/> the base did not serve. Every asset of a pack with no base.</summary>
    public required int Added { get; init; }

    /// <summary>
    /// How many of <see cref="Assets"/> the base served with other bytes. Always 0 for a pack with no
    /// base, and <c>Added + Replaced == Assets</c>.
    /// </summary>
    public required int Replaced { get; init; }

    /// <summary>How many blobs it holds. Fewer than <see cref="Assets"/> when some share bytes.</summary>
    public required int Blobs { get; init; }

    /// <summary>The bytes left out because two assets had the same content.</summary>
    public required long SharedBytes { get; init; }

    /// <summary>How many ids the pack takes away from the one it was built against.</summary>
    public required int Removed { get; init; }
}
