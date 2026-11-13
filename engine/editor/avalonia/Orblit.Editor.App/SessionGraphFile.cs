using Orblit.Editor.App.Graph;
using Orblit.Editor.App.Shell;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>
/// A graph script open in the engine, shown through one function at a time: the first until another is opened. The
/// canvas works on one function, and a script with none shows an empty canvas that refuses every edit.
/// </summary>
internal sealed class SessionGraphFile : IGraphFile
{
    static readonly GraphEditResult s_noFunction = new(33, "The script has no function.");
    readonly GraphScriptSession _session;
    string? _open;

    // The history is made here and not on first use, so an edit that comes before anything asks for it is still an entry.
    public SessionGraphFile(GraphScriptSession session)
    {
        _session = session;
        History = new GraphHistory(this);
    }

    public GraphDocument Document => _session.Document;

    public GraphHistory History { get; }

    public string GetSnapshot() => _session.GetSnapshot();

    public GraphEditResult Restore(string snapshot) => _session.Restore(snapshot);

    public string GetPayload(IReadOnlyList<int> ids) => Function is { } function ? _session.GetPayload(function.Name, ids) : "";

    public GraphPasteResult PasteSteps(string payload, double x, double y, GraphPasteChoice choice) =>
        Function is { } function ? _session.PasteSteps(function.Name, payload, x, y, choice) : new GraphPasteResult(s_noFunction, [], []);

    // The new function takes the first free name, and a person renames it from the functions panel.
    public GraphFold Collapse(IReadOnlyList<int> ids) =>
        Function is { } function
            ? _session.Collapse(function.Name, ids, PanelFields.Unique(Document.Functions.Select(f => f.Name), "collapsed"))
            : new GraphFold(s_noFunction, []);

    public GraphFold Expand(int call) => Function is { } function ? _session.Expand(function.Name, call) : new GraphFold(s_noFunction, []);

    // A function that was removed or renamed away from the open name falls back to the first one.
    public GraphFunction? Function => _session.Document.Functions.FirstOrDefault(function => function.Name == _open) ?? _session.Document.Functions.FirstOrDefault();

    public void OpenFunction(string name) => _open = name;

    public event Action? Changed
    {
        add => _session.Changed += value;
        remove => _session.Changed -= value;
    }

    public int NextStepId() => Function is { } function ? _session.GetNextStepId(function.Name) : 1;

    public GraphEditResult AddStep(int id, string kind) => InFunction(name => _session.AddStep(name, id, kind));

    public GraphEditResult RemoveStep(int id) => InFunction(name => _session.RemoveStep(name, id));

    public GraphEditResult MoveStep(int id, double x, double y) => InFunction(name => _session.MoveStep(name, id, x, y));

    public GraphEditResult SetStepParameter(int id, string parameter, string valueText) => InFunction(name => _session.SetStepParameter(name, id, parameter, valueText));

    public GraphEditResult SetStepPins(int id, int count) => InFunction(name => _session.SetStepPins(name, id, count));

    public GraphEditResult Connect(GraphWire wire) => InFunction(name => _session.Connect(name, wire));

    public GraphEditResult Disconnect(GraphWire wire) => InFunction(name => _session.Disconnect(name, wire));

    public GraphEditResult SetFrame(GraphFrame frame) => InFunction(name => _session.SetFrame(name, frame));

    public GraphEditResult RemoveFrame(int id) => InFunction(name => _session.RemoveFrame(name, id));

    // A virtual method starts with an underscore, so only such a function asks the engine what it overrides.
    public IReadOnlyList<GraphStepEntry> FindSteps(string text, GraphDraggedSide side, int type, GraphPortSort sort)
    {
        var found = _session.FindSteps(text, side, type, sort);
        var parent = Function is { Name: ['_', ..] and var name } ? ParentCall.EntryFor(name, _session.FindOverrides(), text, side, sort) : null;
        return parent is null ? found : [parent, .. found];
    }

    public GraphEditResult AddVariable(string name, int type) => _session.AddVariable(name, type);

    public GraphEditResult RemoveVariable(string name) => _session.RemoveVariable(name);

    public GraphEditResult RenameVariable(string name, string newName) => _session.RenameVariable(name, newName);

    public GraphEditResult SetVariableType(string name, int type) => _session.SetVariableType(name, type);

    public GraphEditResult SetVariableDefault(string name, string valueText) => _session.SetVariableDefault(name, valueText);

    public GraphEditResult SetVariableExported(string name, bool exported) => _session.SetVariableExported(name, exported);

    public GraphEditResult SetVariableInfo(string name, string category, string description) => _session.SetVariableInfo(name, category, description);

    public GraphEditResult AddSignal(string name) => _session.AddSignal(name);

    public GraphEditResult RemoveSignal(string name) => _session.RemoveSignal(name);

    public GraphEditResult RenameSignal(string name, string newName) => _session.RenameSignal(name, newName);

    public GraphEditResult SetSignalArguments(string name, IReadOnlyList<GraphTyped> arguments) => _session.SetSignalArguments(name, arguments);

    // The function and its Entry step are two engine calls. One entry in the history covers both.
    public GraphEditResult AddFunction(string name)
    {
        var result = new GraphEditResult(0, "");
        History.Group(() =>
        {
            result = _session.AddFunction(name);
            if (result.Ok)
            {
                result = _session.AddStep(name, 1, "entry");
            }
        });
        return result;
    }

    public IReadOnlyList<GraphStepEntry> FindOverrides() => _session.FindOverrides();

    // A signature the engine refuses takes the new function away again. A name that is taken is refused before anything is added.
    public GraphEditResult AddOverride(GraphStepEntry entry)
    {
        var result = new GraphEditResult(0, "");
        History.Group(() =>
        {
            result = AddFunction(entry.Label);
            if (result.Ok && CopySignature(entry) is { Ok: false } refused)
            {
                _session.RemoveFunction(entry.Label);
                result = refused;
            }
        });
        return result;
    }

    // The Entry step is step 1 in a new function.
    GraphEditResult CopySignature(GraphStepEntry entry)
    {
        foreach (var (parameter, text) in entry.Parameters)
        {
            var result = _session.SetStepParameter(entry.Label, 1, parameter, text);
            if (!result.Ok)
            {
                return result;
            }
        }

        return new GraphEditResult(0, "");
    }

    public GraphEditResult RemoveFunction(string name) => _session.RemoveFunction(name);

    public GraphEditResult RenameFunction(string name, string newName)
    {
        var wasOpen = Function?.Name == name;
        var result = _session.RenameFunction(name, newName);
        if (result.Ok && wasOpen)
        {
            _open = newName;
        }

        return result;
    }

    public void Save() => _session.Save();

    public void Dispose() => _session.Dispose();

    GraphEditResult InFunction(Func<string, GraphEditResult> edit) => Function is { } function ? edit(function.Name) : s_noFunction;
}
