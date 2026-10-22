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

    /// <summary>A component by position, 0 to 2.</summary>
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

    /// <summary>The summed squares, added z first, an order that fixes the last bit.</summary>
    public double Length2 => (Z * Z) + (Y * Y) + (X * X);

    public double Length => Math.Sqrt(Length2);

    public static Vector3 Zero() => new(0, 0, 0);

    public static Vector3 All(double value) => new(value, value, value);

    public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static Vector3 operator *(Vector3 a, double scale) => new(a.X * scale, a.Y * scale, a.Z * scale);

    /// <summary>A multiply by the reciprocal, not a divide.</summary>
    public static Vector3 operator /(Vector3 a, double scale) => a * (1.0 / scale);

    public static Vector3 operator -(Vector3 a) => new(-a.X, -a.Y, -a.Z);

    public Vector3 Clone() => new(X, Y, Z);

    public void SetFrom(Vector3 other)
    {
        Z = other.Z;
        Y = other.Y;
        X = other.X;
    }

    public void SetZero()
    {
        Z = 0;
        Y = 0;
        X = 0;
    }

    /// <summary>Adds <paramref name="other"/> into this one, in place.</summary>
    public void Add(Vector3 other)
    {
        Z += other.Z;
        Y += other.Y;
        X += other.X;
    }

    /// <summary>Adds <paramref name="other"/> times <paramref name="factor"/> into this one, in place.</summary>
    public void AddScaled(Vector3 other, double factor)
    {
        Z += other.Z * factor;
        Y += other.Y * factor;
        X += other.X * factor;
    }

    /// <summary>Multiplies this one by <paramref name="factor"/>, in place.</summary>
    public void Scale(double factor)
    {
        Z *= factor;
        Y *= factor;
        X *= factor;
    }

    public Vector3 Scaled(double factor) => this * factor;

    public void SetValues(double x, double y, double z)
    {
        Z = z;
        Y = y;
        X = x;
    }

    /// <summary>The products summed z first, an order that fixes the last bit.</summary>
    public double Dot(Vector3 other) => (Z * other.Z) + (Y * other.Y) + (X * other.X);

    public Vector3 Cross(Vector3 other) =>
        new((Y * other.Z) - (Z * other.Y), (Z * other.X) - (X * other.Z), (X * other.Y) - (Y * other.X));

    /// <summary>The squares summed x first, unlike <see cref="Length2"/>, because that order fixes the last bit.</summary>
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
    /// multiply by the reciprocal is used, not a divide.
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

    /// <summary>
    /// Scales to length one, in place, and returns the length it had. A vector of
    /// length zero is left as it is and gives 0.
    /// </summary>
    public double Normalize()
    {
        var length = Length;
        if (length == 0)
        {
            return 0;
        }

        var d = 1.0 / length;
        X *= d;
        Y *= d;
        Z *= d;
        return length;
    }

    /// <summary>Component by component, so NaN is not equal to NaN.</summary>
    public bool Equals(Vector3? other) => other is not null && X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj) => Equals(obj as Vector3);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => FormattableString.Invariant($"[{X},{Y},{Z}]");
}
