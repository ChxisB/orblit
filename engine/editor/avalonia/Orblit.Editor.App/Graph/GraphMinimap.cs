using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Graph;

/// <summary>A small map of the whole graph with the part on show outlined. A press or a drag on it centers the view there.</summary>
internal sealed class GraphMinimap : Control
{
    internal const double MapWidth = 180;
    internal const double MapHeight = 120;

    const double Pad = 8;

    static readonly IBrush s_back = new ImmutableSolidColorBrush(Color.Parse("#d0181818"));
    static readonly IBrush s_step = new ImmutableSolidColorBrush(Color.Parse("#a0b0b0b0"));
    static readonly IPen s_view = new Pen(EditorBrushes.Accent, 1).ToImmutable();

    readonly GraphScene _scene;
    readonly GraphViewport _viewport;
    readonly Control _surface;

    public GraphMinimap(GraphScene scene, GraphViewport viewport, Control surface)
    {
        _scene = scene;
        _viewport = viewport;
        _surface = surface;
        Width = MapWidth;
        Height = MapHeight;
    }

    /// <summary>Raised after the view moved, so the surface can draw again.</summary>
    public event Action? Moved;

    /// <summary>The graph point under a point of the map.</summary>
    public Point ToGraph(Point map)
    {
        var (scale, origin, offset) = Mapping();
        return new Point(((map.X - offset.X) / scale) + origin.X, ((map.Y - offset.Y) / scale) + origin.Y);
    }

    public override void Render(DrawingContext context)
    {
        context.DrawRectangle(s_back, null, new Rect(Bounds.Size), 4);
        var (scale, origin, offset) = Mapping();
        Rect Map(Rect graph) => new((graph.X - origin.X) * scale + offset.X, (graph.Y - origin.Y) * scale + offset.Y, graph.Width * scale, graph.Height * scale);
        foreach (var step in _scene.Steps.Where(step => !_scene.IsHidden(step)))
        {
            context.DrawRectangle(s_step, null, Map(step.Bounds));
        }
        context.DrawRectangle(null, s_view, Map(_viewport.VisibleRect(_surface.Bounds.Size)));
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        e.Pointer.Capture(this);
        CenterAt(e.GetPosition(this));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            CenterAt(e.GetPosition(this));
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e) => e.Pointer.Capture(null);

    void CenterAt(Point map)
    {
        _viewport.CenterOn(ToGraph(map), _surface.Bounds.Size);
        InvalidateVisual();
        Moved?.Invoke();
    }

    // The map holds the content and the view, so the outline never leaves it.
    (double Scale, Point Origin, Point Offset) Mapping()
    {
        var area = _scene.ContentBounds.Union(_viewport.VisibleRect(_surface.Bounds.Size));
        var scale = Math.Min((MapWidth - (2 * Pad)) / Math.Max(area.Width, 1), (MapHeight - (2 * Pad)) / Math.Max(area.Height, 1));
        var offset = new Point((MapWidth - (area.Width * scale)) / 2, (MapHeight - (area.Height * scale)) / 2);
        return (scale, area.TopLeft, offset);
    }
}
