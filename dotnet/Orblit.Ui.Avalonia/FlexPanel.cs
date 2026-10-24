using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Ui.Avalonia;

/// <summary>Where a flex puts its children along the way it runs.</summary>
public enum FlexMain
{
    Start,
    Center,
    End,
    Between,
    Around,
    Evenly,
}

/// <summary>Where a flex puts its children across the way it runs.</summary>
public enum FlexCross
{
    Start,
    Center,
    End,
    Stretch,
}

/// <summary>
/// A row or a column that shares out its room the way a flex box does. Children that grow split what
/// the others leave, but only when there is a bounded amount to split. With nothing to share they
/// keep their own size, which shows a row that did not stretch and does not take the frame down. The
/// gap is a slot of its own, so spreading the children spreads the gaps with them.
/// </summary>
public sealed class FlexPanel : Panel
{
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<FlexPanel, Orientation>(nameof(Orientation), Orientation.Vertical);

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<FlexPanel, double>(nameof(Gap));

    public static readonly StyledProperty<FlexMain> MainAxisProperty =
        AvaloniaProperty.Register<FlexPanel, FlexMain>(nameof(MainAxis));

    public static readonly StyledProperty<FlexCross> CrossAxisProperty =
        AvaloniaProperty.Register<FlexPanel, FlexCross>(nameof(CrossAxis));

    /// <summary>How much of what is left a child takes, as a share. Zero keeps its own size.</summary>
    public static readonly AttachedProperty<int> GrowProperty =
        AvaloniaProperty.RegisterAttached<FlexPanel, Control, int>("Grow");

    /// <summary>The child takes the whole width of a column, whatever it asked for.</summary>
    public static readonly AttachedProperty<bool> FillWidthProperty =
        AvaloniaProperty.RegisterAttached<FlexPanel, Control, bool>("FillWidth");

    /// <summary>The child takes the whole height of a row, whatever it asked for.</summary>
    public static readonly AttachedProperty<bool> FillHeightProperty =
        AvaloniaProperty.RegisterAttached<FlexPanel, Control, bool>("FillHeight");

    static FlexPanel()
    {
        AffectsMeasure<FlexPanel>(OrientationProperty, GapProperty, MainAxisProperty, CrossAxisProperty);
        AffectsParentMeasure<FlexPanel>(GrowProperty, FillWidthProperty, FillHeightProperty);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public FlexMain MainAxis
    {
        get => GetValue(MainAxisProperty);
        set => SetValue(MainAxisProperty, value);
    }

    public FlexCross CrossAxis
    {
        get => GetValue(CrossAxisProperty);
        set => SetValue(CrossAxisProperty, value);
    }

    private bool Horizontal => Orientation == Orientation.Horizontal;

    public static int GetGrow(Control control) => control.GetValue(GrowProperty);

    public static void SetGrow(Control control, int value) => control.SetValue(GrowProperty, value);

    public static bool GetFillWidth(Control control) => control.GetValue(FillWidthProperty);

    public static void SetFillWidth(Control control, bool value) => control.SetValue(FillWidthProperty, value);

    public static bool GetFillHeight(Control control) => control.GetValue(FillHeightProperty);

    public static void SetFillHeight(Control control, bool value) => control.SetValue(FillHeightProperty, value);

    protected override Size MeasureOverride(Size availableSize)
    {
        var available = Main(availableSize);
        var across = Cross(availableSize);
        var used = Gaps();
        var cross = 0.0;
        foreach (var child in Children.Where(child => !IsGrowing(child)))
        {
            child.Measure(Pack(double.PositiveInfinity, across));
            used += Main(child.DesiredSize);
            cross = Math.Max(cross, Cross(child.DesiredSize));
        }

        var growing = Children.Where(IsGrowing).ToList();
        var shares = Shares(growing, available - used);
        for (var i = 0; i < growing.Count; i++)
        {
            growing[i].Measure(Pack(shares[i], across));
            cross = Math.Max(cross, Cross(growing[i].DesiredSize));
            used += double.IsFinite(shares[i]) ? 0 : Main(growing[i].DesiredSize);
        }

        return Pack(double.IsFinite(available) ? available : used, cross);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var main = Main(finalSize);
        var items = Items(main);
        var (lead, between) = Spread(MainAxis, Math.Max(0, main - items.Sum(item => item.Size)), items.Count);
        var at = lead;
        foreach (var (child, size) in items)
        {
            if (child is not null)
            {
                ArrangeChild(child, at, size, Cross(finalSize));
            }

            at += size + between;
        }

        return finalSize;
    }

    private static bool IsGrowing(Control child) => GetGrow(child) > 0;

    // A growing child shares what is left. With an unbounded amount left there is nothing to share.
    private static double[] Shares(List<Control> growing, double free)
    {
        var total = growing.Sum(GetGrow);
        return [.. growing.Select(child => double.IsFinite(free) ? Math.Max(0, free) * GetGrow(child) / total : double.PositiveInfinity)];
    }

    private static (double Lead, double Between) Spread(FlexMain how, double free, int count) => how switch
    {
        FlexMain.Center => (free / 2, 0),
        FlexMain.End => (free, 0),
        FlexMain.Between when count > 1 => (0, free / (count - 1)),
        FlexMain.Around => (free / count / 2, free / count),
        FlexMain.Evenly => (free / (count + 1), free / (count + 1)),
        _ => (0, 0),
    };

    private double Main(Size size) => Horizontal ? size.Width : size.Height;

    private double Cross(Size size) => Horizontal ? size.Height : size.Width;

    private Size Pack(double main, double cross) => Horizontal ? new Size(main, cross) : new Size(cross, main);

    private double Gaps() => Gap > 0 && Children.Count > 1 ? Gap * (Children.Count - 1) : 0;

    // The children and the gaps between them, in order, each with its size along the way it runs.
    private List<(Control? Child, double Size)> Items(double main)
    {
        var fixedMain = Gaps() + Children.Where(child => !IsGrowing(child)).Sum(child => Main(child.DesiredSize));
        var shares = Shares([.. Children.Where(IsGrowing)], main - fixedMain);
        List<(Control?, double)> items = [];
        var next = 0;
        foreach (var child in Children)
        {
            if (items.Count > 0 && Gap > 0)
            {
                items.Add((null, Gap));
            }

            items.Add((child, IsGrowing(child) ? shares[next++] : Main(child.DesiredSize)));
        }

        return items;
    }

    private bool FillsAcross(Control child) =>
        CrossAxis == FlexCross.Stretch || (Horizontal ? GetFillHeight(child) : GetFillWidth(child));

    private void ArrangeChild(Control child, double at, double size, double cross)
    {
        var across = FillsAcross(child) ? cross : Cross(child.DesiredSize);
        var offset = CrossAxis switch
        {
            FlexCross.Center => (cross - across) / 2,
            FlexCross.End => cross - across,
            _ => 0,
        };
        child.Arrange(Horizontal ? new Rect(at, offset, size, across) : new Rect(offset, at, across, size));
    }
}
