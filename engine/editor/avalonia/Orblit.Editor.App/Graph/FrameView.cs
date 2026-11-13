using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Orblit.Editor.App.Theme;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App.Graph;

internal enum FramePart
{
    Title,
    Toggle,
    Grip,
}

/// <summary>
/// A frame as the canvas shows it: a title bar with a button that folds it, and a grip at the corner that resizes
/// it. A folded frame shows only its bar and hides the steps inside it. A step is inside when its center is.
/// </summary>
internal sealed class FrameView
{
    internal const double TitleHeight = 26;
    internal const double MinWidth = 80;
    internal const double MinHeight = 50;

    const double Corner = 6;
    const double ButtonSize = 18;
    const double GripSize = 14;

    static readonly IBrush s_fill = new ImmutableSolidColorBrush(Color.Parse("#30505a6a"));
    static readonly IBrush s_bar = new ImmutableSolidColorBrush(Color.Parse("#70505a6a"));
    static readonly IPen s_border = new Pen(new ImmutableSolidColorBrush(Color.Parse("#6a7686")), 1).ToImmutable();
    static readonly IPen s_selected = new Pen(EditorBrushes.Accent, 2).ToImmutable();
    static readonly Typeface s_face = new(FontFamily.Default, FontStyle.Normal, FontWeight.SemiBold);

    FormattedText? _title;

    public FrameView(GraphFrame frame) => Frame = frame;

    public GraphFrame Frame { get; }

    public int Id => Frame.Id;

    /// <summary>How far a drag has moved the frame on show.</summary>
    public Vector Offset { get; set; }

    /// <summary>How much a drag has grown the frame on show.</summary>
    public Vector Growth { get; set; }

    /// <summary>The whole frame, also when it is folded. Steps inside it are the ones this holds.</summary>
    public Rect FullBounds => new(
        Frame.X + Offset.X,
        Frame.Y + Offset.Y,
        Math.Max(Frame.Width + Growth.X, MinWidth),
        Math.Max(Frame.Height + Growth.Y, MinHeight));

    /// <summary>What shows: the title bar of a folded frame, else all of it.</summary>
    public Rect Bounds => Frame.Collapsed ? new Rect(FullBounds.X, FullBounds.Y, FullBounds.Width, TitleHeight) : FullBounds;

    public Rect TitleRect => new(Bounds.X, Bounds.Y, Bounds.Width, TitleHeight);

    public Rect ToggleRect => new(Bounds.Right - ButtonSize - 4, Bounds.Y + ((TitleHeight - ButtonSize) / 2), ButtonSize, ButtonSize);

    Rect GripRect => new(Bounds.Right - GripSize, Bounds.Bottom - GripSize, GripSize, GripSize);

    public bool Holds(StepView step) => FullBounds.Contains(step.Bounds.Center);

    /// <summary>The part of the frame a point lands on. Null for a point on the body or off the frame.</summary>
    public FramePart? PartAt(Point point)
    {
        if (ToggleRect.Contains(point))
        {
            return FramePart.Toggle;
        }
        if (TitleRect.Contains(point))
        {
            return FramePart.Title;
        }
        return !Frame.Collapsed && GripRect.Contains(point) ? FramePart.Grip : null;
    }

    public void Draw(DrawingContext context, bool selected)
    {
        var bounds = Bounds;
        context.DrawRectangle(s_fill, selected ? s_selected : s_border, new RoundedRect(bounds, Corner));
        context.DrawRectangle(s_bar, null, new RoundedRect(TitleRect, new CornerRadius(Corner, Corner, Frame.Collapsed ? Corner : 0, Frame.Collapsed ? Corner : 0)));
        _title ??= new FormattedText(Frame.Title, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, s_face, 12, EditorBrushes.Text);
        context.DrawText(_title, new Point(bounds.X + 10, bounds.Y + 5));
        DrawToggle(context);
        if (!Frame.Collapsed)
        {
            DrawGrip(context);
        }
    }

    void DrawToggle(DrawingContext context)
    {
        var box = ToggleRect;
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            // A folded frame points right and an open one points down.
            var points = Frame.Collapsed
                ? new[] { new Point(box.X + 6, box.Y + 4), new Point(box.X + 13, box.Y + 9), new Point(box.X + 6, box.Y + 14) }
                : new[] { new Point(box.X + 4, box.Y + 6), new Point(box.X + 14, box.Y + 6), new Point(box.X + 9, box.Y + 13) };
            path.BeginFigure(points[0], true);
            path.LineTo(points[1]);
            path.LineTo(points[2]);
            path.EndFigure(true);
        }
        context.DrawGeometry(EditorBrushes.Text, null, geometry);
    }

    void DrawGrip(DrawingContext context)
    {
        var grip = GripRect;
        var pen = s_border;
        context.DrawLine(pen, new Point(grip.Right - 2, grip.Y + 4), new Point(grip.X + 4, grip.Bottom - 2));
        context.DrawLine(pen, new Point(grip.Right - 2, grip.Y + 9), new Point(grip.X + 9, grip.Bottom - 2));
    }
}
