namespace Orblit.Editor.Bridge;

/// <summary>What an edit did. The engine refuses a bad edit with a code and leaves the script as it was.</summary>
public sealed record GraphEditResult(long Code, string Reason)
{
    const long Done = 0;

    public bool Ok => Code == Done;

    // The engine gives codes, not text, so the words are here. One code can come from more than one edit.
    internal static GraphEditResult From(long code) => new(code, code switch
    {
        Done => "",
        2 => "The step is of a kind the engine does not know, so it has no ports to wire.",
        22 => "That input already has a wire.",
        31 => "That does not fit. The ports do not match, or the engine does not take that value.",
        32 => "That is already there.",
        33 => "The function, step or wire is not there.",
        43 => "The value is not written as Variant text.",
        _ => $"The engine refused the change with error code {code}.",
    });
}

/// <summary>The side of a step a wire is dragged from. The palette lists what fits the other side.</summary>
public enum GraphDraggedSide
{
    None,
    Input,
    Output,
}

/// <summary>
/// One graph script open for editing. The engine holds the script. This class asks it to change and
/// reads back what it holds, so what the editor shows is what the engine would run.
/// </summary>
public sealed class GraphScriptSession : IDisposable
{
    const long CacheReuse = 1;

    readonly EngineHost _host;
    readonly nint _script;
    bool _disposed;

    GraphScriptSession(EngineHost host, nint script, string path)
    {
        _host = host;
        _script = script;
        Path = path;
        Document = ReadDocument();
    }

    nint Script
    {
        get
        {
            // A released handle is freed memory in the engine, so a late call must not reach it.
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _script;
        }
    }

    /// <summary>The resource path the script was opened from and is saved to.</summary>
    public string Path { get; }

    /// <summary>What the engine held after the last edit.</summary>
    public GraphDocument Document { get; private set; }

    /// <summary>Raised after an edit the engine took. A refused edit changes nothing, so it raises nothing.</summary>
    public event Action? Changed;

    /// <exception cref="IOException">The engine could not load the file.</exception>
    public static GraphScriptSession Open(EngineHost host, string resourcePath)
    {
        ResourcePath.RequireFile(resourcePath, ".ograph", "a graph script file");
        var script = host.ResourceLoaderLoad(host.Singleton("ResourceLoader"), resourcePath, "", CacheReuse);
        if (script == 0)
        {
            throw new IOException($"The engine could not load {resourcePath}.");
        }
        return new GraphScriptSession(host, script, resourcePath);
    }

    public GraphEditResult AddFunction(string name) => Edit(_host.GraphScriptAddFunction(Script, name));

    public GraphEditResult RemoveFunction(string name) => Edit(_host.GraphScriptRemoveFunction(Script, name));

    /// <summary>A step id the function does not use yet.</summary>
    public int GetNextStepId(string function) => _host.GraphScriptGetValidStepId(Script, function);

    public GraphEditResult AddStep(string function, int id, string kind) => Edit(_host.GraphScriptAddStep(Script, function, id, kind));

    public GraphEditResult RemoveStep(string function, int id) => Edit(_host.GraphScriptRemoveStep(Script, function, id));

    public GraphEditResult MoveStep(string function, int id, double x, double y) => Edit(_host.GraphScriptMoveStep(Script, function, id, x, y));

    /// <summary>Sets a parameter from Variant text, such as <c>Vector2(1, 2)</c> or <c>"total"</c>.</summary>
    public GraphEditResult SetStepParameter(string function, int id, string parameter, string valueText) =>
        Edit(_host.GraphScriptSetStepParameter(Script, function, id, parameter, valueText));

    public GraphEditResult SetStepPins(string function, int id, int count) => Edit(_host.GraphScriptSetStepPins(Script, function, id, count));

    public GraphEditResult Connect(string function, GraphWire wire) =>
        Edit(_host.GraphScriptConnectSteps(Script, function, wire.FromStep, wire.FromPort, wire.ToStep, wire.ToPort));

    public GraphEditResult Disconnect(string function, GraphWire wire) =>
        Edit(_host.GraphScriptDisconnectSteps(Script, function, wire.FromStep, wire.FromPort, wire.ToStep, wire.ToPort));

    /// <summary>Adds the frame, or replaces the frame with the same id.</summary>
    public GraphEditResult SetFrame(string function, GraphFrame frame) => Edit(_host.GraphScriptSetFrame(Script, function, GraphJson.Write(frame)));

    public GraphEditResult RemoveFrame(string function, int id) => Edit(_host.GraphScriptRemoveFrame(Script, function, id));

    public GraphEditResult RenameFunction(string name, string newName) => Edit(_host.GraphScriptRenameFunction(Script, name, newName));

    public GraphEditResult RenameVariable(string name, string newName) => Edit(_host.GraphScriptRenameVariable(Script, name, newName));

    public GraphEditResult RenameSignal(string name, string newName) => Edit(_host.GraphScriptRenameSignal(Script, name, newName));

    /// <summary>Adds a variable. The type is a Variant type number, and 0 is a variable that holds any value.</summary>
    public GraphEditResult AddVariable(string name, int type) => Edit(_host.GraphScriptAddVariable(Script, name, type));

    public GraphEditResult RemoveVariable(string name) => Edit(_host.GraphScriptRemoveVariable(Script, name));

    /// <summary>Changes the type. A default that does not fit the new type goes back to the default of that type.</summary>
    public GraphEditResult SetVariableType(string name, int type) => Edit(_host.GraphScriptSetVariableType(Script, name, type));

    /// <summary>Sets the default from Variant text, such as <c>2.5</c>. It must be of the variable's type.</summary>
    public GraphEditResult SetVariableDefault(string name, string valueText) => Edit(_host.GraphScriptSetVariableDefault(Script, name, valueText));

    public GraphEditResult SetVariableExported(string name, bool exported) => Edit(_host.GraphScriptSetVariableExported(Script, name, exported));

    /// <summary>Sets the inspector hint. The hint is a PropertyHint number.</summary>
    public GraphEditResult SetVariableHint(string name, int hint, string hintString) => Edit(_host.GraphScriptSetVariableHint(Script, name, hint, hintString));

    public GraphEditResult SetVariableInfo(string name, string category, string description) =>
        Edit(_host.GraphScriptSetVariableInfo(Script, name, category, description));

    public GraphEditResult AddSignal(string name) => Edit(_host.GraphScriptAddSignal(Script, name));

    public GraphEditResult RemoveSignal(string name) => Edit(_host.GraphScriptRemoveSignal(Script, name));

    public GraphEditResult SetSignalArguments(string name, IReadOnlyList<GraphTyped> arguments) =>
        Edit(_host.GraphScriptSetSignalArguments(Script, name, GraphTyped.WriteList(arguments)));

    /// <summary>The steps that fit a wire dragged from the given side. A type of 0 and no side list every step.</summary>
    /// <exception cref="InvalidDataException">The engine refused the question, for example for a type that is not a Variant type.</exception>
    public IReadOnlyList<GraphStepEntry> FindSteps(string text, GraphDraggedSide side, int type, GraphPortSort sort)
    {
        var sideText = side switch
        {
            GraphDraggedSide.Input => "input",
            GraphDraggedSide.Output => "output",
            _ => "",
        };
        var sortText = sort == GraphPortSort.Flow ? "flow" : "data";
        return GraphStepEntry.ParseList(_host.GraphScriptFindSteps(Script, text, sideText, type, sortText));
    }

    /// <exception cref="IOException">The engine could not write the file.</exception>
    public void Save()
    {
        var code = _host.ResourceSaverSave(_host.Singleton("ResourceSaver"), Script, Path, 0);
        if (code != 0)
        {
            throw new IOException($"The engine could not save {Path}. Error code {code}.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _host.Release(_script);
    }

    GraphEditResult Edit(long code)
    {
        var result = GraphEditResult.From(code);
        if (result.Ok)
        {
            Document = ReadDocument();
            Changed?.Invoke();
        }
        return result;
    }

    GraphDocument ReadDocument() => GraphDocument.Parse(_host.GraphScriptGetDocument(Script));
}
