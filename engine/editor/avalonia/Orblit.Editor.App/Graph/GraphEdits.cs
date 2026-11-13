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

    public void MoveSteps(IEnumerable<int> ids, Vector delta) => Batch(() =>
    {
        foreach (var step in ids.Select(_scene.Find).OfType<StepView>())
        {
            Report(_source.MoveStep(step.Id, Math.Round(step.Step.X + delta.X), Math.Round(step.Step.Y + delta.Y)));
        }
    });

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
    /// </summary>
    // shortcut: six calls with no undo group, so one undo takes back one call. Group them when the history lands.
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
            work();
        }
        finally
        {
            Busy = false;
            Settled?.Invoke();
        }
    }
}
