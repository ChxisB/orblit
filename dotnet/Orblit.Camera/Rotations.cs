using Orblit.VectorMath;

namespace Orblit.Camera;

/// <summary>Rotation maths the cameras share.</summary>
public static class Rotations
{
    /// <summary>
    /// Rotates a vector by a quaternion. Written out through the rotation matrix rather than
    /// the quaternion's own rotate, which applies the turn in the opposite sense to the one
    /// every other part of this package means by it. A silently mirrored camera is a bad afternoon.
    /// </summary>
    public static Vector3 RotateVector(Quaternion q, Vector3 v)
    {
        double[] m =
        [
            1 - (2 * ((q.Y * q.Y) + (q.Z * q.Z))),
            2 * ((q.X * q.Y) + (q.Z * q.W)),
            2 * ((q.X * q.Z) - (q.Y * q.W)),
            2 * ((q.X * q.Y) - (q.Z * q.W)),
            1 - (2 * ((q.X * q.X) + (q.Z * q.Z))),
            2 * ((q.Y * q.Z) + (q.X * q.W)),
            2 * ((q.X * q.Z) + (q.Y * q.W)),
            2 * ((q.Y * q.Z) - (q.X * q.W)),
            1 - (2 * ((q.X * q.X) + (q.Y * q.Y))),
        ];
        return new Vector3(
            (m[0] * v.X) + (m[3] * v.Y) + (m[6] * v.Z),
            (m[1] * v.X) + (m[4] * v.Y) + (m[7] * v.Z),
            (m[2] * v.X) + (m[5] * v.Y) + (m[8] * v.Z));
    }

    /// <summary>Spherical interpolation that always takes the shorter arc.</summary>
    public static Quaternion SlerpShortest(Quaternion a, Quaternion b, double t)
    {
        var dot = (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z) + (a.W * b.W);

        // A quaternion and its negation describe the same orientation, so flipping one when
        // they point apart is what makes the interpolation take the short way round.
        var target = b;
        if (dot < 0)
        {
            target = new Quaternion(-b.X, -b.Y, -b.Z, -b.W);
            dot = -dot;
        }

        Quaternion result;
        if (dot > 0.9995)
        {
            // Nearly aligned: a linear blend that is normalised again avoids dividing by a
            // sine that has gone to zero.
            result = new Quaternion(
                a.X + ((target.X - a.X) * t),
                a.Y + ((target.Y - a.Y) * t),
                a.Z + ((target.Z - a.Z) * t),
                a.W + ((target.W - a.W) * t));
        }
        else
        {
            var theta = Math.Acos(Doubles.Clamp(dot, -1.0, 1.0));
            var sinTheta = Math.Sin(theta);
            var wa = Math.Sin((1 - t) * theta) / sinTheta;
            var wb = Math.Sin(t * theta) / sinTheta;
            result = new Quaternion(
                (a.X * wa) + (target.X * wb),
                (a.Y * wa) + (target.Y * wb),
                (a.Z * wa) + (target.Z * wb),
                (a.W * wa) + (target.W * wb));
        }

        result.Normalize();
        return result;
    }

    /// <summary>
    /// A rotation that looks along <paramref name="forward"/>, kept upright against
    /// <paramref name="up"/>. Falls back to another axis when the two are parallel, which
    /// happens the moment a camera looks straight down at something. That is the case that
    /// produces a roll spike if it is not handled.
    /// </summary>
    public static Quaternion LookRotation(Vector3 forward, Vector3? up = null)
    {
        var f = forward.Normalized();
        var reference = (up ?? new Vector3(0, 1, 0)).Normalized();

        if (f.Cross(reference).Length < 1e-4)
        {
            reference = Math.Abs(f.Z) < 0.9 ? new Vector3(0, 0, 1) : new Vector3(1, 0, 0);
        }

        // A camera looks down its own negative Z, so the third basis vector points backwards.
        // The other two come from it rather than from forward: deriving them from forward gives
        // a left handed basis, which is a reflection and converts to a quaternion that refuses to turn.
        var back = -f;
        var right = reference.Cross(back).Normalized();
        var upward = back.Cross(right);

        return FromBasis(right, upward, back);
    }

    /// <summary>
    /// Builds a quaternion from three orthonormal basis vectors, written out here where it can
    /// be read and tested. Shepperd's method: divide by the largest of the four components,
    /// since dividing by the smallest is where the precision goes.
    /// </summary>
    private static Quaternion FromBasis(Vector3 right, Vector3 up, Vector3 back)
    {
        double m00 = right.X, m10 = right.Y, m20 = right.Z;
        double m01 = up.X, m11 = up.Y, m21 = up.Z;
        double m02 = back.X, m12 = back.Y, m22 = back.Z;

        var trace = m00 + m11 + m22;

        Quaternion result;
        if (trace > 0)
        {
            var s = Math.Sqrt(trace + 1.0) * 2;
            result = new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25 * s);
        }
        else if (m00 > m11 && m00 > m22)
        {
            var s = Math.Sqrt(1.0 + m00 - m11 - m22) * 2;
            result = new Quaternion(0.25 * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s);
        }
        else if (m11 > m22)
        {
            var s = Math.Sqrt(1.0 + m11 - m00 - m22) * 2;
            result = new Quaternion((m01 + m10) / s, 0.25 * s, (m12 + m21) / s, (m02 - m20) / s);
        }
        else
        {
            var s = Math.Sqrt(1.0 + m22 - m00 - m11) * 2;
            result = new Quaternion((m02 + m20) / s, (m12 + m21) / s, 0.25 * s, (m10 - m01) / s);
        }

        result.Normalize();
        return result;
    }
}
