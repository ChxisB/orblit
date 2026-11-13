using Avalonia;

namespace Orblit.Editor.App.Graph;

/// <summary>Where the canvas looks into the graph: a zoom and a pan. Graph points and screen points convert both ways.</summary>
internal sealed class GraphViewport
{
    internal const double MinZoom = 0.2;
    internal const double MaxZoom = 3;

    const double FitPadding = 40;

    public double Zoom { get; private set; } = 1;

    /// <summary>Where the graph origin falls on the screen.</summary>
    public Vector Pan { get; private set; }

    public Matrix Matrix => Matrix.CreateScale(Zoom, Zoom) * Matrix.CreateTranslation(Pan.X, Pan.Y);

    public Point ToGraph(Point screen) => new((screen.X - Pan.X) / Zoom, (screen.Y - Pan.Y) / Zoom);

    public Point ToScreen(Point graph) => new((graph.X * Zoom) + Pan.X, (graph.Y * Zoom) + Pan.Y);

    public Rect VisibleRect(Size view) => new(ToGraph(new Point(0, 0)), ToGraph(new Point(view.Width, view.Height)));

    public void PanBy(Vector screenDelta) => Pan += screenDelta;

    /// <summary>Zooms by a factor and keeps the graph point under the screen point where it is.</summary>
    public void ZoomAt(Point screen, double factor)
    {
        var anchor = ToGraph(screen);
        Zoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        Pan = new Vector(screen.X - (anchor.X * Zoom), screen.Y - (anchor.Y * Zoom));
    }

    public void CenterOn(Point graph, Size view) =>
        Pan = new Vector((view.Width / 2) - (graph.X * Zoom), (view.Height / 2) - (graph.Y * Zoom));

    /// <summary>Shows all of the content. It never zooms in past 1, so a small graph is not blown up.</summary>
    public void Fit(Rect content, Size view)
    {
        if (view.Width <= 0 || view.Height <= 0)
        {
            return;
        }
        var across = (view.Width - (2 * FitPadding)) / Math.Max(content.Width, 1);
        var down = (view.Height - (2 * FitPadding)) / Math.Max(content.Height, 1);
        Zoom = Math.Clamp(Math.Min(1, Math.Min(across, down)), MinZoom, MaxZoom);
        CenterOn(content.Center, view);
    }
}
