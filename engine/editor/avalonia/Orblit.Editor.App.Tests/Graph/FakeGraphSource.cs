using Orblit.Editor.App.Graph;
using Orblit.Editor.App.Shell;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

/// <summary>
/// A graph source that holds a function in memory and keeps a log of the calls it got. It refuses what the engine
/// refuses in the cases the tests use: a wire between ports of unlike sorts or unlike data types, a second wire
/// into an input, and a step or wire that is not there. It offers a small catalogue of steps to search. It keeps
/// variables, signals and function names, and refuses a name that is taken, a name that is not an identifier and a
/// name that is not there.
/// </summary>
internal sealed class FakeGraphSource : IGraphFile
{
    const int Unfit = 31;
    const int InUse = 22;
    const int Missing = 33;
    const int Exists = 32;

    sealed record Template(GraphStepEntry Entry, GraphPort[] Inputs, GraphPort[] Outputs);

    static readonly Template[] s_catalogue =
    [
        new(new GraphStepEntry("branch", "Flow", "Branch", new Dictionary<string, string>()), [Flow("run"), Data("condition", 1, "condition")], [Flow("true"), Flow("false")]),
        new(new GraphStepEntry("translate", "Spatial", "Translate", new Dictionary<string, string>()), [Flow("run"), Data("offset", 5, "offset")], [Flow("then")]),
        new(new GraphStepEntry("print", "Output", "Print", new Dictionary<string, string> { ["text"] = "\"\"" }), [Flow("run"), Data("text", 4, "text")], [Flow("then")]),
        new(new GraphStepEntry("constant", "Values", "Constant", new Dictionary<string, string> { ["value"] = "0" }), [], [Data("value", 0)]),
    ];

    // The steps a pin edit adds. They are kept apart from the catalogue because the palette tests list the catalogue.
    static readonly Dictionary<string, (GraphPort[] Inputs, GraphPort[] Outputs, GraphGrowth Growth)> s_kinds = new()
    {
        ["get"] = ([], [Data("value", 0)], NoGrowth),
        ["local"] = ([], [Data("value", 0)], NoGrowth),
        ["set"] = ([Flow("run"), Data("value", 0, "value")], [Flow("then")], NoGrowth),
        ["set_local"] = ([Flow("run"), Data("value", 0, "value")], [Flow("then")], NoGrowth),
        ["decompose"] = ([Data("value", 0, "value")], [Data("part", 0)], new GraphGrowth(GraphGrowthSide.Output, 1, 1, 64)),
        ["compose"] = ([Data("argument", 0)], [Data("value", 0)], new GraphGrowth(GraphGrowthSide.Input, 0, 0, 64)),
    };

    static GraphGrowth NoGrowth => new(GraphGrowthSide.None, 0, 0, 64);

    readonly List<GraphStep> _steps = [];
    readonly List<GraphWire> _wires = [];
    readonly List<GraphFrame> _frames = [];
    readonly List<GraphVariable> _variables = [];
    readonly List<GraphSignal> _signals = [];
    readonly List<string> _otherFunctions = [];
    string _openName = "main";

    public List<string> Calls { get; } = [];

    public int Saves { get; private set; }

    public bool SaveFails { get; set; }

    public bool Disposed { get; private set; }

    public void Save()
    {
        if (SaveFails)
        {
            throw new IOException("The disk is full.");
        }

        Saves++;
    }

    public void Dispose() => Disposed = true;

    public bool Empty { get; set; }

    /// <summary>One line for each search the palette made: the text, the side, the type and the sort.</summary>
    public List<string> Searches { get; } = [];

    public GraphFunction? Function => Empty ? null : new GraphFunction(_openName, "", [.. _steps], [.. _wires], [.. _frames]);

    public GraphDocument Document => new("Entity", "", [.. _variables], [.. _signals], [.. FunctionList()], []);

    public void Put(GraphVariable variable) => _variables.Add(variable);

    public void Put(GraphSignal signal) => _signals.Add(signal);

    /// <summary>Names a function that has no steps, so the script holds more than the one on show.</summary>
    public void PutFunction(string name) => _otherFunctions.Add(name);

    public event Action? Changed;

    public static GraphPort Flow(string name) => new(name, GraphPortSort.Flow, 0, "", false);

    public static GraphPort Data(string name, int type = 3, string parameter = "") => new(name, GraphPortSort.Data, type, parameter, false);

    public static GraphStep MakeStep(int id, string kind, double x, double y, GraphPort[] inputs, GraphPort[] outputs, GraphGrowth? growth = null, params (string Name, string Value)[] parameters) =>
        new(id, kind, true, "Test", x, y, parameters.ToDictionary(pair => pair.Name, pair => pair.Value), "", inputs, outputs, growth ?? new GraphGrowth(GraphGrowthSide.None, 0, 0, 64));

    /// <summary>Three steps in a row. Entry runs Branch, Branch runs Print, and Print holds the text it prints.</summary>
    public static FakeGraphSource Sample()
    {
        var source = new FakeGraphSource();
        source._steps.Add(MakeStep(1, "entry", 0, 0, [], [Flow("then")]));
        source._steps.Add(MakeStep(2, "branch", 260, 0, [Flow("run"), Data("condition", 1, "condition")], [Flow("true"), Flow("false")], null, ("condition", "false")));
        source._steps.Add(MakeStep(3, "print", 520, 0, [Flow("run"), Data("text", 4, "text")], [Flow("then")], null, ("text", "\"hi\"")));
        source._wires.Add(new GraphWire(1, 0, 2, 0));
        source._wires.Add(new GraphWire(2, 0, 3, 0));
        return source;
    }

    public void Put(GraphStep step) => _steps.Add(step);

    public void Put(GraphWire wire) => _wires.Add(wire);

    public void Put(GraphFrame frame) => _frames.Add(frame);

    /// <summary>Drops a step with no change event, as when another editor removed it and this one has not heard yet.</summary>
    public void Forget(int id) => _steps.RemoveAll(step => step.Id == id);

    public int NextStepId() => _steps.Count == 0 ? 1 : _steps.Max(step => step.Id) + 1;

    public GraphEditResult AddStep(int id, string kind)
    {
        var template = Array.Find(s_catalogue, found => found.Entry.Kind == kind);
        var made = s_kinds.GetValueOrDefault(kind);
        GraphPort[] inputs = kind == "reroute" ? [Data("value", 0)] : template?.Inputs ?? made.Inputs ?? [Flow("run")];
        GraphPort[] outputs = kind == "reroute" ? [Data("value", 0)] : template?.Outputs ?? made.Outputs ?? [Flow("then")];
        return Take($"AddStep {id} {kind}", () => _steps.Add(MakeStep(id, kind, 0, 0, inputs, outputs, made.Growth)));
    }

    public IReadOnlyList<GraphStepEntry> FindSteps(string text, GraphDraggedSide side, int type, GraphPortSort sort)
    {
        Searches.Add($"{text}|{side}|{type}|{sort}");
        return [.. s_catalogue.Where(template => template.Entry.Label.Contains(text, StringComparison.OrdinalIgnoreCase) && Fits(template, side, type, sort)).Select(template => template.Entry)];
    }

    public GraphEditResult RemoveStep(int id)
    {
        if (!_steps.Exists(step => step.Id == id))
        {
            return Refuse($"RemoveStep {id}", Missing, "The step is not there.");
        }
        return Take($"RemoveStep {id}", () =>
        {
            _steps.RemoveAll(step => step.Id == id);
            _wires.RemoveAll(wire => wire.FromStep == id || wire.ToStep == id);
        });
    }

    public GraphEditResult MoveStep(int id, double x, double y)
    {
        var at = _steps.FindIndex(step => step.Id == id);
        return at < 0 ? Refuse($"MoveStep {id}", Missing, "The step is not there.") : Take($"MoveStep {id} {x} {y}", () => _steps[at] = _steps[at] with { X = x, Y = y });
    }

    public GraphEditResult SetStepParameter(int id, string parameter, string valueText)
    {
        var at = _steps.FindIndex(step => step.Id == id);
        if (at < 0)
        {
            return Refuse($"SetStepParameter {id}", Missing, "The step is not there.");
        }
        var parameters = new Dictionary<string, string>(_steps[at].Parameters) { [parameter] = valueText };
        return Take($"SetStepParameter {id} {parameter} {valueText}", () => _steps[at] = _steps[at] with { Parameters = parameters });
    }

    public GraphEditResult SetStepPins(int id, int count)
    {
        var at = _steps.FindIndex(step => step.Id == id);
        if (at < 0 || _steps[at].Growth.Side == GraphGrowthSide.None)
        {
            return Refuse($"SetStepPins {id} {count}", Unfit, "That step does not take more pins.");
        }
        return Take($"SetStepPins {id} {count}", () => _steps[at] = _steps[at] with { Growth = _steps[at].Growth with { Count = count } });
    }

    public GraphEditResult Connect(GraphWire wire)
    {
        var from = _steps.Find(step => step.Id == wire.FromStep)?.Outputs.ElementAtOrDefault(wire.FromPort);
        var to = _steps.Find(step => step.Id == wire.ToStep)?.Inputs.ElementAtOrDefault(wire.ToPort);
        var call = $"Connect {wire.FromStep}.{wire.FromPort} {wire.ToStep}.{wire.ToPort}";
        if (from is null || to is null)
        {
            return Refuse(call, Missing, "The step or port is not there.");
        }
        if (from.Sort != to.Sort || (from.Type != 0 && to.Type != 0 && from.Type != to.Type))
        {
            return Refuse(call, Unfit, "That does not fit. The ports do not match, or the engine does not take that value.");
        }
        if (_wires.Exists(other => other.ToStep == wire.ToStep && other.ToPort == wire.ToPort))
        {
            return Refuse(call, InUse, "That input already has a wire.");
        }
        return Take(call, () => _wires.Add(wire));
    }

    public GraphEditResult Disconnect(GraphWire wire) =>
        _wires.Contains(wire)
            ? Take($"Disconnect {wire.FromStep}.{wire.FromPort} {wire.ToStep}.{wire.ToPort}", () => _wires.Remove(wire))
            : Refuse("Disconnect", Missing, "The wire is not there.");

    public GraphEditResult SetFrame(GraphFrame frame) => Take($"SetFrame {frame.Id} {frame.Title} {frame.X} {frame.Y} {frame.Width} {frame.Height} {frame.Collapsed}", () =>
    {
        var at = _frames.FindIndex(other => other.Id == frame.Id);
        if (at < 0)
        {
            _frames.Add(frame);
        }
        else
        {
            _frames[at] = frame;
        }
    });

    public GraphEditResult RemoveFrame(int id) =>
        _frames.Exists(frame => frame.Id == id) ? Take($"RemoveFrame {id}", () => _frames.RemoveAll(frame => frame.Id == id)) : Refuse($"RemoveFrame {id}", Missing, "The frame is not there.");

    public void OpenFunction(string name) => Calls.Add($"OpenFunction {name}");

    public GraphEditResult AddVariable(string name, int type) =>
        Declare($"AddVariable {name} {type}", name, _variables.Exists(v => v.Name == name), () => _variables.Add(new GraphVariable(name, type, "null", false, 0, "", "", "")));

    public GraphEditResult RemoveVariable(string name) => Vary($"RemoveVariable {name}", name, _ => null);

    public GraphEditResult RenameVariable(string name, string newName) =>
        Rename($"RenameVariable {name} {newName}", newName, _variables.Exists(v => v.Name == newName), () => Vary($"RenameVariable {name} {newName}", name, v => v with { Name = newName }));

    public GraphEditResult SetVariableType(string name, int type) => Vary($"SetVariableType {name} {type}", name, v => v with { Type = type, Default = "null" });

    public GraphEditResult SetVariableDefault(string name, string valueText) => Vary($"SetVariableDefault {name} {valueText}", name, v => v with { Default = valueText });

    public GraphEditResult SetVariableExported(string name, bool exported) => Vary($"SetVariableExported {name} {exported}", name, v => v with { Exported = exported });

    public GraphEditResult SetVariableInfo(string name, string category, string description) =>
        Vary($"SetVariableInfo {name} {category} {description}", name, v => v with { Category = category, Description = description });

    public GraphEditResult AddSignal(string name) => Declare($"AddSignal {name}", name, _signals.Exists(s => s.Name == name), () => _signals.Add(new GraphSignal(name, [])));

    public GraphEditResult RemoveSignal(string name) => Sign($"RemoveSignal {name}", name, _ => null);

    public GraphEditResult RenameSignal(string name, string newName) =>
        Rename($"RenameSignal {name} {newName}", newName, _signals.Exists(s => s.Name == newName), () => Sign($"RenameSignal {name} {newName}", name, s => s with { Name = newName }));

    public GraphEditResult SetSignalArguments(string name, IReadOnlyList<GraphTyped> arguments) =>
        Sign($"SetSignalArguments {name} {GraphTyped.WriteList(arguments)}", name, s => s with { Arguments = arguments });

    public GraphEditResult AddFunction(string name) =>
        Declare($"AddFunction {name}", name, FunctionList().Any(f => f.Name == name), () => _otherFunctions.Add(name));

    public GraphEditResult RemoveFunction(string name) =>
        _otherFunctions.Contains(name) ? Take($"RemoveFunction {name}", () => _otherFunctions.Remove(name)) : Refuse($"RemoveFunction {name}", Missing, "There is no such function.");

    public GraphEditResult RenameFunction(string name, string newName)
    {
        var call = $"RenameFunction {name} {newName}";
        var at = _otherFunctions.IndexOf(name);
        if (at < 0 && name != _openName)
        {
            return Refuse(call, Missing, "There is no such function.");
        }

        return Rename(call, newName, FunctionList().Any(f => f.Name == newName), () => Take(call, () =>
        {
            if (at < 0)
            {
                _openName = newName;
            }
            else
            {
                _otherFunctions[at] = newName;
            }
        }));
    }

    IEnumerable<GraphFunction> FunctionList()
    {
        if (Function is { } open)
        {
            yield return open;
        }

        foreach (var name in _otherFunctions)
        {
            yield return new GraphFunction(name, "", [], [], []);
        }
    }

    // A name has to be an identifier and one nothing else has. The callback does the change once those hold.
    GraphEditResult Rename(string call, string newName, bool taken, Func<GraphEditResult> change)
    {
        if (!IsIdentifier(newName))
        {
            return Refuse(call, Unfit, "That is not a name the engine takes.");
        }

        return taken ? Refuse(call, Exists, $"There is already something named {newName}.") : change();
    }

    GraphEditResult Declare(string call, string name, bool taken, Action add) =>
        !IsIdentifier(name) ? Refuse(call, Unfit, "That is not a name the engine takes.")
        : taken ? Refuse(call, Exists, $"There is already something named {name}.")
        : Take(call, add);

    // A null result removes the variable.
    GraphEditResult Vary(string call, string name, Func<GraphVariable, GraphVariable?> change)
    {
        var at = _variables.FindIndex(v => v.Name == name);
        if (at < 0)
        {
            return Refuse(call, Missing, $"There is no variable named {name}.");
        }

        return Take(call, () =>
        {
            var changed = change(_variables[at]);
            if (changed is null)
            {
                _variables.RemoveAt(at);
            }
            else
            {
                _variables[at] = changed;
            }
        });
    }

    GraphEditResult Sign(string call, string name, Func<GraphSignal, GraphSignal?> change)
    {
        var at = _signals.FindIndex(s => s.Name == name);
        if (at < 0)
        {
            return Refuse(call, Missing, $"There is no signal named {name}.");
        }

        return Take(call, () =>
        {
            var changed = change(_signals[at]);
            if (changed is null)
            {
                _signals.RemoveAt(at);
            }
            else
            {
                _signals[at] = changed;
            }
        });
    }

    static bool IsIdentifier(string name) => name.Length > 0 && !char.IsDigit(name[0]) && name.All(c => char.IsLetterOrDigit(c) || c == '_');

    // A wire dragged from an output needs an input on the step, and the other way round. Flow wires ignore the type.
    static bool Fits(Template template, GraphDraggedSide side, int type, GraphPortSort sort) =>
        side == GraphDraggedSide.None
        || (side == GraphDraggedSide.Output ? template.Inputs : template.Outputs)
            .Any(port => port.Sort == sort && (sort == GraphPortSort.Flow || type == 0 || port.Type == 0 || port.Type == type));

    GraphEditResult Take(string call, Action change)
    {
        Calls.Add(call);
        change();
        Changed?.Invoke();
        return new GraphEditResult(0, "");
    }

    GraphEditResult Refuse(string call, long code, string reason)
    {
        Calls.Add(call);
        return new GraphEditResult(code, reason);
    }
}
