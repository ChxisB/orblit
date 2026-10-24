using Orblit.Json;
using Orblit.VectorMath;

namespace Orblit.Ui;

/// <summary>
/// Reads a class list such as <c>p-4 row md:text-xl</c> into one <see cref="UiStyle"/>. A word it does
/// not know is left out of the style and named by <see cref="UnknownIn"/>.
/// </summary>
public sealed class UiUtilities(UiTheme? theme = null)
{
    private static readonly Dictionary<string, UiStyle> Words = new()
    {
        ["row"] = new() { Direction = "row" },
        ["col"] = new() { Direction = "column" },
        ["column"] = new() { Direction = "column" },
        ["stack"] = new() { Direction = "stack" },
        ["grow"] = new() { Grow = 1 },
        ["clip"] = new() { Clip = true },
        ["scroll-y"] = new() { Scroll = "y" },
        ["scroll-x"] = new() { Scroll = "x" },
        ["italic"] = new() { Italic = true },
        ["uppercase"] = new() { Uppercase = true },
        ["border"] = new() { BorderWidth = 1 },
        ["shadow"] = new() { Shadow = 8 },
        ["shadow-lg"] = new() { Shadow = 20 },
        ["w-full"] = new() { Width = double.PositiveInfinity },
        ["h-full"] = new() { Height = double.PositiveInfinity },
        ["full"] = new() { Width = double.PositiveInfinity, Height = double.PositiveInfinity },
    };

    private static readonly string[] CrossAxes = ["start", "center", "end", "stretch", "baseline"];

    private static readonly string[] MainAxes = ["start", "center", "end", "between", "around", "evenly"];

    private static readonly string[] Alignments = ["left", "center", "right", "justify"];

    private static readonly Dictionary<string, long> Weights = new()
    {
        ["thin"] = 100,
        ["light"] = 300,
        ["normal"] = 400,
        ["medium"] = 500,
        ["semibold"] = 600,
        ["bold"] = 700,
        ["black"] = 900,
    };

    // Every prefix ends in a hyphen and none starts another, so at most one applies to a word.
    private static readonly (string Prefix, Func<UiUtilities, string, UiStyle?> Read)[] Rules =
    [
        ("items-", (_, rest) => OneOf(rest, CrossAxes, v => new() { CrossAxis = v })),
        ("justify-", (_, rest) => OneOf(rest, MainAxes, v => new() { MainAxis = v })),
        ("text-", (u, rest) => u.TextRule(rest)),
        ("font-", (_, rest) => Weights.TryGetValue(rest, out var weight) ? new UiStyle { FontWeight = weight } : null),
        ("bg-", (u, rest) => u.Colour(rest) is { } fill ? new UiStyle { Background = fill } : null),
        ("border-", (u, rest) => u.BorderRule(rest)),
        ("rounded-", (u, rest) => u.RoundedRule(rest)),
        ("opacity-", (_, rest) => Number(rest, v => new() { Opacity = Doubles.Clamp(v / 100, 0, 1) })),
        ("flex-", (_, rest) => Number(rest, v => new() { Grow = v })),
        ("leading-", (_, rest) => Number(rest, v => new() { LineHeight = v })),
        ("tracking-", (_, rest) => Number(rest, v => new() { LetterSpacing = v })),
        Sized("p-", v => new() { PaddingTop = v, PaddingRight = v, PaddingBottom = v, PaddingLeft = v }),
        Sized("px-", v => new() { PaddingLeft = v, PaddingRight = v }),
        Sized("py-", v => new() { PaddingTop = v, PaddingBottom = v }),
        Sized("pt-", v => new() { PaddingTop = v }),
        Sized("pr-", v => new() { PaddingRight = v }),
        Sized("pb-", v => new() { PaddingBottom = v }),
        Sized("pl-", v => new() { PaddingLeft = v }),
        Sized("m-", v => new() { MarginTop = v, MarginRight = v, MarginBottom = v, MarginLeft = v }),
        Sized("mx-", v => new() { MarginLeft = v, MarginRight = v }),
        Sized("my-", v => new() { MarginTop = v, MarginBottom = v }),
        Sized("mt-", v => new() { MarginTop = v }),
        Sized("mr-", v => new() { MarginRight = v }),
        Sized("mb-", v => new() { MarginBottom = v }),
        Sized("ml-", v => new() { MarginLeft = v }),
        Sized("w-", v => new() { Width = v }),
        Sized("h-", v => new() { Height = v }),
        Sized("min-w-", v => new() { MinWidth = v }),
        Sized("min-h-", v => new() { MinHeight = v }),
        Sized("max-w-", v => new() { MaxWidth = v }),
        Sized("max-h-", v => new() { MaxHeight = v }),
        Sized("inset-", v => new() { Top = v, Right = v, Bottom = v, Left = v }),
        Sized("top-", v => new() { Top = v }),
        Sized("right-", v => new() { Right = v }),
        Sized("bottom-", v => new() { Bottom = v }),
        Sized("left-", v => new() { Left = v }),
        Sized("gap-", v => new() { Gap = v }),
    ];

    private readonly UiTheme _theme = theme ?? new UiTheme();

    /// <summary>
    /// The style a class list gives. A word with a breakpoint in front, such as <c>md:p-4</c>, counts
    /// only at a <paramref name="width"/> where that breakpoint is active, and wins over a plain
    /// word. Without a width only plain words count. Later words win.
    /// </summary>
    public UiStyle Parse(string classes, double? width = null)
    {
        var names = PlainText.SplitSpace(classes).Where(name => name.Length > 0).ToList();
        var layers = new List<string> { string.Empty };
        if (width is { } at)
        {
            layers.AddRange(_theme.Breakpoints.ActiveAt(at));
        }

        return layers.Aggregate(UiStyle.None, (style, layer) => names.Aggregate(style, (s, name) => Apply(s, name, layer)));
    }

    /// <summary>The words in a class list that <see cref="Parse"/> would not use at any width.</summary>
    public List<string> UnknownIn(string classes) =>
        [.. PlainText.SplitSpace(classes).Where(name => name.Length > 0 && !Knows(name))];

    private UiStyle Apply(UiStyle style, string name, string layer)
    {
        var (prefix, bare) = SplitLayer(name);
        return prefix == layer && Rule(bare) is { } rule ? style.Merge(rule) : style;
    }

    private static (string Prefix, string Name) SplitLayer(string name)
    {
        var colon = name.IndexOf(':');
        return colon < 0 ? (string.Empty, name) : (name[..colon], name[(colon + 1)..]);
    }

    private bool Knows(string name)
    {
        var colon = name.IndexOf(':');
        return colon < 0
            ? Rule(name) is not null
            : _theme.Breakpoints.Knows(name[..colon]) && Rule(name[(colon + 1)..]) is not null;
    }

    private UiStyle? Rule(string name)
    {
        if (name == "rounded")
        {
            return new UiStyle { Radius = Lookup(_theme.Radius, "md") };
        }

        if (Words.TryGetValue(name, out var word))
        {
            return word;
        }

        foreach (var (prefix, read) in Rules)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return read(this, name[prefix.Length..]);
            }
        }

        return null;
    }

    private UiStyle? TextRule(string rest)
    {
        if (_theme.Text.TryGetValue(rest, out var size))
        {
            return new UiStyle { FontSize = size };
        }

        if (Alignments.Contains(rest))
        {
            return new UiStyle { TextAlign = rest };
        }

        return _theme.Colour(rest) is { } colour ? new UiStyle { Colour = colour } : null;
    }

    private UiStyle? BorderRule(string rest)
    {
        if (PlainText.ParseDouble(rest) is { } width)
        {
            return new UiStyle { BorderWidth = width };
        }

        return Colour(rest) is { } colour ? new UiStyle { BorderColour = colour } : null;
    }

    private UiStyle? RoundedRule(string rest)
    {
        if (_theme.Radius.TryGetValue(rest, out var named))
        {
            return new UiStyle { Radius = named };
        }

        return Number(rest, v => new UiStyle { Radius = v });
    }

    private static (string Prefix, Func<UiUtilities, string, UiStyle?> Read) Sized(string prefix, Func<double, UiStyle> build) =>
        (prefix, (u, rest) => u.Measure(rest) is { } value ? build(value) : null);

    private static UiStyle? OneOf(string how, string[] allowed, Func<string, UiStyle> build) =>
        allowed.Contains(how) ? build(how) : null;

    private static UiStyle? Number(string text, Func<double, UiStyle> build) =>
        PlainText.ParseDouble(text) is { } number ? build(number) : null;

    private static double? Lookup(IReadOnlyDictionary<string, double> table, string name) =>
        table.TryGetValue(name, out var value) ? value : null;

    private double? Measure(string value)
    {
        if (value.StartsWith('[') && value.EndsWith(']'))
        {
            return PlainText.ParseDouble(value[1..^1]);
        }

        return value switch
        {
            "full" => double.PositiveInfinity,
            "px" => 1,
            _ => PlainText.ParseDouble(value) * _theme.Step,
        };
    }

    private UiColour? Colour(string value) =>
        value.StartsWith('[') && value.EndsWith(']') ? UiColour.FromText(value[1..^1]) : _theme.Colour(value);
}
