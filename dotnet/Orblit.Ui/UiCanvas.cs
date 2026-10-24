using System.Collections.ObjectModel;
using Orblit.VectorMath;

namespace Orblit.Ui;

/// <summary>
/// How a canvas laid out at one size is shown at another. A phone, a laptop and a television are
/// three aspect ratios, and an interface authored once has to arrive on all of them. The choice is
/// which dimension is allowed to be the one that fits.
/// </summary>
public enum CanvasFit
{
    /// <summary>
    /// Not scaled. The interface is laid out at whatever size the screen is, the prefixed classes
    /// decide what changes on the way, and text and spacing grow with the screen.
    /// </summary>
    Responsive,

    /// <summary>Scaled so the reference width fills the screen. The height overflows or falls short.</summary>
    Width,

    /// <summary>Scaled so the reference height fits.</summary>
    Height,

    /// <summary>
    /// Scaled so all of it fits, with bars where the aspect does not match. Nothing is cut off, so this
    /// is the default.
    /// </summary>
    Contain,

    /// <summary>Not scaled at all. Pixels are pixels, and the canvas is as big as the screen gives it.</summary>
    None,
}

/// <summary>The names a canvas fit has in a file and on a screen.</summary>
public static class CanvasFits
{
    private static readonly (CanvasFit Fit, string Name, string Label)[] All =
    [
        (CanvasFit.Responsive, "responsive", "Responsive"),
        (CanvasFit.Width, "width", "Match width"),
        (CanvasFit.Height, "height", "Match height"),
        (CanvasFit.Contain, "contain", "Fit inside"),
        (CanvasFit.None, "none", "Actual size"),
    ];

    /// <summary>The name a file holds.</summary>
    public static string Name(this CanvasFit fit) => All[(int)fit].Name;

    /// <summary>The words a person is shown.</summary>
    public static string Label(this CanvasFit fit) => All[(int)fit].Label;

    /// <summary>The fit a file's name stands for. Anything that is not a name gives <see cref="CanvasFit.Contain"/>.</summary>
    public static CanvasFit Named(object? name)
    {
        foreach (var (fit, known, _) in All)
        {
            if (known.Equals(name))
            {
                return fit;
            }
        }

        return CanvasFit.Contain;
    }
}

/// <summary>
/// The surface an interface is laid out on. A canvas is a design-time idea: it says what size the
/// interface was authored at and how to get from that to whatever the screen is. It draws nothing.
/// The editor shows its bounds and its safe area. A game shows the interface and no canvas.
/// </summary>
public sealed record UiCanvas
{
    /// <summary>The narrowest a column may be drawn before the grid gives up a division.</summary>
    public const double MinColumnWidth = 40;

    /// <summary>What it was laid out at.</summary>
    public double Width { get; init; } = 1920;

    public double Height { get; init; } = 1080;

    public CanvasFit Fit { get; init; } = CanvasFit.Contain;

    /// <summary>
    /// How much of each edge is not to be trusted, as a fraction. A television overscans and a phone
    /// has a notch. It is a guide and not a clip, so an interface can bleed to the edge on purpose.
    /// </summary>
    public double SafeArea { get; init; } = 0.05;

    /// <summary>How many columns the layout grid is divided into. It positions nothing by itself.</summary>
    public int Columns { get; init; } = 12;

    /// <summary>The space between two columns, in canvas pixels.</summary>
    public double Gutter { get; init; } = 24;

    /// <summary>
    /// How far the fluid scale may go on a narrow screen. Below the floor a phone stops shrinking and
    /// starts scrolling.
    /// </summary>
    public double MinScale { get; init; } = 0.8;

    /// <summary>How far the fluid scale may go on a wide screen. Above the ceiling it shows more.</summary>
    public double MaxScale { get; init; } = 1.5;

    public double Aspect => Height == 0 ? 1 : Width / Height;

    /// <summary>
    /// How much bigger everything measured in the vocabulary gets, on a screen
    /// <paramref name="atWidth"/> wide. It is one for every fit but <see cref="CanvasFit.Responsive"/>,
    /// where scaling the text again would compound.
    /// </summary>
    public double FluidScale(double atWidth)
    {
        if (Fit != CanvasFit.Responsive || Width <= 0 || atWidth <= 0 || !double.IsFinite(atWidth))
        {
            return 1;
        }

        return Doubles.Clamp(atWidth / Width, MinScale, MaxScale);
    }

    /// <summary>
    /// Where the grid starts and stops across a canvas <paramref name="ofWidth"/> wide. It sits inside
    /// the safe area, because the margin a grid needs and the edge a television eats are one distance.
    /// </summary>
    public (double Left, double Right) GridSpanAcross(double ofWidth) =>
        (ofWidth * SafeArea, ofWidth * (1 - SafeArea));

    /// <summary>
    /// How many columns the grid draws across a canvas <paramref name="ofWidth"/> wide. It is the
    /// authored count where that fits, and otherwise the largest divisor of it that does, so every
    /// narrower grid is a subset of the wider one's lines. It is zero when not even one column fits.
    /// </summary>
    public int ColumnsAt(double ofWidth)
    {
        if (Columns <= 0 || ofWidth <= 0)
        {
            return 0;
        }

        var (left, right) = GridSpanAcross(ofWidth);
        var usable = right - left;
        for (var count = Columns; count >= 1; count--)
        {
            if (Columns % count == 0 && (usable - (Gutter * (count - 1))) / count >= MinColumnWidth)
            {
                return count;
            }
        }

        return 0;
    }

    /// <summary>
    /// Every column, left and right, across a canvas <paramref name="ofWidth"/> wide. It is empty when
    /// none fit, which draws and snaps to nothing.
    /// </summary>
    public IReadOnlyList<(double Left, double Right)> ColumnsAcross(double ofWidth)
    {
        var count = ColumnsAt(ofWidth);
        if (count == 0)
        {
            return ReadOnlyCollection<(double, double)>.Empty;
        }

        var (left, right) = GridSpanAcross(ofWidth);
        var each = (right - left - (Gutter * (count - 1))) / count;
        if (each <= 0)
        {
            return ReadOnlyCollection<(double, double)>.Empty;
        }

        return [.. Enumerable.Range(0, count).Select(i => (left + (i * (each + Gutter)), left + (i * (each + Gutter)) + each))];
    }

    /// <summary>
    /// The column edge nearest <paramref name="x"/>, or null if none is within
    /// <paramref name="within"/> canvas pixels. It makes the grid something to lay out against.
    /// </summary>
    public double? SnapAcross(double x, double ofWidth, double within = 8)
    {
        double? nearest = null;
        var closest = within;
        foreach (var (left, right) in ColumnsAcross(ofWidth))
        {
            foreach (var edge in new[] { left, right })
            {
                var distance = Math.Abs(x - edge);
                if (distance <= closest)
                {
                    closest = distance;
                    nearest = edge;
                }
            }
        }

        return nearest;
    }

    /// <summary>How much to scale by to put this canvas on a screen of the given size.</summary>
    public double ScaleFor(double intoWidth, double intoHeight)
    {
        if (Width <= 0 || Height <= 0)
        {
            return 1;
        }

        return Fit switch
        {
            CanvasFit.Width => intoWidth / Width,
            CanvasFit.Height => intoHeight / Height,
            CanvasFit.Contain => intoWidth / Width < intoHeight / Height ? intoWidth / Width : intoHeight / Height,
            _ => 1,
        };
    }

    public OrderedDictionary<string, object?> ToJson() => new()
    {
        ["width"] = Width,
        ["height"] = Height,
        ["fit"] = Fit.Name(),
        ["safeArea"] = SafeArea,
        ["columns"] = Columns,
        ["gutter"] = Gutter,
        ["minScale"] = MinScale,
        ["maxScale"] = MaxScale,
    };

    /// <summary>
    /// Reads one, taking the default for anything that is missing or is not a number and holding each
    /// setting inside the range the editor offers.
    /// </summary>
    public static UiCanvas FromJson(object? value)
    {
        if (value is not IReadOnlyDictionary<string, object?> map)
        {
            return new UiCanvas();
        }

        return new UiCanvas
        {
            Width = Number(map, "width", 1920),
            Height = Number(map, "height", 1080),
            Fit = CanvasFits.Named(map.GetValueOrDefault("fit")),
            SafeArea = Doubles.Clamp(Number(map, "safeArea", 0.05), 0, 0.45),
            Columns = ColumnCount(map.GetValueOrDefault("columns")),
            Gutter = Doubles.Clamp(Number(map, "gutter", 24), 0, 400),
            MinScale = Doubles.Clamp(Number(map, "minScale", 0.8), 0.1, 1),
            MaxScale = Doubles.Clamp(Number(map, "maxScale", 1.5), 1, 8),
        };
    }

    private static double Number(IReadOnlyDictionary<string, object?> map, string key, double fallback) =>
        map.GetValueOrDefault(key) switch
        {
            int whole => whole,
            long whole => whole,
            double fraction => fraction,
            _ => fallback,
        };

    // A count too large for a long saturates, as the conversion does, and then clamps to the top.
    private static int ColumnCount(object? value) => value switch
    {
        int whole => Math.Clamp(whole, 1, 24),
        long whole => (int)Math.Clamp(whole, 1, 24),
        double fraction => (int)Math.Clamp(Doubles.Round(fraction), 1, 24),
        _ => 12,
    };
}
