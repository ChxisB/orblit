using Orblit.VectorMath;

namespace Orblit.Camera;

/// <summary>
/// Where a point lands on screen, in normalised device coordinates: the visible area runs
/// from -1 to 1 on both axes, with the origin at the centre and y upwards. Used rather than
/// pixels because composition rules are about proportions of the frame.
/// </summary>
/// <param name="X">Across, from -1 at the left to 1 at the right.</param>
/// <param name="Y">Up, from -1 at the bottom to 1 at the top.</param>
/// <param name="InFront">
/// False when the point is behind the camera, where projection lies: a point directly behind
/// maps to the same coordinates as one directly ahead.
/// </param>
public sealed record ScreenPoint(double X, double Y, bool InFront);

/// <summary>Where things land in the frame, and the turn that puts them there.</summary>
public static class Framing
{
    // F2: a projection needs all five inputs and no pair of them belongs together.
    /// <summary>Projects a world point into the frame.</summary>
    public static ScreenPoint Project(
        Vector3 cameraPosition,
        Quaternion cameraRotation,
        Vector3 worldPoint,
        Lens lens,
        double aspect)
    {
        var inverse = cameraRotation.Clone();
        inverse.Conjugate();
        var local = Rotations.RotateVector(inverse, worldPoint - cameraPosition);

        // The camera looks down its own negative Z, so a visible point has negative z here
        // and the depth used for the divide is its negation.
        var depth = -local.Z;
        if (depth <= 1e-6)
        {
            return new ScreenPoint(0, 0, false);
        }

        if (lens.Orthographic)
        {
            // No divide by depth: that is what orthographic means. How far away something is
            // changes nothing about where it sits in the frame, which is why a camera over a
            // flat game has to move rather than turn to bring anything into shot.
            var half = lens.Height / 2;
            return new ScreenPoint(local.X / (half * aspect), local.Y / half, true);
        }

        var tanHalf = Math.Tan(lens.FieldOfView * Math.PI / 360);
        return new ScreenPoint(local.X / depth / (tanHalf * aspect), local.Y / depth / tanHalf, true);
    }

    // F2: a placement needs all seven inputs, and only the up axis has a sensible default.
    /// <summary>
    /// The rotation that makes <paramref name="worldPoint"/> appear at a given place in the
    /// frame. Solved rather than approached: there is exactly one upright rotation that puts it
    /// there, so a composer can aim at the edge of its dead zone directly instead of easing
    /// towards it and overshooting.
    /// </summary>
    /// <remarks>
    /// Built as a turn and then a tilt about the given up, rather than as the shortest rotation
    /// from the centre of the frame to the wanted spot. The shortest one rolls the camera: an
    /// offset that is both sideways and vertical turns about an oblique axis, and the part of
    /// that along the view direction is roll. It then accumulates, because the next frame damps
    /// towards a rotation that is already tilted.
    /// </remarks>
    public static Quaternion RotationPlacing(
        Vector3 cameraPosition,
        Vector3 worldPoint,
        double ndcX,
        double ndcY,
        Lens lens,
        double aspect,
        Vector3? up = null)
    {
        var toTarget = worldPoint - cameraPosition;
        if (toTarget.Length2 < 1e-9)
        {
            return Quaternion.Identity();
        }

        // Solved with the up axis standing at Y, then taken back. A camera levelled against
        // something other than the world's own up is still levelled.
        var upward = (up ?? new Vector3(0, 1, 0)).Normalized();
        var toLevel = ShortestArc(upward, new Vector3(0, 1, 0));
        var fromLevel = toLevel.Clone();
        fromLevel.Conjugate();

        var t = Rotations.RotateVector(toLevel, toTarget.Normalized());

        // Where the wanted spot in the frame lies, as a direction in the camera's own space.
        var tanHalf = Math.Tan(lens.FieldOfView * Math.PI / 360);
        var d = new Vector3(ndcX * tanHalf * aspect, ndcY * tanHalf, -1);
        d.Normalize();

        // The tilt. Rotating d about X leaves its x alone and has to bring its y to the
        // target's, and a cosine and a sine of one angle against a constant is a single cosine
        // with a phase, so it inverts rather than being searched.
        var reach = Math.Sqrt((d.Y * d.Y) + (d.Z * d.Z));
        var phase = Math.Atan2(-d.Z, d.Y);
        var swing = Math.Acos(Doubles.Clamp(t.Y / reach, -1.0, 1.0));

        // Two tilts satisfy it: a camera upright, and the same camera upside down.
        var nearer = phase - swing;
        var further = phase + swing;
        var tilt = Math.Abs(nearer) <= Math.Abs(further) ? nearer : further;

        var ct = Math.Cos(tilt);
        var st = Math.Sin(tilt);
        var tilted = new Vector3(d.X, (ct * d.Y) - (st * d.Z), (st * d.Y) + (ct * d.Z));

        // What is left is a rotation about Y, which in that plane is the difference of two bearings.
        var turn = Math.Atan2(t.X, t.Z) - Math.Atan2(tilted.X, tilted.Z);

        var levelled =
            Quaternion.AxisAngle(new Vector3(0, 1, 0), turn) *
            Quaternion.AxisAngle(new Vector3(1, 0, 0), tilt);

        var result = fromLevel * levelled;
        result.Normalize();
        return result;
    }

    private static Quaternion ShortestArc(Vector3 from, Vector3 to)
    {
        var a = from.Normalized();
        var b = to.Normalized();
        var dot = a.Dot(b);

        if (dot > 0.999999)
        {
            return Quaternion.Identity();
        }

        if (dot < -0.999999)
        {
            // Opposed: any perpendicular axis is a valid half turn.
            var perpendicular = a.Cross(new Vector3(1, 0, 0));
            var chosen = perpendicular.Length2 < 1e-6 ? a.Cross(new Vector3(0, 1, 0)) : perpendicular;
            return Quaternion.AxisAngle(chosen.Normalized(), Math.PI);
        }

        var axis = a.Cross(b);
        var result = new Quaternion(axis.X, axis.Y, axis.Z, 1 + dot);
        result.Normalize();
        return result;
    }
}
