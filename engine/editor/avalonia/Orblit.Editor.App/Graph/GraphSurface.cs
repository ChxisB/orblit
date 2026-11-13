using System.Globalization;
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
    const double LabelSize = 12;
    const double LabelPad = 4;
    const double LabelGap = 14;
    const int LabelChars = 60;

    static readonly IPen s_grid = new Pen(new ImmutableSolidColorBrush(Color.Parse("#26ffffff")), 1).ToImmutable();
    static readonly IPen s_band = new Pen(EditorBrushes.Accent, 1, new DashStyle([4, 3], 0)).ToImmutable();
    static readonly IBrush s_bandFill = new ImmutableSolidColorBrush(Color.Parse("#2a3d8fff"));
    static readonly IPen s_labelEdge = new Pen(EditorBrushes.Accent, 1).ToImmutable();

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

    /// <summary>The hover that shows the value of a wire while a game is stopped, given by the canvas that owns the surface.</summary>
    internal WireHover? Hover { get; set; }

    /// <summary>The commands of the keyboard, given by the canvas that owns the surface.</summary>
    internal GraphShortcuts? Shortcuts { get; set; }

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
        else if (Hover is { } hover)
        {
            var before = hover.Label;
            hover.Move(_viewport.ToGraph(_pointer));
            if (hover.Label != before)
            {
                InvalidateVisual();
            }
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
        e.Handled = Shortcuts?.TryRun(e.Key, e.KeyModifiers, _viewport.ToGraph(_pointer)) == true || RunPlainKey(e);
        Changed();
    }

    bool RunPlainKey(KeyEventArgs e)
    {
        if (e.Key is Key.Delete or Key.Back)
        {
            _edits.DeleteSelection();
        }
        else if (e.Key == Key.Escape)
        {
            _gestures.Cancel();
        }
        else if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.None)
        {
            _gestures.RequestPalette(_pointer);
        }
        else
        {
            return false;
        }

        return true;
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
            step.Draw(context, _scene.MarksOf(step));
        }
        DrawPendingWire(context);
        if (Hover?.Label is { } label)
        {
            DrawLabel(context, label);
        }
        if (_gestures.Band is { } band)
        {
            context.DrawRectangle(s_bandFill, s_band, band);
        }
    }

    static void DrawLabel(DrawingContext context, HoverLabel label)
    {
        var shown = label.Text.Length > LabelChars ? label.Text[..LabelChars] + "..." : label.Text;
        var text = new FormattedText(shown, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default), LabelSize, EditorBrushes.Text);
        var box = new Rect(label.At.X - text.Width - (2 * LabelPad) - LabelGap, label.At.Y - text.Height - (2 * LabelPad) - LabelPad, text.Width + (2 * LabelPad), text.Height + (2 * LabelPad));
        context.DrawRectangle(EditorBrushes.Panel, s_labelEdge, box, 3, 3);
        context.DrawText(text, box.TopLeft + new Point(LabelPad, LabelPad));
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
