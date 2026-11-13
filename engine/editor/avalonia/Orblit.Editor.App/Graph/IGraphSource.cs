using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// What the graph canvas needs from the graph it shows: the function, and one call for each edit. The engine
/// holds the rules. An edit it refuses comes back with its reason and changes nothing. The canvas owns this
/// interface.
/// </summary>
internal interface IGraphSource
{
    /// <summary>The function on show, as the engine holds it. Null when the script has no function.</summary>
    GraphFunction? Function { get; }

    /// <summary>Raised after an edit the engine took.</summary>
    event Action? Changed;

    /// <summary>A step id the function does not use yet.</summary>
    int NextStepId();

    GraphEditResult AddStep(int id, string kind);

    GraphEditResult RemoveStep(int id);

    GraphEditResult MoveStep(int id, double x, double y);

    /// <summary>Sets a parameter from Variant text, such as <c>Vector2(1, 2)</c>.</summary>
    GraphEditResult SetStepParameter(int id, string parameter, string valueText);

    GraphEditResult SetStepPins(int id, int count);

    GraphEditResult Connect(GraphWire wire);

    GraphEditResult Disconnect(GraphWire wire);

    /// <summary>Adds the frame, or replaces the frame with the same id.</summary>
    GraphEditResult SetFrame(GraphFrame frame);

    GraphEditResult RemoveFrame(int id);
}
