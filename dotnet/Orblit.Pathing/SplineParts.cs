using Orblit.VectorMath;

namespace Orblit.Pathing;

/// <summary>The extension a spline is written under.</summary>
public static class PathNames
{
    public const string Extension = ".opath";
}

/// <summary>A spline file that could not be read.</summary>
public sealed class PathFormatException(string message) : Exception(message);

/// <summary>
/// A point a <see cref="Spline"/> passes through, how the way tilts there, and the
/// handles that bend the curve into and out of it.
/// A handle is an offset from the point, in metres. A knot with no handles
/// is a corner: the curve arrives and leaves it in a straight line.
/// </summary>
public sealed class PathKnot
{
    public PathKnot(Vector3 position, double tilt = 0, Vector3? handleIn = null, Vector3? handleOut = null)
    {
        Tilt = tilt;
        PositionRef = position.Clone();
        InRef = handleIn?.Clone() ?? Vector3.Zero();
        OutRef = handleOut?.Clone() ?? Vector3.Zero();
        if (!(double.IsFinite(tilt) && IsFinite(PositionRef) && IsFinite(InRef) && IsFinite(OutRef)))
        {
            throw new ArgumentException("A knot is made of finite numbers.");
        }
    }

    /// <summary>
    /// How far the up direction is rolled about the way forward, in radians.
    /// Positive rolls it towards the right-hand side of travel.
    /// </summary>
    public double Tilt { get; }

    public Vector3 Position => PositionRef.Clone();

    /// <summary>Where the curve comes from, as an offset from <see cref="Position"/>.</summary>
    public Vector3 HandleIn => InRef.Clone();

    /// <summary>Where the curve is going, as an offset from <see cref="Position"/>.</summary>
    public Vector3 HandleOut => OutRef.Clone();

    internal Vector3 PositionRef { get; }

    internal Vector3 InRef { get; }

    internal Vector3 OutRef { get; }

    private static bool IsFinite(Vector3 v) => double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z);
}

/// <summary>Where a spline is at one distance, and which way it faces there.</summary>
/// <param name="Position">Where the spline is.</param>
/// <param name="Forward">The direction of travel, a unit vector.</param>
/// <param name="Up">
/// Which way is up for something riding the curve, a unit vector at a right angle to
/// <paramref name="Forward"/> with the knots' tilt already in it.
/// </param>
public sealed record PathFrame(Vector3 Position, Vector3 Forward, Vector3 Up)
{
    /// <summary>The right-hand side of travel.</summary>
    public Vector3 Right => Forward.Cross(Up);
}

/// <summary>The point of a spline nearest to somewhere.</summary>
/// <param name="Distance">How far along the spline the point is, in metres.</param>
/// <param name="Position">The point.</param>
/// <param name="Offset">How far the place asked about is from <paramref name="Position"/>.</param>
public sealed record PathNearest(double Distance, Vector3 Position, double Offset);
