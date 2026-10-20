namespace Orblit.VectorMath;

/// <summary>
/// Two doubles. A class and not a struct, for the reason <see cref="Vector3"/>
/// is one.
/// </summary>
public sealed class Vector2(double x, double y) : IEquatable<Vector2>
{
    public double X { get; set; } = x;

    public double Y { get; set; } = y;

    public static Vector2 All(double value) => new(value, value);

    public static Vector2 Zero() => new(0, 0);

    /// <summary>The summed squares, added y first as Dart's does, so the last bit agrees.</summary>
    public double Length2 => (Y * Y) + (X * X);

    public double Length => Math.Sqrt(Length2);

    public Vector2 Clone() => new(X, Y);

    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);

    public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);

    public static Vector2 operator -(Vector2 a) => new(-a.X, -a.Y);

    public static Vector2 operator *(Vector2 a, double scale) => new(a.X * scale, a.Y * scale);

    /// <summary>A multiply by the reciprocal, which is what Dart does, not a divide.</summary>
    public static Vector2 operator /(Vector2 a, double scale) => a * (1.0 / scale);

    /// <summary>Component by component, so NaN is not equal to NaN, as in Dart.</summary>
    public bool Equals(Vector2? other) => other is not null && X == other.X && Y == other.Y;

    public override bool Equals(object? obj) => Equals(obj as Vector2);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public override string ToString() => FormattableString.Invariant($"[{X},{Y}]");
}
