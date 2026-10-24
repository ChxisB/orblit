using Orblit.Json;
using Orblit.VectorMath;

namespace Orblit.Ui;

/// <summary>
/// Reads a CSS declaration list such as <c>padding: 8px; color: #fff</c> into one <see cref="UiStyle"/>.
/// It takes the subset a box here can show. A property it does not know is left out of the style
/// and named by <see cref="UnknownIn"/>.
/// </summary>
public sealed class UiCss(UiTheme? theme = null)
{
    private static readonly Dictionary<string, Func<double, UiStyle>> LengthRules = new()
    {
        ["gap"] = v => new() { Gap = v },
        ["padding-top"] = v => new() { PaddingTop = v },
        ["padding-right"] = v => new() { PaddingRight = v },
        ["padding-bottom"] = v => new() { PaddingBottom = v },
        ["padding-left"] = v => new() { PaddingLeft = v },
        ["margin-top"] = v => new() { MarginTop = v },
        ["margin-right"] = v => new() { MarginRight = v },
        ["margin-bottom"] = v => new() { MarginBottom = v },
        ["margin-left"] = v => new() { MarginLeft = v },
        ["width"] = v => new() { Width = v },
        ["height"] = v => new() { Height = v },
        ["min-width"] = v => new() { MinWidth = v },
        ["min-height"] = v => new() { MinHeight = v },
        ["max-width"] = v => new() { MaxWidth = v },
        ["max-height"] = v => new() { MaxHeight = v },
        ["top"] = v => new() { Top = v },
        ["right"] = v => new() { Right = v },
        ["bottom"] = v => new() { Bottom = v },
        ["left"] = v => new() { Left = v },
        ["border-width"] = v => new() { BorderWidth = v },
        ["border-radius"] = v => new() { Radius = v },
        ["font-size"] = v => new() { FontSize = v },
        ["letter-spacing"] = v => new() { LetterSpacing = v },
    };

    private static readonly Dictionary<string, Func<UiColour, UiStyle>> ColourRules = new()
    {
        ["background"] = c => new() { Background = c },
        ["background-color"] = c => new() { Background = c },
        ["color"] = c => new() { Colour = c },
        ["border-color"] = c => new() { BorderColour = c },
    };

    private static readonly Dictionary<string, string> Across = new()
    {
        ["flex-start"] = "start",
        ["start"] = "start",
        ["center"] = "center",
        ["flex-end"] = "end",
        ["end"] = "end",
        ["stretch"] = "stretch",
        ["baseline"] = "baseline",
    };

    private static readonly Dictionary<string, string> Along = new()
    {
        ["flex-start"] = "start",
        ["start"] = "start",
        ["center"] = "center",
        ["flex-end"] = "end",
        ["end"] = "end",
        ["space-between"] = "between",
        ["space-around"] = "around",
        ["space-evenly"] = "evenly",
    };

    private static readonly Dictionary<string, Func<UiCss, string, UiStyle?>> OtherRules = new()
    {
        ["display"] = (_, v) => Keyword(v, ("flex", "row"), ("block", "column")),
        ["flex-direction"] = (_, v) => Keyword(v, ("row", "row"), ("column", "column")),
        ["position"] = (_, v) => v == "absolute" ? new UiStyle() : null,
        ["flex"] = (_, v) => Number(PlainText.SplitSpace(v)[0], n => new() { Grow = n }),
        ["flex-grow"] = (_, v) => Number(PlainText.SplitSpace(v)[0], n => new() { Grow = n }),
        ["align-items"] = (_, v) => new UiStyle { CrossAxis = Across.GetValueOrDefault(v) },
        ["justify-content"] = (_, v) => new UiStyle { MainAxis = Along.GetValueOrDefault(v) },
        ["text-align"] = (_, v) => v is "left" or "center" or "right" or "justify" ? new UiStyle { TextAlign = v } : null,
        ["padding"] = (u, v) => u.Sides(v) is { } e ? new UiStyle { PaddingTop = e.Top, PaddingRight = e.Right, PaddingBottom = e.Bottom, PaddingLeft = e.Left } : null,
        ["margin"] = (u, v) => u.Sides(v) is { } e ? new UiStyle { MarginTop = e.Top, MarginRight = e.Right, MarginBottom = e.Bottom, MarginLeft = e.Left } : null,
        ["border"] = (u, v) => u.Border(v),
        ["opacity"] = (_, v) => Number(v, n => new() { Opacity = Doubles.Clamp(n, 0, 1) }),
        ["font-weight"] = (_, v) => FontWeight(v) is { } weight ? new UiStyle { FontWeight = weight } : null,
        ["font-style"] = (_, v) => Keyword(v, ("italic", true), ("normal", false)),
        ["line-height"] = (_, v) => Number(v, n => new() { LineHeight = n }),
        ["text-transform"] = (_, v) => v == "uppercase" ? new UiStyle { Uppercase = true } : null,
        ["overflow"] = (_, v) => Overflow(v, "y"),
        ["overflow-y"] = (_, v) => Overflow(v, "y"),
        ["overflow-x"] = (_, v) => Overflow(v, "x"),
        ["box-shadow"] = (u, v) => u.Lengths(v) is [.., var last] ? new UiStyle { Shadow = last } : new UiStyle { Shadow = 8 },
    };

    private static readonly string[] Units = ["px", "rem", "em", "pt"];

    private readonly UiTheme _theme = theme ?? new UiTheme();

    /// <summary>
    /// The style a declaration list gives. A rule with braces around it, such as <c>.card { padding: 6px }</c>,
    /// is read for what is inside them. Later declarations win.
    /// </summary>
    public UiStyle Parse(string source)
    {
        var text = PlainText.Trim(source);
        var open = text.IndexOf('{');
        if (open >= 0 && text.EndsWith('}'))
        {
            text = text[(open + 1)..^1];
        }

        return Declarations(text).Aggregate(
            UiStyle.None,
            (style, declaration) => Declaration(declaration.Property, declaration.Value) is { } rule ? style.Merge(rule) : style);
    }

    /// <summary>
    /// The properties in a declaration list that <see cref="Parse"/> would not use. Braces are not read
    /// here, so a rule written with them names its first property with the selector in front.
    /// </summary>
    public List<string> UnknownIn(string source) =>
        [.. Declarations(source).Where(d => Declaration(d.Property, d.Value) is null).Select(d => d.Property)];

    // A declaration with no colon, no property or no value is skipped, not unknown.
    private static IEnumerable<(string Property, string Value)> Declarations(string text) =>
        text.Split(';')
            .Select(declaration => (Colon: declaration.IndexOf(':'), Declaration: declaration))
            .Where(d => d.Colon >= 0)
            .Select(d => (Property: PlainText.ToLower(PlainText.Trim(d.Declaration[..d.Colon])), Value: PlainText.Trim(d.Declaration[(d.Colon + 1)..])))
            .Where(d => d.Property.Length > 0 && d.Value.Length > 0);

    private UiStyle? Declaration(string property, string value)
    {
        if (LengthRules.TryGetValue(property, out var build))
        {
            return Length(value) is { } length ? build(length) : null;
        }

        if (ColourRules.TryGetValue(property, out var paint))
        {
            return Colour(value) is { } colour ? paint(colour) : null;
        }

        return OtherRules.TryGetValue(property, out var read) ? read(this, value) : null;
    }

    private static UiStyle? Keyword(string value, params (string Word, string Direction)[] words) =>
        words.Where(w => w.Word == value).Select(w => new UiStyle { Direction = w.Direction }).FirstOrDefault();

    private static UiStyle? Keyword(string value, params (string Word, bool Italic)[] words) =>
        words.Where(w => w.Word == value).Select(w => new UiStyle { Italic = w.Italic }).FirstOrDefault();

    private static UiStyle? Number(string text, Func<double, UiStyle> build) =>
        PlainText.ParseDouble(text) is { } number ? build(number) : null;

    private static long? FontWeight(string value) => value switch
    {
        "normal" => 400,
        "bold" => 700,
        _ => PlainText.ParseInt(value),
    };

    private static UiStyle? Overflow(string value, string axis) => value switch
    {
        "hidden" => new UiStyle { Clip = true },
        "auto" or "scroll" => new UiStyle { Clip = true, Scroll = axis },
        _ => null,
    };

    private UiStyle Border(string value)
    {
        var style = new UiStyle { BorderWidth = 1 };
        foreach (var part in PlainText.SplitSpace(value))
        {
            if (Length(part) is { } length)
            {
                style = style with { BorderWidth = length };
            }
            else if (Colour(part) is { } colour)
            {
                style = style with { BorderColour = colour };
            }
        }

        return style;
    }

    private (double Top, double Right, double Bottom, double Left)? Sides(string value) => Lengths(value) switch
    {
        [var all] => (all, all, all, all),
        [var vertical, var horizontal] => (vertical, horizontal, vertical, horizontal),
        [var top, var horizontal, var bottom] => (top, horizontal, bottom, horizontal),
        [var top, var right, var bottom, var left] => (top, right, bottom, left),
        _ => null,
    };

    private List<double> Lengths(string value) =>
        [.. PlainText.SplitSpace(value).Select(Length).OfType<double>()];

    private double? Length(string value)
    {
        var text = PlainText.ToLower(PlainText.Trim(value));
        switch (text)
        {
            case "0":
                return 0;
            case "auto" or "none":
                return null;
            case "100%":
                return double.PositiveInfinity;
        }

        var unit = Units.FirstOrDefault(u => text.EndsWith(u, StringComparison.Ordinal));
        if (unit is null)
        {
            return PlainText.ParseDouble(text);
        }

        return PlainText.ParseDouble(text[..^unit.Length]) is { } number ? Convert(unit, number) : null;
    }

    // The order of the multiplications is part of the result, so the factors are not folded together.
    private double Convert(string unit, double number) => unit switch
    {
        "rem" or "em" => number * _theme.Step * 4,
        "pt" => number * 4 / 3,
        _ => number,
    };

    private UiColour? Colour(string value) => UiColour.FromText(value) ?? _theme.Colour(PlainText.Trim(value));
}
