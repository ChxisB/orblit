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

    public Vector3 Clone() => new(X, Y, Z);

    /// <summary>Component by component, so NaN is not equal to NaN, as in Dart.</summary>
    public bool Equals(Vector3? other) => other is not null && X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object? obj) => Equals(obj as Vector3);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => FormattableString.Invariant($"[{X},{Y},{Z}]");
}
