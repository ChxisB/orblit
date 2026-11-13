using Avalonia;
using Avalonia.Media;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>A wire with the places its ends fall in the graph and the color it is drawn in.</summary>
internal readonly record struct WireShape(GraphWire Wire, Point From, Point To, IBrush Brush);

/// <summary>
/// What the canvas shows of one function: a view for each step and frame, the wires, and what is selected.
/// It is rebuilt from the engine's function after every edit, so it never holds a state the engine does not.
/// One kind is selected at a time: some steps, one wire or one frame.
/// </summary>
internal sealed class GraphScene
{
    const double WireGrab = 6;

    readonly List<StepView> _steps = [];
    readonly Dictionary<int, StepView> _byId = [];
    readonly List<FrameView> _frames = [];
    readonly HashSet<int> _hidden = [];
    IReadOnlyList<GraphWire> _wires = [];

    public IReadOnlyList<StepView> Steps => _steps;

    public IReadOnlyList<FrameView> Frames => _frames;

    public IReadOnlyList<GraphWire> Wires => _wires;

    public HashSet<int> SelectedSteps { get; } = [];

    public int SelectedFrame { get; private set; }

    public GraphWire? SelectedWire { get; private set; }

    public WireStyle Style { get; set; }

    public int NextFrameId => _frames.Count == 0 ? 1 : _frames.Max(frame => frame.Id) + 1;

    /// <summary>The box that holds every step and frame, or an empty box for an empty graph.</summary>
    public Rect ContentBounds
    {
        get
        {
            var boxes = _steps.Select(step => step.Bounds).Concat(_frames.Select(frame => frame.Bounds)).ToList();
            return boxes.Count == 0 ? default : boxes.Aggregate((all, box) => all.Union(box));
        }
    }

    public void Load(GraphFunction? function)
    {
        _wires = function?.Wires ?? [];
        var wired = _wires.SelectMany(wire => new[] { new PinRef(wire.FromStep, wire.FromPort, false), new PinRef(wire.ToStep, wire.ToPort, true) }).ToHashSet();
        _steps.Clear();
        _steps.AddRange((function?.Steps ?? []).Select(step => new StepView(step, wired)));
        _byId.Clear();
        _steps.ForEach(step => _byId[step.Id] = step);
        _frames.Clear();
        _frames.AddRange((function?.Frames ?? []).Select(frame => new FrameView(frame)));
        HideFoldedSteps();
        PruneSelection();
    }

    public StepView? Find(int id) => _byId.GetValueOrDefault(id);

    public FrameView? FindFrame(int id) => _frames.Find(frame => frame.Id == id);

    public bool IsHidden(StepView step) => _hidden.Contains(step.Id);

    public IEnumerable<StepView> Members(FrameView frame) => _steps.Where(step => frame.Holds(step));

    /// <summary>The top step at a point and the part of it the point lands on.</summary>
    public (StepView Step, StepHit Hit)? HitStep(Point point)
    {
        for (var i = _steps.Count - 1; i >= 0; i--)
        {
            if (!IsHidden(_steps[i]) && _steps[i].HitAt(point) is { } hit)
            {
                return (_steps[i], hit);
            }
        }
        return null;
    }

    public PinRef? PinAt(Point point) => HitStep(point) is { Hit.Part: StepPart.Pin } found ? found.Hit.Pin : null;

    /// <summary>The top frame whose bar, button or grip is at the point. A frame body is a backdrop and is not hit.</summary>
    public (FrameView Frame, FramePart Part)? HitFrame(Point point)
    {
        for (var i = _frames.Count - 1; i >= 0; i--)
        {
            if (_frames[i].PartAt(point) is { } part)
            {
                return (_frames[i], part);
            }
        }
        return null;
    }

    public GraphWire? WireAt(Point point) =>
        VisibleWires().Where(shape => WireLayer.Near(shape.From, shape.To, Style, point, WireGrab)).Select(shape => (GraphWire?)shape.Wire).FirstOrDefault();

    public IEnumerable<WireShape> VisibleWires()
    {
        foreach (var wire in _wires)
        {
            if (_byId.TryGetValue(wire.FromStep, out var from) && _byId.TryGetValue(wire.ToStep, out var to) && !IsHidden(from) && !IsHidden(to))
            {
                yield return new WireShape(wire, from.PinCenter(false, wire.FromPort), to.PinCenter(true, wire.ToPort), ColorOf(from, wire.FromPort));
            }
        }
    }

    public void SelectStep(int id, bool toggle)
    {
        ClearKindsOtherThanSteps();
        if (toggle)
        {
            if (!SelectedSteps.Remove(id))
            {
                SelectedSteps.Add(id);
            }
        }
        else if (!SelectedSteps.Contains(id))
        {
            SelectedSteps.Clear();
            SelectedSteps.Add(id);
        }
    }

    public void SelectWire(GraphWire wire)
    {
        Clear();
        SelectedWire = wire;
    }

    public void SelectFrame(int id)
    {
        Clear();
        SelectedFrame = id;
    }

    public void SelectWithin(Rect box, bool additive)
    {
        if (!additive)
        {
            Clear();
        }
        ClearKindsOtherThanSteps();
        foreach (var step in _steps.Where(step => !IsHidden(step) && step.Bounds.Intersects(box)))
        {
            SelectedSteps.Add(step.Id);
        }
    }

    public void Clear()
    {
        SelectedSteps.Clear();
        SelectedFrame = 0;
        SelectedWire = null;
    }

    static IBrush ColorOf(StepView step, int port) => port < step.Step.Outputs.Count ? PinColors.Of(step.Step.Outputs[port]) : PinColors.Any;

    void ClearKindsOtherThanSteps()
    {
        SelectedFrame = 0;
        SelectedWire = null;
    }

    void HideFoldedSteps()
    {
        _hidden.Clear();
        foreach (var frame in _frames.Where(frame => frame.Frame.Collapsed))
        {
            foreach (var step in Members(frame))
            {
                _hidden.Add(step.Id);
            }
        }
    }

    void PruneSelection()
    {
        SelectedSteps.RemoveWhere(id => !_byId.ContainsKey(id));
        if (FindFrame(SelectedFrame) is null)
        {
            SelectedFrame = 0;
        }
        if (SelectedWire is { } wire && !_wires.Contains(wire))
        {
            SelectedWire = null;
        }
    }
}
