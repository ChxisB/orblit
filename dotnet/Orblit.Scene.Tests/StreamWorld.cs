using System.Collections.Concurrent;
using System.Text;
using Orblit.Cells;

namespace Orblit.Scene.Tests;

/// <summary>A scene that cells stream into, files for the cells to read, and a clock the test sets.</summary>
internal sealed class StreamWorld
{
    /// <summary>The point stands in the middle of cell (0, 0). Cells are ten across.</summary>
    public const double Middle = 5.0;

    public static readonly Grid Ground = new(10);

    private readonly ConcurrentQueue<CellLoad> _loads = new();
    private readonly List<Cell> _asked = [];
    private readonly Dictionary<Cell, TaskCompletionSource<byte[]?>> _held = [];

    public TimeSpan Now { get; set; }

    public SceneDocument Scene { get; private set; } = new();

    /// <summary>What applying a diff adds to the clock.</summary>
    public TimeSpan Cost { get; set; }

    /// <summary>What asking for a cell adds to the clock.</summary>
    public TimeSpan FetchCost { get; set; }

    public bool Explode { get; set; }

    /// <summary>What each cell holds: bytes, an exception to throw, or nothing.</summary>
    public Dictionary<Cell, object?> Files { get; } = [];

    public IReadOnlyList<Cell> Asked => _asked;

    public IReadOnlyList<CellLoad> Loads => [.. _loads];

    public static byte[] File(params SceneEntity[] entities) =>
        Encoding.UTF8.GetBytes(new SceneDocument(entities: entities).Encode());

    public static byte[] Tree(string name) => File(new SceneEntity("root", name), new SceneEntity("leaf", name, "root"));

    /// <summary>The fetch for <paramref name="cell"/> waits until the test answers it. One never answered is a request lost on a network.</summary>
    public TaskCompletionSource<byte[]?> Hold(Cell cell) => _held[cell] = new TaskCompletionSource<byte[]?>();

    public CellStreamer Streamer(
        int radius = 1,
        int inFlight = 2,
        TimeSpan? budget = null,
        TimeSpan? fetchTimeout = null,
        PrefabSource? prefabs = null) => new(
        new CellWindow(Ground, radius),
        Fetch,
        Apply,
        _loads.Enqueue,
        prefabs,
        budget,
        inFlight,
        TimeSpan.FromSeconds(5),
        fetchTimeout,
        () => Now);

    /// <summary>Steps until nothing is waiting, as a game would over a few frames.</summary>
    public static void Settle(CellStreamer streamer, double x = Middle)
    {
        for (var i = 0; i < 40; i++)
        {
            streamer.Step(x, Middle);
        }
    }

    private Task<byte[]?> Fetch(Cell cell)
    {
        _asked.Add(cell);
        Now += FetchCost;
        if (_held.TryGetValue(cell, out var held))
        {
            return held.Task;
        }

        var found = Files.GetValueOrDefault(cell);
        return found is Exception thrown
            ? Task.FromException<byte[]?>(thrown)
            : Task.FromResult((byte[]?)found);
    }

    private void Apply(SceneDiff diff)
    {
        Now += Cost;
        Scene = diff.ApplyTo(Scene);
        if (Explode)
        {
            throw new InvalidOperationException("a shape the physics will not take");
        }
    }
}
