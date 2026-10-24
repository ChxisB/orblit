using System.Collections.ObjectModel;

namespace Orblit.Ui;

/// <summary>
/// The scale a class list and a CSS declaration read their words against: a spacing step, named
/// text sizes and corner radii, a palette and the breakpoints.
/// </summary>
public sealed class UiTheme
{
    private static readonly IReadOnlyDictionary<string, double> DefaultText = Table<double>(
        ("xs", 11), ("sm", 12.5), ("base", 14), ("lg", 16), ("xl", 19), ("2xl", 23), ("3xl", 30), ("4xl", 38));

    private static readonly IReadOnlyDictionary<string, double> DefaultRadius = Table<double>(
        ("none", 0), ("sm", 3), ("md", 6), ("lg", 10), ("xl", 16), ("2xl", 24), ("full", 9999));

    /// <summary>The palette a theme has until it is given another. Names run from the greys to the accents.</summary>
    public static IReadOnlyDictionary<string, UiColour> DefaultPalette { get; } = Table<UiColour>(
        ("transparent", new UiColour(0x00000000)),
        ("black", new UiColour(0xFF000000)),
        ("white", new UiColour(0xFFFFFFFF)),
        ("current", new UiColour(0xFFFFFFFF)),
        ("slate-50", new UiColour(0xFFF6F8FA)),
        ("slate-100", new UiColour(0xFFE9EEF4)),
        ("slate-200", new UiColour(0xFFD3DBE5)),
        ("slate-300", new UiColour(0xFFB2BECD)),
        ("slate-400", new UiColour(0xFF8593A6)),
        ("slate-500", new UiColour(0xFF64748B)),
        ("slate-600", new UiColour(0xFF4A5768)),
        ("slate-700", new UiColour(0xFF333E4C)),
        ("slate-800", new UiColour(0xFF212932)),
        ("slate-900", new UiColour(0xFF141920)),
        ("ember-50", new UiColour(0xFFFDF1E9)),
        ("ember-100", new UiColour(0xFFF9DCC7)),
        ("ember-200", new UiColour(0xFFF2BA95)),
        ("ember-300", new UiColour(0xFFE8975F)),
        ("ember-400", new UiColour(0xFFD97B37)),
        ("ember-500", new UiColour(0xFFC25E22)),
        ("ember-600", new UiColour(0xFFA34C1B)),
        ("ember-700", new UiColour(0xFF7E3A15)),
        ("ember-800", new UiColour(0xFF5A290F)),
        ("ember-900", new UiColour(0xFF3A1A09)),
        ("steel-50", new UiColour(0xFFEFF7FA)),
        ("steel-100", new UiColour(0xFFD9ECF3)),
        ("steel-200", new UiColour(0xFFB0D8E6)),
        ("steel-300", new UiColour(0xFF7FBFD4)),
        ("steel-400", new UiColour(0xFF56A3BE)),
        ("steel-500", new UiColour(0xFF3B7F97)),
        ("steel-600", new UiColour(0xFF2F667B)),
        ("steel-700", new UiColour(0xFF244F5F)),
        ("steel-800", new UiColour(0xFF193843)),
        ("steel-900", new UiColour(0xFF0F2229)),
        ("moss-50", new UiColour(0xFFEEF7F1)),
        ("moss-100", new UiColour(0xFFD6EDDE)),
        ("moss-200", new UiColour(0xFFAADCBC)),
        ("moss-300", new UiColour(0xFF79C595)),
        ("moss-400", new UiColour(0xFF4FA871)),
        ("moss-500", new UiColour(0xFF2F7A4E)),
        ("moss-600", new UiColour(0xFF26643F)),
        ("moss-700", new UiColour(0xFF1D4D31)),
        ("moss-800", new UiColour(0xFF143522)),
        ("moss-900", new UiColour(0xFF0C2015)),
        ("rose-50", new UiColour(0xFFFDF0F1)),
        ("rose-100", new UiColour(0xFFF9D8DB)),
        ("rose-200", new UiColour(0xFFF2B0B6)),
        ("rose-300", new UiColour(0xFFE8828B)),
        ("rose-400", new UiColour(0xFFD95E70)),
        ("rose-500", new UiColour(0xFFC2415A)),
        ("rose-600", new UiColour(0xFFA33449)),
        ("rose-700", new UiColour(0xFF7E2839)),
        ("rose-800", new UiColour(0xFF5A1B28)),
        ("rose-900", new UiColour(0xFF3A1119)),
        ("amber-50", new UiColour(0xFFFDF7E9)),
        ("amber-100", new UiColour(0xFFF9EBC5)),
        ("amber-200", new UiColour(0xFFF2D68F)),
        ("amber-300", new UiColour(0xFFE8BE55)),
        ("amber-400", new UiColour(0xFFD9A62D)),
        ("amber-500", new UiColour(0xFFB98A1C)),
        ("amber-600", new UiColour(0xFF977016)),
        ("amber-700", new UiColour(0xFF745611)),
        ("amber-800", new UiColour(0xFF523D0C)),
        ("amber-900", new UiColour(0xFF352706))
    );

    /// <summary>The pixels in one spacing unit, so <c>p-4</c> is four steps.</summary>
    public double Step { get; init; } = 4;

    /// <summary>Font sizes by name, in pixels.</summary>
    public IReadOnlyDictionary<string, double> Text { get; init; } = DefaultText;

    /// <summary>Corner radii by name, in pixels.</summary>
    public IReadOnlyDictionary<string, double> Radius { get; init; } = DefaultRadius;

    /// <summary>Colours by name.</summary>
    public IReadOnlyDictionary<string, UiColour> Palette { get; init; } = DefaultPalette;

    /// <summary>Where the layout changes.</summary>
    public UiBreakpoints Breakpoints { get; init; } = new();

    /// <summary>
    /// The same theme with the step, the text sizes and the radii multiplied by <paramref name="factor"/>.
    /// A factor that is not a positive number, or is 1, gives this theme back.
    /// </summary>
    public UiTheme Scaled(double factor)
    {
        if (!double.IsFinite(factor) || factor <= 0 || factor == 1)
        {
            return this;
        }

        return new UiTheme
        {
            Step = Step * factor,
            Text = Times(Text, factor),
            Radius = Times(Radius, factor),
            Palette = Palette,
            Breakpoints = Breakpoints,
        };
    }

    /// <summary>The colour a name stands for, or null for a name the palette lacks.</summary>
    public UiColour? Colour(string name) => Palette.TryGetValue(name, out var colour) ? colour : null;

    private static ReadOnlyDictionary<string, T> Table<T>(params (string Name, T Value)[] rows)
    {
        var table = new OrderedDictionary<string, T>();
        foreach (var (name, value) in rows)
        {
            table[name] = value;
        }

        return new ReadOnlyDictionary<string, T>(table);
    }

    private static ReadOnlyDictionary<string, double> Times(IReadOnlyDictionary<string, double> table, double factor) =>
        Table([.. table.Select(e => (e.Key, e.Value * factor))]);
}
