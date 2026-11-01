using Orblit.VectorMath;

namespace Orblit.Camera;

/// <summary>Where a camera points.</summary>
public interface ICameraAim
{
    // F2: the lens, aspect and delta are what every aim reads, and they travel together.
    Quaternion Solve(
        Quaternion current,
        Vector3 cameraPosition,
        ICameraTarget? lookAt,
        Lens lens,
        double aspect,
        double delta);

    /// <summary>
    /// The framing rules this aim works by, for something to draw over the frame. Null rather
    /// than an empty rectangle: an aim that points straight at its subject has no zones.
    /// </summary>
    CameraGuides? Guides { get; }
}

/// <summary>Keeps whatever rotation it was given.</summary>
public sealed class StaticAim(Quaternion rotation) : ICameraAim
{
    public Quaternion Rotation { get; } = rotation;

    /// <summary>A fixed rotation is not composing anything.</summary>
    public CameraGuides? Guides => null;

    public Quaternion Solve(
        Quaternion current,
        Vector3 cameraPosition,
        ICameraTarget? lookAt,
        Lens lens,
        double aspect,
        double delta) => Rotation.Clone();
}

/// <summary>
/// Points straight at the target, with no lag. Correct and rarely what you want: a camera that
/// tracks perfectly reads as mechanical, because a real operator is always a little behind.
/// </summary>
public sealed class HardLookAt(Vector3? up = null) : ICameraAim
{
    public Vector3? Up { get; } = up;

    /// <summary>Pointed straight at the subject: no zones to draw.</summary>
    public CameraGuides? Guides => null;

    public Quaternion Solve(
        Quaternion current,
        Vector3 cameraPosition,
        ICameraTarget? lookAt,
        Lens lens,
        double aspect,
        double delta)
    {
        if (lookAt is null)
        {
            return current;
        }

        var toTarget = lookAt.Position - cameraPosition;
        return toTarget.Length2 < 1e-9 ? current : Rotations.LookRotation(toTarget, Up);
    }
}

/// <summary>
/// Frames a target, with room to move before the camera reacts. Two rectangles centred on
/// where the subject should sit. Inside the dead zone the camera does not move at all, which
/// is most of why a composed shot feels operated rather than computed. Between the dead zone
/// and the soft zone the camera follows with lag. At the soft zone edge it stops lagging and
/// holds the subject there, so nothing important ever leaves the frame.
/// </summary>
public sealed class ComposerAim(
    double screenX = 0.5,
    double screenY = 0.5,
    double deadZoneWidth = 0.1,
    double deadZoneHeight = 0.1,
    double softZoneWidth = 0.8,
    double softZoneHeight = 0.8,
    double damping = 0.5,
    Vector3? up = null) : ICameraAim
{
    /// <summary>Where the subject should sit, as a fraction of the frame. A third is the rule most shots use.</summary>
    public double ScreenX { get; set; } = screenX;

    public double ScreenY { get; set; } = screenY;

    /// <summary>The rectangle the subject may move inside without the camera reacting, as a fraction of the frame.</summary>
    public double DeadZoneWidth { get; set; } = deadZoneWidth;

    public double DeadZoneHeight { get; set; } = deadZoneHeight;

    /// <summary>The rectangle the subject may never leave.</summary>
    public double SoftZoneWidth { get; set; } = softZoneWidth;

    public double SoftZoneHeight { get; set; } = softZoneHeight;

    /// <summary>Seconds of lag while catching up.</summary>
    public double Damping { get; set; } = damping;

    public Vector3? Up { get; set; } = up;

    /// <summary>
    /// The zones as fractions of the frame. The widths are half extents in normalised
    /// coordinates, where the frame runs from minus one to one, so a half extent of a tenth is
    /// a rectangle a fifth of the frame across. Getting that factor wrong draws a box half the
    /// size of the one the camera is using, which is worse than drawing none.
    /// </summary>
    public CameraGuides Guides => new(
        ScreenX,
        ScreenY,
        new ScreenRect(ScreenX - DeadZoneWidth, ScreenY - DeadZoneHeight, DeadZoneWidth * 2, DeadZoneHeight * 2),
        new ScreenRect(ScreenX - SoftZoneWidth, ScreenY - SoftZoneHeight, SoftZoneWidth * 2, SoftZoneHeight * 2));

    private double IdealX => (ScreenX - 0.5) * 2;

    private double IdealY => (0.5 - ScreenY) * 2;

    public Quaternion Solve(
        Quaternion current,
        Vector3 cameraPosition,
        ICameraTarget? lookAt,
        Lens lens,
        double aspect,
        double delta)
    {
        if (lookAt is null)
        {
            return current;
        }

        var seen = Framing.Project(cameraPosition, current, lookAt.Position, lens, aspect);

        // A subject behind the camera cannot be composed, only recovered: swing to it directly
        // rather than reading a projection that has folded over.
        if (!seen.InFront)
        {
            return Framing.RotationPlacing(cameraPosition, lookAt.Position, IdealX, IdealY, lens, aspect, Up);
        }

        var errorX = seen.X - IdealX;
        var errorY = seen.Y - IdealY;

        // Aim for the nearest point on the dead zone edge, not for the centre. Aiming at the
        // centre and stopping early is the same motion with an arbitrary threshold.
        var wantedX = IdealX + Doubles.Clamp(errorX, -DeadZoneWidth, DeadZoneWidth);
        var wantedY = IdealY + Doubles.Clamp(errorY, -DeadZoneHeight, DeadZoneHeight);

        var wanted = Framing.RotationPlacing(cameraPosition, lookAt.Position, wantedX, wantedY, lens, aspect, Up);
        var result = Rotations.SlerpShortest(current, wanted, Smoothing.Factor(Damping, delta));

        // The soft zone is a promise rather than a preference, so it is enforced after the
        // damping rather than folded into it.
        var after = Framing.Project(cameraPosition, result, lookAt.Position, lens, aspect);
        var outsideX = Math.Abs(after.X - IdealX) > SoftZoneWidth;
        var outsideY = Math.Abs(after.Y - IdealY) > SoftZoneHeight;

        if (!after.InFront || outsideX || outsideY)
        {
            result = Framing.RotationPlacing(
                cameraPosition,
                lookAt.Position,
                IdealX + Doubles.Clamp(after.X - IdealX, -SoftZoneWidth, SoftZoneWidth),
                IdealY + Doubles.Clamp(after.Y - IdealY, -SoftZoneHeight, SoftZoneHeight),
                lens,
                aspect,
                Up);
        }

        return result;
    }
}

/// <summary>
/// Looks wherever the target is looking. First person, and nothing else is. A camera that
/// follows a character's eyes must not compose, damp or frame: any of those puts the view
/// somewhere other than where the character is looking. The damping is here for the one case
/// that wants it, a vehicle, where the driver's head does not snap to the chassis.
/// </summary>
public sealed class HeadAim(double damping = 0, double tilt = 0) : ICameraAim
{
    /// <summary>Seconds of lag behind the target's own rotation. Zero for a person.</summary>
    public double Damping { get; set; } = damping;

    /// <summary>Degrees to look up or down from wherever the target faces, for a head on a body.</summary>
    public double Tilt { get; set; } = tilt;

    /// <summary>Nothing is composed, so there is nothing to draw.</summary>
    public CameraGuides? Guides => null;

    public Quaternion Solve(
        Quaternion current,
        Vector3 cameraPosition,
        ICameraTarget? lookAt,
        Lens lens,
        double aspect,
        double delta)
    {
        if (lookAt is null)
        {
            return current;
        }

        var wanted = lookAt.Rotation.Clone();
        wanted.Normalize();
        if (Tilt != 0)
        {
            wanted *= Quaternion.AxisAngle(new Vector3(1, 0, 0), Tilt * Math.PI / 180);
        }

        if (Damping <= 0)
        {
            wanted.Normalize();
            return wanted;
        }

        return Rotations.SlerpShortest(current, wanted, Smoothing.Factor(Damping, delta));
    }
}

/// <summary>Aim driven by the player rather than by a target.</summary>
public sealed class PovAim(
    double yaw = 0,
    double pitch = 0,
    double minimumPitch = -85,
    double maximumPitch = 85,
    double damping = 0) : ICameraAim
{
    /// <summary>Degrees about world up.</summary>
    public double Yaw { get; set; } = yaw;

    /// <summary>Degrees above the horizon, clamped short of straight up and down where the horizon would flip.</summary>
    public double Pitch { get; set; } = pitch;

    public double MinimumPitch { get; set; } = minimumPitch;

    public double MaximumPitch { get; set; } = maximumPitch;

    public double Damping { get; set; } = damping;

    /// <summary>Aimed by hand rather than at anything.</summary>
    public CameraGuides? Guides => null;

    public Quaternion Solve(
        Quaternion current,
        Vector3 cameraPosition,
        ICameraTarget? lookAt,
        Lens lens,
        double aspect,
        double delta)
    {
        var clamped = Doubles.Clamp(Pitch, MinimumPitch, MaximumPitch);
        var wanted =
            Quaternion.AxisAngle(new Vector3(0, 1, 0), Yaw * Math.PI / 180) *
            Quaternion.AxisAngle(new Vector3(1, 0, 0), clamped * Math.PI / 180);

        return Damping <= 0
            ? wanted
            : Rotations.SlerpShortest(current, wanted, Smoothing.Factor(Damping, delta));
    }
}
