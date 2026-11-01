namespace Orblit.Asset;

/// <summary>
/// Thrown when a pack is offered to a <see cref="MountedPacks"/> it does not belong in. The pack is
/// fine. It is the wrong pack for this game, this target or this place in the chain, and mounting it
/// anyway would read the wrong bytes.
/// </summary>
public sealed class PackMountRefusedException(string message) : Exception(message);

/// <summary>
/// The packs of one game, searched as one <see cref="IAssetSource"/>. A chain is a pack with no base,
/// then patches, each built against the one before it. Mods sit above the whole chain, later mods above
/// earlier ones. Which pack answers for an id is worked out when a pack is mounted, so a read is one
/// map lookup however many packs there are.
/// </summary>
/// <remarks>
/// Order of mounting does not change the answer. A patch mounted after a mod still lands in the chain
/// beneath it, so an app that downloads a patch late cannot undo a mod by doing so. Nothing is
/// unmounted. A mod switched off takes a restart.
/// </remarks>
public sealed class MountedPacks(CookTarget target) : IAssetSource
{
    private readonly List<AssetPack> _chain = [];
    private readonly List<AssetPack> _mods = [];
    private IReadOnlyDictionary<AssetId, AssetPack> _owner = new Dictionary<AssetId, AssetPack>();

    /// <summary>The cook target every mounted pack is for.</summary>
    public string Target { get; } = target.Name;

    /// <summary>The hash a patch must name as its base: the top of the chain, or null before <see cref="Start"/>.</summary>
    public ContentHash? ChainHash => _chain.Count == 0 ? null : _chain[^1].Hash;

    /// <summary>What every mounted asset is, with removals and mods spent.</summary>
    public AssetManifest Manifest { get; private set; } = new();

    /// <summary>
    /// Mounts <paramref name="top"/> with the packs it was built on, found by hash in
    /// <paramref name="pool"/>. A patch holds only what it changes, so it means nothing without the
    /// packs under it. The pool may hold more than the chain needs and may list its packs in any order.
    /// Throws <see cref="PackMountRefusedException"/> when a pack in the chain names a base that the
    /// pool does not hold.
    /// </summary>
    public static MountedPacks WithBases(AssetPack top, IEnumerable<AssetPack> pool)
    {
        var byHash = new Dictionary<ContentHash, AssetPack>();
        foreach (var pack in pool)
        {
            byHash[pack.Hash] = pack;
        }

        var chain = new List<AssetPack> { top };
        for (var @base = top.Base; @base is not null; @base = chain[^1].Base)
        {
            chain.Add(byHash.GetValueOrDefault(@base) ?? throw new PackMountRefusedException(
                $"A pack in this chain was built on {@base.Short} and that pack was not given."));
        }

        var packs = new MountedPacks(new CookTarget(top.Target));
        packs.Start(chain[^1]);
        for (var i = chain.Count - 2; i >= 0; i--)
        {
            packs.Patch(chain[i]);
        }

        return packs;
    }

    /// <summary>
    /// The mounted pack that answers for <paramref name="id"/>, or null when none does. A mod above the
    /// chain is the owner of an id it covers, and a patch is the owner of one it replaced.
    /// </summary>
    public AssetPack? OwnerOf(AssetId id) => _owner.GetValueOrDefault(id);

    /// <summary>Mounts the pack the chain stands on. It has no base.</summary>
    public void Start(AssetPack pack)
    {
        CheckTarget(pack);
        if (_chain.Count > 0)
        {
            throw new PackMountRefusedException(
                "The chain has already started. Use patch for a pack built against the one on top.");
        }

        if (pack.Base is not null)
        {
            throw new PackMountRefusedException(
                "This pack is a patch and the chain has nothing under it yet. Start with the pack it was built against.");
        }

        _chain.Add(pack);
        Resolve();
    }

    /// <summary>Mounts a pack built against the top of the chain.</summary>
    public void Patch(AssetPack pack)
    {
        CheckTarget(pack);
        var top = ChainHash ?? throw new PackMountRefusedException(
            "There is nothing for a patch to apply to. Start the chain first.");
        if (pack.Base is null)
        {
            throw new PackMountRefusedException(
                "This pack has no base, so it is a whole pack and not a patch. Use addMod to put it above the chain.");
        }

        if (pack.Base != top)
        {
            throw new PackMountRefusedException(
                $"This patch was built against {pack.Base.Short} and the top of the chain is {top.Short}.");
        }

        _chain.Add(pack);
        Resolve();
    }

    /// <summary>Mounts a pack above the chain. It has no base.</summary>
    public void AddMod(AssetPack pack)
    {
        CheckTarget(pack);
        if (pack.Base is not null)
        {
            throw new PackMountRefusedException(
                "This pack is a patch, built against another pack. A mod stands on its own.");
        }

        if (_mods.Any(mod => mod.Hash == pack.Hash) || _chain.Any(link => link.Hash == pack.Hash))
        {
            throw new PackMountRefusedException($"The pack {pack.Hash.Short} is already mounted.");
        }

        _mods.Add(pack);
        Resolve();
    }

    public async Task<byte[]> ReadAsync(AssetId id)
    {
        var owner = _owner.GetValueOrDefault(id) ?? throw new AssetNotFoundException(id, WhyNot());
        return await owner.ReadAsync(id).ConfigureAwait(false);
    }

    public Task<bool> ExistsAsync(AssetId id) => Task.FromResult(_owner.ContainsKey(id));

    private void CheckTarget(AssetPack pack)
    {
        if (pack.Target != Target)
        {
            throw new PackMountRefusedException($"A pack for {pack.Target} cannot join packs for {Target}.");
        }
    }

    private void Resolve()
    {
        var owner = new OrderedDictionary<AssetId, AssetPack>();
        var entries = new OrderedDictionary<AssetId, AssetEntry>();
        foreach (var pack in _chain.Concat(_mods))
        {
            foreach (var id in pack.Removed)
            {
                owner.Remove(id);
                entries.Remove(id);
            }

            foreach (var (id, entry) in pack.Manifest.Entries)
            {
                owner[id] = pack;
                entries[id] = entry;
            }
        }

        _owner = owner;
        Manifest = new AssetManifest(entries);
    }

    private string WhyNot() => _chain.Count == 0 && _mods.Count == 0
        ? "no pack is mounted"
        : $"none of the {_chain.Count + _mods.Count} mounted packs has it";
}
