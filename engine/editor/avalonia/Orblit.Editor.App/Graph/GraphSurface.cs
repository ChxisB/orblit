using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Graph;

/// <summary>
/// Draws the graph and passes the pointer and the keys on to the gestures and edits. It holds no state of its
/// own beyond what it is given, so everything it shows can be asked of the scene.
/// </summary>
internal sealed class GraphSurface : Control
{
    const double GridStep = 20;
    const double MinGridZoom = 0.4;
    const double WheelZoom = 1.1;

    static readonly IPen s_grid = new Pen(new ImmutableSolidColorBrush(Color.Parse("#26ffffff")), 1).ToImmutable();
    static readonly IPen s_band = new Pen(EditorBrushes.Accent, 1, new DashStyle([4, 3], 0)).ToImmutable();
    static readonly IBrush s_bandFill = new ImmutableSolidColorBrush(Color.Parse("#2a3d8fff"));

    readonly GraphScene _scene;
    readonly GraphViewport _viewport;
    readonly GraphGestures _gestures;
    readonly GraphEdits _edits;
    Point _pointer;

    public GraphSurface(GraphScene scene, GraphViewport viewport, GraphGestures gestures, GraphEdits edits)
    {
        _scene = scene;
        _viewport = viewport;
        _gestures = gestures;
        _edits = edits;
        Focusable = true;
        ClipToBounds = true;
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    /// <summary>Raised after the pointer or the keys changed what shows, so a minimap can follow.</summary>
    public event Action? Interacted;

    public override void Render(DrawingContext context)
    {
        context.DrawRectangle(EditorBrushes.Window, null, new Rect(Bounds.Size));
        DrawGrid(context);
        using (context.PushTransform(_viewport.Matrix))
        {
            DrawContent(context, _viewport.VisibleRect(Bounds.Size));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        var button = point.Properties.PointerUpdateKind switch
        {
            PointerUpdateKind.MiddleButtonPressed => MouseButton.Middle,
            PointerUpdateKind.RightButtonPressed => MouseButton.Right,
            _ => MouseButton.Left,
        };
        Focus();
        e.Pointer.Capture(this);
        _gestures.Press(point.Position, button, e.KeyModifiers, e.ClickCount);
        Changed();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        _pointer = e.GetPosition(this);
        if (_gestures.IsActive)
        {
            _gestures.Move(_pointer);
            Changed();
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (!_gestures.IsActive)
        {
            return;
        }
        e.Pointer.Capture(null);
        _gestures.Release(e.GetPosition(this));
        Changed();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        _viewport.ZoomAt(e.GetPosition(this), e.Delta.Y > 0 ? WheelZoom : 1 / WheelZoom);
        Changed();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        var handled = true;
        if (e.Key is Key.Delete or Key.Back)
        {
            _edits.DeleteSelection();
        }
        else if (e.Key == Key.Escape)
        {
            _gestures.Cancel();
        }
        else if (e.Key == Key.G && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0)
        {
            _edits.FrameSelection();
        }
        else if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.None)
        {
            _gestures.RequestPalette(_pointer);
        }
        else
        {
            handled = false;
        }
        e.Handled = handled;
        Changed();
    }

    // A drop carries something from another panel: a file, an entity, a variable. It becomes the step that reads it.
    void OnDrop(object? sender, DragEventArgs e)
    {
        if (GraphDrop.TryParse(e.DataTransfer.TryGetValue(GraphDrop.Format), out var drop))
        {
            _edits.AddStep(drop.ToEntry(), _viewport.ToGraph(e.GetPosition(this)), null);
            Changed();
        }
    }

    void Changed()
    {
        InvalidateVisual();
        Interacted?.Invoke();
    }

    void DrawGrid(DrawingContext context)
    {
        var step = GridStep * _viewport.Zoom;
        if (_viewport.Zoom < MinGridZoom)
        {
            return;
        }
        for (var x = Mod(_viewport.Pan.X, step); x < Bounds.Width; x += step)
        {
            context.DrawLine(s_grid, new Point(x, 0), new Point(x, Bounds.Height));
        }
        for (var y = Mod(_viewport.Pan.Y, step); y < Bounds.Height; y += step)
        {
            context.DrawLine(s_grid, new Point(0, y), new Point(Bounds.Width, y));
        }
    }

    static double Mod(double value, double step) => ((value % step) + step) % step;

    void DrawContent(DrawingContext context, Rect view)
    {
        foreach (var frame in _scene.Frames.Where(frame => frame.Bounds.Intersects(view)))
        {
            frame.Draw(context, frame.Id == _scene.SelectedFrame);
        }
        foreach (var wire in _scene.VisibleWires())
        {
            WireLayer.Draw(context, wire.From, wire.To, wire.Brush, _scene.Style, wire.Wire == _scene.SelectedWire);
        }
        foreach (var step in _scene.Steps.Where(step => !_scene.IsHidden(step) && step.Bounds.Intersects(view)))
        {
            step.Draw(context, _scene.SelectedSteps.Contains(step.Id));
        }
        DrawPendingWire(context);
        if (_gestures.Band is { } band)
        {
            context.DrawRectangle(s_bandFill, s_band, band);
        }
    }

    void DrawPendingWire(DrawingContext context)
    {
        if (_gestures.PendingWire is not { } pending || _scene.Find(pending.From.Step) is not { } step)
        {
            return;
        }
        var pin = step.PinCenter(pending.From.IsInput, pending.From.Port);
        var (from, to) = pending.From.IsInput ? (pending.To, pin) : (pin, pending.To);
        WireLayer.Draw(context, from, to, EditorBrushes.Accent, _scene.Style, selected: false);
    }
}
