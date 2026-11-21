using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Orblit.Editor.App.Theme;

namespace Orblit.Editor.App.Shell;

/// <summary>
/// A ruler along an edge of the 2D view, lying down with its ticks rising from its bottom edge. A major tick falls
/// on each multiple of a step of 1, 2 or 5 times a power of ten, at least 100 pixels apart, and carries its value.
/// Nine minor ticks split each gap. The left ruler is this control turned a quarter, so its owner gives it the
/// view from that end.
/// </summary>
internal sealed class Ruler : Control
{
    internal const double Thickness = 22;

    const double MajorGap = 100;
    const int MinorPerMajor = 10;
    const double MinorTall = 6;
    const double MajorTall = 12;
    const double LabelSize = 9.5;

    static readonly Typeface s_label = new(EditorTheme.Mono);

    double _start;
    double _perPixel = 1;

    public Ruler()
    {
        Height = Thickness;
        ClipToBounds = true;
    }

    /// <summary>Shows the world from this value at the ruler's start, going this many world units for each pixel along it.</summary>
    public void Show(double start, double perPixel)
    {
        _start = start;
        _perPixel = perPixel;
        InvalidateVisual();
    }

    /// <summary>Each tick as its distance along the ruler, with the value it carries. A minor tick carries none.</summary>
    internal IReadOnlyList<(double At, string? Label)> Marks()
    {
        var minor = MinorStep(_perPixel);
        var end = _start + Bounds.Width * _perPixel;
        var first = (long)Math.Ceiling(Math.Min(_start, end) / minor);
        var last = (long)Math.Floor(Math.Max(_start, end) / minor);
        var marks = new List<(double At, string? Label)>();
        for (var tick = first; tick <= last; tick++)
        {
            var value = tick * minor;
            marks.Add(((value - _start) / _perPixel, tick % MinorPerMajor == 0 ? value.ToString("0.###", CultureInfo.InvariantCulture) : null));
        }
        return marks;
    }

    public override void Render(DrawingContext context)
    {
        var size = Bounds.Size;
        context.FillRectangle(EditorBrushes.Ruler, new Rect(size));
        context.FillRectangle(EditorBrushes.RulerEdge, new Rect(0, size.Height - 1, size.Width, 1));
        foreach (var (at, label) in Marks())
        {
            var x = Math.Round(at);
            var tall = label is null ? MinorTall : MajorTall;
            context.FillRectangle(label is null ? EditorBrushes.RulerTick : EditorBrushes.RulerMajor, new Rect(x, size.Height - tall, 1, tall));
            if (label is not null)
            {
                context.DrawText(new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, s_label, LabelSize, EditorBrushes.Unit), new Point(x + 3, 2));
            }
        }
    }

    /// <summary>The world units between two minor ticks at this many world units for each pixel.</summary>
    internal static double MinorStep(double perPixel) => MajorStep(MajorGap * Math.Abs(perPixel)) / MinorPerMajor;

    // The smallest of 1, 2 and 5 times a power of ten that spans the world units in the gap.
    static double MajorStep(double span)
    {
        var power = Math.Pow(10, Math.Floor(Math.Log10(span)));
        return new[] { 1, 2, 5, 10 }.Select(times => times * power).First(step => step >= span);
    }
}
