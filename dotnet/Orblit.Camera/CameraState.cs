using Orblit.VectorMath;

namespace Orblit.Camera;

/// <summary>
/// Something a camera can follow or look at. An interface rather than an entity handle, so
/// the camera solver depends on nothing but maths. A game implements it over its entities;
/// a test implements it with two numbers.
/// </summary>
public interface ICameraTarget
{
    Vector3 Position { get; }

    Quaternion Rotation { get; }
}

/// <summary>A target that does not move, for tests and for framing a fixed point.</summary>
public sealed class FixedTarget(Vector3 position, Quaternion? rotation = null) : ICameraTarget
{
    public Vector3 Position { get; set; } = position;

    public Quaternion Rotation { get; set; } = rotation ?? Quaternion.Identity();
}

/// <summary>Where a camera is and what it sees.</summary>
public sealed class CameraState(Vector3? position = null, Quaternion? rotation = null, Lens? lens = null)
{
    public Vector3 Position { get; set; } = position ?? Vector3.Zero();

    public Quaternion Rotation { get; set; } = rotation ?? Quaternion.Identity();

    public Lens Lens { get; set; } = lens ?? new Lens();

    /// <summary>The direction the camera is looking.</summary>
    public Vector3 Forward => Rotations.RotateVector(Rotation, new Vector3(0, 0, -1));

    public Vector3 Up => Rotations.RotateVector(Rotation, new Vector3(0, 1, 0));

    public Vector3 Right => Rotations.RotateVector(Rotation, new Vector3(1, 0, 0));

    public CameraState Clone() => new(Position.Clone(), Rotation.Clone(), Lens);

    /// <summary>
    /// Blends two camera states. Rotation goes the short way round, which is what a viewer
    /// expects from a cut between two shots and is not what interpolating four components gives.
    /// </summary>
    public static CameraState Lerp(CameraState a, CameraState b, double t)
    {
        if (t <= 0)
        {
            return a.Clone();
        }

        if (t >= 1)
        {
            return b.Clone();
        }

        return new CameraState(
            a.Position + ((b.Position - a.Position) * t),
            Rotations.SlerpShortest(a.Rotation, b.Rotation, t),
            Lens.Lerp(a.Lens, b.Lens, t));
    }
}
