using System.Collections.Frozen;
using System.Globalization;
using Orblit.Filament;
using Orblit.Json;

namespace Orblit.Stage;

/// <summary>One uniform an author declared, as an inspector needs to show it.</summary>
public sealed record CookedParam
{
    public required string Name { get; init; }

    /// <summary><c>bool</c>, <c>int</c>, <c>float</c>, <c>vec2</c>, <c>vec3</c>, <c>vec4</c> or <c>sampler2D</c>.</summary>
    public required string Type { get; init; }

    /// <summary>
    /// Whether each object may state its own. The renderer takes a value for any declared name, so this
    /// only says which ones an editor should offer.
    /// </summary>
    public bool Instance { get; init; }

    /// <summary>Each hint the author wrote, by name, with its arguments as written.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Hints { get; init; } =
        FrozenDictionary<string, IReadOnlyList<string>>.Empty;

    /// <summary>A colour the author marked <c>source_color</c>.</summary>
    public bool IsColour => Hints.ContainsKey("source_color");

    /// <summary>
    /// The slider bounds from <c>hint_range(min, max, step)</c>, or null. The step is nought when the
    /// author left it out.
    /// </summary>
    public (double Min, double Max, double Step)? Range
    {
        get
        {
            if (!Hints.TryGetValue("hint_range", out var args) || args.Count < 2)
            {
                return null;
            }

            if (!TryNumber(args[0], out var min) || !TryNumber(args[1], out var max) || min >= max)
            {
                return null;
            }

            return (min, max, args.Count > 2 && TryNumber(args[2], out var step) ? step : 0);
        }
    }

    private static bool TryNumber(string text, out double number) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
}

/// <summary>
/// An author-written surface after the cook: where its package is, and what it declares.
/// </summary>
/// <remarks>
/// The package carries no defaults, so the stage sends them. An object may state only what is declared
/// here, which keeps a value left in a scene for a uniform the author has since removed from reaching
/// the renderer.
/// </remarks>
public sealed record CookedSurface
{
    /// <summary>The width of each type word a <c>params.json</c> can hold. A picture has none.</summary>
    private static readonly FrozenDictionary<string, int> WidthOfType = new Dictionary<string, int>
    {
        ["bool"] = 1,
        ["int"] = 1,
        ["float"] = 1,
        ["vec2"] = 2,
        ["vec3"] = 3,
        ["vec4"] = 4,
        ["sampler2D"] = 0,
    }.ToFrozenDictionary();

    /// <summary>The compiled package, relative to the project.</summary>
    public required string Package { get; init; }

    /// <summary>How many numbers each number, flag and vector takes, by name.</summary>
    public IReadOnlyDictionary<string, int> Widths { get; init; } = FrozenDictionary<string, int>.Empty;

    /// <summary>What the author wrote after each uniform, by name.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<double>> Defaults { get; init; } =
        FrozenDictionary<string, IReadOnlyList<double>>.Empty;

    /// <summary>Every picture the surface reads, by name, and whether it holds a colour.</summary>
    public IReadOnlyDictionary<string, bool> Pictures { get; init; } = FrozenDictionary<string, bool>.Empty;

    /// <summary>Every uniform the author declared, in the order they wrote them.</summary>
    public IReadOnlyList<CookedParam> Params { get; init; } = [];

    /// <summary>
    /// Reads the <c>params.json</c> the cook writes beside the package, as decoded JSON. Anything it
    /// cannot read is left out, so a file from a newer cook still loads.
    /// </summary>
    public static CookedSurface FromParams(string package, object? declared)
    {
        var widths = new OrderedDictionary<string, int>();
        var defaults = new OrderedDictionary<string, IReadOnlyList<double>>();
        var pictures = new OrderedDictionary<string, bool>();
        var read = new List<CookedParam>();
        foreach (var entry in declared as IReadOnlyList<object?> ?? [])
        {
            if (entry is not IReadOnlyDictionary<string, object?> map || ParamOf(map) is not { } param)
            {
                continue;
            }

            read.Add(param);
            var width = WidthOfType[param.Type];
            if (width == 0)
            {
                pictures[param.Name] = param.IsColour;
                continue;
            }

            widths[param.Name] = width;
            if (NumbersOf(map.GetValueOrDefault("default")) is { } given && given.Length == width)
            {
                defaults[param.Name] = given;
            }
        }

        return new CookedSurface
        {
            Package = package,
            Widths = widths,
            Defaults = defaults,
            Pictures = pictures,
            Params = read,
        };
    }

    /// <summary>
    /// The numbers the surface draws <paramref name="name"/> with for an object that states
    /// <paramref name="given"/>: its own when they fit the width, else the default, else zeros.
    /// </summary>
    public IReadOnlyList<double> ValueOf(string name, IReadOnlyDictionary<string, object> given) =>
        Stated(name, given) ?? Defaults.GetValueOrDefault(name) ?? new double[Widths.GetValueOrDefault(name, 1)];

    /// <summary>
    /// The surface one object draws: the defaults, then what <paramref name="given"/> states. Null when
    /// the package cannot be placed. A value of the wrong width, or for a name nothing declares, is left
    /// out and the default stands.
    /// </summary>
    public CustomSurface? SurfaceFor(IReadOnlyDictionary<string, object> given, Func<string, string?> locate)
    {
        if (locate(Package) is not { } path)
        {
            return null;
        }

        var values = new OrderedDictionary<string, IReadOnlyList<double>>(Defaults);
        var textures = new OrderedDictionary<string, Texture>();
        foreach (var (name, value) in given)
        {
            if (Pictures.TryGetValue(name, out var colour))
            {
                if (value is string file && locate(file) is { } found)
                {
                    textures[name] = new Texture(found, colour);
                }
            }
            else if (Stated(name, given) is { } numbers)
            {
                values[name] = numbers;
            }
        }

        return new CustomSurface(path, values, textures);
    }

    private double[]? Stated(string name, IReadOnlyDictionary<string, object> given) =>
        given.TryGetValue(name, out var raw) && NumbersOf(raw) is { } numbers && numbers.Length == Widths.GetValueOrDefault(name, -1)
            ? numbers
            : null;

    private static CookedParam? ParamOf(IReadOnlyDictionary<string, object?> entry) =>
        entry.GetValueOrDefault("name") is string name
        && entry.GetValueOrDefault("type") is string type
        && WidthOfType.ContainsKey(type)
            ? new CookedParam
            {
                Name = name,
                Type = type,
                Instance = entry.GetValueOrDefault("instance") is true,
                Hints = HintsOf(entry.GetValueOrDefault("hints")),
            }
            : null;

    // A number, a flag or a list of numbers as the doubles a renderer is sent.
    private static double[]? NumbersOf(object? raw) => raw switch
    {
        long whole => [whole],
        double fraction => [fraction],
        bool flag => [flag ? 1.0 : 0.0],
        IReadOnlyList<object?> list when list.All(static one => one is long or double) =>
            [.. list.Select(static one => one is long whole ? whole : (double)one!)],
        _ => null,
    };

    // The hints of one declaration: each name with its arguments as text.
    private static OrderedDictionary<string, IReadOnlyList<string>> HintsOf(object? raw)
    {
        var hints = new OrderedDictionary<string, IReadOnlyList<string>>();
        foreach (var (name, args) in raw as IReadOnlyDictionary<string, object?> ?? FrozenDictionary<string, object?>.Empty)
        {
            hints[name] = args is IReadOnlyList<object?> list
                ? [.. list.Select(static arg => arg as string ?? JsonText.Encode(arg))]
                : [];
        }

        return hints;
    }
}
