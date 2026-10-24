using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Orblit.Ui.Avalonia;

/// <summary>
/// The box every element sits in: margin, size, background, border, corners and padding, all on one
/// border. An element that asks for none of them is not wrapped at all.
/// </summary>
internal static class UiBox
{
    private static readonly Color DefaultBorder = Color.FromUInt32(0x33FFFFFF);
    private static readonly Color ShadowColour = Color.FromUInt32(0x40000000);

    /// <summary>Infinity means "as much as there is" in a class list, and is not a number a size or an inset can take.</summary>
    internal static double? Finite(double? value) => value is { } number && double.IsFinite(number) ? number : null;

    internal static IBrush Brush(UiColour colour) => new SolidColorBrush(Color.FromUInt32(colour.Argb));

    internal static Control Wrap(Control child, UiStyle style)
    {
        if (!(style.HasPadding || style.HasBox || style.HasSize || style.HasMargin || style.Clip == true || style.Opacity < 1))
        {
            return child;
        }

        var box = new Border
        {
            Child = child,
            Padding = Edges(style.PaddingLeft, style.PaddingTop, style.PaddingRight, style.PaddingBottom),
            Margin = Edges(style.MarginLeft, style.MarginTop, style.MarginRight, style.MarginBottom),
            ClipToBounds = style.Clip == true,
        };
        Paint(box, style);
        Size(box, style);
        if (style.Opacity is { } opacity && opacity < 1)
        {
            box.Opacity = Math.Max(0, opacity);
        }

        return box;
    }

    private static Thickness Edges(double? left, double? top, double? right, double? bottom) =>
        new(Edge(left), Edge(top), Edge(right), Edge(bottom));

    private static double Edge(double? value) => Math.Max(0, Finite(value) ?? 0);

    private static void Paint(Border box, UiStyle style)
    {
        if (style.Background is { } background)
        {
            box.Background = Brush(background);
        }

        if (Finite(style.Radius) is { } radius)
        {
            box.CornerRadius = new CornerRadius(Math.Clamp(radius, 0, 999));
        }

        if (Finite(style.BorderWidth) is { } width)
        {
            box.BorderThickness = new Thickness(Math.Max(0, width));
            box.BorderBrush = style.BorderColour is { } colour ? Brush(colour) : new SolidColorBrush(DefaultBorder);
        }

        if (Finite(style.Shadow) is > 0 and var blur)
        {
            box.BoxShadow = new BoxShadows(new BoxShadow { OffsetY = blur * 0.35, Blur = blur, Color = ShadowColour });
        }
    }

    // NaN is "as the content wants", which is also what an infinite size means: the box stretches.
    private static void Size(Border box, UiStyle style)
    {
        box.Width = Exact(style.Width);
        box.Height = Exact(style.Height);
        box.MinWidth = Least(style.MinWidth);
        box.MinHeight = Least(style.MinHeight);
        box.MaxWidth = Most(style.MaxWidth);
        box.MaxHeight = Most(style.MaxHeight);
    }

    private static double Exact(double? value) => Finite(value) is { } number ? Math.Max(0, number) : double.NaN;

    private static double Least(double? value) => Finite(value) is { } number ? Math.Max(0, number) : 0;

    private static double Most(double? value) => value is { } number && !double.IsNaN(number) ? Math.Max(0, number) : double.PositiveInfinity;
}
