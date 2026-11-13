using Avalonia;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// The edits a gesture or a key makes, each as calls to the source. The engine decides whether an edit is
/// allowed. A refusal is reported with its reason and the canvas shows the engine's graph again, so nothing
/// drawn can disagree with it. Edits that take more than one call run as a batch, so the canvas is rebuilt once.
/// </summary>
internal sealed class GraphEdits
{
    const string RerouteData = "reroute";
    const string RerouteFlow = "reroute_flow";
    const double FramePadding = 20;
    const string NewFrameTitle = "Frame";

    readonly IGraphSource _source;
    readonly GraphScene _scene;

    public GraphEdits(IGraphSource source, GraphScene scene)
    {
        _source = source;
        _scene = scene;
    }

    /// <summary>True while a batch runs. The canvas waits for <see cref="Settled"/> instead of rebuilding after each call.</summary>
    public bool Busy { get; private set; }

    /// <summary>Raised with the engine's reason when it refuses an edit.</summary>
    public event Action<string>? Refused;

    /// <summary>Raised after a batch of edits.</summary>
    public event Action? Settled;

    /// <summary>Raised after a collapse or an expand that was taken, with the ids of the steps to select.</summary>
    public event Action<IReadOnlyList<int>>? Folded;

    /// <summary>Joins an output to an input. Pins on one side cannot be joined, and the engine checks the rest.</summary>
    public bool Connect(PinRef from, PinRef to)
    {
        if (from.IsInput == to.IsInput)
        {
            Refused?.Invoke("A wire joins an output to an input.");
            return false;
        }
        return Report(_source.Connect(from.WireTo(to)));
    }

    /// <summary>Puts the script back to what it was before the last edit. Nothing to undo is reported as a refusal.</summary>
    public void Undo() => Report(_source.History.Undo());

    /// <summary>Takes back the last undo. Nothing to redo is reported as a refusal.</summary>
    public void Redo() => Report(_source.History.Redo());

    public void MoveSteps(IEnumerable<int> ids, Vector delta) => MoveEach(ids.Select(id => new Move(id, delta)));

    /// <summary>Moves each step by its own amount, as one undo. A step the function does not have is skipped.</summary>
    public void MoveEach(IEnumerable<Move> moves) => Batch(() =>
    {
        foreach (var move in moves)
        {
            if (_scene.Find(move.Id) is { } step)
            {
                Report(_source.MoveStep(step.Id, Math.Round(step.Step.X + move.By.X), Math.Round(step.Step.Y + move.By.Y)));
            }
        }
    });

    /// <summary>Moves the selected steps into a function of their own and leaves a call to it, as one undo.</summary>
    public void Collapse() => Fold(() => _source.Collapse([.. _scene.SelectedSteps]));

    /// <summary>Opens the selected Call step out into the steps of the function it calls, as one undo.</summary>
    public void Expand() => Fold(() => _scene.SelectedSteps.Count == 1
        ? _source.Expand(_scene.SelectedSteps.Single())
        : new GraphFold(new GraphEditResult(31, "Select one Call step to expand."), []));

    /// <summary>Removes the selected steps, or else the selected wire, or else the selected frame.</summary>
    public void DeleteSelection() => Batch(() =>
    {
        foreach (var id in _scene.SelectedSteps.ToList())
        {
            Report(_source.RemoveStep(id));
        }
        if (_scene.SelectedWire is { } wire)
        {
            Report(_source.Disconnect(wire));
        }
        else if (_scene.SelectedFrame != 0)
        {
            Report(_source.RemoveFrame(_scene.SelectedFrame));
        }
    });

    public bool SetValue(int step, string parameter, string text) => Report(_source.SetStepParameter(step, parameter, text));

    /// <summary>Adds or removes pins on a step that grows them.</summary>
    public bool ChangePins(int step, int change) =>
        _scene.Find(step) is { } view && Report(_source.SetStepPins(step, view.Step.Growth.Count + change));

    /// <summary>
    /// Puts a reroute on a wire where the point is. The engine has no call for it, so it adds the reroute, then
    /// swaps the wire for two. A refused call leaves the graph in the state of the calls that went before it.
    /// The batch is one entry in the history.
    /// </summary>
    public void InsertReroute(GraphWire wire, Point at) => Batch(() =>
    {
        var kind = RerouteKindFor(wire);
        var id = _source.NextStepId();
        var steps = new[]
        {
            _source.AddStep(id, kind),
            _source.MoveStep(id, Math.Round(at.X - (StepView.RerouteSize / 2)), Math.Round(at.Y - (StepView.RerouteSize / 2))),
            _source.Disconnect(wire),
            _source.Connect(new GraphWire(wire.FromStep, wire.FromPort, id, 0)),
            _source.Connect(new GraphWire(id, 0, wire.ToStep, wire.ToPort)),
        };
        Array.ForEach(steps, result => Report(result));
    });

    /// <summary>
    /// Adds a step where the point is, with the values the entry starts it with. When a wire was dragged out to
    /// ask for it, the first port of the new step that carries the same sort of wire is joined to the pin.
    /// </summary>
    public void AddStep(GraphStepEntry entry, Point at, PinRef? from) => Batch(() =>
    {
        var id = _source.NextStepId();
        if (!Report(_source.AddStep(id, entry.Kind)))
        {
            return;
        }
        foreach (var (parameter, text) in entry.Parameters)
        {
            Report(_source.SetStepParameter(id, parameter, text));
        }
        Report(_source.MoveStep(id, Math.Round(at.X), Math.Round(at.Y)));
        if (from is { } pin)
        {
            JoinAdded(id, pin);
        }
    });

    /// <summary>Puts a frame around the selected steps.</summary>
    public bool FrameSelection()
    {
        var steps = _scene.SelectedSteps.Select(_scene.Find).OfType<StepView>().ToList();
        if (steps.Count == 0)
        {
            return false;
        }
        var box = steps.Select(step => step.Bounds).Aggregate((all, one) => all.Union(one)).Inflate(FramePadding);
        var frame = new GraphFrame(_scene.NextFrameId, NewFrameTitle, box.X, box.Y - FrameView.TitleHeight, box.Width, box.Height + FrameView.TitleHeight, false);
        return Report(_source.SetFrame(frame));
    }

    /// <summary>Moves a frame and the steps inside it.</summary>
    public void MoveFrame(FrameView frame, Vector delta, IEnumerable<int> members) => Batch(() =>
    {
        Report(_source.SetFrame(frame.Frame with { X = Math.Round(frame.Frame.X + delta.X), Y = Math.Round(frame.Frame.Y + delta.Y) }));
        MoveSteps(members, delta);
    });

    public bool ResizeFrame(FrameView frame, Vector growth) => Report(_source.SetFrame(frame.Frame with
    {
        Width = Math.Max(Math.Round(frame.Frame.Width + growth.X), FrameView.MinWidth),
        Height = Math.Max(Math.Round(frame.Frame.Height + growth.Y), FrameView.MinHeight),
    }));

    public bool ToggleFrame(FrameView frame) => Report(_source.SetFrame(frame.Frame with { Collapsed = !frame.Frame.Collapsed }));

    public bool RenameFrame(FrameView frame, string title) => Report(_source.SetFrame(frame.Frame with { Title = title }));

    // The steps are selected after the batch settles, because the canvas is rebuilt then and the ids belong to the new graph.
    void Fold(Func<GraphFold> run)
    {
        GraphFold? done = null;
        Batch(() =>
        {
            done = run();
            Report(done.Result);
        });
        if (done is { Result.Ok: true } fold)
        {
            Folded?.Invoke(fold.Steps);
        }
    }

    // The palette already lists steps that fit the pin, so the first port of the same sort nearly always takes the
    // wire. A port the engine refuses is passed over, and the first refusal is shown if none takes it.
    void JoinAdded(int id, PinRef pin)
    {
        var added = _source.Function?.Steps.FirstOrDefault(step => step.Id == id);
        var dragged = _scene.Find(pin.Step)?.Step;
        if (added is null || dragged is null)
        {
            return;
        }
        var sort = (pin.IsInput ? dragged.Inputs : dragged.Outputs)[pin.Port].Sort;
        var ports = pin.IsInput ? added.Outputs : added.Inputs;
        GraphEditResult? refused = null;
        for (var port = 0; port < ports.Count; port++)
        {
            if (ports[port].Sort != sort)
            {
                continue;
            }
            var result = _source.Connect(pin.WireTo(new PinRef(id, port, !pin.IsInput)));
            if (result.Ok)
            {
                return;
            }
            refused ??= result;
        }
        if (refused is not null)
        {
            Report(refused);
        }
    }

    // A wire that carries the order of steps takes a flow reroute, and any other wire takes a data reroute.
    string RerouteKindFor(GraphWire wire) =>
        _scene.Find(wire.FromStep) is { } from && wire.FromPort < from.Step.Outputs.Count && from.Step.Outputs[wire.FromPort].Sort == GraphPortSort.Flow
            ? RerouteFlow
            : RerouteData;

    bool Report(GraphEditResult result)
    {
        if (!result.Ok)
        {
            Refused?.Invoke(result.Reason);
        }
        return result.Ok;
    }

    // A nested batch leaves the outer one to settle, so MoveFrame does not rebuild twice.
    void Batch(Action work)
    {
        if (Busy)
        {
            work();
            return;
        }
        Busy = true;
        try
        {
            _source.History.Group(work);
        }
        finally
        {
            Busy = false;
            Settled?.Invoke();
        }
    }
}
