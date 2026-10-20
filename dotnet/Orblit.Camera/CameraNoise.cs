using Orblit.VectorMath;

namespace Orblit.Camera;

/// <summary>
/// Procedural handheld movement. A camera that holds perfectly still reads as a tripod,
/// which is right for a menu and wrong for almost everything else. Deterministic: the same
/// time and seed always give the same offset, so a replay, a recording and a test agree.
/// </summary>
public sealed class CameraNoise(
    Vector3? positionAmplitude = null,
    Vector3? positionFrequency = null,
    Vector3? rotationAmplitude = null,
    Vector3? rotationFrequency = null,
    long seed = 0)
{
    /// <summary>Metres of sway, per axis.</summary>
    public Vector3 PositionAmplitude { get; set; } = positionAmplitude ?? new Vector3(0.05, 0.05, 0.02);

    /// <summary>Cycles per second, per axis.</summary>
    public Vector3 PositionFrequency { get; set; } = positionFrequency ?? new Vector3(0.4, 0.5, 0.3);

    /// <summary>Degrees of wobble: pitch, yaw, roll.</summary>
    public Vector3 RotationAmplitude { get; set; } = rotationAmplitude ?? new Vector3(0.6, 0.6, 0.3);

    public Vector3 RotationFrequency { get; set; } = rotationFrequency ?? new Vector3(0.5, 0.4, 0.2);

    /// <summary>Tells one camera's shake from another's, so two cameras in a scene do not sway in unison.</summary>
    public long Seed { get; set; } = seed;

    /// <summary>How far the camera should be displaced at <paramref name="time"/> seconds.</summary>
    public Vector3 PositionAt(double time) => new(
        PositionAmplitude.X * Wave(time * PositionFrequency.X, unchecked(Seed + 1)),
        PositionAmplitude.Y * Wave(time * PositionFrequency.Y, unchecked(Seed + 2)),
        PositionAmplitude.Z * Wave(time * PositionFrequency.Z, unchecked(Seed + 3)));

    /// <summary>Pitch, yaw and roll in degrees at <paramref name="time"/> seconds.</summary>
    public Vector3 RotationAt(double time) => new(
        RotationAmplitude.X * Wave(time * RotationFrequency.X, unchecked(Seed + 4)),
        RotationAmplitude.Y * Wave(time * RotationFrequency.Y, unchecked(Seed + 5)),
        RotationAmplitude.Z * Wave(time * RotationFrequency.Z, unchecked(Seed + 6)));

    // Sines at ratios that never line up, so the result never visibly repeats. Cheaper than
    // gradient noise and indistinguishable at these amplitudes.
    private static double Wave(double t, long seed)
    {
        var phase = seed * 1.61803398875;
        return (Math.Sin((t + phase) * 2 * Math.PI) * 0.55)
            + (Math.Sin(((t * 2.176) + phase) * 2 * Math.PI) * 0.3)
            + (Math.Sin(((t * 4.813) + phase) * 2 * Math.PI) * 0.15);
    }
}
