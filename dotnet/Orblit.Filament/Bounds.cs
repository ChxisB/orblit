using Orblit.VectorMath;

namespace Orblit.Filament;

/// <summary>A box in world space.</summary>
public sealed class Bounds(Vector3 minimum, Vector3 maximum)
{
    public Vector3 Minimum { get; } = minimum;

    public Vector3 Maximum { get; } = maximum;

    public Vector3 Centre => (Minimum + Maximum) * 0.5;

    public Vector3 Size => Maximum - Minimum;

    /// <summary>
    /// The surface area, which is what a tree is built to minimise. The chance of a random ray
    /// hitting a box is proportional to it, so a tree whose boxes have less area between them
    /// answers a query having opened fewer of them.
    /// </summary>
    public double Area
    {
        get
        {
            var extent = Size;
            if (extent.X < 0 || extent.Y < 0 || extent.Z < 0)
            {
                return 0;
            }

            return 2 * ((extent.X * extent.Y) + (extent.Y * extent.Z) + (extent.Z * extent.X));
        }
    }

    /// <summary>A box around one point.</summary>
    public static Bounds At(Vector3 point) => new(point, point);

    /// <summary>A box around everything in <paramref name="boxes"/>, or null if there are none.</summary>
    public static Bounds? Around(IEnumerable<Bounds> boxes)
    {
        Bounds? all = null;
        foreach (var box in boxes)
        {
            all = all?.Union(box) ?? box;
        }

        return all;
    }

    public Bounds Union(Bounds other) => new(
        new Vector3(
            Minimum.X < other.Minimum.X ? Minimum.X : other.Minimum.X,
            Minimum.Y < other.Minimum.Y ? Minimum.Y : other.Minimum.Y,
            Minimum.Z < other.Minimum.Z ? Minimum.Z : other.Minimum.Z),
        new Vector3(
            Maximum.X > other.Maximum.X ? Maximum.X : other.Maximum.X,
            Maximum.Y > other.Maximum.Y ? Maximum.Y : other.Maximum.Y,
            Maximum.Z > other.Maximum.Z ? Maximum.Z : other.Maximum.Z));

    public bool Overlaps(Bounds other) =>
        Minimum.X <= other.Maximum.X
        && Maximum.X >= other.Minimum.X
        && Minimum.Y <= other.Maximum.Y
        && Maximum.Y >= other.Minimum.Y
        && Minimum.Z <= other.Maximum.Z
        && Maximum.Z >= other.Minimum.Z;

    public bool Contains(Vector3 point) =>
        point.X >= Minimum.X
        && point.X <= Maximum.X
        && point.Y >= Minimum.Y
        && point.Y <= Maximum.Y
        && point.Z >= Minimum.Z
        && point.Z <= Maximum.Z;

    /// <summary>
    /// Where a ray enters and leaves, or null if it misses. The ray is clipped against each pair
    /// of parallel faces in turn, and what survives all three is the span inside the box. There is
    /// no branch on the sign of the direction: a division by a near-zero direction gives an
    /// infinity that compares correctly, and a direction that small is handled as parallel.
    /// </summary>
    public (double Near, double Far)? Hit(Vector3 from, Vector3 direction)
    {
        var near = 0.0;
        var far = double.PositiveInfinity;

        for (var axis = 0; axis < 3; axis++)
        {
            var origin = from[axis];
            var along = direction[axis];
            var low = Minimum[axis];
            var high = Maximum[axis];

            if (Math.Abs(along) < 1e-12)
            {
                // Parallel to this pair of faces: between them all the way, or never.
                if (origin < low || origin > high)
                {
                    return null;
                }

                continue;
            }

            var first = (low - origin) / along;
            var second = (high - origin) / along;
            if (first > second)
            {
                (first, second) = (second, first);
            }

            near = first > near ? first : near;
            far = second < far ? second : far;
            if (near > far)
            {
                return null;
            }
        }

        return (near, far);
    }
}
