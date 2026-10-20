using Orblit.VectorMath;

namespace Orblit.Terrain;

/// <summary>
/// How ground marked <see cref="Cover.Automatic"/> chooses its sets: <c>steep</c> where the slope
/// is steep or the ground high, <c>flat</c> where it is level and low, and a blend between.
/// </summary>
public sealed class AutoCover(long steep = 0, long flat = 1, double slope = 1, double heightFalloff = 0.1)
{
    /// <summary>The set on steep and high ground. Cliffs, scree.</summary>
    public long Steep { get; } = steep;

    /// <summary>The set on level, low ground. Grass.</summary>
    public long Flat { get; } = flat;

    /// <summary>
    /// How quickly steepness hands over to <see cref="Steep"/>. At 1, ground tilted 60° is all
    /// steep; at 2, ground tilted 41° is.
    /// </summary>
    public double Slope { get; } = slope;

    /// <summary>
    /// How quickly height hands over to <see cref="Steep"/>, per hundred metres: at 0.1, ground a
    /// thousand metres up is all steep however level it is.
    /// </summary>
    public double HeightFalloff { get; } = heightFalloff;

    /// <summary>
    /// How much of <see cref="Flat"/> shows on ground whose normal points <paramref name="normalY"/>
    /// up, at <paramref name="height"/>: 1 is all flat, 0 all steep.
    /// </summary>
    public double Flatness(double normalY, double height) =>
        Doubles.Clamp((Slope * 2 * (normalY - 1)) + 1 - (HeightFalloff * 0.01 * height), 0.0, 1.0);

    /// <summary>The cover automatic ground has at that slope and height.</summary>
    public Cover CoverFor(double normalY, double height) =>
        Cover.Of(@base: (int)Steep, overlay: (int)Flat, blend: Flatness(normalY, height));

    public OrderedDictionary<string, object?> ToJson() => new()
    {
        ["steep"] = Steep,
        ["flat"] = Flat,
        ["slope"] = Slope,
        ["heightFalloff"] = HeightFalloff,
    };

    public static AutoCover FromJson(object? raw)
    {
        var fallback = new AutoCover();
        if (JsonReads.Map(raw) is not { } map)
        {
            return fallback;
        }

        return new AutoCover(
            JsonReads.Integer(map, "steep") ?? fallback.Steep,
            JsonReads.Integer(map, "flat") ?? fallback.Flat,
            JsonReads.Number(map, "slope") ?? fallback.Slope,
            JsonReads.Number(map, "heightFalloff") ?? fallback.HeightFalloff);
    }
}
