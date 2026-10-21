namespace Orblit.Asset;

/// <summary>
/// What an update of an installed kit does to each file. The project is the person's, so a file is
/// touched only when it is still as <c>kits.lock</c> recorded it. A file that differs is kept and listed.
/// </summary>
public sealed class KitUpdatePlan(
    IEnumerable<AssetId> replace, IEnumerable<AssetId> add, IEnumerable<AssetId> remove, IEnumerable<AssetId> kept)
{
    /// <summary>Files the release changed that the person has not edited.</summary>
    public IReadOnlyList<AssetId> Replace { get; } = [.. replace];

    /// <summary>Files new in the release with nothing in their place.</summary>
    public IReadOnlyList<AssetId> Add { get; } = [.. add];

    /// <summary>Files the release dropped that the person has not edited.</summary>
    public IReadOnlyList<AssetId> Remove { get; } = [.. remove];

    /// <summary>
    /// Files the person edited, deleted or put another file in place of, that the release would have
    /// written or removed.
    /// </summary>
    public IReadOnlyList<AssetId> Kept { get; } = [.. kept];

    public bool IsEmpty => Replace.Count == 0 && Add.Count == 0 && Remove.Count == 0 && Kept.Count == 0;

    private enum Move
    {
        Nothing,
        Replace,
        Add,
        Remove,
        Keep,
    }

    /// <summary>
    /// Plans an update from what <paramref name="locked"/> recorded, what the new release holds in
    /// <paramref name="incoming"/> and what is on disk now in <paramref name="onDisk"/>. All three are keyed by
    /// place in the project. A place missing from <paramref name="onDisk"/> counts as not there. Answers and
    /// changes nothing.
    /// </summary>
    public static KitUpdatePlan Plan(
        IReadOnlyDictionary<AssetId, ContentHash> locked,
        IReadOnlyDictionary<AssetId, ContentHash> incoming,
        IReadOnlyDictionary<AssetId, ContentHash?> onDisk)
    {
        var moves = Enum.GetValues<Move>().ToDictionary(move => move, _ => new List<AssetId>());
        foreach (var place in Ordered.ById(locked.Keys.Union(incoming.Keys)))
        {
            var move = Decide(locked.GetValueOrDefault(place), incoming.GetValueOrDefault(place), onDisk.GetValueOrDefault(place));
            moves[move].Add(place);
        }

        return new KitUpdatePlan(moves[Move.Replace], moves[Move.Add], moves[Move.Remove], moves[Move.Keep]);
    }

    private static Move Decide(ContentHash? was, ContentHash? will, ContentHash? found)
    {
        // No lock entry means the release has the file.
        if (was is null)
        {
            return DecideNew(will!, found); // the place came from one of the two maps, so the release has it
        }

        if (will == was || (will is null && found is null))
        {
            return Move.Nothing;
        }

        if (found != was)
        {
            return Move.Keep;
        }

        return will is null ? Move.Remove : Move.Replace;
    }

    private static Move DecideNew(ContentHash will, ContentHash? found)
    {
        if (found is null)
        {
            return Move.Add;
        }

        return found == will ? Move.Nothing : Move.Keep;
    }
}
