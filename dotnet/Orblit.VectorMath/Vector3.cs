namespace Orblit.VectorMath;

/// <summary>
/// Three doubles. A class and not a struct, as in <c>vector_math_64</c>: a
/// component is written through the vector (<c>light.Color.X = 0</c>), and a
/// copy has to be asked for with <see cref="Clone"/>.
/// </summary>
public sealed class Vector3(double x, double y, double z) : IEquatable<Vector3>
{
    public double X { get; set; } = x;

    public double Y { get; set; } = y;

    public double Z { get; set; } = z;

    /// <summary>A component by position, 0 to 2, as Dart's <c>[]</c> reads it.</summary>
    public double this[int index]
    {
        get => index switch
        {
            0 => X,
            1 => Y,
            2 => Z,
            _ => throw OutOfRange(index),
        };
        set
        {
            switch (index)
            {
                case 0:
                    X = value;
                    break;
                case 1:
                    Y = value;
                    break;
                case 2:
                    Z = value;
                    break;
                default:
                    throw OutOfRange(index);
            }
        }
    }

    private static ArgumentOutOfRangeException OutOfRange(int index) =>
        new(nameof(index), index, "A Vector3 has three components, 0 to 2.");

    /// <summary>The summed squares, added z first as Dart's does, so the last bit agrees.</summary>
    public double Length2 => (Z * Z) + (Y * Y) + (X * X);

    public double Length => Math.Sqrt(Length2);

    public static Vector3 Zero() => new(0, 0, 0);

    public static Vector3 All(double value) => new(value, value, value);

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vector3 operator *(Vector3 a, double scale) => new(a.X * scale, a.Y * scale, a.Z * scale);

    /// <summary>A multiply by the reciprocal, which is what Dart does, not a divide.</summary>
    public static Vector3 operator /(Vector3 a, double scale) => a * (1.0 / scale);

    public static Vector3 operator -(Vector3 a) => new(-a.X, -a.Y, -a.Z);

    public Vector3 Clone() => new(X, Y, Z);

    public void SetValues(double x, double y, double z)
    {
        Z = z;
        Y = y;
        X = x;
    }

    /// <summary>The products summed z first, as Dart's does, so the last bit agrees.</summary>
    public double Dot(Vector3 other) => (Z * other.Z) + (Y * other.Y) + (X * other.X);

    public Vector3 Cross(Vector3 other) =>
        new((Y * other.Z) - (Z * other.Y), (Z * other.X) - (X * other.Z), (X * other.Y) - (Y * other.X));

    /// <summary>The squares summed x first, unlike <see cref="Length2"/>, because that is the order Dart sums them in.</summary>
    public double DistanceToSquared(Vector3 other)
    {
        var dz = Z - other.Z;
        var dy = Y - other.Y;
        var dx = X - other.X;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    public double DistanceTo(Vector3 other) => Math.Sqrt(DistanceToSquared(other));

    /// <summary>
    /// A copy of length one. A vector of length zero comes back unchanged, and a
    /// multiply by the reciprocal is what Dart does, not a divide.
    /// </summary>
    public Vector3 Normalized()
    {
        var length = Length;
        if (length == 0)
        {
            return Clone();
        }

        var d = 1.0 / length;
        return new Vector3(X * d, Y * d, Z * d);
    }

    /// <summary>Component by component, so NaN is not equal to NaN, as in Dart.</summary>
    public bool Equals(Vector3? other) => other is not null && X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj) => Equals(obj as Vector3);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => FormattableString.Invariant($"[{X},{Y},{Z}]");
}
