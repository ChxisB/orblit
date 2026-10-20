using Orblit.VectorMath;

namespace Orblit.Terrain;

/// <summary>What a brush does to the ground.</summary>
public enum BrushTool
{
    /// <summary>Lifts the ground under it; <see cref="TerrainStroke.Invert"/> lowers it instead.</summary>
    Raise,

    /// <summary>Sinks the ground under it; <see cref="TerrainStroke.Invert"/> raises it instead.</summary>
    Lower,

    /// <summary>Evens out the bumps, drawing each height towards its neighbours'.</summary>
    Smooth,

    /// <summary>
    /// Draws the ground towards one height: the height where the stroke began, unless the stroke is
    /// given one.
    /// </summary>
    Flatten,

    /// <summary>
    /// Draws the ground towards a ramp from where the stroke began to the farthest it has gone from
    /// there. Drag from one end to the other and back over the way, and the way back lays the ramp.
    /// </summary>
    Slope,

    /// <summary>
    /// Lays one texture set over what is there. <see cref="TerrainStroke.Invert"/> hands the ground
    /// back to automatic cover.
    /// </summary>
    Cover,

    /// <summary>Tints the ground towards one colour. <see cref="TerrainStroke.Invert"/> washes the tint back out.</summary>
    Colour,

    /// <summary>
    /// Nudges the ground rougher or glossier than its textures. <see cref="TerrainStroke.Invert"/>
    /// takes the nudge back off.
    /// </summary>
    Roughness,

    /// <summary>Cuts holes, for a cave mouth or a well. <see cref="TerrainStroke.Invert"/> fills them.</summary>
    Hole,
}

public static class BrushTools
{
    /// <summary>The map the tool writes, which is all its undo keeps.</summary>
    public static TerrainLayer Layer(this BrushTool tool) => tool switch
    {
        BrushTool.Raise or BrushTool.Lower or BrushTool.Smooth or BrushTool.Flatten or BrushTool.Slope =>
            TerrainLayer.Height,
        BrushTool.Cover or BrushTool.Hole => TerrainLayer.Cover,
        BrushTool.Colour or BrushTool.Roughness => TerrainLayer.Colour,
        _ => throw new InvalidOperationException($"{tool} is not a brush tool."),
    };
}

/// <summary>
/// What every brush tool shares: how big it is, how hard it works, how soft its edge is, how
/// scattered and how close together its dabs are.
/// One set of settings for every tool, because a brush that behaves differently for each of them is
/// a brush nobody learns.
/// </summary>
public sealed class Brush(
    double size = 16,
    double strength = 0.5,
    double falloff = 0.5,
    double jitter = 0,
    double spacing = 0.25) : IEquatable<Brush>
{
    /// <summary>
    /// The smallest <see cref="Spacing"/> a stroke uses, so a brush set to nothing does not lay a dab
    /// for every pixel the pointer crosses.
    /// </summary>
    public const double MinSpacing = 0.02;

    /// <summary>
    /// The spacing <see cref="Strength"/> is measured at. A stroke at other spacing lays more or
    /// fewer dabs, each weaker or stronger, to the same total.
    /// </summary>
    public const double ReferenceSpacing = 0.25;

    /// <summary>Metres across.</summary>
    public double Size { get; } = size;

    /// <summary>
    /// How hard it works, 0 to 1.
    /// Measured per pass rather than per dab, so closer <see cref="Spacing"/> makes a smoother stroke
    /// and not a stronger one. At 1, one pass of a height tool moves the ground under the centre by a
    /// quarter of <see cref="Size"/>, and one pass of a blending tool all but reaches what it is
    /// blending towards.
    /// </summary>
    public double Strength { get; } = strength;

    /// <summary>
    /// How much of the radius eases off, 0 to 1: at 0 the brush is a hard disc, at 1 it fades from
    /// the very centre.
    /// </summary>
    public double Falloff { get; } = falloff;

    /// <summary>How far each dab may stray from the path, as a share of the radius, 0 to 1.</summary>
    public double Jitter { get; } = jitter;

    /// <summary>How far apart the dabs are along a stroke, as a share of <see cref="Size"/>.</summary>
    public double Spacing { get; } = spacing;

    public double Radius => Size / 2;

    public static bool operator ==(Brush? left, Brush? right) => left is null ? right is null : left.Equals(right);

    public static bool operator !=(Brush? left, Brush? right) => !(left == right);

    /// <summary>
    /// How much of a dab reaches a point <paramref name="distance"/> metres from its centre: 1 across
    /// the hard middle, easing to 0 at the edge.
    /// </summary>
    public double WeightAt(double distance)
    {
        var radius = Radius;
        if (!(radius > 0))
        {
            return 0;
        }

        var along = distance / radius;
        if (along >= 1)
        {
            return 0;
        }

        var core = 1 - Doubles.Clamp(Falloff, 0.0, 1.0);
        if (along <= core)
        {
            return 1;
        }

        var left = 1 - ((along - core) / (1 - core));
        return left * left * (3 - (2 * left));
    }

    public Brush CopyWith(
        double? size = null,
        double? strength = null,
        double? falloff = null,
        double? jitter = null,
        double? spacing = null) => new(
            size ?? Size,
            strength ?? Strength,
            falloff ?? Falloff,
            jitter ?? Jitter,
            spacing ?? Spacing);

    public bool Equals(Brush? other) =>
        other is not null &&
        (Size, Strength, Falloff, Jitter, Spacing).Equals(
            (other.Size, other.Strength, other.Falloff, other.Jitter, other.Spacing));

    public override bool Equals(object? obj) => Equals(obj as Brush);

    public override int GetHashCode() => HashCode.Combine(Size, Strength, Falloff, Jitter, Spacing);
}
