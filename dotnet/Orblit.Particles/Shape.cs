using Orblit.VectorMath;

namespace Orblit.Particles;

/// <summary>
/// Where a particle starts and which way it first heads, in the emitter's own
/// space, with +Y up. The subclasses are the five shapes, and only this
/// assembly can add one.
/// </summary>
public abstract class Shape
{
    private protected Shape()
    {
    }

    /// <summary>
    /// Writes the start of particle <paramref name="index"/> to
    /// <paramref name="position"/> and the unit direction it heads in to
    /// <paramref name="direction"/>.
    /// </summary>
    public abstract void Pick(long seed, long index, Vector3 position, Vector3 direction);

    private protected static void AtLeastZero(double value, string name) =>
        Require.That(value >= 0 && double.IsFinite(value), name, value, "must be 0 or more");

    /// <summary>A unit vector, evenly over the sphere.</summary>
    private protected static void Anywhere(long seed, long index, Vector3 output)
    {
        var up = (UnitRandom.At(seed, index, 2) * 2) - 1;
        var side = Math.Sqrt(1 - (up * up));
        var turn = 2 * Math.PI * UnitRandom.At(seed, index, 3);
        output.SetValues(side * Math.Cos(turn), up, side * Math.Sin(turn));
    }
}

/// <summary>Every particle starts at the origin and heads off in a random direction.</summary>
public sealed class PointShape : Shape
{
    public override void Pick(long seed, long index, Vector3 position, Vector3 direction)
    {
        position.SetZero();
        Anywhere(seed, index, direction);
    }
}

/// <summary>Starts anywhere inside a ball and heads away from its centre.</summary>
public sealed class SphereShape : Shape
{
    public SphereShape(double radius = 1)
    {
        AtLeastZero(radius, nameof(radius));
        Radius = radius;
    }

    public double Radius { get; }

    public override void Pick(long seed, long index, Vector3 position, Vector3 direction)
    {
        Anywhere(seed, index, direction);

        // The cube root makes the ball evenly filled and not crowded at the middle.
        var reach = Radius * Math.Pow(UnitRandom.At(seed, index, 4), 1.0 / 3);
        position.SetFrom(direction);
        position.Scale(reach);
    }
}

/// <summary>
/// Starts on a disc of <c>Radius</c> in the XZ plane and heads up, spread by up
/// to <c>Angle</c> radians from straight up.
/// </summary>
public sealed class ConeShape : Shape
{
    public ConeShape(double angle = 0.4, double radius = 0)
    {
        AtLeastZero(radius, nameof(radius));
        Require.That(angle >= 0 && angle <= Math.PI, nameof(angle), angle, "must be 0 to pi");
        Angle = angle;
        Radius = radius;
    }

    public double Angle { get; }

    public double Radius { get; }

    public override void Pick(long seed, long index, Vector3 position, Vector3 direction)
    {
        var across = Radius * Math.Sqrt(UnitRandom.At(seed, index, 4));
        var around = 2 * Math.PI * UnitRandom.At(seed, index, 5);
        position.SetValues(across * Math.Cos(around), 0, across * Math.Sin(around));

        // Even over the cap of the sphere, so the middle of the cone is not denser.
        var up = 1 - (UnitRandom.At(seed, index, 2) * (1 - Math.Cos(Angle)));
        var side = Math.Sqrt(Math.Max(0, 1 - (up * up)));
        var turn = 2 * Math.PI * UnitRandom.At(seed, index, 3);
        direction.SetValues(side * Math.Cos(turn), up, side * Math.Sin(turn));
    }
}

/// <summary>Starts anywhere inside a box of <c>HalfExtents</c> and heads up.</summary>
public sealed class BoxShape : Shape
{
    public BoxShape(Vector3 halfExtents)
    {
        HalfExtents = halfExtents.Clone();
        for (var i = 0; i < 3; i++)
        {
            AtLeastZero(halfExtents[i], nameof(halfExtents));
        }
    }

    public Vector3 HalfExtents { get; }

    public override void Pick(long seed, long index, Vector3 position, Vector3 direction)
    {
        position.SetValues(
            ((UnitRandom.At(seed, index, 2) * 2) - 1) * HalfExtents.X,
            ((UnitRandom.At(seed, index, 3) * 2) - 1) * HalfExtents.Y,
            ((UnitRandom.At(seed, index, 4) * 2) - 1) * HalfExtents.Z);
        direction.SetValues(0, 1, 0);
    }
}
