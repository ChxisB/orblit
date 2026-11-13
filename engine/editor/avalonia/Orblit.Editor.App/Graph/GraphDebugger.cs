using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>What the debugger needs from the running game. The game is far away, so each call is a message.</summary>
internal interface IDebugChannel
{
    /// <summary>The last thing the game said. It is the same instance until the game says something new.</summary>
    DebugState State { get; }

    /// <summary>Takes in what the game has sent since the last poll.</summary>
    void Poll();

    /// <summary>Sends one of the names of <see cref="DebugCommand"/>, in lower case.</summary>
    void Send(string command);

    void SetBreakpoint(string source, int step, bool set);

    void RequestVariables(int level);

    void RequestWire(int level, int step, int port);
}

public enum DebugCommand
{
    Step,
    Next,
    Out,
    Continue,
    Break,
}

/// <summary>A step of a graph script the game stopped on.</summary>
internal readonly record struct DebugPlace(string Script, string Function, int Step);

/// <summary>
/// The editor's side of a debug session. It follows what the game says, forwards breakpoints while a game is
/// there, and sends the commands that suit the state. A command that does not suit it is dropped, since the
/// state a person saw can be a poll behind.
/// </summary>
internal sealed class GraphDebugger : IDisposable, IWireProbe
{
    readonly IDebugChannel _channel;
    readonly GraphBreakpoints _breakpoints;
    DebugState _seen = DebugState.None;

    public GraphDebugger(IDebugChannel channel, GraphBreakpoints breakpoints)
    {
        _channel = channel;
        _breakpoints = breakpoints;
        _breakpoints.Changed += Forward;
    }

    /// <summary>Raised when a poll brought a new state.</summary>
    public event Action? Changed;

    public DebugState State => _channel.State;

    public bool CanResume => State.Stopped;

    public bool CanPause => State.Connected && !State.Stopped;

    /// <summary>The graph step of the selected level of the stack, or null when the game runs or the level is not a graph step.</summary>
    public DebugPlace? Current
    {
        get
        {
            var state = State;
            if (!state.Stopped || state.Level < 0 || state.Level >= state.Stack.Count)
            {
                return null;
            }

            var frame = state.Stack[state.Level];
            return GraphBreakpoints.Split(frame.Source) is var (script, function) ? new DebugPlace(script, function, frame.Line) : null;
        }
    }

    public void Poll()
    {
        _channel.Poll();
        if (!ReferenceEquals(State, _seen))
        {
            _seen = State;
            Changed?.Invoke();
        }
    }

    public void Send(DebugCommand command)
    {
        if (command == DebugCommand.Break ? CanPause : CanResume)
        {
            _channel.Send(command.ToString().ToLowerInvariant());
        }
    }

    public void SelectLevel(int level)
    {
        if (State.Stopped && level >= 0 && level < State.Stack.Count)
        {
            _channel.RequestVariables(level);
        }
    }

    public void AskWire(int step, int port)
    {
        if (State.Stopped)
        {
            _channel.RequestWire(State.Level, step, port);
        }
    }

    /// <summary>The value the game last saw cross the wire into this input, or null when it has not said.</summary>
    public string? WireValue(int step, int port) =>
        State.Wire is { } wire && wire.Level == State.Level && wire.Step == step && wire.Port == port ? wire.Value : null;

    public void Dispose() => _breakpoints.Changed -= Forward;

    void Forward(string source, int step, bool set)
    {
        if (State.Connected)
        {
            _channel.SetBreakpoint(source, step, set);
        }
    }
}
