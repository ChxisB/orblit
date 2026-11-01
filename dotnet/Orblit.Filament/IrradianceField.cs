using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>
/// Light stored in the world rather than on the screen. A screen space bounce only knows what is
/// in frame, so turning away from a red wall takes its bounce with it. A field keeps the answer
/// somewhere the camera cannot move: a lattice of probes standing in the world, each holding what
/// light arrives at it from every direction, built up over many frames and read by every surface
/// near it. A value with many fields, so it is changed with <c>with</c>.
/// </summary>
public sealed record IrradianceField
{
    /// <summary>How many probes a field may hold, past which it is reported.</summary>
    public const int MaxProbes = 1024;

    /// <summary>Floats a field occupies in the packed array.</summary>
    public const int Stride = 14;

    /// <summary>Nothing: the scene is lit as it was before there was a field.</summary>
    public static readonly IrradianceField None = new();

    public bool Enabled { get; init; }

    /// <summary>Where the probe at the corner of the lattice stands.</summary>
    public Vector3 Origin { get; init; } = Vector3.Zero();

    /// <summary>
    /// How far apart the probes are, in metres, along each axis. Light cannot vary faster than the
    /// probes are spaced, so two metres is the scale at which a field of room size says anything.
    /// </summary>
    public Vector3 Spacing { get; init; } = new(2, 2, 2);

    /// <summary>How many probes along each axis.</summary>
    public Vector3 Counts { get; init; } = new(8, 4, 8);

    /// <summary>
    /// The target the probes are filled from. A field is built by reading the picture the scene
    /// has already drawn, so a graph has to write a target of this name.
    /// </summary>
    public string From { get; init; } = "frame";

    /// <summary>How much of the stored light reaches surfaces.</summary>
    public double Intensity { get; init; } = 1;

    /// <summary>
    /// How much of a probe's stored value survives each frame. High is stable and slow, and low is
    /// responsive and noisy.
    /// </summary>
    public double Retention { get; init; } = 0.94;

    /// <summary>
    /// How far off a surface a probe is sampled from, as a fraction of the probe spacing.
    /// Sampling exactly at the surface makes a surface find itself in its own probes.
    /// </summary>
    public double Bias { get; init; } = 0.4;

    /// <summary>How many probes this field asks for.</summary>
    public long ProbeCount =>
        Math.Clamp(Doubles.Round(Counts.X) * Doubles.Round(Counts.Y) * Doubles.Round(Counts.Z), 0, 1 << 20);

    internal float[] Packed => Packing.Floats(
        Packing.Flag(Enabled),
        Origin.X, Origin.Y, Origin.Z,
        Spacing.X, Spacing.Y, Spacing.Z,
        Counts.X, Counts.Y, Counts.Z,
        Intensity,
        Retention,
        Bias,
        0);
}
