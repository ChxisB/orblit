using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Ui.Avalonia;

/// <summary>
/// Builds Avalonia controls from a description. There is no reconciler and no retained tree. A
/// description arrives and a control tree comes out, and a new description builds a new tree.
/// </summary>
public sealed class UiBuilder
{
    // What a stick or a swipe pad is when its style gives no size. It has no children, so without one
    // it would be nothing to touch.
    private const double TouchFace = 120;

    private static readonly Lazy<Cursor> Hand = new(() => new Cursor(StandardCursorType.Hand));

    private readonly UiBuilderOptions _options;
    private readonly UiUtilities _utilities;
    private readonly UiCss _css;
    private readonly FontFamily? _font;

    public UiBuilder(UiBuilderOptions options)
    {
        _options = options;
        _utilities = new UiUtilities(options.Theme);
        _css = new UiCss(options.Theme);
        _font = FamilyOf(options.FontFamily, options.FontFallback);
    }

    /// <summary>
    /// The font a host asked for with its fallbacks, or null when it asked for none. With fallbacks and
    /// no family of its own the fallbacks follow the default font.
    /// </summary>
    internal static FontFamily? FamilyOf(string? family, IReadOnlyList<string> fallback)
    {
        if (string.IsNullOrEmpty(family) && fallback.Count == 0)
        {
            return null;
        }

        var first = string.IsNullOrEmpty(family) ? FontFamily.DefaultFontFamilyName : family;
        return new FontFamily(string.Join(',', fallback.Prepend(first)));
    }

    /// <summary>The style a node adds up to: its classes, then its CSS over the top.</summary>
    public UiStyle StyleOf(UiNode node)
    {
        var style = UiStyle.None;
        if (node.Classes.Length > 0)
        {
            style = style.Merge(_utilities.Parse(node.Classes, _options.Width));
        }

        return node.Css.Length > 0 ? style.Merge(_css.Parse(node.Css)) : style;
    }

    /// <summary>Everything in a description that means nothing here, for the editor to show while somebody is typing.</summary>
    public List<string> UnknownIn(UiNode node) =>
    [
        .. _utilities.UnknownIn(node.Classes),
        .. _css.UnknownIn(node.Css),
        .. node.Children.SelectMany(UnknownIn),
    ];

    /// <summary>Builds one node and everything under it.</summary>
    public Control Build(UiNode node, IReadOnlyList<int>? path = null) => BuildStyled(node, path ?? []).Control;

    private static int GrowOf(UiStyle style) =>
        style.Grow is > 0 and var grow ? (int)Math.Round(Math.Clamp(grow, 1, 1000), MidpointRounding.AwayFromZero) : 0;

    private static bool Fills(double? size) => size is { } number && !double.IsFinite(number) && !double.IsNaN(number);

    private static string WordsOf(UiNode node, UiStrings? strings) =>
        node.TextKey is { } key ? strings?.Find(key, node.TextArgs) ?? node.Text ?? key : node.Text ?? string.Empty;

    private static TextAlignment? AlignOf(string? how) => how switch
    {
        "center" => TextAlignment.Center,
        "right" => TextAlignment.Right,
        "justify" => TextAlignment.Justify,
        "left" => TextAlignment.Left,
        _ => null,
    };

    private static FlexMain MainOf(string? how) => how switch
    {
        "center" => FlexMain.Center,
        "end" => FlexMain.End,
        "between" => FlexMain.Between,
        "around" => FlexMain.Around,
        "evenly" => FlexMain.Evenly,
        _ => FlexMain.Start,
    };

    // A baseline is not something this layout can line up on, so it reads as the start.
    private static FlexCross CrossOf(string? how) => how switch
    {
        "center" => FlexCross.Center,
        "end" => FlexCross.End,
        "stretch" => FlexCross.Stretch,
        _ => FlexCross.Start,
    };

    private static FontWeight? WeightOf(long? value) =>
        value is >= 100 and <= 900 && value % 100 == 0 ? (FontWeight)(int)value : null;

    private static HorizontalAlignment Across(double? left, double? right, bool fill) =>
        fill || (left is not null && right is not null) ? HorizontalAlignment.Stretch
        : right is not null ? HorizontalAlignment.Right : HorizontalAlignment.Left;

    private static VerticalAlignment Down(double? top, double? bottom, bool fill) =>
        fill || (top is not null && bottom is not null) ? VerticalAlignment.Stretch
        : bottom is not null ? VerticalAlignment.Bottom : VerticalAlignment.Top;

    // Where a stack puts a child: against the edges it names, and at the top left when it names none.
    private static void Place(Control child, UiStyle style)
    {
        var (left, top, right, bottom) = (UiBox.Finite(style.Left), UiBox.Finite(style.Top), UiBox.Finite(style.Right), UiBox.Finite(style.Bottom));
        child.HorizontalAlignment = Across(left, right, Fills(style.Width));
        child.VerticalAlignment = Down(top, bottom, Fills(style.Height));
        child.Margin += new Thickness(Math.Max(0, left ?? 0), Math.Max(0, top ?? 0), Math.Max(0, right ?? 0), Math.Max(0, bottom ?? 0));
    }

    // What a flex reads from a child: how much it grows and whether it fills the way across.
    private static void Flex(Control child, UiNode node, UiStyle style)
    {
        var grow = GrowOf(style);
        FlexPanel.SetGrow(child, grow == 0 && node.Type == "spacer" ? 1 : grow);
        FlexPanel.SetFillWidth(child, Fills(style.Width));
        FlexPanel.SetFillHeight(child, Fills(style.Height));
    }

    private static double FaceSize(double? size) => size is null ? TouchFace : UiBox.Finite(size) is { } number ? Math.Max(0, number) : double.NaN;

    private static async Task Show(Image image, ImageLoader load, string source)
    {
        try
        {
            image.Source = await load(source);
        }
        catch (Exception)
        {
            // A picture that will not load shows nothing, which is what the original does.
        }
    }

    private (Control Control, UiStyle Style) BuildStyled(UiNode node, IReadOnlyList<int> path)
    {
        var style = StyleOf(node);
        var control = Tapped(Touched(UiBox.Wrap(Leaf(node, style, path), style), node, style), node);

        // The host's wrapper goes on before anything a parent reads. Growing and placing are read from
        // the outermost control, so one between the two would lose the child its size or its place.
        if (_options.Decorate is { } decorate)
        {
            control = decorate(node, path, control);
        }

        control.Tag = node.Key;
        return (control, style);
    }

    private Control Leaf(UiNode node, UiStyle style, IReadOnlyList<int> path) => node.Type switch
    {
        "text" => Words(node, style, true),
        "button" => ButtonOf(node, style, path),
        "field" => FieldOf(node, style),
        "image" => ImageOf(node),
        "stick" or "swipe-pad" => new Border { Width = FaceSize(style.Width), Height = FaceSize(style.Height), Background = Brushes.Transparent },
        "spacer" => new Border(),
        _ => Container(node, style, path),
    };

    private void Raise(string handler, object? payload) => _options.OnEvent?.Invoke(handler, payload);

    // Anything can be tapped, not only buttons. A button takes the press on its own face instead.
    private Control Tapped(Control control, UiNode node)
    {
        if (node.Type == "button" || node.HandlerFor("onTap") is not { } handler)
        {
            return control;
        }

        var tap = new Border { Background = Brushes.Transparent, Child = control };
        tap.Tapped += (_, _) => Raise(handler, null);
        return tap;
    }

    private TouchSend? SenderFor(UiNode node, string eventName) =>
        node.HandlerFor(eventName) is { } handler ? payload => Raise(handler, payload) : null;

    // The touch areas wrap the finished face, so its padding and background take touches too.
    private Control Touched(Control face, UiNode node, UiStyle style) => node.Type switch
    {
        "stick" => new TouchStick(face, KnobOf(style), SenderFor(node, "onMove")),
        "touch-button" => new TouchHold(face, SenderFor(node, "onHold")),
        "swipe-pad" => new TouchSwipe(face, SenderFor(node, "onSwipe")),
        _ => face,
    };

    private static IBrush KnobOf(UiStyle style) => style.Colour is { } colour ? UiBox.Brush(colour) : new SolidColorBrush(Color.FromUInt32(0xB3FFFFFF));

    private TextBlock Words(UiNode node, UiStyle style, bool align)
    {
        var words = WordsOf(node, _options.Strings);
        var block = new TextBlock
        {
            Text = style.Uppercase == true ? words.ToUpperInvariant() : words,
            TextWrapping = TextWrapping.Wrap,
        };
        if (align && AlignOf(style.TextAlign) is { } alignment)
        {
            block.TextAlignment = alignment;
        }

        Type(block, style);
        return block;
    }

    // The text style of one element. It is set on that element only, as it is in the original: a
    // column with a size does not pass it to the words inside.
    private void Type(AvaloniaObject target, UiStyle style)
    {
        if (style.Colour is { } colour)
        {
            target.SetValue(TextElement.ForegroundProperty, UiBox.Brush(colour));
        }

        if (UiBox.Finite(style.FontSize) is { } size)
        {
            target.SetValue(TextElement.FontSizeProperty, Math.Max(1, size));
        }

        if (WeightOf(style.FontWeight) is { } weight)
        {
            target.SetValue(TextElement.FontWeightProperty, weight);
        }

        if (style.Italic == true)
        {
            target.SetValue(TextElement.FontStyleProperty, FontStyle.Italic);
        }

        if (UiBox.Finite(style.LetterSpacing) is { } spacing)
        {
            target.SetValue(TextElement.LetterSpacingProperty, spacing);
        }

        if (UiBox.Finite(style.LineHeight) is { } height)
        {
            target.SetValue(TextBlock.LineHeightProperty, height * (UiBox.Finite(style.FontSize) ?? BaseSize));
        }

        if (_font is not null)
        {
            target.SetValue(TextElement.FontFamilyProperty, _font);
        }
    }

    private double BaseSize => _options.Theme.Text.GetValueOrDefault("base", 14);

    private Border ButtonOf(UiNode node, UiStyle style, IReadOnlyList<int> path)
    {
        var handler = node.HandlerFor("onPressed") ?? node.HandlerFor("onTap");

        // Built from the same pieces as everything else, so a class list styles a button the way it
        // styles a box. A button adds the press and the cursor and nothing else.
        var face = new Border
        {
            Background = Brushes.Transparent,
            Child = node.Children.Count == 0 ? Words(node, style, false) : Container(node, style, path),
        };
        if (handler is not null)
        {
            face.Cursor = Hand.Value;
            face.Tapped += (_, _) => Raise(handler, null);
        }

        return face;
    }

    private TextBox FieldOf(UiNode node, UiStyle style)
    {
        var field = new TextBox
        {
            Text = node.Text ?? string.Empty,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Padding = new Thickness(0),
            MinHeight = 0,
            PlaceholderText = node.Props.GetValueOrDefault("placeholder") as string,
        };
        Type(field, style);
        if (node.HandlerFor("onChanged") is { } handler)
        {
            // The property, not TextChanged: that event is raised later, and a handler should hear
            // the text as it is when somebody types.
            field.PropertyChanged += (_, change) =>
            {
                if (change.Property == TextBox.TextProperty)
                {
                    Raise(handler, field.Text ?? string.Empty);
                }
            };
        }

        return field;
    }

    private Control ImageOf(UiNode node)
    {
        if (node.Props.GetValueOrDefault("src") is not string { Length: > 0 } source)
        {
            return new Border();
        }

        var image = new Image { Stretch = Stretch.UniformToFill };
        if (_options.Images is { } load)
        {
            _ = Show(image, load, source);
        }

        return image;
    }

    private Control Container(UiNode node, UiStyle style, IReadOnlyList<int> path)
    {
        var direction = style.Direction ?? node.Type switch
        {
            "row" => "row",
            "stack" => "stack",
            "column" => "column",
            _ => node.Children.Count > 1 ? "column" : "none",
        };

        if (node.Children.Count == 0)
        {
            return new Border();
        }

        if (direction == "stack")
        {
            return StackOf(node, style, path);
        }

        if (direction == "none" && node.Children.Count == 1)
        {
            return Build(node.Children[0], [.. path, 0]);
        }

        var flex = FlexOf(node, style, path, direction == "row");
        return style.Scroll is { } scroll ? Scrolling(flex, scroll) : flex;
    }

    private static ScrollViewer Scrolling(Control flex, string scroll)
    {
        var sideways = scroll == "x";
        return new ScrollViewer
        {
            Content = flex,
            HorizontalScrollBarVisibility = sideways ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = sideways ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto,
        };
    }

    // Every child shares one cell, so each is as big as the stack needs it to be and sits where its
    // edges put it.
    private Grid StackOf(UiNode node, UiStyle style, IReadOnlyList<int> path)
    {
        var stack = new Grid { ClipToBounds = style.Clip == true };
        for (var i = 0; i < node.Children.Count; i++)
        {
            var (child, childStyle) = BuildStyled(node.Children[i], [.. path, i]);
            if (childStyle.IsPositioned || Fills(childStyle.Width) || Fills(childStyle.Height))
            {
                Place(child, childStyle);
            }

            stack.Children.Add(child);
        }

        return stack;
    }

    private FlexPanel FlexOf(UiNode node, UiStyle style, IReadOnlyList<int> path, bool horizontal)
    {
        var flex = new FlexPanel
        {
            Orientation = horizontal ? Orientation.Horizontal : Orientation.Vertical,
            Gap = Math.Max(0, UiBox.Finite(style.Gap) ?? 0),
            MainAxis = MainOf(style.MainAxis),
            CrossAxis = CrossOf(style.CrossAxis),
        };
        for (var i = 0; i < node.Children.Count; i++)
        {
            var (child, childStyle) = BuildStyled(node.Children[i], [.. path, i]);
            Flex(child, node.Children[i], childStyle);
            flex.Children.Add(child);
        }

        return flex;
    }
}
