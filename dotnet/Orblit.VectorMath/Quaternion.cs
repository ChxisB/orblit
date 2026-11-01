namespace Orblit.VectorMath;

/// <summary>A rotation as x, y, z and w, with w the scalar part.</summary>
public sealed class Quaternion(double x, double y, double z, double w) : IEquatable<Quaternion>
{
    public double X { get; set; } = x;

    public double Y { get; set; } = y;

    public double Z { get; set; } = z;

    public double W { get; set; } = w;

    public static Quaternion Identity() => new(0, 0, 0, 1);

    /// <summary>
    /// A turn of <paramref name="radians"/> about <paramref name="axis"/>. An axis
    /// of length zero gives the zero quaternion, not the identity.
    /// The axis is divided by its own length here, so it need not be normalised.
    /// </summary>
    public static Quaternion AxisAngle(Vector3 axis, double radians)
    {
        var length = axis.Length;
        if (length == 0)
        {
            return new Quaternion(0, 0, 0, 0);
        }

        var halfSin = Math.Sin(radians * 0.5) / length;
        return new Quaternion(axis.X * halfSin, axis.Y * halfSin, axis.Z * halfSin, Math.Cos(radians * 0.5));
    }

    /// <summary>
    /// The shortest turn that takes the direction of <paramref name="a"/> to the direction of
    /// <paramref name="b"/>.
    /// </summary>
    /// <remarks>
    /// Directions less than about two degrees apart give the identity, and opposite ones a half
    /// turn about an axis square to <paramref name="a"/>. A zero vector gives the zero quaternion, since it
    /// has no direction.
    /// </remarks>
    public static Quaternion FromTwoVectors(Vector3 a, Vector3 b)
    {
        var v1 = a.Normalized();
        var v2 = b.Normalized();
        var c = v1.Dot(v2);
        var angle = Math.Acos(c);
        var axis = v1.Cross(v2);
        if (Math.Abs(1.0 + c) < 0.0005)
        {
            angle = Math.PI;

            // Opposite directions: any axis square to the first will do, found by crossing with an axis the
            // first does not lean towards.
            axis = v1.X > v1.Y && v1.X > v1.Z ? v1.Cross(new Vector3(0, 1, 0)) : v1.Cross(new Vector3(1, 0, 0));
        }
        else if (Math.Abs(1.0 - c) < 0.0005)
        {
            angle = 0.0;
            axis = new Vector3(1, 0, 0);
        }

        return AxisAngle(axis.Normalized(), angle);
    }

    /// <summary>
    /// The rotation held in the top left of a matrix. A matrix that carries a
    /// scale gives a quaternion that is not a unit one: nothing is divided out.
    /// </summary>
    public static Quaternion FromRotation(Matrix3 rotation)
    {
        var trace = rotation.Trace;
        if (trace > 0)
        {
            var s = Math.Sqrt(trace + 1.0);
            var w = s * 0.5;
            s = 0.5 / s;
            return new Quaternion(
                (rotation[5] - rotation[7]) * s,
                (rotation[6] - rotation[2]) * s,
                (rotation[1] - rotation[3]) * s,
                w);
        }

        var i = rotation[0] < rotation[4]
            ? (rotation[4] < rotation[8] ? 2 : 1)
            : (rotation[0] < rotation[8] ? 2 : 0);
        var j = (i + 1) % 3;
        var k = (i + 2) % 3;
        var root = Math.Sqrt(rotation.Entry(i, i) - rotation.Entry(j, j) - rotation.Entry(k, k) + 1.0);
        var q = new double[3];
        q[i] = root * 0.5;
        root = 0.5 / root;
        q[j] = (rotation.Entry(j, i) + rotation.Entry(i, j)) * root;
        q[k] = (rotation.Entry(k, i) + rotation.Entry(i, k)) * root;
        return new Quaternion(q[0], q[1], q[2], (rotation.Entry(k, j) - rotation.Entry(j, k)) * root);
    }

    /// <summary>
    /// The turn from three half-angle products:
    /// <paramref name="yaw"/> about y, <paramref name="pitch"/> about x and
    /// <paramref name="roll"/> about z.
    /// </summary>
    public static Quaternion Euler(double yaw, double pitch, double roll)
    {
        var halfYaw = yaw * 0.5;
        var halfPitch = pitch * 0.5;
        var halfRoll = roll * 0.5;
        var cosYaw = Math.Cos(halfYaw);
        var sinYaw = Math.Sin(halfYaw);
        var cosPitch = Math.Cos(halfPitch);
        var sinPitch = Math.Sin(halfPitch);
        var cosRoll = Math.Cos(halfRoll);
        var sinRoll = Math.Sin(halfRoll);
        return new Quaternion(
            (cosRoll * sinPitch * cosYaw) + (sinRoll * cosPitch * sinYaw),
            (cosRoll * cosPitch * sinYaw) - (sinRoll * sinPitch * cosYaw),
            (sinRoll * cosPitch * cosYaw) - (cosRoll * sinPitch * sinYaw),
            (cosRoll * cosPitch * cosYaw) + (sinRoll * sinPitch * sinYaw));
    }

    /// <summary>The turn <paramref name="b"/> followed by the turn <paramref name="a"/>.</summary>
    public static Quaternion operator *(Quaternion a, Quaternion b) => new(
        (a.W * b.X) + (a.X * b.W) + (a.Y * b.Z) - (a.Z * b.Y),
        (a.W * b.Y) + (a.Y * b.W) + (a.Z * b.X) - (a.X * b.Z),
        (a.W * b.Z) + (a.Z * b.W) + (a.X * b.Y) - (a.Y * b.X),
        (a.W * b.W) - (a.X * b.X) - (a.Y * b.Y) - (a.Z * b.Z));

    public Quaternion Clone() => new(X, Y, Z, W);

    public void SetFrom(Quaternion other)
    {
        X = other.X;
        Y = other.Y;
        Z = other.Z;
        W = other.W;
    }

    public void SetValues(double x, double y, double z, double w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    /// <summary>A copy turned the other way. W is kept.</summary>
    public Quaternion Conjugated()
    {
        var copy = Clone();
        copy.Conjugate();
        return copy;
    }

    public Quaternion Normalized()
    {
        var copy = Clone();
        copy.Normalize();
        return copy;
    }

    /// <summary>
    /// A copy that undoes this turn. Unlike <see cref="Conjugated"/> it divides by the squared length, so a
    /// quaternion that is not of length one still gives the true inverse, and the zero quaternion gives
    /// not-a-number.
    /// </summary>
    public Quaternion Inverted()
    {
        var copy = Clone();
        copy.Inverse();
        return copy;
    }

    /// <summary>
    /// The rotation as a matrix. The scale is divided out by the squared length,
    /// so a quaternion that is not of length one still gives a rotation, and the
    /// zero quaternion gives a matrix of not-a-number.
    /// </summary>
    public Matrix3 AsRotationMatrix()
    {
        var s = 2.0 / Length2;
        var xs = X * s;
        var ys = Y * s;
        var zs = Z * s;
        var wx = W * xs;
        var wy = W * ys;
        var wz = W * zs;
        var xx = X * xs;
        var xy = X * ys;
        var xz = X * zs;
        var yy = Y * ys;
        var yz = Y * zs;
        var zz = Z * zs;

        var m = new Matrix3();
        m[0] = 1.0 - (yy + zz);
        m[1] = xy + wz;
        m[2] = xz - wy;
        m[3] = xy - wz;
        m[4] = 1.0 - (xx + zz);
        m[5] = yz + wx;
        m[6] = xz + wy;
        m[7] = yz - wx;
        m[8] = 1.0 - (xx + yy);
        return m;
    }

    /// <summary>Negates x, y and z in place, which turns the other way. W is left as it is.</summary>
    public void Conjugate()
    {
        X = -X;
        Y = -Y;
        Z = -Z;
    }

    /// <summary>Inverts in place: turns the other way and divides by the squared length.</summary>
    public void Inverse()
    {
        var l = 1.0 / Length2;
        W *= l;
        Z = -Z * l;
        Y = -Y * l;
        X = -X * l;
    }

    public double Length2 => (X * X) + (Y * Y) + (Z * Z) + (W * W);

    public double Length => Math.Sqrt(Length2);

    /// <summary>
    /// Scales to length one, in place. A quaternion of length zero is left as it
    /// is, and the multiply is by the reciprocal.
    /// </summary>
    public void Normalize()
    {
        var length = Length;
        if (length == 0)
        {
            return;
        }

        var d = 1.0 / length;
        X *= d;
        Y *= d;
        Z *= d;
        W *= d;
    }

    public bool Equals(Quaternion? other) =>
        other is not null && X == other.X && Y == other.Y && Z == other.Z && W == other.W;

    public override bool Equals(object? obj) => Equals(obj as Quaternion);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);

    public override string ToString() => FormattableString.Invariant($"{X}, {Y}, {Z} @ {W}");
}
