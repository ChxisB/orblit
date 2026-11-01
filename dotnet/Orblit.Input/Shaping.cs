using Orblit.VectorMath;

namespace Orblit.Input;

/// <summary>
/// A dead zone, a saturation point and a response curve. A stick is shaped by its length, so the
/// zone is round and a diagonal cannot outrun a straight push.
/// </summary>
public sealed class Shaping : IEquatable<Shaping>
{
    public Shaping(double inner = 0.15, double outer = 0.95, double curve = 1.0)
    {
        Inner = inner;
        Outer = outer;
        Curve = curve;
    }

    public static Shaping Triggers { get; } = new(inner: 0.06, outer: 0.98);

    public static Shaping None { get; } = new(inner: 0, outer: 1);

    public double Inner { get; }

    public double Outer { get; }

    public double Curve { get; }

    public double Scalar(double value)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        var shaped = Shape(Math.Abs(value));
        return double.IsNegative(value) ? -shaped : shaped;
    }

    public Vector2 Stick(Vector2 value)
    {
        var length = value.Length;
        if (!double.IsFinite(length) || length <= Inner)
        {
            return Vector2.Zero();
        }

        var shaped = Shape(length);
        if (shaped <= 0)
        {
            return Vector2.Zero();
        }

        // Divided by the measured length rather than normalised in place, so a caller's vector is
        // never modified and a zero length never divides.
        return value * (shaped / length);
    }

    /// <summary>Two shapings with the same fields are equal.</summary>
    public bool Equals(Shaping? other) =>
        other is not null && Inner == other.Inner && Outer == other.Outer && Curve == other.Curve;

    public override bool Equals(object? obj) => Equals(obj as Shaping);

    public override int GetHashCode() => HashCode.Combine(Inner, Outer, Curve);

    public override string ToString() =>
        FormattableString.Invariant($"Shaping({Inner}..{Outer}, curve {Curve})");

    private double Shape(double magnitude)
    {
        if (magnitude <= Inner)
        {
            return 0;
        }

        var span = Outer - Inner;
        if (span <= 0)
        {
            return 1;
        }

        var t = Doubles.Clamp((magnitude - Inner) / span, 0.0, 1.0);
        return Curve == 1.0 ? t : Math.Pow(t, Curve);
    }
}
