using Orblit.Editor.App.Graph;
using Orblit.Editor.App.Shell;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Tests.Graph;

/// <summary>
/// A graph source that holds a function in memory and keeps a log of the calls it got. It refuses what the engine
/// refuses in the cases the tests use: a wire between ports of unlike sorts, a second wire into an input, and
/// a step or wire that is not there.
/// </summary>
internal sealed class FakeGraphSource : IGraphFile
{
    const int Unfit = 31;
    const int InUse = 22;
    const int Missing = 33;

    readonly List<GraphStep> _steps = [];
    readonly List<GraphWire> _wires = [];
    readonly List<GraphFrame> _frames = [];

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

    public GraphFunction? Function => Empty ? null : new GraphFunction("main", "", [.. _steps], [.. _wires], [.. _frames]);

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
        GraphPort[] inputs = kind == "reroute" ? [Data("value", 0)] : [Flow("run")];
        GraphPort[] outputs = kind == "reroute" ? [Data("value", 0)] : [Flow("then")];
        return Take($"AddStep {id} {kind}", () => _steps.Add(MakeStep(id, kind, 0, 0, inputs, outputs)));
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
        if (from.Sort != to.Sort)
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
