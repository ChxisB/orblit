namespace Orblit.VectorMath;

/// <summary>Four doubles, written through as <see cref="Vector3"/> is.</summary>
public sealed class Vector4(double x, double y, double z, double w) : IEquatable<Vector4>
{
    public double X { get; set; } = x;

    public double Y { get; set; } = y;

    public double Z { get; set; } = z;

    public double W { get; set; } = w;

    /// <summary>The summed squares, added w first.</summary>
    public double Length2 => (W * W) + (Z * Z) + (Y * Y) + (X * X);

    public double Length => Math.Sqrt(Length2);

    public Vector4 Clone() => new(X, Y, Z, W);

    public bool Equals(Vector4? other) =>
        other is not null && X == other.X && Y == other.Y && Z == other.Z && W == other.W;

    public override bool Equals(object? obj) => Equals(obj as Vector4);

    public override int GetHashCode() => HashCode.Combine(X, Y, Z, W);

    public override string ToString() => FormattableString.Invariant($"[{X},{Y},{Z},{W}]");
}
