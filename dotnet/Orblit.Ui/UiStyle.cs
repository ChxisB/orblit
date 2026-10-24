namespace Orblit.Ui;

/// <summary>
/// What a class list or a CSS declaration says about one box. Every field is null until something
/// sets it, because a style that says nothing about a width must not read as a width of zero.
/// Later styles win a field one at a time, through <see cref="Merge"/>.
/// </summary>
public sealed record UiStyle
{
    /// <summary>A style that says nothing.</summary>
    public static readonly UiStyle None = new();

    /// <summary><c>row</c> or <c>column</c>.</summary>
    public string? Direction { get; init; }

    /// <summary>Where children sit along the direction: <c>start</c>, <c>center</c>, <c>end</c>, <c>between</c>, <c>around</c> or <c>evenly</c>.</summary>
    public string? MainAxis { get; init; }

    /// <summary>Where children sit across the direction: <c>start</c>, <c>center</c>, <c>end</c>, <c>stretch</c> or <c>baseline</c>.</summary>
    public string? CrossAxis { get; init; }

    /// <summary>Space between children.</summary>
    public double? Gap { get; init; }

    /// <summary>The share of spare room the box takes. Zero or less means none.</summary>
    public double? Grow { get; init; }

    /// <summary>Padding on the top side.</summary>
    public double? PaddingTop { get; init; }

    /// <summary>Padding on the right side.</summary>
    public double? PaddingRight { get; init; }

    /// <summary>Padding on the bottom side.</summary>
    public double? PaddingBottom { get; init; }

    /// <summary>Padding on the left side.</summary>
    public double? PaddingLeft { get; init; }

    /// <summary>Margin on the top side.</summary>
    public double? MarginTop { get; init; }

    /// <summary>Margin on the right side.</summary>
    public double? MarginRight { get; init; }

    /// <summary>Margin on the bottom side.</summary>
    public double? MarginBottom { get; init; }

    /// <summary>Margin on the left side.</summary>
    public double? MarginLeft { get; init; }

    /// <summary>A width in pixels. Infinity means the whole of the parent.</summary>
    public double? Width { get; init; }

    /// <summary>A height in pixels. Infinity means the whole of the parent.</summary>
    public double? Height { get; init; }

    /// <summary>The smallest width in pixels.</summary>
    public double? MinWidth { get; init; }

    /// <summary>The smallest height in pixels.</summary>
    public double? MinHeight { get; init; }

    /// <summary>The largest width in pixels.</summary>
    public double? MaxWidth { get; init; }

    /// <summary>The largest height in pixels.</summary>
    public double? MaxHeight { get; init; }

    /// <summary>The fill.</summary>
    public UiColour? Background { get; init; }

    /// <summary>The text colour.</summary>
    public UiColour? Colour { get; init; }

    /// <summary>The border colour.</summary>
    public UiColour? BorderColour { get; init; }

    /// <summary>The border width.</summary>
    public double? BorderWidth { get; init; }

    /// <summary>The corner radius.</summary>
    public double? Radius { get; init; }

    /// <summary>From 0 to 1.</summary>
    public double? Opacity { get; init; }

    /// <summary>The font size in pixels.</summary>
    public double? FontSize { get; init; }

    /// <summary>From 100 to 900.</summary>
    public long? FontWeight { get; init; }

    /// <summary>Slanted text.</summary>
    public bool? Italic { get; init; }

    /// <summary>Extra space between letters in pixels.</summary>
    public double? LetterSpacing { get; init; }

    /// <summary>A multiple of the font size.</summary>
    public double? LineHeight { get; init; }

    /// <summary><c>left</c>, <c>center</c>, <c>right</c> or <c>justify</c>.</summary>
    public string? TextAlign { get; init; }

    /// <summary>Draws text in capitals.</summary>
    public bool? Uppercase { get; init; }

    /// <summary>The blur of a drop shadow. Zero or less draws none.</summary>
    public double? Shadow { get; init; }

    /// <summary>Cuts what overflows.</summary>
    public bool? Clip { get; init; }

    /// <summary><c>x</c> or <c>y</c>.</summary>
    public string? Scroll { get; init; }

    /// <summary>The distance from the top of the parent. Any edge makes the box positioned.</summary>
    public double? Top { get; init; }

    /// <summary>The distance from the right of the parent.</summary>
    public double? Right { get; init; }

    /// <summary>The distance from the bottom of the parent.</summary>
    public double? Bottom { get; init; }

    /// <summary>The distance from the left of the parent.</summary>
    public double? Left { get; init; }

    /// <summary>True when any edge is set, which takes the box out of the flow.</summary>
    public bool IsPositioned => Top != null || Right != null || Bottom != null || Left != null;

    /// <summary>True when any side of the padding is set.</summary>
    public bool HasPadding => PaddingTop != null || PaddingRight != null || PaddingBottom != null || PaddingLeft != null;

    /// <summary>True when any side of the margin is set.</summary>
    public bool HasMargin => MarginTop != null || MarginRight != null || MarginBottom != null || MarginLeft != null;

    /// <summary>True when the box paints anything of its own: a fill, a border, rounded corners or a shadow.</summary>
    public bool HasBox => Background != null || BorderWidth != null || Radius != null || (Shadow ?? 0) > 0;

    /// <summary>True when any width or height limit is set.</summary>
    public bool HasSize => Width != null || Height != null || MinWidth != null || MinHeight != null || MaxWidth != null || MaxHeight != null;

    /// <summary>
    /// This style with every field <paramref name="other"/> sets taken from it. A field it leaves null
    /// keeps this style's value.
    /// </summary>
    public UiStyle Merge(UiStyle other) =>
        MergeFlow(other).MergeSpacing(other).MergeSize(other).MergePaint(other).MergeText(other).MergeLayer(other);

    private UiStyle MergeFlow(UiStyle other) =>
        this with
        {
            Direction = other.Direction ?? Direction,
            MainAxis = other.MainAxis ?? MainAxis,
            CrossAxis = other.CrossAxis ?? CrossAxis,
            Gap = other.Gap ?? Gap,
            Grow = other.Grow ?? Grow,
        };

    private UiStyle MergeSpacing(UiStyle other) =>
        this with
        {
            PaddingTop = other.PaddingTop ?? PaddingTop,
            PaddingRight = other.PaddingRight ?? PaddingRight,
            PaddingBottom = other.PaddingBottom ?? PaddingBottom,
            PaddingLeft = other.PaddingLeft ?? PaddingLeft,
            MarginTop = other.MarginTop ?? MarginTop,
            MarginRight = other.MarginRight ?? MarginRight,
            MarginBottom = other.MarginBottom ?? MarginBottom,
            MarginLeft = other.MarginLeft ?? MarginLeft,
        };

    private UiStyle MergeSize(UiStyle other) =>
        this with
        {
            Width = other.Width ?? Width,
            Height = other.Height ?? Height,
            MinWidth = other.MinWidth ?? MinWidth,
            MinHeight = other.MinHeight ?? MinHeight,
            MaxWidth = other.MaxWidth ?? MaxWidth,
            MaxHeight = other.MaxHeight ?? MaxHeight,
        };

    private UiStyle MergePaint(UiStyle other) =>
        this with
        {
            Background = other.Background ?? Background,
            Colour = other.Colour ?? Colour,
            BorderColour = other.BorderColour ?? BorderColour,
            BorderWidth = other.BorderWidth ?? BorderWidth,
            Radius = other.Radius ?? Radius,
            Opacity = other.Opacity ?? Opacity,
        };

    private UiStyle MergeText(UiStyle other) =>
        this with
        {
            FontSize = other.FontSize ?? FontSize,
            FontWeight = other.FontWeight ?? FontWeight,
            Italic = other.Italic ?? Italic,
            LetterSpacing = other.LetterSpacing ?? LetterSpacing,
            LineHeight = other.LineHeight ?? LineHeight,
            TextAlign = other.TextAlign ?? TextAlign,
            Uppercase = other.Uppercase ?? Uppercase,
        };

    private UiStyle MergeLayer(UiStyle other) =>
        this with
        {
            Shadow = other.Shadow ?? Shadow,
            Clip = other.Clip ?? Clip,
            Scroll = other.Scroll ?? Scroll,
            Top = other.Top ?? Top,
            Right = other.Right ?? Right,
            Bottom = other.Bottom ?? Bottom,
            Left = other.Left ?? Left,
        };
}
