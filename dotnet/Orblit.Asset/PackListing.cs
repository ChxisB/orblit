namespace Orblit.Asset;

/// <summary>A file in a pack folder that could not be opened as a pack, and why.</summary>
public sealed record UnreadablePack(string Path, string Reason)
{
    /// <summary>The path and the reason, as one line.</summary>
    public override string ToString() => $"{Path}: {Reason}";
}

/// <summary>What a pack folder holds.</summary>
public sealed class PackListing
{
    /// <summary>Every pack that opened, in the order of their hashes.</summary>
    public IReadOnlyList<AssetPack> Packs { get; init; } = [];

    /// <summary>
    /// Files that look like packs and are not. A damaged file is reported here and not thrown, because
    /// one bad file in the folder must not stop a game that has three good ones.
    /// </summary>
    public IReadOnlyList<UnreadablePack> Unreadable { get; init; } = [];

    /// <summary>
    /// Downloads that started and have not finished, by the pack's hash. Asking for the same offer
    /// again resumes from what is there.
    /// </summary>
    public IReadOnlyList<ContentHash> Unfinished { get; init; } = [];
}
