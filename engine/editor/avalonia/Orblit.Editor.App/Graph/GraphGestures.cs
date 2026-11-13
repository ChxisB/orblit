using Avalonia;
using Avalonia.Input;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// What a pointer does on the canvas. A press picks the gesture from what is under it. Moves show the effect
/// without touching the engine, and the release makes the one edit. Points are screen points.
/// </summary>
internal sealed class GraphGestures
{
    const double DragSlop = 3;

    enum Mode
    {
        None,
        Pan,
        Steps,
        Wire,
        Band,
        FrameMove,
        FrameResize,
    }

    readonly GraphScene _scene;
    readonly GraphViewport _viewport;
    readonly GraphEdits _edits;
    Mode _mode;
    Point _pressScreen;
    Point _lastScreen;
    Point _pressGraph;
    bool _moved;
    bool _bandAdditive;
    List<int> _members = [];
    FrameView? _frame;
    PinRef _wireFrom;
    MouseButton _button;

    public GraphGestures(GraphScene scene, GraphViewport viewport, GraphEdits edits)
    {
        _scene = scene;
        _viewport = viewport;
        _edits = edits;
    }

    /// <summary>Raised when a press starts, before it does anything. A refusal shown for the last gesture can go.</summary>
    public event Action? Pressing;

    /// <summary>Raised when a press lands on a value box. The canvas puts a text box over it.</summary>
    public event Action<StepView, string>? ValueEditRequested;

    /// <summary>
    /// Raised with a screen point when a step is asked for there. The pin is the one a wire was dragged from, or
    /// null when there was no wire.
    /// </summary>
    public event Action<Point, PinRef?>? PaletteRequested;

    /// <summary>Raised when a frame title is pressed twice. The canvas puts a text box over it.</summary>
    public event Action<FrameView>? TitleEditRequested;

    /// <summary>The box a drag on empty space is selecting, in graph points.</summary>
    public Rect? Band { get; private set; }

    /// <summary>The wire being dragged from a pin, with the graph point its loose end is at.</summary>
    public (PinRef From, Point To)? PendingWire => _mode == Mode.Wire ? (_wireFrom, _viewport.ToGraph(_lastScreen)) : null;

    public bool IsActive => _mode != Mode.None;

    public void Press(Point screen, MouseButton button, KeyModifiers keys, int clicks)
    {
        Pressing?.Invoke();
        _pressScreen = _lastScreen = screen;
        _pressGraph = _viewport.ToGraph(screen);
        _moved = false;
        _button = button;
        if (button != MouseButton.Left)
        {
            _mode = Mode.Pan;
        }
        else if (_scene.HitStep(_pressGraph) is { } hit)
        {
            PressStep(hit.Step, hit.Hit, keys);
        }
        else if (_scene.HitFrame(_pressGraph) is { } frame)
        {
            PressFrame(frame.Frame, frame.Part, clicks);
        }
        else if (_scene.WireAt(_pressGraph) is { } wire)
        {
            PressWire(wire, clicks);
        }
        else
        {
            PressEmpty(keys);
        }
    }

    public void Move(Point screen)
    {
        if (_mode == Mode.None)
        {
            return;
        }
        var screenDelta = screen - _pressScreen;
        _moved |= Math.Abs(screenDelta.X) + Math.Abs(screenDelta.Y) > DragSlop;
        var graphDelta = screenDelta / _viewport.Zoom;
        switch (_mode)
        {
            case Mode.Pan:
                _viewport.PanBy(screen - _lastScreen);
                break;
            case Mode.Steps:
                SetStepOffsets(_members, graphDelta);
                break;
            case Mode.FrameMove:
                _frame!.Offset = graphDelta;
                SetStepOffsets(_members, graphDelta);
                break;
            case Mode.FrameResize:
                _frame!.Growth = graphDelta;
                break;
            case Mode.Band:
                Band = Normalized(_pressGraph, _viewport.ToGraph(screen));
                break;
        }
        _lastScreen = screen;
    }

    public void Release(Point screen)
    {
        Move(screen);
        var graphDelta = (screen - _pressScreen) / _viewport.Zoom;
        var mode = _mode;
        var moved = _moved;
        var members = _members;
        var frame = _frame;
        var wireFrom = _wireFrom;
        Cancel();
        switch (mode)
        {
            case Mode.Steps when moved:
                _edits.MoveSteps(members, graphDelta);
                break;
            case Mode.FrameMove when moved:
                _edits.MoveFrame(frame!, graphDelta, members);
                break;
            case Mode.FrameResize when moved:
                _edits.ResizeFrame(frame!, graphDelta);
                break;
            case Mode.Wire when _scene.PinAt(_viewport.ToGraph(screen)) is { } target:
                _edits.Connect(wireFrom, target);
                break;
            case Mode.Band:
                _scene.SelectWithin(Normalized(_pressGraph, _viewport.ToGraph(screen)), additive: _bandAdditive);
                break;
            default:
                AskForStep(mode, moved, wireFrom, screen);
                break;
        }
    }

    public void RequestPalette(Point screen, PinRef? from = null) => PaletteRequested?.Invoke(screen, from);

    /// <summary>Drops the gesture and puts what it moved back, as Escape does.</summary>
    public void Cancel()
    {
        _mode = Mode.None;
        Band = null;
        _frame = null;
        _members = [];
        foreach (var step in _scene.Steps)
        {
            step.Offset = default;
        }
        foreach (var frame in _scene.Frames)
        {
            frame.Offset = default;
            frame.Growth = default;
        }
    }

    // A wire let go over empty space, or a right click that did not drag, asks for a step to put there. A press
    // keeps its button until the next press, so the release can still read it.
    void AskForStep(Mode mode, bool moved, PinRef wireFrom, Point screen)
    {
        if (mode == Mode.Wire && moved && _scene.HitStep(_viewport.ToGraph(screen)) is null)
        {
            RequestPalette(screen, wireFrom);
        }
        else if (mode == Mode.Pan && !moved && _button == MouseButton.Right)
        {
            RequestPalette(screen);
        }
    }

    static Rect Normalized(Point a, Point b) => new(new Point(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y)), new Point(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y)));

    static bool Additive(KeyModifiers keys) => (keys & (KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Meta)) != 0;

    void PressStep(StepView step, StepHit hit, KeyModifiers keys)
    {
        switch (hit.Part)
        {
            case StepPart.Pin:
                _mode = Mode.Wire;
                _wireFrom = hit.Pin;
                break;
            case StepPart.Value:
                _scene.SelectStep(step.Id, toggle: false);
                ValueEditRequested?.Invoke(step, hit.Parameter);
                break;
            case StepPart.Grow:
                _edits.ChangePins(step.Id, 1);
                break;
            case StepPart.Shrink:
                _edits.ChangePins(step.Id, -1);
                break;
            case StepPart.Margin:
                _scene.ToggleBreakpoint(step.Id);
                break;
            default:
                _scene.SelectStep(step.Id, Additive(keys));
                _members = [.. _scene.SelectedSteps];
                _mode = Mode.Steps;
                break;
        }
    }

    void PressFrame(FrameView frame, FramePart part, int clicks)
    {
        if (part == FramePart.Toggle)
        {
            _edits.ToggleFrame(frame);
            return;
        }
        _scene.SelectFrame(frame.Id);
        if (part == FramePart.Grip)
        {
            _mode = Mode.FrameResize;
        }
        else if (clicks >= 2)
        {
            TitleEditRequested?.Invoke(frame);
            return;
        }
        else
        {
            _mode = Mode.FrameMove;
            _members = _scene.Members(frame).Select(step => step.Id).ToList();
        }
        _frame = frame;
    }

    void PressWire(GraphWire wire, int clicks)
    {
        _scene.SelectWire(wire);
        if (clicks >= 2)
        {
            _edits.InsertReroute(wire, _pressGraph);
        }
    }

    void PressEmpty(KeyModifiers keys)
    {
        _bandAdditive = Additive(keys);
        if (!_bandAdditive)
        {
            _scene.Clear();
        }
        _mode = Mode.Band;
        Band = new Rect(_pressGraph, new Size(0, 0));
    }

    void SetStepOffsets(IEnumerable<int> ids, Vector offset)
    {
        foreach (var step in ids.Select(_scene.Find).OfType<StepView>())
        {
            step.Offset = offset;
        }
    }
}
