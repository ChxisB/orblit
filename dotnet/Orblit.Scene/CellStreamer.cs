using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Orblit.Cells;

namespace Orblit.Scene;

/// <summary>What one cell cost to bring in. The record a profile of loads is made of.</summary>
// F2: seven facts about one load, called by name.
public sealed class CellLoad(
    Cell cell,
    TimeSpan fetch,
    TimeSpan apply,
    int bytes,
    int entities,
    IReadOnlyList<string>? problems = null,
    Exception? error = null)
{
    public Cell Cell { get; } = cell;

    /// <summary>From asking for the bytes to having them.</summary>
    public TimeSpan Fetch { get; } = fetch;

    /// <summary>Decoding and applying, which is the part that happens inside a frame.</summary>
    public TimeSpan Apply { get; } = apply;

    public int Bytes { get; } = bytes;

    public int Entities { get; } = entities;

    /// <summary>
    /// What the scene reader said about the file: entities it left out and components it did not
    /// know. A cell with problems still loads.
    /// </summary>
    public IReadOnlyList<string> Problems { get; } = problems ?? [];

    /// <summary>Why the cell did not load, or null when it did.</summary>
    public Exception? Error { get; } = error;
}

/// <summary>What one <see cref="CellStreamer.Step"/> did.</summary>
public sealed class CellStep(TimeSpan spent = default, int loaded = 0, int dropped = 0)
{
    public TimeSpan Spent { get; } = spent;

    public int Loaded { get; } = loaded;

    public int Dropped { get; } = dropped;
}

/// <summary>
/// Keeps the cells of a world that are near a point in a scene, and no others.
/// <para>
/// A cell is a scene file. It is fetched as bytes, read, and put into the scene as one diff. Every
/// entity in it takes the cell as a prefix on its id (see <see cref="IdIn"/>), so two cells never
/// clash, and the diff is handed to the apply function. The caller sends that diff wherever the
/// scene is kept: the document view that draws it, the physics that gives its bodies a world, or
/// both. A body joins when its cell loads and leaves when it goes, because that is what a diff that
/// adds and removes an entity already does to them.
/// </para>
/// <para>
/// A cell file is written in world coordinates. Nothing is moved when it is put in, so it is tied to
/// its place. Entities keep the order of their file, and cells are not ordered between each other:
/// that depends on which loaded first. A scene that draws by document order should draw by depth
/// instead.
/// </para>
/// <para>
/// The removal of a cell carries ids only, so its diff is for applying and not for undoing. If
/// the apply function throws while a cell is going in, what got in is still the cell's and a later
/// drop takes it out, and the throw reaches the caller of <see cref="Step"/>.
/// </para>
/// <para>
/// Work in a frame is limited by <see cref="Budget"/>. The time is read between cells, so one cell is
/// never split: a frame can run over by the cost of one cell, and at least one is done in every
/// frame that has something to do.
/// </para>
/// <para>
/// Call <see cref="Step"/> from one thread, once a frame. A fetch can finish on any thread. A fetch
/// that fails is reported to the load handler at that moment, which is inside <see cref="Step"/>
/// when the fetch fails at once and on the finishing thread otherwise. A cell that loads is
/// reported inside <see cref="Step"/>.
/// </para>
/// </summary>
public sealed class CellStreamer
{
    private static readonly UTF8Encoding Strict = new(false, true);

    private readonly object _gate = new();
    private readonly Func<Cell, Task<byte[]?>> _fetch;
    private readonly Action<SceneDiff> _apply;
    private readonly Action<CellLoad>? _onLoad;
    private readonly PrefabSource? _prefabs;
    private readonly Func<TimeSpan> _clock;

    // The ids each loaded cell added, so it can take them out again. An empty list is a cell the
    // world has nothing in, which is loaded all the same. Only Step reads and writes it.
    private readonly Dictionary<Cell, List<string>> _loaded = [];
    private readonly HashSet<Cell> _fetching = [];
    private readonly List<Fetched> _ready = [];
    private readonly Dictionary<Cell, TimeSpan> _failedUntil = [];

    private CellStep _last = new();

    // F2: three required values and six settings, called by name.
    public CellStreamer(
        CellWindow window,
        Func<Cell, Task<byte[]?>> fetch,
        Action<SceneDiff> apply,
        Action<CellLoad>? onLoad = null,
        PrefabSource? prefabs = null,
        TimeSpan? budget = null,
        int inFlight = 2,
        TimeSpan? retryAfter = null,
        TimeSpan? fetchTimeout = null,
        Func<TimeSpan>? clock = null)
    {
        Budget = Positive(budget ?? TimeSpan.FromMilliseconds(4), nameof(budget));
        if (inFlight < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(inFlight), inFlight, "not above zero");
        }

        RetryAfter = Positive(retryAfter ?? TimeSpan.FromSeconds(5), nameof(retryAfter));
        FetchTimeout = Positive(fetchTimeout ?? TimeSpan.FromSeconds(30), nameof(fetchTimeout));
        Window = window;
        InFlight = inFlight;
        _fetch = fetch;
        _apply = apply;
        _onLoad = onLoad;
        _prefabs = prefabs;
        _clock = clock ?? Monotonic();
    }

    public CellWindow Window { get; }

    /// <summary>The most time a frame spends reading and applying cells.</summary>
    public TimeSpan Budget { get; }

    /// <summary>
    /// The most cells fetched or waiting to be applied at one time. Bounds the memory a slow
    /// frame can pile up.
    /// </summary>
    public int InFlight { get; }

    /// <summary>How long a cell that could not be read is left alone.</summary>
    public TimeSpan RetryAfter { get; }

    /// <summary>
    /// How long a fetch may take. One that never completes would hold a place in
    /// <see cref="InFlight"/> for good, so it fails and is asked for again after
    /// <see cref="RetryAfter"/>.
    /// </summary>
    public TimeSpan FetchTimeout { get; }

    /// <summary>The loaded cells, as a copy that a later <see cref="Step"/> does not disturb.</summary>
    public IReadOnlyList<Cell> Loaded => [.. _loaded.Keys];

    /// <summary>Cells asked for and not yet in the scene.</summary>
    public int Pending
    {
        get
        {
            lock (_gate)
            {
                return _fetching.Count + _ready.Count;
            }
        }
    }

    /// <summary>What the most recent <see cref="Step"/> did.</summary>
    public CellStep LastStep => _last;

    /// <summary>The id an entity written as <paramref name="id"/> in the file for <paramref name="cell"/> has in the scene.</summary>
    public static string IdIn(Cell cell, string id) =>
        string.Create(CultureInfo.InvariantCulture, $"cell_{cell.X}_{cell.Z}:{id}");

    public bool IsLoaded(Cell cell) => _loaded.ContainsKey(cell);

    /// <summary>
    /// Moves the window to (<paramref name="x"/>, <paramref name="z"/>), facing
    /// (<paramref name="facingX"/>, <paramref name="facingZ"/>): drops the cells it has left, applies
    /// the ones that have arrived and asks for the next. Call it once a frame. A
    /// <paramref name="pinned"/> cell is loaded and kept wherever it is.
    /// </summary>
    // F2: a point, the way it faces and the cells to keep. The facing and the pins are optional.
    public void Step(
        double x,
        double z,
        double facingX = 0,
        double facingZ = 0,
        IReadOnlySet<Cell>? pinned = null)
    {
        if (Window.Grid.CellOf(x, z) is not { } focus)
        {
            return;
        }

        pinned ??= FrozenSet<Cell>.Empty;
        var began = _clock();
        ForgetExpiredFailures(began);
        var plan = Window.Plan(x, z, facingX, facingZ, _loaded.Keys.ToHashSet(), pinned);

        bool Over() => _clock() - began >= Budget;

        var dropped = DropAll(plan.Drop, Over);
        var loaded = AdmitReady(plan, focus, pinned, Over);
        foreach (var cell in plan.Load)
        {
            if (Pending >= InFlight)
            {
                break;
            }

            if (!Busy(cell))
            {
                _ = AskAsync(cell);
            }
        }

        _last = new CellStep(_clock() - began, loaded, dropped);
    }

    private static TimeSpan Positive(TimeSpan value, string name) =>
        value > TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(name, value, "not above zero");

    private static Func<TimeSpan> Monotonic()
    {
        var watch = Stopwatch.StartNew();
        return () => watch.Elapsed;
    }

    private void ForgetExpiredFailures(TimeSpan now)
    {
        lock (_gate)
        {
            foreach (var cell in _failedUntil.Where(one => one.Value <= now).Select(one => one.Key).ToList())
            {
                _failedUntil.Remove(cell);
            }
        }
    }

    private int DropAll(IReadOnlyList<Cell> cells, Func<bool> over)
    {
        var dropped = 0;
        foreach (var cell in cells)
        {
            if (over())
            {
                break;
            }

            Drop(cell);
            dropped++;
        }

        return dropped;
    }

    /// <summary>
    /// Puts in the cells that have arrived, nearest first, while there is time. Counts the ones that
    /// went in, not the ones that failed to read.
    /// </summary>
    private int AdmitReady(CellPlan plan, Cell focus, IReadOnlySet<Cell> pinned, Func<bool> over)
    {
        var wanted = new Dictionary<Cell, int>();
        for (var i = 0; i < plan.Load.Count; i++)
        {
            wanted[plan.Load[i]] = i;
        }

        Fetched[] arrived;
        lock (_gate)
        {
            _ready.RemoveAll(one => !Window.Keeps(one.Cell, focus) && !pinned.Contains(one.Cell));

            // OrderBy is stable, so cells with the same rank go in the order they arrived.
            arrived = [.. _ready.OrderBy(one => wanted.GetValueOrDefault(one.Cell, plan.Load.Count))];
            _ready.Clear();
            _ready.AddRange(arrived);
        }

        var loaded = 0;
        foreach (var one in arrived)
        {
            if (over())
            {
                break;
            }

            lock (_gate)
            {
                _ready.RemoveAt(0);
            }

            if (Admit(one))
            {
                loaded++;
            }
        }

        return loaded;
    }

    // Includes the cells loaded in this very step, which the plan was made before and still lists.
    private bool Busy(Cell cell)
    {
        if (_loaded.ContainsKey(cell))
        {
            return true;
        }

        lock (_gate)
        {
            return _fetching.Contains(cell) || _failedUntil.ContainsKey(cell) || _ready.Any(one => one.Cell == cell);
        }
    }

    // The fetcher is the caller's code and may throw anything. A cell that cannot be read is not a
    // reason to stop the world loading.
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A fetcher can fail in any way.")]
    private async Task AskAsync(Cell cell)
    {
        lock (_gate)
        {
            _fetching.Add(cell);
        }

        var asked = _clock();
        try
        {
            var bytes = await _fetch(cell).WaitAsync(FetchTimeout).ConfigureAwait(false);
            lock (_gate)
            {
                _ready.Add(new Fetched(cell, bytes, _clock() - asked));
                _fetching.Remove(cell);
            }
        }
        catch (Exception error)
        {
            Fail(cell, error, _clock() - asked);
            lock (_gate)
            {
                _fetching.Remove(cell);
            }
        }
    }

    private bool Admit(Fetched fetched)
    {
        var began = _clock();
        var cell = fetched.Cell;
        SceneLoad read;
        try
        {
            read = Read(fetched.Bytes);
        }
        catch (Exception error) when (error is SceneFormatException or PrefabException or DecoderFallbackException
            or IOException or UnauthorizedAccessException)
        {
            // A file that is not a scene reads as a cell that failed, and so does a prefab that
            // cannot be read. A bug in the caller's code, such as a bad argument, is not one of these
            // and reaches the caller of Step.
            Fail(cell, error, fetched.Took);
            return false;
        }

        var document = read.Document;
        var ids = document.Entities.Select(entity => IdIn(cell, entity.Id)).ToList();
        var adds = Additions(cell, document);
        try
        {
            if (adds.Count > 0)
            {
                _apply(new SceneDiff(adds));
            }
        }
        finally
        {
            // Whatever got in is the cell's, so a drop takes it out again.
            _loaded[cell] = ids;
        }

        _onLoad?.Invoke(new CellLoad(
            cell,
            fetched.Took,
            _clock() - began,
            fetched.Bytes?.Length ?? 0,
            ids.Count,
            read.Problems));
        return true;
    }

    private SceneLoad Read(byte[]? bytes)
    {
        if (bytes is null)
        {
            return new SceneLoad(new SceneDocument());
        }

        var read = SceneDocument.Decode(Strict.GetString(bytes));
        if (_prefabs is null)
        {
            return read;
        }

        var opened = Instances.Expand(read.Document, _prefabs);
        return new SceneLoad(opened.Document, [.. read.Problems, .. opened.Problems]);
    }

    private static List<SceneOp> Additions(Cell cell, SceneDocument document)
    {
        var adds = new List<SceneOp>();
        string? previous = null;
        foreach (var entity in document.Entities)
        {
            var id = IdIn(cell, entity.Id);
            var parent = entity.Parent is { } above ? IdIn(cell, above) : null;
            adds.Add(new AddEntity(entity.CopyWith(id: id, parent: parent).ToJson(), previous));
            previous = id;
        }

        return adds;
    }

    private void Drop(Cell cell)
    {
        // The plan lists only loaded cells, so the ids are there.
        var ids = _loaded[cell];
        _loaded.Remove(cell);
        if (ids.Count == 0)
        {
            return;
        }

        _apply(new SceneDiff([.. ids.Select(id => new RemoveEntity(new Dictionary<string, object?> { ["id"] = id }))]));
    }

    private void Fail(Cell cell, Exception error, TimeSpan took)
    {
        lock (_gate)
        {
            _failedUntil[cell] = _clock() + RetryAfter;
        }

        _onLoad?.Invoke(new CellLoad(cell, took, TimeSpan.Zero, 0, 0, error: error));
    }

    private sealed class Fetched(Cell cell, byte[]? bytes, TimeSpan took)
    {
        public Cell Cell { get; } = cell;

        public byte[]? Bytes { get; } = bytes;

        public TimeSpan Took { get; } = took;
    }
}
