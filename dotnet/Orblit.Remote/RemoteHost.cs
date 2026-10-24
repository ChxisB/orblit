namespace Orblit.Remote;

/// <summary>
/// The parts of a running game the surface can reach. Each is optional, and a verb that needs a
/// part the game did not give answers <see cref="RemoteException.Unavailable"/>.
/// </summary>
/// <remarks>
/// A callback that runs to the end on the game's thread is not covered by <see cref="Timeout"/>,
/// so <see cref="Perf"/>, <see cref="Draws"/> and <see cref="Material"/> must be quick.
/// </remarks>
public sealed record RemoteHost
{
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(10);

    public IWorldInspector? World { get; init; }

    public TimeGate? Gate { get; init; }

    public LogTail? Log { get; init; }

    /// <summary>The host's phase times, as JSON.</summary>
    public Func<IReadOnlyDictionary<string, object?>>? Perf { get; init; }

    /// <summary>Passes one input event, as JSON, to the game.</summary>
    public Func<IReadOnlyDictionary<string, object?>, Task>? Input { get; init; }

    /// <summary>The render view as PNG bytes.</summary>
    public Func<Task<byte[]>>? Screenshot { get; init; }

    /// <summary>
    /// The last frame's passes in the order they ran, as rows of <c>name</c>, <c>milliseconds</c>
    /// and <c>draws</c>. What the renderer reports.
    /// </summary>
    public Func<Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>>>? Passes { get; init; }

    /// <summary>
    /// What the scene submitted to draw, one row each. A row carries a text <c>id</c> that is
    /// unique among the rows. Culling and batching come after the scene, so this is not what the
    /// renderer drew. <see cref="Passes"/> is the check on it.
    /// </summary>
    public Func<IReadOnlyList<IReadOnlyDictionary<string, object?>>>? Draws { get; init; }

    /// <summary>One material by its key, or null for a key the scene does not hold.</summary>
    public Func<long, IReadOnlyDictionary<string, object?>?>? Material { get; init; }

    /// <summary>
    /// Further state kept beside the entities in a snapshot, by name, as JSON. The physics
    /// world's own snapshot is native memory with no JSON form, so a host puts what it wants
    /// compared here, such as contacts or stats.
    /// </summary>
    public IReadOnlyDictionary<string, Func<IReadOnlyDictionary<string, object?>>> Extras { get; init; } =
        new Dictionary<string, Func<IReadOnlyDictionary<string, object?>>>();

    /// <summary>
    /// Refuses <c>entity.set</c> and <c>input.send</c>. Time control and snapshots stay, because
    /// they change no value by themselves.
    /// </summary>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// How long a verb waits on the game before it answers <see cref="RemoteException.TimedOut"/>:
    /// for the next stepped frame, the input sink, the screenshot and <see cref="Passes"/>.
    /// Throws <see cref="ArgumentOutOfRangeException"/> unless it is above zero.
    /// </summary>
    public TimeSpan Timeout
    {
        get => _timeout;
        init => _timeout = value > TimeSpan.Zero
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "The timeout is not above 0.");
    }
}
