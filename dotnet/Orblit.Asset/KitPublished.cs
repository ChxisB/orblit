namespace Orblit.Asset;

/// <summary>What <see cref="KitPacking.PublishAsync"/> made: the kit file and the entry that lists it.</summary>
public sealed record KitPublished
{
    /// <summary>What was written to the kit file. Its hash and size are the release's.</summary>
    public required PackSummary Summary { get; init; }

    /// <summary>The release this run made.</summary>
    public required KitRelease Release { get; init; }

    /// <summary>The whole entry as it should read: the releases it listed already, and <see cref="Release"/>.</summary>
    public required KitEntry Entry { get; init; }

    /// <summary>
    /// The file <see cref="Entry"/> was written to, or null when no folder of entries was given and
    /// nothing but the kit file was written.
    /// </summary>
    public string? EntryPath { get; init; }
}
