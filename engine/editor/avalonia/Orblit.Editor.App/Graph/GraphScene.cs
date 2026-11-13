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
    readonly Dictionary<string, HashSet<int>> _bookmarks = [];
    HashSet<int> _warned = [];
    string _function = "";
    IReadOnlyList<GraphWire> _wires = [];

    public IReadOnlyList<StepView> Steps => _steps;

    public IReadOnlyList<FrameView> Frames => _frames;

    public IReadOnlyList<GraphWire> Wires => _wires;

    public HashSet<int> SelectedSteps { get; } = [];

    public int SelectedFrame { get; private set; }

    public GraphWire? SelectedWire { get; private set; }

    public WireStyle Style { get; set; }

    /// <summary>The steps of the function on show that carry a bookmark.</summary>
    // shortcut: bookmarks live in memory with the scene and are lost when the script closes. Keep them in the file when it has a place for editor data.
    public IReadOnlySet<int> Bookmarked => _bookmarks.GetValueOrDefault(_function) ?? [];

    /// <summary>The breakpoints of every script. The scene shows the ones of its own script and function.</summary>
    public GraphBreakpoints Breakpoints { get; set; } = new();

    /// <summary>The path of the script on show, which names where its breakpoints are.</summary>
    public string Script { get; set; } = "";

    /// <summary>The steps of the function on show that carry a breakpoint.</summary>
    public IReadOnlySet<int> Breakpointed => Breakpoints.Steps(BreakpointSource);

    /// <summary>The step a running game stopped on, or null when none did. It is marked when its script and function are on show.</summary>
    public DebugPlace? StoppedAt { get; set; }

    /// <summary>The steps of the function on show that a warning names.</summary>
    public IReadOnlySet<int> Warned => _warned;

    string BreakpointSource => GraphBreakpoints.SourceOf(Script, _function);

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

    /// <param name="function">The function to show.</param>
    /// <param name="warnings">The warnings of the whole script. The ones that name a step of this function mark it.</param>
    public void Load(GraphFunction? function, IReadOnlyList<GraphWarning>? warnings = null)
    {
        _function = function?.Name ?? "";
        _warned = [.. (warnings ?? []).Where(warning => warning.Step > 0 && warning.Function == _function).Select(warning => warning.Step)];
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
        // A step id can be used again after the step is gone, so a bookmark outlives only its step.
        _bookmarks.GetValueOrDefault(_function)?.IntersectWith(_byId.Keys);
        Breakpoints.Keep(BreakpointSource, _byId.Keys);
    }

    /// <summary>What the canvas has marked on a step.</summary>
    public StepMarks MarksOf(StepView step) =>
        (SelectedSteps.Contains(step.Id) ? StepMarks.Selected : StepMarks.None)
        | (Bookmarked.Contains(step.Id) ? StepMarks.Bookmarked : StepMarks.None)
        | (Breakpointed.Contains(step.Id) ? StepMarks.Breakpoint : StepMarks.None)
        | (Warned.Contains(step.Id) ? StepMarks.Warning : StepMarks.None)
        | (IsStoppedAt(step) ? StepMarks.Current : StepMarks.None);

    bool IsStoppedAt(StepView step) => StoppedAt is { } at && at.Script == Script && at.Function == _function && at.Step == step.Id;

    /// <summary>Sets a breakpoint on the step or takes it off. True when it is set after the call.</summary>
    public bool ToggleBreakpoint(int id) => Breakpoints.Toggle(BreakpointSource, id);

    /// <summary>Marks the step or takes its mark off. True when it is marked after the call.</summary>
    public bool ToggleBookmark(int id)
    {
        if (!_bookmarks.TryGetValue(_function, out var marked))
        {
            _bookmarks[_function] = marked = [];
        }

        return !marked.Remove(id) && marked.Add(id);
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
