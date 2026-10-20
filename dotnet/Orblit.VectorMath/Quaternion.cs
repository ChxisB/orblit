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
    /// of length zero gives the zero quaternion, not the identity, as in Dart.
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

    /// <summary>The turn <paramref name="b"/> followed by the turn <paramref name="a"/>, as Dart's <c>*</c>.</summary>
    public static Quaternion operator *(Quaternion a, Quaternion b) => new(
        (a.W * b.X) + (a.X * b.W) + (a.Y * b.Z) - (a.Z * b.Y),
        (a.W * b.Y) + (a.Y * b.W) + (a.Z * b.X) - (a.X * b.Z),
        (a.W * b.Z) + (a.Z * b.W) + (a.X * b.Y) - (a.Y * b.X),
        (a.W * b.W) - (a.X * b.X) - (a.Y * b.Y) - (a.Z * b.Z));

    public Quaternion Clone() => new(X, Y, Z, W);

    public bool Equals(Quaternion? other) =>
        other is not null && X == other.X && Y == other.Y && Z == other.Z && W == other.W;

    public override bool Equals(object? obj) => Equals(obj as Quaternion);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);

    public override string ToString() => FormattableString.Invariant($"{X}, {Y}, {Z} @ {W}");
}
