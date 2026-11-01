using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>What a distortion is. The renderer reads the index, and nought is kept for none so a row of zeros does nothing.</summary>
public enum DistortionKind
{
    None,

    /// <summary>A shell of air running out from a point.</summary>
    Shockwave,

    /// <summary>A box of hot air with the shimmer rising through it.</summary>
    Haze,

    /// <summary>The whole frame warped about its middle.</summary>
    Lens,
}

/// <summary>
/// Air that bends the light passing through it. On the screen that is reading the picture from a
/// little way off, so each distortion is an offset added to where each pixel is read from. The
/// renderer sums them in one pass over the finished frame, and can split the channels apart where
/// the bending is strongest. A haze or a wave bends only what is behind it. A scene whose
/// distortions all have a strength of nought, or that has none, draws no extra pass.
/// </summary>
public sealed record Distortion
{
    /// <summary>Floats one distortion takes in the message.</summary>
    public const int Stride = 12;

    /// <summary>The most one frame draws. The renderer's loop is bounded by it.</summary>
    public const int Capacity = 8;

    private Distortion()
    {
    }

    public DistortionKind Kind { get; private init; }

    /// <summary>How far it moves the picture. What the number means depends on <see cref="Kind"/>.</summary>
    public double Strength { get; private init; }

    /// <summary>How far the red and blue channels are pulled apart, as a fraction of how far the picture moves.</summary>
    public double Chromatic { get; private init; }

    public Vector3? Centre { get; private init; }

    public double Radius { get; private init; }

    public double Thickness { get; private init; }

    public Vector3? HalfSize { get; private init; }

    public double Scale { get; private init; }

    /// <summary>How far a haze's shimmer has risen, in metres.</summary>
    public double Rise { get; private init; }

    /// <summary>Whether it moves anything at all.</summary>
    public bool IsActive => Kind != DistortionKind.None && Strength != 0 && double.IsFinite(Strength);

    /// <summary>
    /// A shell of air <paramref name="radius"/> metres out from <paramref name="centre"/>,
    /// <paramref name="thickness"/> metres through. Surfaces it passes through are pushed outwards
    /// by up to <paramref name="strength"/>, a fraction of the frame's height.
    /// </summary>
    // F2: the original's named parameters, kept as optional parameters.
    public static Distortion Shockwave(
        Vector3 centre,
        double radius,
        double thickness = 0.6,
        double strength = 0.03,
        double chromatic = 0) => new()
        {
            Kind = DistortionKind.Shockwave,
            Strength = strength,
            Chromatic = chromatic,
            Centre = centre,
            Radius = radius,
            Thickness = thickness,
        };

    /// <summary>
    /// A shockwave <paramref name="age"/> seconds after it went off: grown at
    /// <paramref name="speed"/> metres a second, and weakening until it is gone after
    /// <paramref name="lifetime"/>. The clock is the host's, so a frame drawn at a given moment is
    /// always the same frame.
    /// </summary>
    // F2: the original's named parameters, kept as optional parameters.
    public static Distortion Expanding(
        Vector3 centre,
        double age,
        double speed = 5,
        double lifetime = 2,
        double thickness = 0.6,
        double strength = 0.03,
        double chromatic = 0)
    {
        var left = age < 0 || age >= lifetime ? 0.0 : 1 - (age / lifetime);
        return Shockwave(centre, speed * Math.Clamp(age, 0, lifetime), thickness, strength * left, chromatic);
    }

    /// <summary>
    /// Hot air in a box <paramref name="halfSize"/> either side of <paramref name="centre"/>,
    /// shimmering upwards. The pattern rises at <paramref name="speed"/> metres a second on the
    /// host's own clock, <paramref name="seconds"/>, and is strongest at the bottom of the box.
    /// </summary>
    // F2: the original's named parameters, kept as optional parameters.
    public static Distortion Haze(
        Vector3 centre,
        Vector3 halfSize,
        double strength = 0.006,
        double scale = 0.25,
        double speed = 0.8,
        double seconds = 0,
        double chromatic = 0) => new()
        {
            Kind = DistortionKind.Haze,
            Strength = strength,
            Chromatic = chromatic,
            Centre = centre,
            HalfSize = halfSize,
            Scale = scale,
            Rise = speed * seconds,
        };

    /// <summary>
    /// The whole frame warped about its middle: barrel for a positive <paramref name="strength"/>,
    /// pincushion for a negative one.
    /// </summary>
    public static Distortion Lens(double strength, double chromatic = 0) => new()
    {
        Kind = DistortionKind.Lens,
        Strength = strength,
        Chromatic = chromatic,
    };

    /// <summary>The ones that move anything, packed end to end, at most <see cref="Capacity"/>.</summary>
    internal static float[] PackAll(IEnumerable<Distortion> distortions)
    {
        var active = distortions.Where(one => one.IsActive).Take(Capacity).ToArray();
        var packed = new float[active.Length * Stride];
        for (var i = 0; i < active.Length; i++)
        {
            active[i].Pack(packed, i * Stride);
        }

        return packed;
    }

    private void Pack(float[] into, int at)
    {
        var centre = Centre ?? Vector3.Zero();
        Packing.Floats((int)Kind, Strength, Chromatic, centre.X, centre.Y, centre.Z).CopyTo(into, at);
        switch (Kind)
        {
            case DistortionKind.Shockwave:
                Packing.Floats(Radius, Thickness).CopyTo(into, at + 6);
                break;
            case DistortionKind.Haze:
                var size = HalfSize ?? new Vector3(1, 1, 1);
                Packing.Floats(size.X, size.Y, size.Z, Scale, Rise).CopyTo(into, at + 6);
                break;
        }
    }
}
