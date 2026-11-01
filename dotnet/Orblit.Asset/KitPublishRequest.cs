namespace Orblit.Asset;

/// <summary>What <see cref="KitPacking.PublishAsync"/> is asked to publish, and where.</summary>
public sealed record KitPublishRequest
{
    /// <summary>The folder of the kit, with its <c>kit.json</c>.</summary>
    public required string Folder { get; init; }

    /// <summary>Where the kit file will be hosted.</summary>
    public required string Url { get; init; }

    /// <summary>Where the kit file is written. Not inside <see cref="Folder"/>.</summary>
    public required string OutPath { get; init; }

    /// <summary>The day of the release, as its own clock spells it.</summary>
    public required DateTime Released { get; init; }

    /// <summary>The one line of change notes.</summary>
    public string Notes { get; init; } = "";

    /// <summary>The folder of entries <c>kit index</c> reads, or null to write only the kit file.</summary>
    public string? EntriesPath { get; init; }
}
