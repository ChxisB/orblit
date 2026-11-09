using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Orblit.Editor.Bridge;
using Vector2 = System.Numerics.Vector2;

namespace Orblit.Editor.App;

/// <summary>Reads the engine's latest frame as RGBA bytes. Returns false when there is no frame yet.</summary>
internal delegate bool FrameReader(out int width, out int height, ref byte[] rgba);

/// <summary>
/// Shows frames from a running engine. Each <see cref="Tick"/> runs one engine frame and keeps its pixels.
/// The control draws them as large as fit, with the frame's shape. A left drag raises <see cref="Orbited"/>,
/// a left click raises <see cref="Clicked"/> and the wheel raises <see cref="Zoomed"/>. A left drag that starts on a
/// part of the gizmo raises <see cref="GizmoDragged"/> and does not orbit, and its release raises
/// <see cref="GizmoReleased"/>. The mode says which parts are there. The caller creates, stops and disposes the
/// control on the UI thread, and stops it before the engine goes. While <see cref="PlanarSource"/> says yes the
/// gizmo is gone: a left drag that starts on the mark raises <see cref="PlanarMoved"/>, and any other left drag
/// raises <see cref="PlanarPanned"/> and does not orbit.
/// </summary>
internal sealed class ViewportControl(Func<bool> iterate, FrameReader read) : Control
{
    const float RadiansPerPixel = 0.01f;

    // A press that moves less than this is a click. Hands shake a little.
    const double ClickSlop = 4;

    const double TipRadius = 5;
    const double TipHalf = 4;
    const double CentreHalf = 5;
    const double HandleWidth = 3;
    const double OuterWidth = 2;

    // The mark of the selected 2D entity: a ring with a cross, in the orange of the selection box in 3D.
    const double MarkRadius = 7;
    const double MarkArm = 12;

    // A press this near the mark, in control pixels, takes it.
    const double MarkGrab = 12;

    static readonly IBrush CentreBrush = new ImmutableSolidColorBrush(Color.Parse("#e0e0e0"));
    static readonly IPen OuterPen = new Pen(new ImmutableSolidColorBrush(Color.Parse("#b3ffffff")), OuterWidth).ToImmutable();
    static readonly IBrush BallBrush = new ImmutableSolidColorBrush(Color.Parse("#26ffffff"));
    static readonly IPen MarkPen = new Pen(new ImmutableSolidColorBrush(Color.Parse("#ff8000")), HandleWidth).ToImmutable();

    // In the order of GizmoAxis, with the colours of the axis gizmo.
    static readonly IBrush[] AxisBrushes =
    [
        new ImmutableSolidColorBrush(Color.Parse("#f63252")),
        new ImmutableSolidColorBrush(Color.Parse("#87d703")),
        new ImmutableSolidColorBrush(Color.Parse("#288cf5")),
    ];

    static readonly IPen[] AxisPens = [.. AxisBrushes.Select(brush => new Pen(brush, HandleWidth).ToImmutable())];

    static readonly GizmoAxis[] Axes = Enum.GetValues<GizmoAxis>();

    // The engine window takes a few frames to change size, so a new ask waits. A window too small to see
    // is a layout that has not run yet.
    const int TicksBetweenAsks = 8;
    const int SmallestSide = 64;

    byte[] _rgba = [];
    PixelSize _asked;
    int _ticksSinceAsked = TicksBetweenAsks;
    bool _running;
    Point? _dragFrom;
    Point? _pressedAt;
    GizmoHandles? _handles;
    GizmoHit? _grab;
    GizmoRings? _rings;
    Vector2? _mark;
    bool _markGrab;

    /// <summary>Asked for whether the view is the 2D one. It is the 3D one until set.</summary>
    internal Func<bool> PlanarSource { get; set; } = () => false;

    /// <summary>Asked on each tick, while the view is 2D, for the pivot to mark in the frame's pixels. Null from it marks none.</summary>
    internal Func<Vector2?>? MarkSource { get; set; }

    /// <summary>Asked for the mode, which says what the gizmo shows and takes presses on. Transform until set.</summary>
    internal Func<GizmoMode> ModeSource { get; set; } = () => GizmoMode.Transform;

    /// <summary>Asked for whether Rotate mode has its trackball, the ball inside the outer ring. Off until set.</summary>
    internal Func<bool> TrackballSource { get; set; } = () => false;

    /// <summary>Asked on each tick for the rotate rings to draw, in the frame's pixels. Null from it draws none.</summary>
    internal Func<GizmoRings?>? RingsSource { get; set; }

    /// <summary>Asked on each tick for the move handles to draw, in the frame's pixels. Null from it draws none.</summary>
    internal Func<GizmoHandles?>? HandlesSource { get; set; }

    /// <summary>The newest frame, or null before the first one.</summary>
    internal WriteableBitmap? Frame { get; private set; }

    /// <summary>True while the control pumps the engine each animation frame.</summary>
    internal bool IsRunning => _running;

    /// <summary>Raised once, when the engine asks to quit.</summary>
    public event Action? Ended;

    /// <summary>Raised while the left button drags, with the yaw and pitch change in radians.</summary>
    public event Action<float, float>? Orbited;

    /// <summary>Raised when the left button goes up near where it went down, with the point in the frame's pixels.</summary>
    public event Action<Vector2>? Clicked;

    /// <summary>Raised while a part of the gizmo drags, with the pointer move since the last event in the frame's pixels.</summary>
    internal event Action<GizmoDrag>? GizmoDragged;

    /// <summary>Raised when the drag of a gizmo part ends.</summary>
    internal event Action? GizmoReleased;

    /// <summary>Raised when the frame is not the size of the control, with the size the engine should draw.</summary>
    public event Action<int, int>? SizeWanted;

    /// <summary>Raised on a wheel turn, with the notches turned. Positive is toward the user.</summary>
    public event Action<float>? Zoomed;

    /// <summary>Raised while a 2D drag pans the view, with the pointer move since the last event in the frame's pixels.</summary>
    internal event Action<Vector2>? PlanarPanned;

    /// <summary>Raised while a drag that started on the mark moves, with the pointer move since the last event in the frame's pixels.</summary>
    internal event Action<FrameMove>? PlanarMoved;

    /// <summary>Runs one engine frame and keeps its pixels. Stops the control when the engine asks to quit.</summary>
    public void Tick()
    {
        if (iterate())
        {
            Stop();
            Ended?.Invoke();
            return;
        }
        var mode = ModeSource();
        var planar = PlanarSource();
        _handles = !planar && GizmoZones.HasHandles(mode) ? HandlesSource?.Invoke() : null;
        _rings = !planar && GizmoZones.HasRings(mode) ? RingsSource?.Invoke() : null;
        _mark = planar ? MarkSource?.Invoke() : null;
        if (read(out var width, out var height, ref _rgba))
        {
            Keep(width, height);
        }

        AskForOwnSize();
    }

    public void Stop() => _running = false;

    void AskForOwnSize()
    {
        _ticksSinceAsked++;
        // Bounds are device independent. The engine draws in pixels, so a 2x screen needs twice the frame.
        var scale = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var want = new PixelSize((int)(Bounds.Width * scale), (int)(Bounds.Height * scale));
        if (want == _asked || want == Frame?.PixelSize || _ticksSinceAsked < TicksBetweenAsks
            || Math.Min(want.Width, want.Height) < SmallestSide)
        {
            return;
        }

        _asked = want;
        _ticksSinceAsked = 0;
        SizeWanted?.Invoke(want.Width, want.Height);
    }

    /// <summary>Where a frame is drawn inside the control: centred, scaled to fit, shape kept.</summary>
    internal static Rect FitRect(Size frame, Size bounds)
    {
        if (frame.Width <= 0 || frame.Height <= 0)
        {
            return default;
        }

        var scale = Math.Min(bounds.Width / frame.Width, bounds.Height / frame.Height);
        var size = new Size(frame.Width * scale, frame.Height * scale);
        return new Rect(new Point((bounds.Width - size.Width) / 2, (bounds.Height - size.Height) / 2), size);
    }

    /// <summary>
    /// A point of the control as a pixel of the frame drawn in it. Null when the point is outside the frame, which
    /// leaves bars beside it when the control's shape differs.
    /// </summary>
    internal static Vector2? FrameAt(Point control, Size frame, Size bounds)
    {
        var fit = FitRect(frame, bounds);
        if (fit.Width <= 0 || !fit.Contains(control))
        {
            return null;
        }

        var scale = frame.Width / fit.Width;
        return new Vector2((float)((control.X - fit.X) * scale), (float)((control.Y - fit.Y) * scale));
    }

    /// <summary>A pixel of the frame as a point of the control it is drawn in. The inverse of <see cref="FrameAt"/>, also outside the frame.</summary>
    internal static Vector2 ControlAt(Vector2 framePoint, Size frame, Size bounds)
    {
        var fit = FitRect(frame, bounds);
        var scale = fit.Width <= 0 ? 0 : (float)(fit.Width / frame.Width);
        return new Vector2((float)fit.X, (float)fit.Y) + framePoint * scale;
    }

    /// <summary>The part of the gizmo a press at this point of the control takes, or null for none. See <see cref="GizmoZones"/>.</summary>
    internal GizmoHit? HitAt(Point press) =>
        GizmoZones.HitAt(new GizmoShown(ModeSource(), TrackballSource(), ControlHandles(), ControlRings()), new Vector2((float)press.X, (float)press.Y));

    GizmoHandles? ControlHandles() => _handles is { } handles && Frame is not null
        ? new GizmoHandles(Control(handles.Origin), Control(handles.X), Control(handles.Y), Control(handles.Z))
        : null;

    GizmoRings? ControlRings() => _rings is { } rings && Frame is not null
        ? new GizmoRings(Control(rings.Origin), Control(rings.X), Control(rings.Y), Control(rings.Z)) { Outer = Control(rings.Outer) }
        : null;

    bool OnMark(Point press) =>
        _mark is { } mark && Frame is not null && Vector2.Distance(Control(mark), new Vector2((float)press.X, (float)press.Y)) <= MarkGrab;

    Vector2 Control(Vector2 framePoint) => ControlAt(framePoint, Frame!.Size, Bounds.Size);

    Vector2[] Control(Vector2[] ring) => [.. ring.Select(Control)];

    public override void Render(DrawingContext context)
    {
        // A transparent fill makes the whole control take the pointer, even before a frame arrives.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
        if (Frame is not null)
        {
            context.DrawImage(Frame, new Rect(Frame.Size), FitRect(Frame.Size, Bounds.Size));
            var mode = ModeSource();
            if (ControlRings() is { } rings)
            {
                DrawRings(context, rings, mode == GizmoMode.Rotate && TrackballSource());
            }

            if (ControlHandles() is { } handles)
            {
                DrawHandles(context, handles, mode);
            }

            if (_mark is { } mark)
            {
                DrawMark(context, Control(mark));
            }
        }
    }

    static void DrawMark(DrawingContext context, Vector2 at)
    {
        var centre = new Point(at.X, at.Y);
        context.DrawEllipse(null, MarkPen, centre, MarkRadius, MarkRadius);
        context.DrawLine(MarkPen, new Point(at.X - MarkArm, at.Y), new Point(at.X + MarkArm, at.Y));
        context.DrawLine(MarkPen, new Point(at.X, at.Y - MarkArm), new Point(at.X, at.Y + MarkArm));
    }

    static void DrawRings(DrawingContext context, GizmoRings rings, bool trackball)
    {
        if (rings.Outer.Length > 0)
        {
            if (trackball)
            {
                var radius = rings.Outer.Average(point => Vector2.Distance(point, rings.Origin));
                context.DrawEllipse(BallBrush, null, new Point(rings.Origin.X, rings.Origin.Y), radius, radius);
            }
            DrawLoop(context, OuterPen, rings.Outer);
        }

        foreach (var axis in Axes)
        {
            DrawLoop(context, AxisPens[(int)axis], rings.Of(axis));
        }
    }

    static void DrawLoop(DrawingContext context, IPen pen, Vector2[] ring)
    {
        for (var i = 0; i < ring.Length; i++)
        {
            var from = ring[i];
            var to = ring[(i + 1) % ring.Length];
            context.DrawLine(pen, new Point(from.X, from.Y), new Point(to.X, to.Y));
        }
    }

    // Move mode ends its lines in dots. The modes that also stretch end them in boxes, and Scale mode has a box
    // at the pivot that stretches all three axes.
    static void DrawHandles(DrawingContext context, GizmoHandles handles, GizmoMode mode)
    {
        var origin = new Point(handles.Origin.X, handles.Origin.Y);
        foreach (var axis in Axes)
        {
            var tip = new Point(handles.Tip(axis).X, handles.Tip(axis).Y);
            context.DrawLine(AxisPens[(int)axis], origin, tip);
            if (mode == GizmoMode.Move)
            {
                context.DrawEllipse(AxisBrushes[(int)axis], null, tip, TipRadius, TipRadius);
            }
            else
            {
                context.DrawRectangle(AxisBrushes[(int)axis], null, Box(tip, TipHalf));
            }
        }

        if (mode == GizmoMode.Scale)
        {
            context.DrawRectangle(CentreBrush, null, Box(origin, CentreHalf));
        }
    }

    static Rect Box(Point centre, double half) => new(centre.X - half, centre.Y - half, half * 2, half * 2);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var at = e.GetPosition(this);
            _grab = HitAt(at);
            _markGrab = _grab is null && OnMark(at);
            _dragFrom = at;
            // A grab is a drag from the first pixel, so it is never a click.
            _pressedAt = _grab is null && !_markGrab ? at : null;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragFrom is not { } from)
        {
            return;
        }

        var to = e.GetPosition(this);
        if (_grab is not null || _markGrab)
        {
            DragGrab(from, to, (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0);
            _dragFrom = to;
            e.Handled = true;
            return;
        }

        if (_pressedAt is { } pressed)
        {
            // Hold the camera still until the press is a drag, so a click does not nudge the view.
            if (double.Hypot(to.X - pressed.X, to.Y - pressed.Y) < ClickSlop)
            {
                e.Handled = true;
                return;
            }
            _pressedAt = null;
        }
        _dragFrom = to;
        Turn(from, to);
        e.Handled = true;
    }

    void DragGrab(Point from, Point to, bool inverts)
    {
        if (MoveOf(from, to) is not { } move)
        {
            return;
        }

        if (_grab is { } grab)
        {
            GizmoDragged?.Invoke(new GizmoDrag(grab, move, inverts));
        }
        else
        {
            PlanarMoved?.Invoke(move);
        }
    }

    // A drag on nothing turns the 3D view and pans the 2D one.
    void Turn(Point from, Point to)
    {
        if (!PlanarSource())
        {
            // Dragging right turns the scene right, so the camera goes left.
            Orbited?.Invoke(-(float)(to.X - from.X) * RadiansPerPixel, (float)(to.Y - from.Y) * RadiansPerPixel);
        }
        else if (MoveOf(from, to) is { } move)
        {
            PlanarPanned?.Invoke(move.Delta);
        }
    }

    // The drag is in control pixels and the engine wants frame pixels, which differ when the frame is scaled to fit.
    // Its points stay outside the frame too, because the pointer is captured.
    FrameMove? MoveOf(Point from, Point to)
    {
        if (Frame is null || FitRect(Frame.Size, Bounds.Size) is not { Width: > 0 } fit)
        {
            return null;
        }

        var scale = Frame.Size.Width / fit.Width;
        Vector2 At(Point control) => new((float)((control.X - fit.X) * scale), (float)((control.Y - fit.Y) * scale));
        return new FrameMove(At(from), At(to));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left && _pressedAt is not null
            && Frame is not null && FrameAt(e.GetPosition(this), Frame.Size, Bounds.Size) is { } point)
        {
            Clicked?.Invoke(point);
        }
        _pressedAt = null;
    }

    // Avalonia drops the capture when the button goes up, so this ends a drag on release too.
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragFrom = null;
        _pressedAt = null;
        if (_grab is not null)
        {
            _grab = null;
            GizmoReleased?.Invoke();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Zoomed?.Invoke((float)e.Delta.Y);
        e.Handled = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _running = true;
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        Stop();
        base.OnDetachedFromVisualTree(e);
    }

    void OnFrame(TimeSpan time)
    {
        if (!_running)
        {
            return;
        }

        Tick();
        if (_running)
        {
            TopLevel.GetTopLevel(this)?.RequestAnimationFrame(OnFrame);
        }
    }

    void Keep(int width, int height)
    {
        var size = new PixelSize(width, height);
        if (Frame is null || Frame.PixelSize != size)
        {
            Frame?.Dispose();
            Frame = new WriteableBitmap(size, new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Opaque);
        }

        using (var pixels = Frame.Lock())
        {
            CopyRows(_rgba, width, height, pixels.Address, pixels.RowBytes);
        }
        InvalidateVisual();
    }

    /// <summary>Copies packed RGBA rows into memory whose rows are rowBytes apart. The padding is left alone.</summary>
    internal static void CopyRows(byte[] rgba, int width, int height, nint destination, int rowBytes)
    {
        var row = width * 4;
        for (var y = 0; y < height; y++)
        {
            Marshal.Copy(rgba, y * row, destination + y * rowBytes, row);
        }
    }
}
