namespace Orblit.Asset;

/// <summary>A pack in a folder that the chain did not take, and why.</summary>
/// <param name="Pack">The pack left out.</param>
/// <param name="Reason">One sentence, written to be read by whoever has to fix it.</param>
public sealed record SkippedPack(AssetPack Pack, string Reason)
{
    public override string ToString() => $"{Pack.Hash.Short}: {Reason}";
}

/// <summary>
/// The packs found in a folder, put in the order a game mounts them. A patch names the pack it was
/// built on, so the order is not a matter of file names or dates. It is found by starting at the pack
/// the app shipped and following the packs that name it, then the ones that name those. A folder that
/// lists its files in any order gives the same answer.
/// </summary>
/// <remarks>
/// Nothing is mounted here. <see cref="Ordered"/> goes to <see cref="MountedPacks.Patch"/> in order and
/// <see cref="Standalone"/> to <see cref="MountedPacks.AddMod"/>, and a game that wants to say why a
/// pack was left out reads <see cref="Skipped"/>.
/// </remarks>
public sealed class PatchChain
{
    private PatchChain(IReadOnlyList<AssetPack> ordered, IReadOnlyList<AssetPack> standalone, IReadOnlyList<SkippedPack> skipped)
    {
        Ordered = ordered;
        Standalone = standalone;
        Skipped = skipped;
    }

    /// <summary>The patches that stand on the shipped pack, lowest first.</summary>
    public IReadOnlyList<AssetPack> Ordered { get; }

    /// <summary>
    /// Packs with no base, which go above the chain as mods. In the order <see cref="Of"/> was given
    /// them, which is by hash when they come from <see cref="PackFolder.ListAsync"/>. Which mod wins an
    /// id is the order it is mounted in, and an app that lets players choose sorts these itself.
    /// </summary>
    public IReadOnlyList<AssetPack> Standalone { get; }

    public IReadOnlyList<SkippedPack> Skipped { get; }

    /// <summary>
    /// Sorts <paramref name="found"/> against <paramref name="shipped"/>, the pack the chain starts from.
    /// A pack is skipped, with a reason, when it was built for another target, when the pack it was
    /// built on is not here, and when two packs were built on the same one. The last case is a fork.
    /// Nothing says which of the two is the later, so the chain stops there and does not guess.
    /// </summary>
    public static PatchChain Of(AssetPack shipped, IEnumerable<AssetPack> found)
    {
        var sorting = new Sorting(shipped, found);
        sorting.Follow();
        sorting.ExplainTheRest();
        return new PatchChain(sorting.Ordered, sorting.Standalone, [.. sorting.Skipped.Select(e => new SkippedPack(e.Key, e.Value))]);
    }

    // One run of Of, holding what it has worked out so far.
    private sealed class Sorting
    {
        private readonly AssetPack _shipped;
        private readonly OrderedDictionary<ContentHash, AssetPack> _everything = [];
        private readonly List<AssetPack> _patches = [];

        public Sorting(AssetPack shipped, IEnumerable<AssetPack> found)
        {
            _shipped = shipped;
            foreach (var pack in found.Where(pack => pack.Hash != shipped.Hash))
            {
                _everything[pack.Hash] = pack;
            }

            foreach (var pack in _everything.Values)
            {
                Sort(pack);
            }
        }

        public List<AssetPack> Ordered { get; } = [];

        public List<AssetPack> Standalone { get; } = [];

        public OrderedDictionary<AssetPack, string> Skipped { get; } = [];

        // Walks up from the shipped pack, one patch at a time, until nothing names the top or two things do.
        public void Follow()
        {
            var top = _shipped.Hash;
            while (Ordered.Count < _patches.Count)
            {
                var next = _patches.Where(pack => pack.Base == top).ToList();
                if (next.Count == 0)
                {
                    return;
                }

                if (next.Count > 1)
                {
                    foreach (var pack in next)
                    {
                        Skipped[pack] = $"{next.Count} patches were built on {top.Short}, and nothing says which is later";
                    }

                    return;
                }

                Ordered.Add(next[0]);
                top = next[0].Hash;
            }
        }

        // Gives every patch that is neither in the chain nor skipped a reason.
        public void ExplainTheRest()
        {
            foreach (var pack in _patches.Where(pack => !Ordered.Contains(pack) && !Skipped.ContainsKey(pack)))
            {
                var @base = pack.Base!; // a patch has a base, or Sort would not have kept it
                Skipped[pack] = _everything.ContainsKey(@base)
                    ? $"the patch it was built on, {@base.Short}, is not in the chain"
                    : $"it was built on {@base.Short} and the shipped pack is {_shipped.Hash.Short}";
            }
        }

        private void Sort(AssetPack pack)
        {
            if (pack.Target != _shipped.Target)
            {
                Skipped[pack] = $"it is for {pack.Target} and the shipped pack is for {_shipped.Target}";
            }
            else if (pack.Base is null)
            {
                Standalone.Add(pack);
            }
            else
            {
                _patches.Add(pack);
            }
        }
    }
}
