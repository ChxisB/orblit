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

    /// <summary>What the engine found that is not an error, for the whole script, as of the last edit.</summary>
    IReadOnlyList<GraphWarning> Warnings { get; }

    /// <summary>Raised after an edit the engine took.</summary>
    event Action? Changed;

    /// <summary>The undo and redo of the file. Edits made inside <see cref="GraphHistory.Group"/> undo as one.</summary>
    GraphHistory History { get; }

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

    /// <summary>The steps with these ids of the function on show, the wires between them and what they name, as text for <see cref="PasteSteps"/>.</summary>
    string GetPayload(IReadOnlyList<int> ids);

    /// <summary>
    /// Adds the steps of a payload to the function on show with their upper left corner at the point. A clash
    /// with a declaration of the script comes back with the choice to make and changes nothing until it is made.
    /// </summary>
    GraphPasteResult PasteSteps(string payload, double x, double y, GraphPasteChoice choice);

    /// <summary>Moves the steps of the function on show into a new function and puts a Call step in their place. A refusal changes nothing.</summary>
    GraphFold Collapse(IReadOnlyList<int> ids);

    /// <summary>Puts the steps of the function a Call step names where the call is, and takes the call away. A refusal changes nothing.</summary>
    GraphFold Expand(int call);

    /// <summary>
    /// The steps that fit a wire dragged from the given side, matched on the text. The engine does the matching,
    /// so a type of 0 and no side list every step.
    /// </summary>
    IReadOnlyList<GraphStepEntry> FindSteps(string text, GraphDraggedSide side, int type, GraphPortSort sort);
}
