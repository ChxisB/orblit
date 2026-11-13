using Avalonia;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>What the hover needs from a debug session: the last value across a wire, and a way to ask the game for it.</summary>
internal interface IWireProbe
{
    string? WireValue(int step, int port);

    void AskWire(int step, int port);
}

/// <summary>A value to draw, and the input pin it belongs to.</summary>
internal readonly record struct HoverLabel(string Text, Point At);

/// <summary>
/// Follows the pointer over the data wires while a game is stopped. It asks the game for a wire once when the
/// pointer comes onto it. The answer shows next to the input pin when it arrives.
/// </summary>
internal sealed class WireHover
{
    readonly GraphScene _scene;
    readonly IWireProbe _probe;
    GraphWire? _over;

    public WireHover(GraphScene scene, IWireProbe probe)
    {
        _scene = scene;
        _probe = probe;
    }

    /// <summary>The value of the wire under the pointer, or null when there is none or the game has not said.</summary>
    public HoverLabel? Label =>
        _over is { } wire && _scene.Find(wire.ToStep) is { } step && _probe.WireValue(wire.ToStep, wire.ToPort) is { } value
            ? new HoverLabel(value, step.PinCenter(true, wire.ToPort))
            : null;

    /// <summary>Takes the pointer, in graph coordinates, to a new place.</summary>
    public void Move(Point point)
    {
        var wire = _scene.WireAt(point) is { } found && IsData(found) ? found : (GraphWire?)null;
        if (wire == _over)
        {
            return;
        }

        _over = wire;
        Ask();
    }

    /// <summary>Asks again for the wire under the pointer, since a new stop has no value for it yet.</summary>
    public void Refresh() => Ask();

    void Ask()
    {
        if (_over is { } wire)
        {
            _probe.AskWire(wire.ToStep, wire.ToPort);
        }
    }

    bool IsData(GraphWire wire) =>
        _scene.Find(wire.ToStep) is { } step && wire.ToPort < step.Step.Inputs.Count && step.Step.Inputs[wire.ToPort].Sort == GraphPortSort.Data;
}
