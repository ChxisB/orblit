using Avalonia;
using Avalonia.Media;

namespace Orblit.Editor.App.Graph;

internal enum WireStyle
{
    Curved,
    Straight,
}

/// <summary>The shape of a wire. One path serves both drawing and hit tests, so a wire is clicked where it shows.</summary>
internal static class WireLayer
{
    const int Samples = 24;
    const double MinReach = 40;
    const double ReachShare = 0.5;

    public static IReadOnlyList<Point> Path(Point from, Point to, WireStyle style)
    {
        if (style == WireStyle.Straight)
        {
            return [from, to];
        }
        var reach = Math.Max(MinReach, Math.Abs(to.X - from.X) * ReachShare);
        var first = new Point(from.X + reach, from.Y);
        var second = new Point(to.X - reach, to.Y);
        var points = new Point[Samples + 1];
        for (var i = 0; i <= Samples; i++)
        {
            points[i] = Bezier(from, first, second, to, (double)i / Samples);
        }
        return points;
    }

    public static void Draw(DrawingContext context, Point from, Point to, IBrush brush, WireStyle style, bool selected)
    {
        var points = Path(from, to, style);
        var geometry = new StreamGeometry();
        using (var path = geometry.Open())
        {
            path.BeginFigure(points[0], false);
            foreach (var point in points.Skip(1))
            {
                path.LineTo(point);
            }
            path.EndFigure(false);
        }
        context.DrawGeometry(null, new Pen(brush, selected ? 4 : 2), geometry);
    }

    /// <summary>Whether a point lies within the tolerance of the wire.</summary>
    public static bool Near(Point from, Point to, WireStyle style, Point point, double tolerance)
    {
        var points = Path(from, to, style);
        for (var i = 1; i < points.Count; i++)
        {
            if (DistanceToSegment(point, points[i - 1], points[i]) <= tolerance)
            {
                return true;
            }
        }
        return false;
    }

    static Point Bezier(Point a, Point b, Point c, Point d, double t)
    {
        var u = 1 - t;
        var weights = (u * u * u, 3 * u * u * t, 3 * u * t * t, t * t * t);
        return new Point(
            (weights.Item1 * a.X) + (weights.Item2 * b.X) + (weights.Item3 * c.X) + (weights.Item4 * d.X),
            (weights.Item1 * a.Y) + (weights.Item2 * b.Y) + (weights.Item3 * c.Y) + (weights.Item4 * d.Y));
    }

    static double DistanceToSegment(Point p, Point a, Point b)
    {
        var length = ((b.X - a.X) * (b.X - a.X)) + ((b.Y - a.Y) * (b.Y - a.Y));
        var along = length == 0 ? 0 : Math.Clamp((((p.X - a.X) * (b.X - a.X)) + ((p.Y - a.Y) * (b.Y - a.Y))) / length, 0, 1);
        var nearest = new Point(a.X + (along * (b.X - a.X)), a.Y + (along * (b.Y - a.Y)));
        return Math.Sqrt(((p.X - nearest.X) * (p.X - nearest.X)) + ((p.Y - nearest.Y) * (p.Y - nearest.Y)));
    }
}
