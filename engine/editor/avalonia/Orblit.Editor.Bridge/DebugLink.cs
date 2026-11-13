namespace Orblit.Editor.Bridge;

/// <summary>One level of the stack of a stopped game. For a graph script the line is the id of the step.</summary>
public sealed record DebugFrame(string Source, int Line, string Function);

/// <summary>A local or a member of a stopped game, with its value as text.</summary>
public sealed record DebugValue(string Name, string Value);

/// <summary>The value that sits on one input of a step at one level of the stack, as text.</summary>
public sealed record DebugWire(int Level, int Step, int Port, string Value);

/// <summary>What a running game last said to the editor. A game that is not attached is not connected and holds nothing.</summary>
public sealed record DebugState(
    bool Connected,
    bool Stopped,
    string Reason,
    IReadOnlyList<DebugFrame> Stack,
    int Level,
    IReadOnlyList<DebugValue> Locals,
    IReadOnlyList<DebugValue> Members,
    DebugWire? Wire)
{
    public static DebugState None { get; } = new(false, false, "", [], 0, [], [], null);

    /// <exception cref="InvalidDataException">The text is not a state the engine writes.</exception>
    public static DebugState Parse(string json)
    {
        var state = GraphJson.Read<DebugState>(json);
        // The engine writes an empty object when no wire was asked for, which reads as a wire with no value.
        return state.Wire?.Value is null ? state with { Wire = null } : state;
    }
}

/// <summary>
/// The editor's end of the channel to a running game. It listens on the loopback address, and the game connects out
/// to it. The engine keeps what the game said, and this class gives it back as <see cref="State"/>.
/// </summary>
public sealed class DebugLink : IDisposable
{
    readonly EngineHost _host;
    readonly nint _link;
    string _text = "";
    bool _disposed;

    DebugLink(EngineHost host, nint link, int port)
    {
        _host = host;
        _link = link;
        Port = port;
        State = DebugState.None;
    }

    /// <summary>The port the game connects to.</summary>
    public int Port { get; }

    /// <summary>What the game last said. The same instance comes back until the game says something that changes it.</summary>
    public DebugState State { get; private set; }

    /// <exception cref="IOException">The engine could not listen.</exception>
    public static DebugLink Listen(EngineHost host)
    {
        var link = host.Construct("GraphDebugLink");
        var error = host.GraphDebugLinkListen(link, 0);
        if (error != 0)
        {
            host.Release(link);
            throw new IOException($"The engine could not listen for a game, error code {error}.");
        }
        return new DebugLink(host, link, (int)host.GraphDebugLinkGetPort(link));
    }

    /// <summary>Takes in what the game said since the last call.</summary>
    public void Poll()
    {
        _host.GraphDebugLinkPoll(Link);
        var text = _host.GraphDebugLinkGetState(Link);
        if (text != _text)
        {
            _text = text;
            State = DebugState.Parse(text);
        }
    }

    /// <summary>Sends step, next, out, continue or break to the game.</summary>
    public void Send(string command) => _host.GraphDebugLinkSendCommand(Link, command);

    public void SetBreakpoint(string source, int step, bool set) => _host.GraphDebugLinkSetBreakpoint(Link, source, step, set);

    public void RequestVariables(int level) => _host.GraphDebugLinkRequestVariables(Link, level);

    public void RequestWire(int level, int step, int port) => _host.GraphDebugLinkRequestWire(Link, level, step, port);

    nint Link
    {
        get
        {
            // A released handle is freed memory in the engine, so a late call must not reach it.
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _link;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _host.Release(_link);
        }
    }
}
