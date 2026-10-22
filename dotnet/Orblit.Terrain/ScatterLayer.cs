using System.Globalization;
using Orblit.VectorMath;

namespace Orblit.Terrain;

/// <summary>
/// One kind of thing scattered over the ground: a grass, a stone, a tree.
/// Rules, not places. Where each one stands is worked out from these and the ground by a
/// <c>ScatterPlacer</c>, so the rules are a line of the terrain's settings file however many
/// thousand things they put down, and an edit to the ground moves what stands on it without
/// anybody placing it again.
/// A layer with no <see cref="Mesh"/> is a block: a box <see cref="Size"/> across, standing on its
/// bottom face, and drawn by the renderer's built-in cube, which is what lets a hundred thousand
/// blades of grass be one draw. A layer that names a mesh stands on the model's origin.
/// </summary>
public sealed class ScatterLayer(
    string name,
    long seed = 0,
    double density = 1,
    IReadOnlyList<int>? sets = null,
    double minSlope = 0,
    double maxSlope = 90,
    double minHeight = double.NegativeInfinity,
    double maxHeight = double.PositiveInfinity,
    (double, double, double)? size = null,
    double minScale = 1,
    double maxScale = 1,
    double lean = 0,
    bool turn = true,
    double lift = 0,
    long colour = 0xFFFFFF,
    double colourVariation = 0,
    double groundTint = 0,
    double range = 0,
    bool castShadows = false,
    string? mesh = null,
    string? material = null) : IEquatable<ScatterLayer>
{
    /// <summary>
    /// The most a layer can put down per square metre. Eight centimetres apart: past this a layer
    /// is a carpet, not a scatter, and a region of it would be millions.
    /// </summary>
    public const double MaxDensity = 100;

    /// <summary>What the layer is called where people choose it: "grass", "boulders".</summary>
    public string Name { get; } = name;

    /// <summary>
    /// Which pattern it is laid in. Layers with the same seed and the same <see cref="Density"/>
    /// land in the same places and take the same turns and sizes, which is how a trunk and its
    /// crown are two layers; give every other layer a seed of its own, or two of them will stand in
    /// each other.
    /// </summary>
    public long Seed { get; } = seed;

    /// <summary>
    /// How many are put down per square metre where the ground is all of the layer's
    /// <see cref="Sets"/> and inside its slope and height. Fewer where the sets share the ground
    /// with others, none where they are absent.
    /// </summary>
    public double Density { get; } = CheckDensity(density);

    /// <summary>
    /// The terrain sets it grows on, by index. Empty grows on any ground.
    /// Where a texel blends two sets, the layer has the share its sets hold: grass on a texel that
    /// is a quarter scree grows three-quarters as thick. Automatic ground counts as the flat and
    /// steep sets it chooses between.
    /// </summary>
    public IReadOnlyList<int> Sets { get; } = [.. sets ?? []];

    /// <summary>The gentlest ground it stands on, in degrees from level. Scree starts on cliffs.</summary>
    public double MinSlope { get; } = minSlope;

    /// <summary>The steepest ground it stands on, in degrees from level. Trees stop on cliffs.</summary>
    public double MaxSlope { get; } = maxSlope;

    /// <summary>The lowest ground it stands on, in metres. Unbounded unless set.</summary>
    public double MinHeight { get; } = minHeight;

    /// <summary>The highest ground it stands on, in metres. Unbounded unless set.</summary>
    public double MaxHeight { get; } = maxHeight;

    /// <summary>
    /// At full size: a block's width, height and depth in metres, or how much a model is scaled
    /// along its own x, y and z.
    /// </summary>
    public (double, double, double) Size { get; } = size ?? (1, 1, 1);

    /// <summary>The least of <see cref="Size"/> each one is, chosen at random between.</summary>
    public double MinScale { get; } = minScale;

    /// <summary>The most of <see cref="Size"/> each one is, chosen at random between.</summary>
    public double MaxScale { get; } = maxScale;

    /// <summary>
    /// How far each leans with the ground under it, 0 to 1. At 0 it stands straight up whatever
    /// the slope, as a tree does; at 1 it lies square to the ground, as a stone does.
    /// </summary>
    public double Lean { get; } = lean;

    /// <summary>
    /// Whether each is turned a random way about its own up, so a thousand of one model do not all
    /// face north.
    /// </summary>
    public bool Turn { get; } = turn;

    /// <summary>
    /// How far above the ground each one's base is, in metres at full size. Negative sinks it in,
    /// so a stone on a slope does not show its underside and an upright trunk does not stand on one
    /// edge.
    /// </summary>
    public double Lift { get; } = lift;

    /// <summary>
    /// The colour, <c>0xRRGGBB</c>, sRGB. A block is drawn in it; a model is drawn in its
    /// <see cref="Material"/> and ignores it.
    /// </summary>
    public long Colour { get; } = colour;

    /// <summary>
    /// How much brighter or darker each one is at random, 0 to 1: at 0.2, any shade from 80% to
    /// 120% of <see cref="Colour"/>.
    /// </summary>
    public double ColourVariation { get; } = colourVariation;

    /// <summary>
    /// How much the ground's colour map tints each one, 0 to 1, so grass painted yellow grows
    /// yellow.
    /// </summary>
    public double GroundTint { get; } = groundTint;

    /// <summary>
    /// How far off they are still drawn, in metres. Zero draws every one. Grass is gone a stone's
    /// throw away; a forest is seen from the next valley.
    /// </summary>
    public double Range { get; } = range;

    /// <summary>
    /// Whether they cast shadows. Off by default: a shadow per blade of grass costs a great deal to
    /// show very little.
    /// </summary>
    public bool CastShadows { get; } = castShadows;

    /// <summary>The model each one is, a path within the project, or null for a block.</summary>
    public string? Mesh { get; } = mesh;

    /// <summary>
    /// What a <see cref="Mesh"/> is made of, an <c>.omat</c> path within the project. Without one
    /// the model wears its file's own materials, and each copy is drawn on its own rather than all
    /// of them in one go.
    /// </summary>
    public string? Material { get; } = material;

    public static bool operator ==(ScatterLayer? left, ScatterLayer? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ScatterLayer? left, ScatterLayer? right) => !(left == right);

    /// <summary>Whether the set at <paramref name="index"/> is one this layer grows on.</summary>
    public bool GrowsOn(int index) => Sets.Count == 0 || Sets.Contains(index);

    public bool Equals(ScatterLayer? other) =>
        other is not null &&
        other.Sets.SequenceEqual(Sets) &&
        (Name, Seed, Density, MinSlope, MaxSlope, MinHeight, MaxHeight).Equals(
            (other.Name, other.Seed, other.Density, other.MinSlope, other.MaxSlope, other.MinHeight, other.MaxHeight)) &&
        (Size, MinScale, MaxScale, Lean, Turn, Lift, Colour).Equals(
            (other.Size, other.MinScale, other.MaxScale, other.Lean, other.Turn, other.Lift, other.Colour)) &&
        (ColourVariation, GroundTint, Range, CastShadows, Mesh, Material).Equals(
            (other.ColourVariation, other.GroundTint, other.Range, other.CastShadows, other.Mesh, other.Material));

    public override bool Equals(object? obj) => Equals(obj as ScatterLayer);

    public override int GetHashCode()
    {
        var hash = default(HashCode);
        hash.Add(Name);
        hash.Add(Seed);
        hash.Add(Density);
        foreach (var set in Sets)
        {
            hash.Add(set);
        }

        hash.Add((MinSlope, MaxSlope, MinHeight, MaxHeight, Size));
        hash.Add((MinScale, MaxScale, Lean, Turn, Lift, Colour));
        hash.Add((ColourVariation, GroundTint, Range, CastShadows, Mesh, Material));
        return hash.ToHashCode();
    }

    /// <summary>
    /// The layer as one line of the settings file: what is at its default is left out, so the line
    /// says what is particular about this layer.
    /// </summary>
    public OrderedDictionary<string, object?> ToJson()
    {
        var json = new OrderedDictionary<string, object?> { ["name"] = Name };
        Put(json, "seed", Seed, Seed != 0);
        json["density"] = Density;
        Put(json, "sets", Sets.Select(set => (object?)(long)set).ToList(), Sets.Count > 0);
        Put(json, "minSlope", MinSlope, MinSlope != 0);
        Put(json, "maxSlope", MaxSlope, MaxSlope != 90);
        Put(json, "minHeight", MinHeight, double.IsFinite(MinHeight));
        Put(json, "maxHeight", MaxHeight, double.IsFinite(MaxHeight));
        json["size"] = new List<object?> { Size.Item1, Size.Item2, Size.Item3 };
        Put(json, "minScale", MinScale, MinScale != 1);
        Put(json, "maxScale", MaxScale, MaxScale != 1);
        Put(json, "lean", Lean, Lean != 0);
        Put(json, "turn", false, !Turn);
        Put(json, "lift", Lift, Lift != 0);
        json["colour"] = "#" + (Colour & 0xFFFFFF).ToString("x6", CultureInfo.InvariantCulture);
        Put(json, "colourVariation", ColourVariation, ColourVariation != 0);
        Put(json, "groundTint", GroundTint, GroundTint != 0);
        Put(json, "range", Range, Range != 0);
        Put(json, "castShadows", true, CastShadows);
        Put(json, "mesh", Mesh, Mesh is not null);
        Put(json, "material", Material, Material is not null);
        return json;
    }

    /// <summary>
    /// A layer out of a line of the settings file, or null with a note in
    /// <paramref name="problems"/> when it has no name. Anything out of range is brought into it,
    /// with a note saying so.
    /// </summary>
    public static ScatterLayer? FromJson(object? raw, List<string> problems)
    {
        if (JsonReads.Map(raw) is not { } map || map.GetValueOrDefault("name") is not string name)
        {
            problems.Add("A scatter layer with no name was left out.");
            return null;
        }

        var reader = new LayerReader(map, name, problems);
        var fallback = new ScatterLayer(string.Empty);
        var density = reader.Density(fallback.Density);
        var sets = reader.Sets();
        var (minSlope, maxSlope) = Ordered(
            reader.Within("minSlope", 0, 90, fallback.MinSlope),
            reader.Within("maxSlope", 0, 90, fallback.MaxSlope));
        var (minHeight, maxHeight) = Ordered(
            JsonReads.Number(map, "minHeight") ?? fallback.MinHeight,
            JsonReads.Number(map, "maxHeight") ?? fallback.MaxHeight);
        var size = reader.Size() ?? fallback.Size;
        var (minScale, maxScale) = Ordered(
            reader.Within("minScale", 0.001, 1000, fallback.MinScale),
            reader.Within("maxScale", 0.001, 1000, fallback.MaxScale));

        return new ScatterLayer(
            name,
            seed: JsonReads.Integer(map, "seed") ?? fallback.Seed,
            density: density,
            sets: sets,
            minSlope: minSlope,
            maxSlope: maxSlope,
            minHeight: minHeight,
            maxHeight: maxHeight,
            size: size,
            minScale: minScale,
            maxScale: maxScale,
            lean: reader.Within("lean", 0, 1, fallback.Lean),
            turn: map.GetValueOrDefault("turn") is not false,
            lift: JsonReads.Number(map, "lift") ?? fallback.Lift,
            colour: ColourOf(map.GetValueOrDefault("colour")) ?? fallback.Colour,
            colourVariation: reader.Within("colourVariation", 0, 1, fallback.ColourVariation),
            groundTint: reader.Within("groundTint", 0, 1, fallback.GroundTint),
            range: reader.Within("range", 0, double.PositiveInfinity, fallback.Range),
            castShadows: map.GetValueOrDefault("castShadows") is true,
            mesh: JsonReads.Text(map, "mesh"),
            material: JsonReads.Text(map, "material"));
    }

    private static double CheckDensity(double density) =>
        density > 0 && density <= MaxDensity
            ? density
            : throw new ArgumentOutOfRangeException(nameof(density), density, $"A layer puts down more than 0 and at most {MaxDensity} per square metre.");

    private static void Put(OrderedDictionary<string, object?> json, string key, object? value, bool when)
    {
        if (when)
        {
            json[key] = value;
        }
    }

    private static (double Low, double High) Ordered(double low, double high) => low > high ? (high, low) : (low, high);

    // A hex whole number: an optional sign, hex digits, and the whitespace round
    // them trimmed.
    private static long? ColourOf(object? raw)
    {
        if (raw is not string { Length: 7 } text || text[0] != '#')
        {
            return null;
        }

        var digits = text[1..].Trim();
        var negative = digits.StartsWith('-');
        if (negative || digits.StartsWith('+'))
        {
            digits = digits[1..];
        }

        if (digits.Length == 0 || !digits.All(Uri.IsHexDigit))
        {
            return null;
        }

        var value = long.Parse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
        return negative ? -value : value;
    }

    /// <summary>One layer's reads from its line of the file, and the notes they leave.</summary>
    private sealed class LayerReader(IReadOnlyDictionary<string, object?> map, string name, List<string> problems)
    {
        /// <summary>
        /// The number at <paramref name="key"/> brought into <paramref name="low"/> to
        /// <paramref name="high"/>, with a note when it was outside.
        /// </summary>
        public double Within(string key, double low, double high, double otherwise)
        {
            var value = JsonReads.Number(map, key);
            if (value is not { } found)
            {
                return otherwise;
            }

            if (found < low || found > high)
            {
                var kept = Doubles.Clamp(found, low, high);
                problems.Add($"The \"{name}\" layer's {key} was {Doubles.ToFileString(found)}; it is {Doubles.ToFileString(kept)} now.");
                return kept;
            }

            return found;
        }

        public double Density(double otherwise)
        {
            var density = JsonReads.Number(map, "density") ?? otherwise;
            if (density > 0 && density <= MaxDensity)
            {
                return density;
            }

            var kept = density > MaxDensity ? MaxDensity : otherwise;
            problems.Add($"The \"{name}\" layer put down {Doubles.ToFileString(density)} per square metre; it puts down {Doubles.ToFileString(kept)} now.");
            return kept;
        }

        public List<int> Sets()
        {
            var sets = new List<int>();
            foreach (var set in map.GetValueOrDefault("sets") as IReadOnlyList<object?> ?? [])
            {
                if (set is long and >= 0 and < Cover.SetCount)
                {
                    if (!sets.Contains((int)(long)set))
                    {
                        sets.Add((int)(long)set);
                    }
                }
                else
                {
                    problems.Add($"The \"{name}\" layer named a set that is not one: {JsonReads.Describe(set)}.");
                }
            }

            return sets;
        }

        public (double, double, double)? Size()
        {
            var raw = map.GetValueOrDefault("size");
            if (raw is IReadOnlyList<object?> { Count: 3 } parts && parts.All(IsLength))
            {
                return (ToDouble(parts[0]), ToDouble(parts[1]), ToDouble(parts[2]));
            }

            if (raw is not null)
            {
                problems.Add($"The \"{name}\" layer's size was not three lengths; it is 1 m each way now.");
            }

            return null;
        }

        private static bool IsLength(object? part) => part switch
        {
            long whole => whole > 0,
            double fraction => double.IsFinite(fraction) && fraction > 0,
            _ => false,
        };

        private static double ToDouble(object? part) => part is long whole ? whole : (double)part!;
    }
}
