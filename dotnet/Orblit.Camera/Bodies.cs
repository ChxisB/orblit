using Orblit.VectorMath;

namespace Orblit.Camera;

/// <summary>
/// Where a camera puts itself. Separated from where it points, because the two decisions are
/// independent: a camera can orbit a character while framing something else entirely.
/// </summary>
public interface ICameraBody
{
    /// <summary>
    /// The position for this frame, given where the camera was. The lens and the rotation are
    /// here because some bodies frame by moving rather than by turning: a camera over a game seen
    /// flat on cannot turn to bring anything into shot. The bodies that do not need them ignore them.
    /// </summary>
    // F2: the rotation, lens, aspect and delta are what every body reads, and they travel together.
    Vector3 Solve(
        Vector3 current,
        ICameraTarget? follow,
        Quaternion rotation,
        Lens lens,
        double aspect,
        double delta);

    /// <summary>The framing rules this body works by, for something to draw over the frame, or null for a body that frames by nothing.</summary>
    CameraGuides? Guides { get; }
}

/// <summary>A camera that does not move itself.</summary>
public sealed class StaticBody(Vector3 position) : ICameraBody
{
    public Vector3 Position { get; } = position;

    /// <summary>It does not frame anything; it stands where it stands.</summary>
    public CameraGuides? Guides => null;

    public Vector3 Solve(
        Vector3 current,
        ICameraTarget? follow,
        Quaternion rotation,
        Lens lens,
        double aspect,
        double delta) => Position.Clone();
}

/// <summary>Which space an offset is measured in.</summary>
public enum FollowBinding
{
    /// <summary>World axes. The camera keeps the same compass bearing however the target turns.</summary>
    World,

    /// <summary>The target's full rotation. Right for a vehicle and wrong for a person.</summary>
    TargetRotation,

    /// <summary>
    /// The target's heading only, ignoring pitch and roll. What a third person camera almost
    /// always wants: it follows where a character faces without tipping on a slope.
    /// </summary>
    TargetHeading,
}

/// <summary>Holds an offset from a target.</summary>
public sealed class FollowBody(
    Vector3 offset,
    FollowBinding binding = FollowBinding.TargetHeading,
    Vector3? damping = null) : ICameraBody
{
    /// <summary>Where to sit relative to the target, in the space <see cref="Binding"/> names.</summary>
    public Vector3 Offset { get; set; } = offset;

    public FollowBinding Binding { get; } = binding;

    /// <summary>
    /// Seconds of lag per axis, in the same space as the offset. Per axis because the axes want
    /// different answers: loose horizontally and tight vertically reads as smooth, and the
    /// reverse reads as seasick.
    /// </summary>
    public Vector3 Damping { get; set; } = damping ?? new Vector3(0.4, 0.4, 0.4);

    /// <summary>It holds an offset. What that puts on screen is the aim's business.</summary>
    public CameraGuides? Guides => null;

    public Vector3 Solve(
        Vector3 current,
        ICameraTarget? follow,
        Quaternion rotation,
        Lens lens,
        double aspect,
        double delta)
    {
        if (follow is null)
        {
            return current;
        }

        var turn = Binding switch
        {
            FollowBinding.World => Quaternion.Identity(),
            FollowBinding.TargetRotation => follow.Rotation,
            FollowBinding.TargetHeading => HeadingOf(follow.Rotation),
            _ => throw new InvalidOperationException($"Not a follow binding: {Binding}"),
        };

        var desired = follow.Position + Rotations.RotateVector(turn, Offset);
        var toDesired = desired - current;

        // Damped in the binding's own space, so "loose behind, tight above" stays true as the
        // target turns.
        var inverse = turn.Clone();
        inverse.Conjugate();
        var local = Rotations.RotateVector(inverse, toDesired);

        local.X *= Smoothing.Factor(Damping.X, delta);
        local.Y *= Smoothing.Factor(Damping.Y, delta);
        local.Z *= Smoothing.Factor(Damping.Z, delta);

        return current + Rotations.RotateVector(turn, local);
    }

    // The rotation about world up alone, so a target leaning or pitching does not tilt the
    // camera with it.
    private static Quaternion HeadingOf(Quaternion rotation)
    {
        var forward = Rotations.RotateVector(rotation, new Vector3(0, 0, -1));
        var flat = new Vector3(forward.X, 0, forward.Z);
        return flat.Length2 < 1e-8 ? Quaternion.Identity() : Rotations.LookRotation(flat.Normalized());
    }
}

/// <summary>Circles a target at a fixed radius.</summary>
public sealed class OrbitBody(
    double radius = 6,
    double azimuth = 0,
    double elevation = 20,
    double damping = 0.3,
    double height = 0) : ICameraBody
{
    /// <summary>Distance from the target.</summary>
    public double Radius { get; set; } = radius;

    /// <summary>Bearing around the target, in degrees.</summary>
    public double Azimuth { get; set; } = azimuth;

    /// <summary>Angle above the horizon, in degrees. Clamped short of the poles, where the up vector becomes ambiguous and the camera rolls.</summary>
    public double Elevation { get; set; } = elevation;

    /// <summary>Raises the point being orbited, so the camera circles a character's head rather than their feet.</summary>
    public double Height { get; set; } = height;

    public double Damping { get; set; } = damping;

    /// <summary>It holds a distance and an angle, not a place in the frame.</summary>
    public CameraGuides? Guides => null;

    public Vector3 Solve(
        Vector3 current,
        ICameraTarget? follow,
        Quaternion rotation,
        Lens lens,
        double aspect,
        double delta)
    {
        if (follow is null)
        {
            return current;
        }

        var pitch = Doubles.Clamp(Elevation, -89.0, 89.0) * Math.PI / 180;
        var yaw = Azimuth * Math.PI / 180;
        var horizontal = Radius * Math.Cos(pitch);

        var desired =
            follow.Position +
            new Vector3(0, Height, 0) +
            new Vector3(horizontal * Math.Sin(yaw), Radius * Math.Sin(pitch), horizontal * Math.Cos(yaw));

        var factor = Smoothing.Factor(Damping, delta);
        return current + ((desired - current) * factor);
    }
}

/// <summary>
/// Keeps a target a chosen distance away, moving along the view axis only. The dolly of a
/// framing shot: the camera does not swing around the subject to keep it the right size, it
/// moves towards and away, which keeps whatever composition the aim established.
/// </summary>
public sealed class FramingBody(
    Vector3 viewDirection,
    double distance = 8,
    double minimum = 2,
    double maximum = 40,
    double damping = 0.5) : ICameraBody
{
    /// <summary>The direction from the target to the camera.</summary>
    public Vector3 ViewDirection { get; set; } = viewDirection;

    public double Distance { get; set; } = distance;

    public double Minimum { get; set; } = minimum;

    public double Maximum { get; set; } = maximum;

    public double Damping { get; set; } = damping;

    /// <summary>It holds a distance along a fixed direction.</summary>
    public CameraGuides? Guides => null;

    public Vector3 Solve(
        Vector3 current,
        ICameraTarget? follow,
        Quaternion rotation,
        Lens lens,
        double aspect,
        double delta)
    {
        if (follow is null)
        {
            return current;
        }

        var direction = ViewDirection.Normalized();
        var desired = follow.Position + (direction * Doubles.Clamp(Distance, Minimum, Maximum));

        var factor = Smoothing.Factor(Damping, delta);
        return current + ((desired - current) * factor);
    }
}

/// <summary>
/// Frames a target by moving, rather than by turning to face it. A composer turns the camera
/// until the subject sits where it should, which works because turning a perspective view
/// sweeps it across the world. Turning an orthographic view moves nothing through the frame, it
/// rotates the whole picture, so the only way to bring a subject to a place in a flat frame is
/// to move the camera there. This is the body a two dimensional game wants, and what a third
/// person camera often wants in three: sliding rather than tilting keeps the horizon still.
/// The dead zone and the soft zone mean what they mean everywhere else: inside the first the
/// camera holds still, between the two it eases after the subject, past the second it is dragged.
/// </summary>
public sealed class ScreenFollowBody(
    double distance = 10,
    double screenX = 0.5,
    double screenY = 0.5,
    double deadZoneWidth = 0.1,
    double deadZoneHeight = 0.1,
    double softZoneWidth = 0.4,
    double softZoneHeight = 0.4,
    double damping = 0.4,
    (Vector3 Minimum, Vector3 Maximum)? bounds = null) : ICameraBody
{
    /// <summary>
    /// How far in front of the target the camera sits, along its own view direction. For a flat
    /// game this only keeps the subject in front of the near plane; with perspective it is the shot size.
    /// </summary>
    public double Distance { get; set; } = distance;

    /// <summary>Where in the frame the subject belongs, in fractions of it.</summary>
    public double ScreenX { get; set; } = screenX;

    public double ScreenY { get; set; } = screenY;

    public double DeadZoneWidth { get; set; } = deadZoneWidth;

    public double DeadZoneHeight { get; set; } = deadZoneHeight;

    public double SoftZoneWidth { get; set; } = softZoneWidth;

    public double SoftZoneHeight { get; set; } = softZoneHeight;

    /// <summary>Seconds of lag while catching up inside the soft zone.</summary>
    public double Damping { get; set; } = damping;

    /// <summary>
    /// A box the camera is kept inside, or null for a world without edges. Applied last, after
    /// the framing, because a wall is not a preference.
    /// </summary>
    public (Vector3 Minimum, Vector3 Maximum)? Bounds { get; set; } = bounds;

    public CameraGuides Guides => new(
        ScreenX,
        ScreenY,
        new ScreenRect(ScreenX - DeadZoneWidth, ScreenY - DeadZoneHeight, DeadZoneWidth * 2, DeadZoneHeight * 2),
        new ScreenRect(ScreenX - SoftZoneWidth, ScreenY - SoftZoneHeight, SoftZoneWidth * 2, SoftZoneHeight * 2));

    public Vector3 Solve(
        Vector3 current,
        ICameraTarget? follow,
        Quaternion rotation,
        Lens lens,
        double aspect,
        double delta)
    {
        if (follow is null)
        {
            return current;
        }

        var forward = Rotations.RotateVector(rotation, new Vector3(0, 0, -1));
        var right = Rotations.RotateVector(rotation, new Vector3(1, 0, 0));
        var up = Rotations.RotateVector(rotation, new Vector3(0, 1, 0));

        // Where the camera would have to stand for the subject to sit dead in the middle: back
        // along its own view direction by the shot distance.
        var centred = follow.Position - (forward * Distance);

        // Where the subject is in the frame from where the camera is now.
        var seen = Framing.Project(current, rotation, follow.Position, lens, aspect);

        // Behind the camera there is nothing to frame, only something to recover from: go
        // straight to where it should be rather than reading a projection that has folded over.
        if (!seen.InFront)
        {
            return Within(centred + OffsetFor(right, up, lens, aspect, 0, 0));
        }

        var idealX = (ScreenX - 0.5) * 2;
        var idealY = (0.5 - ScreenY) * 2;

        // How far off it is, and how much of that is worth correcting: nothing inside the dead
        // zone, everything past the soft one.
        var errorX = seen.X - idealX;
        var errorY = seen.Y - idealY;

        var overX = Beyond(errorX, DeadZoneWidth);
        var overY = Beyond(errorY, DeadZoneHeight);

        var hardX = Beyond(errorX, SoftZoneWidth);
        var hardY = Beyond(errorY, SoftZoneHeight);

        // Eased inside the soft zone, taken in full outside it.
        var follows = Smoothing.Factor(Damping, delta);
        var moveX = hardX + ((overX - hardX) * follows);
        var moveY = hardY + ((overY - hardY) * follows);

        var wanted = current + OffsetFor(right, up, lens, aspect, moveX, moveY);

        // Held at the shot distance along the view direction, so framing sideways never drifts
        // the camera closer or further away.
        var along = (wanted - follow.Position).Dot(forward);
        return Within(wanted - (forward * (along + Distance)));
    }

    // F2: the frame's two axes, the lens and the aspect are one question asked of the frame.
    // How far in the world a correction of x and y in the frame is.
    private Vector3 OffsetFor(Vector3 right, Vector3 up, Lens lens, double aspect, double x, double y)
    {
        // Half the frame, in metres, at the distance being framed. For a flat lens that is
        // fixed; with perspective it grows with distance, which is why the same fraction of the
        // frame is a different distance to move.
        var halfHeight = lens.Orthographic
            ? lens.Height / 2
            : Distance * Math.Tan(lens.FieldOfView * Math.PI / 360);

        return (right * (x * halfHeight * aspect)) + (up * (y * halfHeight));
    }

    private Vector3 Within(Vector3 at)
    {
        if (Bounds is not { } box)
        {
            return at;
        }

        return new Vector3(
            Doubles.Clamp(at.X, box.Minimum.X, box.Maximum.X),
            Doubles.Clamp(at.Y, box.Minimum.Y, box.Maximum.Y),
            Doubles.Clamp(at.Z, box.Minimum.Z, box.Maximum.Z));
    }

    /// <summary>How far past a zone's edge something is, and zero inside it.</summary>
    private static double Beyond(double error, double zone)
    {
        if (error > zone)
        {
            return error - zone;
        }

        return error < -zone ? error + zone : 0;
    }
}
