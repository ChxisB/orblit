namespace Orblit.Asset;

/// <summary>What happened to one asset.</summary>
public sealed class CookResult
{
    public required AssetId Id { get; init; }

    public required CookStatus Status { get; init; }

    /// <summary>The importer that claimed it, absent only when nothing did.</summary>
    public Importer? Importer { get; init; }

    /// <summary>
    /// The key its outputs are filed under, absent when it was skipped or when it failed before the key could be
    /// built.
    /// </summary>
    public CookKey? Key { get; init; }

    /// <summary>The outputs, present whenever the status is <see cref="CookStatus.Cooked"/> or <see cref="CookStatus.Cached"/>.</summary>
    public CookedAsset? Asset { get; init; }

    /// <summary>The other assets that went into it, which a build system needs in order to know what to watch.</summary>
    public IReadOnlySet<AssetId> Dependencies { get; init; } = new HashSet<AssetId>();

    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>What failed. Its stack trace is the one Dart keeps in a separate <c>trace</c>.</summary>
    public Exception? Error { get; init; }

    public bool Ok => Status != CookStatus.Failed;

    public override string ToString() => Status switch
    {
        CookStatus.Cooked => $"cooked {Id} with {Importer}",
        CookStatus.Cached => $"cached {Id}",
        CookStatus.Skipped => $"skipped {Id}",
        _ => $"failed {Id}: {Error?.Message}",
    };
}
