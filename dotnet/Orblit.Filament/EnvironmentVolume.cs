using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>The shape a volume occupies.</summary>
public enum VolumeShape
{
    /// <summary>
    /// A box, turned to line up with the room it stands for. Rooms, halls and corridors are boxes,
    /// and a sphere fitted round one either leaks out of the doorway or misses the corners.
    /// </summary>
    Box,

    /// <summary>A ball, for places with no walls to line up with: a clearing, the glow round a fire.</summary>
    Sphere,
}

/// <summary>
/// What a volume changes about the look of the scene, and nothing else. Every field is optional,
/// and null means leave it alone: a hall that only wants to be foggier says so and nothing more,
/// and the sunset a day cycle writes into the sky carries on underneath it. A volume that had to
/// state every setting would freeze all it did not mean to, which is what makes a time of day
/// stop at a doorway.
/// </summary>
public sealed record EnvironmentOverrides
{
    /// <summary>The fog's density, per metre.</summary>
    public double? FogDensity { get; init; }

    /// <summary>The fog's colour, linear.</summary>
    public Vector3? FogColour { get; init; }

    public double? FogHeight { get; init; }

    public double? FogHeightFalloff { get; init; }

    /// <summary>How far in front of the camera the fog begins.</summary>
    public double? FogStart { get; init; }

    public double? FogMaximumOpacity { get; init; }

    /// <summary>
    /// Stops of exposure added to the camera's own, so minus one is half the light reaching the
    /// sensor. Compensation and not a setting, because the camera's aperture, shutter and
    /// sensitivity already say how it is set and a volume that replaced them would throw away what
    /// the host chose. Applied to the shutter, which has no other visible effect here.
    /// </summary>
    public double? ExposureCompensation { get; init; }

    /// <summary>
    /// How much light the sky casts, in lux. The most useful override indoors: the flat ambient
    /// comes from every direction and nothing occludes it, so without this a room with a roof is
    /// lit as brightly as the courtyard. Ignored by the renderer while an environment is lighting
    /// the scene, as the sky's ambient always is. Use <see cref="EnvironmentIntensity"/> then.
    /// </summary>
    public double? Ambient { get; init; }

    /// <summary>The colour the ambient light is.</summary>
    public Vector3? SkyColour { get; init; }

    /// <summary>The environment's intensity, in lux.</summary>
    public double? EnvironmentIntensity { get; init; }

    /// <summary>The environment's rotation about the vertical, in radians.</summary>
    public double? EnvironmentRotation { get; init; }

    /// <summary>
    /// The bloom's strength. Turns bloom on if the scene had it off, rising from nothing, so a
    /// volume can make its lamps glow without the courtyard outside starting to.
    /// </summary>
    public double? BloomStrength { get; init; }

    /// <summary>
    /// The grade. Setting any of the five turns the grading on if the scene had it off, starting
    /// from neutral, so the grade outside is the ungraded picture and not whatever numbers a
    /// switched off grading happened to hold. The midtones are a colour the middle of the image is
    /// multiplied by.
    /// </summary>
    public double? Saturation { get; init; }

    public double? Contrast { get; init; }

    public double? Temperature { get; init; }

    public double? Tint { get; init; }

    public Vector3? Midtones { get; init; }
}

/// <summary>
/// A region of the world that looks different from the rest of it. Step out of a sunny courtyard
/// into a hall and the light should drop, the air thicken and the colour warm, and that should
/// happen as the camera goes through the door and not at some line on the floor.
/// </summary>
/// <remarks>
/// Full strength inside the shape, fading to nothing across <see cref="BlendDistance"/> outside it.
/// The fade is measured from the surface outwards, so the whole of the room is the room. Resolved
/// against the camera before the scene is sent, so the renderer is told a fog and a sky and an
/// exposure as it always was and a volume costs nothing on a platform that has not heard of one.
/// </remarks>
public sealed record EnvironmentVolume
{
    private readonly Vector3 _halfExtents = Vector3.Zero();
    private readonly Quaternion _rotation = Quaternion.Identity();

    /// <summary>
    /// This volume's identity. It also breaks a tie between two volumes of the same priority, so
    /// the order they are listed in never matters.
    /// </summary>
    public required long Key { get; init; }

    public required VolumeShape Shape { get; init; }

    /// <summary>The middle of the shape, in world space.</summary>
    public required Vector3 Centre { get; init; }

    /// <summary>What it changes.</summary>
    public required EnvironmentOverrides Overrides { get; init; }

    /// <summary>For a box: half its size along each of its own axes, in metres. Copied.</summary>
    public Vector3 HalfExtents
    {
        get => _halfExtents;
        init => _halfExtents = value.Clone();
    }

    /// <summary>For a box: which way it is turned. Normalised.</summary>
    public Quaternion Rotation
    {
        get => _rotation;
        init => _rotation = value.Normalized();
    }

    /// <summary>For a sphere: its radius, in metres.</summary>
    public double Radius { get; init; }

    /// <summary>
    /// How far outside the shape its influence reaches, in metres. Nought is a hard edge, which is
    /// right for a portal and wrong for nearly everything else: the look jumps in a single frame
    /// and the eye reads it as a cut.
    /// </summary>
    public double BlendDistance { get; init; } = 1;

    /// <summary>
    /// Which volume wins where two overlap: higher is applied later, on top. A cave inside a
    /// mountain inside a valley is three volumes, and the innermost gets the last word without the
    /// others having to know it is there.
    /// </summary>
    public int Priority { get; init; }

    /// <summary>
    /// How much of the volume counts at full strength, from nothing to all of it. What lets a host
    /// fade a whole volume in and out, a storm arriving, without moving it.
    /// </summary>
    public double Weight { get; init; } = 1;

    /// <summary>A box <paramref name="halfExtents"/> from <paramref name="centre"/> along each of its own axes.</summary>
    public static EnvironmentVolume Box(long key, Vector3 centre, Vector3 halfExtents, EnvironmentOverrides overrides) =>
        new() { Key = key, Shape = VolumeShape.Box, Centre = centre, HalfExtents = halfExtents, Overrides = overrides };

    /// <summary>A ball of <paramref name="radius"/> round <paramref name="centre"/>.</summary>
    public static EnvironmentVolume Sphere(long key, Vector3 centre, double radius, EnvironmentOverrides overrides) =>
        new() { Key = key, Shape = VolumeShape.Sphere, Centre = centre, Radius = radius, Overrides = overrides };

    /// <summary>How far <paramref name="point"/> is outside the shape, in metres; nought anywhere inside.</summary>
    public double DistanceTo(Vector3 point)
    {
        var offset = point - Centre;
        if (Shape == VolumeShape.Sphere)
        {
            return Math.Max(0, offset.Length - Radius);
        }

        // Into the box's own frame, where it is axis-aligned: the turn undone. A rotation's
        // transpose is its inverse, and this quaternion was normalised when it was set.
        var undo = Rotation.AsRotationMatrix();
        undo.Transpose();
        var local = undo.Transformed(offset);
        var dx = Math.Max(0.0, Math.Abs(local.X) - HalfExtents.X);
        var dy = Math.Max(0.0, Math.Abs(local.Y) - HalfExtents.Y);
        var dz = Math.Max(0.0, Math.Abs(local.Z) - HalfExtents.Z);
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    /// <summary>
    /// How much of this volume applies at <paramref name="point"/>, from nothing to the weight. A
    /// smoothstep across the blend band and not a straight line: a straight line has a corner at
    /// each end of the band, and a camera walking through it at a steady pace sees the change
    /// start and stop with a jolt, where the smoothstep eases in and out.
    /// </summary>
    public double InfluenceAt(Vector3 point)
    {
        var strength = Math.Clamp(Weight, 0.0, 1.0);
        if (strength == 0)
        {
            return 0;
        }

        var outside = DistanceTo(point);
        if (outside <= 0)
        {
            return strength;
        }

        if (BlendDistance <= 0)
        {
            return 0;
        }

        var t = Math.Clamp(1 - (outside / BlendDistance), 0.0, 1.0);
        return strength * t * t * (3 - (2 * t));
    }
}
