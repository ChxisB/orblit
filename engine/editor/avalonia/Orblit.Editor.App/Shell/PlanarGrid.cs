using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// Lines across the 2D view on every other minor tick of the rulers, so they follow the zoom, and the world's two
/// axes through its origin: x in red and y in green. It takes no pointer.
/// </summary>
// shortcut: the grid draws over the scene, because the engine draws the scene under the overlay. Draw it in the engine's backdrop if it hides art.
internal sealed class PlanarGrid : Control
{
    const int TicksPerLine = 2;

    Vector2 _center;
    double _perPixel = 1;

    public PlanarGrid()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
    }

    /// <summary>The world units between two lines.</summary>
    internal double Step => Ruler.MinorStep(_perPixel) * TicksPerLine;

    /// <summary>Shows the world with this point at the middle, going this many world units for each pixel.</summary>
    public void Show(Vector2 center, double perPixel)
    {
        _center = center;
        _perPixel = perPixel;
        InvalidateVisual();
    }

    /// <summary>Each vertical line's distance from the left edge.</summary>
    internal IReadOnlyList<double> Across() => Lines(_center.X, Bounds.Width);

    /// <summary>Each horizontal line's distance from the top edge.</summary>
    internal IReadOnlyList<double> Down() => Lines(_center.Y, Bounds.Height);

    /// <summary>Where the world's origin lands, from the top left.</summary>
    internal Point Origin => new(At(0, _center.X, Bounds.Width), At(0, _center.Y, Bounds.Height));

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        foreach (var x in Across())
        {
            context.FillRectangle(EditorBrushes.GridLine, new Rect(Math.Round(x), 0, 1, size.Height));
        }

        foreach (var y in Down())
        {
            context.FillRectangle(EditorBrushes.GridLine, new Rect(0, Math.Round(y), size.Width, 1));
        }

        var origin = Origin;
        context.FillRectangle(EditorBrushes.GridAxisX, new Rect(0, Math.Round(origin.Y), size.Width, 1));
        context.FillRectangle(EditorBrushes.GridAxisY, new Rect(Math.Round(origin.X), 0, 1, size.Height));
    }

    double At(double world, double center, double length) => (length / 2) + ((world - center) / _perPixel);

    List<double> Lines(double center, double length)
    {
        var step = Step;
        var start = center - (length / 2 * _perPixel);
        var first = (long)Math.Ceiling(start / step);
        var last = (long)Math.Floor((start + (length * _perPixel)) / step);
        var lines = new List<double>();
        for (var line = first; line <= last; line++)
        {
            lines.Add(At(line * step, center, length));
        }

        return lines;
    }
}
