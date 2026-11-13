using Orblit.Editor.App.Graph;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

/// <summary>
/// A channel that replays what a game said. A test sets <see cref="Next"/>, and the next poll takes it in. The same
/// state instance stays until then, as the engine's link does, and every call the editor made is logged.
/// </summary>
internal sealed class FakeDebugChannel : IDebugChannel
{
    public DebugState State { get; private set; } = DebugState.None;

    /// <summary>What the game says at the next poll. Null says nothing.</summary>
    public DebugState? Next { get; set; }

    public int Polls { get; private set; }

    public List<string> Sent { get; } = [];

    public static DebugState Running() => DebugState.None with { Connected = true };

    public static DebugState Stopped(string reason, params DebugFrame[] stack) =>
        Running() with { Stopped = true, Reason = reason, Stack = stack };

    public static DebugFrame Frame(string script, string function, int step) => new($"{script}#{function}", step, function);

    public void Poll()
    {
        Polls++;
        if (Next is { } next)
        {
            State = next;
            Next = null;
        }
    }

    public void Send(string command) => Sent.Add(command);

    public void SetBreakpoint(string source, int step, bool set) => Sent.Add($"breakpoint {source} {step} {set}");

    public void RequestVariables(int level) => Sent.Add($"variables {level}");

    public void RequestWire(int level, int step, int port) => Sent.Add($"wire {level} {step} {port}");
}
