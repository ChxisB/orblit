using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Stage;

/// <summary>
/// Makes one model file out of <paramref name="placements"/> of the model at <paramref name="mesh"/>,
/// with each placement baked into the vertices, or returns null to leave those objects as they are.
/// <c>GltfBake.BakeInstancesAsync</c> in <c>Orblit.Asset</c> does the work. Reading the mesh is the
/// caller's job, so the stage never touches a file.
/// </summary>
public delegate Task<byte[]?> StaticBaker(string mesh, IReadOnlyList<Matrix4> placements);

/// <summary>How a <see cref="StaticMerge"/> bakes, hands files over, and decides what is worth merging.</summary>
public sealed record StaticMergeOptions
{
    public required StaticBaker Bake { get; init; }

    /// <summary>Hands bytes to the renderer under a name. Null is <see cref="RenderResources.Provide"/>.</summary>
    public Func<string, byte[], Task>? Provide { get; init; }

    /// <summary>Takes them back. Null is <see cref="RenderResources.Release"/>.</summary>
    public Func<string, Task<bool>>? Release { get; init; }

    /// <summary>
    /// Called when a bake lands, from the thread that finished it. Publish the scene again, because
    /// the merge only shows in a scene that has been through <see cref="StaticMerge.Replace"/> since.
    /// An error it throws is nobody's to catch, as in any task nobody awaits, and the merge stands.
    /// </summary>
    public Action? OnReady { get; init; }

    /// <summary>How many observations an object must hold still before it is merged.</summary>
    public int Settle { get; init; } = 30;

    /// <summary>How few objects are worth a merge.</summary>
    public int Minimum { get; init; } = 8;

    /// <summary>The side of a square of ground, in the scene's units.</summary>
    public double CellSize { get; init; } = 64;

    /// <summary>How many times one group may bake. See the costs on <see cref="StaticMerge"/>.</summary>
    public int MaxBakes { get; init; } = 8;

    /// <summary>The first key of the merged objects, clear of the scene's own keys.</summary>
    public long KeyBase { get; init; } = 0x4d470000;
}

/// <summary>
/// Draws many objects that never move as one object per model and square of ground.
/// A renderer pays for every object it draws, so a thousand rocks cost a thousand draws however
/// small they are. This watches a scene, and when <see cref="StaticMergeOptions.Minimum"/> or more
/// objects of one model and material in one square of ground have held still for
/// <see cref="StaticMergeOptions.Settle"/> observations, it has the caller bake them into one file and
/// swaps that file in for them. When one of them moves, hides or leaves, the group goes back to single
/// objects at once and bakes again without it.
/// Call <see cref="Observe"/> and then <see cref="Replace"/> on the same scene each time you publish
/// it. <see cref="Observe"/> changes state and starts bakes. <see cref="Replace"/> answers with the
/// scene to draw, and checks every member itself, so a scene it has not observed is still drawn
/// correctly, only without a merge it has no right to.
/// What it leaves alone: an object with a clip, morph weights, joints or a variant, one that is
/// hidden, one without a mesh file and one whose colour is not a number. The built-in cube already
/// batches natively. A model the baker declines, because it has skins or compressed buffers, is
/// declined for good and its objects stay single.
/// Costs. Each bake makes a new file. The renderer keeps what it loaded from a file until it is torn
/// down, so a group bakes at most <see cref="StaticMergeOptions.MaxBakes"/> times and then stays as
/// single objects. A merge also holds a second copy of the geometry beside the model it came from.
/// Safe to call from any thread. A bake lands on whichever thread finishes it.
/// </summary>
public sealed class StaticMerge : IAsyncDisposable
{
    // A name loads once, so names count across every instance. Two merges that numbered their own
    // would hand the renderer the same name twice.
    private static int _serial;

    private readonly StaticMergeOptions _options;
    private readonly Func<string, byte[], Task> _provide;
    private readonly Func<string, Task<bool>> _release;
    private readonly Lock _gate = new();
    private readonly Dictionary<Group, Merge> _merges = [];
    private readonly Dictionary<Group, int> _bakes = [];
    private readonly HashSet<string> _provided = [];
    private readonly Dictionary<string, Exception?> _declined = [];
    private Dictionary<long, Track> _tracks = [];
    private int _frame;
    private int _nextKey;
    private int _inFlight;
    private bool _disposed;

    public StaticMerge(StaticMergeOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(options.Settle);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Minimum, 2);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.CellSize);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxBakes, 1);
        _options = options;
        _provide = options.Provide ?? ProvideToRenderer;
        _release = options.Release ?? ReleaseFromRenderer;
    }

    /// <summary>The models the baker would not take, each with the error it threw, or null when it said no.</summary>
    public IReadOnlyDictionary<string, Exception?> Declined
    {
        get
        {
            lock (_gate)
            {
                return new Dictionary<string, Exception?>(_declined);
            }
        }
    }

    /// <summary>How many bakes have started and not yet finished. Tests wait on it.</summary>
    internal int InFlight => Volatile.Read(ref _inFlight);

    /// <summary>Notes where every object is and starts a bake for each group that has held still long enough.</summary>
    public void Observe(RenderScene scene)
    {
        List<Merge> started;
        lock (_gate)
        {
            _frame++;
            var settled = Survey(scene);
            foreach (var group in _merges.Keys.Where(group => !settled.TryGetValue(group, out var members) || members.Count < _options.Minimum).ToList())
            {
                _merges.Remove(group);
            }

            started = [.. settled.Where(entry => entry.Value.Count >= _options.Minimum).Select(entry => Consider(entry.Key, entry.Value)).OfType<Merge>()];
        }

        foreach (var merge in started)
        {
            _ = LandAsync(merge);
        }
    }

    /// <summary>
    /// <paramref name="scene"/> with each ready merge drawn in place of its members. A merge whose
    /// members are not all there, still, and alike is not drawn.
    /// </summary>
    public RenderScene Replace(RenderScene scene)
    {
        lock (_gate)
        {
            var owner = new Dictionary<long, Merge>();
            foreach (var merge in _merges.Values.Where(merge => merge.Mesh is not null))
            {
                foreach (var key in merge.Members.Keys)
                {
                    owner[key] = merge;
                }
            }

            var live = owner.Count == 0 ? [] : LiveMerges(scene, owner);
            return live.Count == 0
                ? scene
                : scene with
                {
                    Objects =
                    [
                        .. scene.Objects.Where(one => !(owner.TryGetValue(one.Key, out var merge) && live.Contains(merge))),
                        .. live.Select(ObjectOf),
                    ],
                };
        }
    }

    /// <summary>Stops merging and releases every file provided so far. A bake that lands afterwards releases its own file.</summary>
    public async ValueTask DisposeAsync()
    {
        string[] names;
        lock (_gate)
        {
            _disposed = true;
            _merges.Clear();
            names = [.. _provided];
            _provided.Clear();
        }

        foreach (var name in names)
        {
            await _release(name);
        }
    }

    private static Task ProvideToRenderer(string name, byte[] bytes)
    {
        RenderResources.Provide(name, bytes);
        return Task.CompletedTask;
    }

    private static Task<bool> ReleaseFromRenderer(string name) => Task.FromResult(RenderResources.Release(name));

    private static bool Same(Matrix4 a, Matrix4 b)
    {
        for (var i = 0; i < 16; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameMembers(SortedDictionary<long, Matrix4> a, SortedDictionary<long, Matrix4> b) =>
        a.Count == b.Count && b.All(entry => a.TryGetValue(entry.Key, out var other) && Same(other, entry.Value));

    private static bool IsStill(RenderObject one) =>
        one.Mesh is not null
        && one.Visible
        && one.Animation is null
        && one.MorphWeights is null
        && one.Joints is null
        && one.Variant is null;

    private static RenderObject ObjectOf(Merge merge) => new()
    {
        Key = merge.Key,
        Transform = Matrix4.Identity(),
        Colour = new Vector3(merge.Group.Red, merge.Group.Green, merge.Group.Blue),
        Mesh = merge.Mesh,
        Material = merge.Group.Material,
        CastShadows = merge.Group.Casts,
        ReceiveShadows = merge.Group.Receives,
        Layer = merge.Group.Layer,
    };

    // A position that is not a number has no square of ground. A colour that is not a number never
    // equals itself, so its group could never settle.
    private static bool HasPlaceAndColour(RenderObject one) =>
        double.IsFinite(one.Transform[12])
        && double.IsFinite(one.Transform[14])
        && !double.IsNaN(one.Colour.X)
        && !double.IsNaN(one.Colour.Y)
        && !double.IsNaN(one.Colour.Z);

    private Group? GroupOf(RenderObject one)
    {
        if (!IsStill(one) || !HasPlaceAndColour(one))
        {
            return null;
        }

        return new Group(
            one.Mesh!, // IsStill checked it.
            one.Material,
            one.Colour.X,
            one.Colour.Y,
            one.Colour.Z,
            one.CastShadows,
            one.ReceiveShadows,
            one.Layer,
            (long)Math.Floor(one.Transform[12] / _options.CellSize),
            (long)Math.Floor(one.Transform[14] / _options.CellSize));
    }

    private Track TrackOf(RenderObject one, Group group) =>
        _tracks.TryGetValue(one.Key, out var old) && old.Group == group && Same(old.Transform, one.Transform)
            ? old
            : new Track(one.Transform.Clone(), group, _frame);

    private bool IsHeld(Merge merge, RenderObject one) =>
        merge.Members.TryGetValue(one.Key, out var stood) && Same(stood, one.Transform) && GroupOf(one) == merge.Group;

    // The merges whose every member is in the scene, still, and alike, in the order they were met.
    private List<Merge> LiveMerges(RenderScene scene, Dictionary<long, Merge> owner)
    {
        var held = new Dictionary<Merge, int>();
        foreach (var one in scene.Objects)
        {
            if (owner.TryGetValue(one.Key, out var merge) && IsHeld(merge, one))
            {
                held[merge] = held.GetValueOrDefault(merge) + 1;
            }
        }

        return [.. held.Where(entry => entry.Value == entry.Key.Members.Count).Select(entry => entry.Key)];
    }

    // Where each object stands now, and the objects of each group that have held still long enough.
    private Dictionary<Group, SortedDictionary<long, Matrix4>> Survey(RenderScene scene)
    {
        var tracks = new Dictionary<long, Track>();
        var settled = new Dictionary<Group, SortedDictionary<long, Matrix4>>();
        foreach (var one in scene.Objects)
        {
            if (GroupOf(one) is not { } group)
            {
                continue;
            }

            var track = TrackOf(one, group);
            tracks[one.Key] = track;
            if (_frame - track.Since >= _options.Settle)
            {
                if (!settled.TryGetValue(group, out var members))
                {
                    settled[group] = members = [];
                }

                members[one.Key] = track.Transform;
            }
        }

        _tracks = tracks;
        return settled;
    }

    // Under the lock. Returns the merge to bake, or null when the group already has one for these
    // members or may not bake.
    private Merge? Consider(Group group, SortedDictionary<long, Matrix4> settled)
    {
        if (_merges.TryGetValue(group, out var current) && SameMembers(current.Members, settled))
        {
            return null;
        }

        var started = _bakes.GetValueOrDefault(group);
        if (started >= _options.MaxBakes || _declined.ContainsKey(group.Mesh))
        {
            _merges.Remove(group);
            return null;
        }

        _bakes[group] = started + 1;
        var merge = new Merge(_options.KeyBase + _nextKey++, group, settled);
        _merges[group] = merge;
        Interlocked.Increment(ref _inFlight);
        return merge;
    }

    private async Task LandAsync(Merge merge)
    {
        try
        {
            var name = await TryPrepareAsync(merge);
            if (name is not null && IsCurrent(merge))
            {
                _options.OnReady?.Invoke();
            }
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    private async Task<string?> TryPrepareAsync(Merge merge)
    {
        try
        {
            return await PrepareAsync(merge);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            lock (_gate)
            {
                _declined[merge.Group.Mesh] = error;
            }

            return null;
        }
    }

    // Bakes and provides the merge's file, and returns its name, or null when the merge is no longer
    // wanted or the baker said no.
    private async Task<string?> PrepareAsync(Merge merge)
    {
        var group = merge.Group;
        var bytes = await _options.Bake(group.Mesh, [.. merge.Members.Values]);

        // A finished bake would otherwise go on inside Observe, and so would OnReady.
        await Task.Yield();
        if (!StillWanted(merge, bytes))
        {
            return null;
        }

        var name = RenderResources.NameFor($"static-merge/{Interlocked.Increment(ref _serial) - 1}.glb");
        await _provide(name, bytes!); // StillWanted refused a null.
        if (!TryAdopt(merge, name))
        {
            await _release(name);
            return null;
        }

        return name;
    }

    private bool IsCurrent(Merge merge)
    {
        lock (_gate)
        {
            return _merges.TryGetValue(merge.Group, out var current) && ReferenceEquals(current, merge);
        }
    }

    private bool StillWanted(Merge merge, byte[]? bytes)
    {
        lock (_gate)
        {
            if (bytes is null)
            {
                _declined[merge.Group.Mesh] = null;
                return false;
            }

            return _merges.TryGetValue(merge.Group, out var current) && ReferenceEquals(current, merge);
        }
    }

    private bool TryAdopt(Merge merge, string name)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return false;
            }

            _provided.Add(name);
            merge.Mesh = name;
            return true;
        }
    }

    // What objects must share to be baked together. The square of ground is in it so a merge is
    // still small enough for the renderer to cull.
    // F2: ten values that make one key, built in one place.
    private readonly record struct Group(
        string Mesh,
        long? Material,
        double Red,
        double Green,
        double Blue,
        bool Casts,
        bool Receives,
        int Layer,
        long East,
        long South);

    // Where an object was and since when.
    private sealed record Track(Matrix4 Transform, Group Group, int Since);

    // One baked file standing in for its members.
    private sealed class Merge(long key, Group group, SortedDictionary<long, Matrix4> members)
    {
        public long Key { get; } = key;

        public Group Group { get; } = group;

        // Each member's key and where it stood when baked, in key order.
        public SortedDictionary<long, Matrix4> Members { get; } = members;

        // The resource name once the bake is ready, null while it runs.
        public string? Mesh { get; set; }
    }
}
