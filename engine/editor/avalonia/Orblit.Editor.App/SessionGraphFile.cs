using Orblit.Editor.App.Shell;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>
/// A graph script open in the engine, shown through its first function. The canvas works on one function, and
/// a script with none shows an empty canvas that refuses every edit.
/// </summary>
internal sealed class SessionGraphFile(GraphScriptSession session) : IGraphFile
{
    static readonly GraphEditResult s_noFunction = new(33, "The script has no function.");

    public GraphFunction? Function => session.Document.Functions.Count > 0 ? session.Document.Functions[0] : null;

    public event Action? Changed
    {
        add => session.Changed += value;
        remove => session.Changed -= value;
    }

    public int NextStepId() => Function is { } function ? session.GetNextStepId(function.Name) : 1;

    public GraphEditResult AddStep(int id, string kind) => InFunction(name => session.AddStep(name, id, kind));

    public GraphEditResult RemoveStep(int id) => InFunction(name => session.RemoveStep(name, id));

    public GraphEditResult MoveStep(int id, double x, double y) => InFunction(name => session.MoveStep(name, id, x, y));

    public GraphEditResult SetStepParameter(int id, string parameter, string valueText) => InFunction(name => session.SetStepParameter(name, id, parameter, valueText));

    public GraphEditResult SetStepPins(int id, int count) => InFunction(name => session.SetStepPins(name, id, count));

    public GraphEditResult Connect(GraphWire wire) => InFunction(name => session.Connect(name, wire));

    public GraphEditResult Disconnect(GraphWire wire) => InFunction(name => session.Disconnect(name, wire));

    public GraphEditResult SetFrame(GraphFrame frame) => InFunction(name => session.SetFrame(name, frame));

    public GraphEditResult RemoveFrame(int id) => InFunction(name => session.RemoveFrame(name, id));

    public IReadOnlyList<GraphStepEntry> FindSteps(string text, GraphDraggedSide side, int type, GraphPortSort sort) => session.FindSteps(text, side, type, sort);

    public void Save() => session.Save();

    public void Dispose() => session.Dispose();

    GraphEditResult InFunction(Func<string, GraphEditResult> edit) => Function is { } function ? edit(function.Name) : s_noFunction;
}
