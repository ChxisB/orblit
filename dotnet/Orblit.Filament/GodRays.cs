using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>
/// Light scattered towards the eye by the air between it and the sun: the shafts through a gap
/// in the trees, the beams across a dusty hall. Each pixel walks towards the sun's place on the
/// screen and adds up how much of the way is open sky, each step counting a little less than the
/// one before. A pillar between the pixel and the sun gathers nothing, and that dark wedge in a
/// bright one reads as a shaft.
/// </summary>
/// <remarks>
/// Driven by the scene's first directional light, so the shafts point where the shadows do. They
/// thin under cloud, since overcast is the sun spread across the whole sky. Off by default, and
/// free when off.
/// </remarks>
public sealed record GodRays
{
    /// <summary>Floats the settings take in the message.</summary>
    public const int Stride = 12;

    /// <summary>No shafts at all.</summary>
    public static readonly GodRays Off = new();

    /// <summary>
    /// How bright the shafts are. Nought is off; one adds as much light as the sky around the sun
    /// already has. The walk's weights are worked out to add up to one, so
    /// <see cref="Samples"/> and <see cref="Decay"/> change the shape of the shafts and never how
    /// bright they are.
    /// </summary>
    public double Strength { get; init; }

    /// <summary>How much each step counts relative to the one before. One is long shafts; lower fades close to what cast them.</summary>
    public double Decay { get; init; } = 0.97;

    /// <summary>How far towards the sun each pixel walks, as a fraction of the way. Above one reaches past the sun.</summary>
    public double Density { get; init; } = 0.9;

    /// <summary>Steps each pixel takes, at most 128. Too few reads as grain.</summary>
    public int Samples { get; init; } = 64;

    /// <summary>The shafts' colour, or null for the light's own.</summary>
    public Vector3? Tint { get; init; }

    /// <summary>Whether there is anything to draw.</summary>
    public bool IsOn => Strength > 0 && Samples > 0;

    /// <summary>
    /// The settings for a light shining towards <paramref name="towardLight"/> in
    /// <paramref name="lightColour"/> under <paramref name="cloudCover"/>. All nought when off or
    /// when there is no light, which the renderer takes as nothing to add.
    /// </summary>
    internal float[] Pack(Vector3? towardLight, Vector3? lightColour, double cloudCover)
    {
        if (!IsOn || towardLight is not { } toward || toward.Length2 == 0)
        {
            return new float[Stride];
        }

        var colour = Tint ?? lightColour ?? new Vector3(1, 1, 1);
        var direction = toward.Normalized();
        return Packing.Floats(
            Strength, Decay, Density, Samples,
            colour.X, colour.Y, colour.Z,
            direction.X, direction.Y, direction.Z,
            cloudCover,
            1);
    }
}
