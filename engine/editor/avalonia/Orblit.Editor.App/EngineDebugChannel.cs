using Orblit.Editor.App.Graph;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>
/// The debug channel over the engine's link. The link opens when a game is about to start, so an editor that never
/// plays holds no port, and the channel says nothing until then.
/// </summary>
internal sealed class EngineDebugChannel(EngineHost host) : IDebugChannel, IDisposable
{
    DebugLink? _link;

    public DebugState State => _link?.State ?? DebugState.None;

    /// <summary>Opens the link when it is not open yet. Returns the port a game connects to.</summary>
    /// <exception cref="IOException">The engine could not listen.</exception>
    public int Listen() => (_link ??= DebugLink.Listen(host)).Port;

    public void Poll() => _link?.Poll();

    public void Send(string command) => _link?.Send(command);

    public void SetBreakpoint(string source, int step, bool set) => _link?.SetBreakpoint(source, step, set);

    public void RequestVariables(int level) => _link?.RequestVariables(level);

    public void RequestWire(int level, int step, int port) => _link?.RequestWire(level, step, port);

    public void Dispose() => _link?.Dispose();
}
