using Orblit.VectorMath;

namespace Orblit.Camera;

/// <summary>
/// How far a ball of <paramref name="radius"/> gets going from <paramref name="from"/> towards
/// <paramref name="to"/> before it meets something solid, in metres, or null when the way is
/// clear. The arm owns this shape and a game fills it in, so nothing here imports a physics
/// engine and a wall in a test can be one comparison.
/// </summary>
public delegate double? ArmProbe(Vector3 from, Vector3 to, double radius);

/// <summary>
/// Holds another body's camera on the near side of whatever stands between it and the target.
/// A third person camera sits on an arm: a line from a point on the target out to where the
/// camera wants to be. When a wall crosses that line the camera is behind it and shows the back
/// of the wall. This body casts a ball along the arm every frame and stops the camera where the
/// ball stops. Shortening is instant, because a camera through a wall is wrong for even one
/// frame. Lengthening is whatever the inner body does to catch up, so the camera eases back out
/// at the pace it settles anywhere else.
/// </summary>
public sealed class ArmBody(ICameraBody inner, ArmProbe probe, double radius = 0.25, double height = 1.5) : ICameraBody
{
    /// <summary>Where the camera wants to be when nothing is in the way.</summary>
    public ICameraBody Inner { get; } = inner;

    public ArmProbe Probe { get; } = probe;

    /// <summary>
    /// How big a ball the camera is, in metres. Wide enough to clear the near plane, or a wall
    /// beside the camera cuts into the frame.
    /// </summary>
    public double Radius { get; set; } = radius;

    /// <summary>
    /// How far above the target's position the arm starts. A person's head rather than their
    /// feet, so a low step does not count as a wall.
    /// </summary>
    public double Height { get; set; } = height;

    /// <summary>The framing is the inner body's. The arm only decides how far out it reaches.</summary>
    public CameraGuides? Guides => Inner.Guides;

    public Vector3 Solve(
        Vector3 current,
        ICameraTarget? follow,
        Quaternion rotation,
        Lens lens,
        double aspect,
        double delta)
    {
        var wanted = Inner.Solve(current, follow, rotation, lens, aspect, delta);
        if (follow is null)
        {
            return wanted;
        }

        var pivot = follow.Position + new Vector3(0, Height, 0);
        var arm = wanted - pivot;
        var length = arm.Length;
        if (length < 1e-9)
        {
            return wanted;
        }

        var reached = Probe(pivot, wanted, Radius);
        if (reached is not { } distance || distance >= length)
        {
            return wanted;
        }

        return pivot + (arm * (Math.Max(0, distance) / length));
    }
}
