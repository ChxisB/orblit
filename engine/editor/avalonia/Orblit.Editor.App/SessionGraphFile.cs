using Orblit.Editor.App.Shell;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>
/// A graph script open in the engine, shown through one function at a time: the first until another is opened. The
/// canvas works on one function, and a script with none shows an empty canvas that refuses every edit.
/// </summary>
internal sealed class SessionGraphFile(GraphScriptSession session) : IGraphFile
{
    static readonly GraphEditResult s_noFunction = new(33, "The script has no function.");
    string? _open;

    public GraphDocument Document => session.Document;

    // A function that was removed or renamed away from the open name falls back to the first one.
    public GraphFunction? Function => session.Document.Functions.FirstOrDefault(function => function.Name == _open) ?? session.Document.Functions.FirstOrDefault();

    public void OpenFunction(string name) => _open = name;

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

    public GraphEditResult AddVariable(string name, int type) => session.AddVariable(name, type);

    public GraphEditResult RemoveVariable(string name) => session.RemoveVariable(name);

    public GraphEditResult RenameVariable(string name, string newName) => session.RenameVariable(name, newName);

    public GraphEditResult SetVariableType(string name, int type) => session.SetVariableType(name, type);

    public GraphEditResult SetVariableDefault(string name, string valueText) => session.SetVariableDefault(name, valueText);

    public GraphEditResult SetVariableExported(string name, bool exported) => session.SetVariableExported(name, exported);

    public GraphEditResult SetVariableInfo(string name, string category, string description) => session.SetVariableInfo(name, category, description);

    public GraphEditResult AddSignal(string name) => session.AddSignal(name);

    public GraphEditResult RemoveSignal(string name) => session.RemoveSignal(name);

    public GraphEditResult RenameSignal(string name, string newName) => session.RenameSignal(name, newName);

    public GraphEditResult SetSignalArguments(string name, IReadOnlyList<GraphTyped> arguments) => session.SetSignalArguments(name, arguments);

    // shortcut: the function and its Entry step are two engine calls, so a refused step leaves a function with no Entry. Group them when the history of task 12 lands.
    public GraphEditResult AddFunction(string name)
    {
        var added = session.AddFunction(name);
        return added.Ok ? session.AddStep(name, 1, "entry") : added;
    }

    public GraphEditResult RemoveFunction(string name) => session.RemoveFunction(name);

    public GraphEditResult RenameFunction(string name, string newName)
    {
        var wasOpen = Function?.Name == name;
        var result = session.RenameFunction(name, newName);
        if (result.Ok && wasOpen)
        {
            _open = newName;
        }

        return result;
    }

    public void Save() => session.Save();

    public void Dispose() => session.Dispose();

    GraphEditResult InFunction(Func<string, GraphEditResult> edit) => Function is { } function ? edit(function.Name) : s_noFunction;
}
