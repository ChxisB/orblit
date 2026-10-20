using Orblit.VectorMath;
using static Orblit.Terrain.ScatterHash;

namespace Orblit.Terrain;

/// <summary>
/// What one <see cref="ScatterLayer"/> put down in one region: a transform and a colour for each,
/// in the flat buffers a renderer draws many copies from.
/// Made again, never changed. A region whose ground moves gets a new group with a new
/// <see cref="Revision"/>, so a renderer that remembers the revision it drew knows to take the
/// buffers again and otherwise leaves them where they are.
/// </summary>
public sealed class ScatterGroup
{
    private static long _lastRevision;

    // F2: seven values that belong together, built in one place and always called by name.
    internal ScatterGroup(RegionKey key, int layer, int id, float[] transforms, float[] colours, Vector3 minimum, Vector3 maximum)
    {
        Key = key;
        Layer = layer;
        Id = id;
        Transforms = transforms;
        Colours = colours;
        Minimum = minimum;
        Maximum = maximum;
        Revision = Interlocked.Increment(ref _lastRevision);
    }

    /// <summary>The region it stands in.</summary>
    public RegionKey Key { get; }

    /// <summary>Which of the terrain's <see cref="Terrain.Scatter"/> layers it is, by index.</summary>
    public int Layer { get; }

    /// <summary>
    /// A number for this region and layer, the same for as long as both are there, and never shared
    /// with another group of the same placer. What a renderer keeps its buffers against.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Sixteen floats each, column-major, in world space. For a block, each takes the cube from -1
    /// to 1 to the box the block fills. For a model, the model's own space to where it stands.
    /// </summary>
    public float[] Transforms { get; }

    /// <summary>Three floats each, linear RGB.</summary>
    public float[] Colours { get; }

    /// <summary>The low corner of a box around every one of them, in world space.</summary>
    public Vector3 Minimum { get; }

    /// <summary>The high corner of a box around every one of them, in world space.</summary>
    public Vector3 Maximum { get; }

    /// <summary>Never the same for two groups.</summary>
    public long Revision { get; }

    /// <summary>How many there are.</summary>
    public int Count => Transforms.Length / 16;
}

/// <summary>
/// Where a terrain's <see cref="Terrain.Scatter"/> layers put things, kept region by region and
/// worked out again only where something changed.
/// Placement is a pattern laid over the whole world, not over each region. The world is cut into
/// squares one per <see cref="ScatterLayer.Density"/>, each square has one spot in it chosen by the
/// layer's seed and the square's place, and the ground there decides whether anything stands on
/// it. So the same ground scatters the same every time, on every machine and whatever size its
/// regions are, and an edit in one region cannot move a stone in the next.
/// Nothing is put where <see cref="Terrain.HeightAt"/> has no height, over a hole or off the edge,
/// and a layer's density is thinned by how much of the ground is its
/// <see cref="ScatterLayer.Sets"/>, so painting scree over a meadow takes the grass away with it.
/// </summary>
public sealed class ScatterPlacer
{
    private static readonly (int X, int Z)[] Around =
    [
        (-1, -1),
        (0, -1),
        (1, -1),
        (-1, 0),
        (1, 0),
        (-1, 1),
        (0, 1),
        (1, 1),
    ];

    private readonly Dictionary<RegionKey, Placed> _placed = [];
    private readonly Dictionary<(RegionKey Key, int Layer), int> _ids = [];
    private int _nextId;
    private List<ScatterLayer> _layers = [];
    private (long Steep, long Flat, double Slope, double HeightFalloff)? _autoCover;
    private IReadOnlyList<ScatterGroup> _groups = [];

    /// <summary>Moves whenever any group is placed again, added or taken away.</summary>
    public long Revision { get; private set; }

    /// <summary>
    /// Every group, region by region along x and then z, and layer by layer within a region. Empty
    /// groups are included: they are how a renderer learns a layer has nothing left in a region.
    /// </summary>
    public IReadOnlyList<ScatterGroup> Groups => _groups;

    /// <summary>
    /// The layers as they were at the last <see cref="Update"/>, which a group's
    /// <see cref="ScatterGroup.Layer"/> counts into. Kept here rather than read from the terrain,
    /// so the rules a group is drawn by are the ones it was placed by even when the terrain's have
    /// changed since.
    /// </summary>
    public IReadOnlyList<ScatterLayer> Layers => _layers;

    /// <summary>How many things are placed in all.</summary>
    public int Count => _groups.Sum(group => group.Count);

    /// <summary>The groups in the region at <paramref name="key"/>, one per layer, or none.</summary>
    public IReadOnlyList<ScatterGroup> GroupsIn(RegionKey key) =>
        _placed.TryGetValue(key, out var placed) ? placed.Groups : [];

    /// <summary>
    /// Brings the placement up to date with <paramref name="terrain"/>. Answers whether anything
    /// changed.
    /// A region is placed again when its own revision has moved, or when a neighbour has changed
    /// along the edge they share, since the heights, slope and cover near an edge are read from
    /// both sides of it, or when a neighbour has come or gone. A layer is placed again everywhere
    /// when its rules change, and only that layer. Otherwise nothing is done, so asking every
    /// frame costs a few comparisons a region.
    /// </summary>
    public bool Update(Terrain terrain)
    {
        var auto = terrain.AutoCover;
        var autoCover = (auto.Steep, auto.Flat, auto.Slope, auto.HeightFalloff);
        var allRules = autoCover != _autoCover;
        var changed = ForgetGone(terrain);
        foreach (var region in terrain.Regions)
        {
            changed |= Refresh(terrain, region, allRules);
        }

        _layers = [.. terrain.Scatter];
        _autoCover = autoCover;
        if (changed)
        {
            _groups = [.. _placed.Keys.OrderBy(key => key.Z).ThenBy(key => key.X).SelectMany(key => _placed[key].Groups)];
            Revision++;
        }

        return changed;
    }

    /// <summary>Forgets everything placed, so the next <see cref="Update"/> places it all again.</summary>
    public void Clear()
    {
        _placed.Clear();
        _ids.Clear();
        _layers = [];
        _autoCover = null;
        _groups = [];
        Revision++;
    }

    // A fingerprint of the two rows of the region nearest the side facing (towardsX, towardsZ), all
    // three maps, which is everything a neighbour on that side reads from it while placing.
    private static uint EdgePrint(TerrainRegion region, int towardsX, int towardsZ)
    {
        var (iFrom, iTo) = Rows(region.Size, towardsX);
        var (jFrom, jTo) = Rows(region.Size, towardsZ);
        var print = 0x2545F491u;
        for (var j = jFrom; j < jTo; j++)
        {
            for (var i = iFrom; i < iTo; i++)
            {
                print = Stir(print, region, (j * region.Size) + i);
            }
        }

        return print;
    }

    // Along an axis, the rows at that side: the last two towards +, the first two towards -, all
    // of them where the side is a corner's.
    private static (int From, int To) Rows(int size, int towards) => towards switch
    {
        1 => (size - 2, size),
        -1 => (0, 2),
        _ => (0, size),
    };

    private static uint Stir(uint print, TerrainRegion region, int at)
    {
        var stirred = Mix(print ^ BitConverter.SingleToUInt32Bits(region.Heights[at]));
        stirred = Mix(stirred ^ region.Cover[at]);
        var colour = region.Colour;
        return Mix(
            stirred ^
            ((uint)colour[at * 4] << 24) ^
            ((uint)colour[(at * 4) + 1] << 16) ^
            ((uint)colour[(at * 4) + 2] << 8) ^
            colour[(at * 4) + 3]);
    }

    private static (long Revision, uint Print)[] Edges(Terrain terrain, RegionKey key) =>
    [
        .. Around.Select(step => terrain.RegionAt(new RegionKey(key.X + step.X, key.Z + step.Z)) is { } neighbour
            ? (neighbour.Revision, EdgePrint(neighbour, -step.X, -step.Z))
            : (-1L, 0u)),
    ];

    // This also refreshes the edge revisions it finds unchanged in a neighbour's fingerprint, so
    // the next frame compares one number instead of hashing the edge again.
    private static bool GroundMoved(Terrain terrain, TerrainRegion region, Placed? placed)
    {
        if (placed is null || region.Revision != placed.Revision)
        {
            return true;
        }

        for (var n = 0; n < Around.Length; n++)
        {
            if (EdgeMoved(terrain, region.Key, placed.Edges, n))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EdgeMoved(Terrain terrain, RegionKey key, (long Revision, uint Print)[] edges, int n)
    {
        var (dx, dz) = Around[n];
        var neighbour = terrain.RegionAt(new RegionKey(key.X + dx, key.Z + dz));
        var revision = neighbour?.Revision ?? -1;
        if (revision == edges[n].Revision)
        {
            return false;
        }

        // The neighbour changed somewhere. Only its edge matters here.
        var print = neighbour is null ? 0u : EdgePrint(neighbour, -dx, -dz);
        if (print != edges[n].Print)
        {
            return true;
        }

        edges[n] = (revision, print);
        return false;
    }

    private bool ForgetGone(Terrain terrain)
    {
        var gone = _placed.Keys.Where(key => terrain.RegionAt(key) is null).ToList();
        foreach (var key in gone)
        {
            _placed.Remove(key);
            ForgetIds(id => id.Key == key);
        }

        return gone.Count > 0;
    }

    private void ForgetIds(Func<(RegionKey Key, int Layer), bool> match)
    {
        foreach (var id in _ids.Keys.Where(match).ToList())
        {
            _ids.Remove(id);
        }
    }

    private int IdFor(RegionKey key, int layer)
    {
        if (!_ids.TryGetValue((key, layer), out var id))
        {
            id = _nextId++;
            _ids[(key, layer)] = id;
        }

        return id;
    }

    // The old group of this layer, when the rules and the ground it was placed on are the ones now.
    private ScatterGroup? Kept(Placed? reusable, int layer, List<ScatterLayer> layers)
    {
        if (reusable is null || layer >= reusable.Groups.Count || layer >= _layers.Count || _layers[layer] != layers[layer])
        {
            return null;
        }

        return reusable.Groups[layer];
    }

    // Places again every layer of the region that needs it. Answers whether any was.
    private bool Refresh(Terrain terrain, TerrainRegion region, bool allRules)
    {
        var key = region.Key;
        var layers = terrain.Scatter;
        _placed.TryGetValue(key, out var placed);
        var ground = GroundMoved(terrain, region, placed);
        var groups = new List<ScatterGroup>();
        var changed = false;
        for (var layer = 0; layer < layers.Count; layer++)
        {
            var kept = Kept(ground || allRules ? null : placed, layer, layers);
            if (kept is null)
            {
                groups.Add(new Planting(terrain, layers[layer], region).Place(layer, IdFor(key, layer)));
                changed = true;
            }
            else
            {
                groups.Add(kept);
            }
        }

        changed |= DropSurplus(key, placed, layers.Count);
        _placed[key] = new Placed(region.Revision, ground || placed is null ? Edges(terrain, key) : placed.Edges, groups);
        return changed;
    }

    private bool DropSurplus(RegionKey key, Placed? placed, int layerCount)
    {
        if (placed is null || placed.Groups.Count <= layerCount)
        {
            return false;
        }

        ForgetIds(id => id.Key == key && id.Layer >= layerCount);
        return true;
    }
}

/// <summary>
/// What a region was placed against: its own revision, and for each of the eight around it a
/// revision and a fingerprint of the edge it shares.
/// </summary>
internal sealed class Placed(long revision, (long Revision, uint Print)[] edges, IReadOnlyList<ScatterGroup> groups)
{
    public long Revision { get; } = revision;

    public (long Revision, uint Print)[] Edges { get; } = edges;

    public IReadOnlyList<ScatterGroup> Groups { get; } = groups;
}
