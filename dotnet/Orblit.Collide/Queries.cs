using System.Diagnostics;
using Orblit.VectorMath;

namespace Orblit.Collide;

/// <summary>A line with a start and a direction.</summary>
public sealed class Ray
{
    public Ray(Vector3 from, Vector3 direction, double distance = double.PositiveInfinity)
    {
        From = from;
        Direction = direction.Length2 < 1e-24 ? new Vector3(0, 0, -1) : direction.Normalized();
        Distance = distance;
    }

    public Vector3 From { get; }

    /// <summary>
    /// Unit length, always. A ray built from a zero vector points forwards and
    /// does not produce NaN at every step downstream.
    /// </summary>
    public Vector3 Direction { get; }

    /// <summary>How far it reaches.</summary>
    public double Distance { get; }

    public Vector3 At(double along) => From + (Direction * along);
}

/// <summary>Where a ray met something.</summary>
public sealed class Hit(double distance, Vector3 point, Vector3 normal, bool inside = false)
{
    /// <summary>How far along the ray, in metres.</summary>
    public double Distance { get; } = distance;

    public Vector3 Point { get; } = point;

    /// <summary>The surface normal where it landed, pointing back towards the ray.</summary>
    public Vector3 Normal { get; } = normal;

    /// <summary>
    /// Whether the ray began inside the shape. Worth reporting and not hiding. A
    /// camera inside geometry and a camera looking at it from outside are the same
    /// hit at distance nought otherwise, and they want opposite handling.
    /// </summary>
    public bool Inside { get; } = inside;
}

public static class Queries
{
    /// <summary>Where <paramref name="ray"/> meets <paramref name="shape"/>, or null.</summary>
    public static Hit? Raycast(Ray ray, Shape shape) => shape switch
    {
        Sphere s => RaySphere(ray, s),
        Box b => RayBox(ray, b),
        Capsule c => RayCapsule(ray, c),
        _ => throw new UnreachableException(),
    };

    /// <summary>Everything in <paramref name="shapes"/> that <paramref name="ray"/> meets, nearest first.</summary>
    public static List<(int Index, Hit Hit)> RaycastAll(Ray ray, IReadOnlyList<Shape> shapes)
    {
        var found = new List<(int Index, Hit Hit)>();
        for (var i = 0; i < shapes.Count; i++)
        {
            var hit = Raycast(ray, shapes[i]);
            if (hit is not null)
            {
                found.Add((i, hit));
            }
        }

        // shortcut: a stable sort, where Dart's is stable only up to 32 items, so
        // ties in a longer list may order differently. Upgrade if a caller needs it.
        return [.. found.OrderBy(f => f.Hit.Distance, Comparer<double>.Create(Doubles.Compare))];
    }

    /// <summary>The first thing <paramref name="ray"/> meets, or null.</summary>
    public static (int Index, Hit Hit)? RaycastFirst(Ray ray, IReadOnlyList<Shape> shapes)
    {
        (int Index, Hit Hit)? best = null;
        for (var i = 0; i < shapes.Count; i++)
        {
            var hit = Raycast(ray, shapes[i]);
            if (hit is null)
            {
                continue;
            }

            if (best is null || hit.Distance < best.Value.Hit.Distance)
            {
                best = (i, hit);
            }
        }

        return best;
    }

    private static Hit? RaySphere(Ray ray, Sphere sphere)
    {
        var offset = ray.From - sphere.Position;
        var b = offset.Dot(ray.Direction);
        var c = offset.Length2 - (sphere.Radius * sphere.Radius);

        // Pointing away from a sphere it is already outside.
        if (c > 0 && b > 0)
        {
            return null;
        }

        var discriminant = (b * b) - c;
        if (discriminant < 0)
        {
            return null;
        }

        var root = Math.Sqrt(discriminant);
        var along = -b - root;
        var inside = along < 0;
        if (inside)
        {
            along = -b + root;
        }

        if (along < 0 || along > ray.Distance)
        {
            return null;
        }

        var point = ray.At(along);
        var normal = (point - sphere.Position).Normalized();
        return new Hit(along, point, inside ? -normal : normal, inside);
    }

    private static Hit? RayBox(Ray ray, Box box)
    {
        var bounds = box.Bounds;
        if (Slabs(ray, bounds) is not var (near, axis, sign))
        {
            return null;
        }

        var inside = bounds.Contains(ray.From);
        if (near > ray.Distance)
        {
            return null;
        }

        var normal = Vector3.Zero();
        normal[axis] = inside ? -sign : sign;
        var along = inside ? 0 : near;
        return new Hit(along, ray.At(along), normal, inside);
    }

    /// <summary>
    /// How far along the ray it enters the box, through which axis and which
    /// side of it, or null when it misses.
    /// </summary>
    private static (double Near, int Axis, double Sign)? Slabs(Ray ray, Aabb bounds)
    {
        var near = 0.0;
        var far = ray.Distance;
        var axis = 0;
        var sign = 1.0;

        for (var i = 0; i < 3; i++)
        {
            var origin = ray.From[i];
            var along = ray.Direction[i];
            var low = bounds.Minimum[i];
            var high = bounds.Maximum[i];

            if (Math.Abs(along) < 1e-12)
            {
                // Parallel to this pair of faces: either between them the whole way or
                // never. Infinity would compare correctly but a divide by nothing is
                // not worth relying on.
                if (origin < low || origin > high)
                {
                    return null;
                }

                continue;
            }

            var first = (low - origin) / along;
            var second = (high - origin) / along;
            var face = -1.0;
            if (first > second)
            {
                (first, second) = (second, first);
                face = 1.0;
            }

            if (first > near)
            {
                near = first;
                axis = i;
                sign = face;
            }

            if (second < far)
            {
                far = second;
            }

            if (near > far)
            {
                return null;
            }
        }

        return (near, axis, sign);
    }

    private static Hit? RayCapsule(Ray ray, Capsule capsule)
    {
        // Marched and not solved. The closed form is a quartic once the ends
        // are included, and the cases where it is ill-conditioned (a ray almost
        // along the axis) are exactly the ones a character controller asks about
        // constantly. This is bounded, monotone and never wrong by more than the
        // tolerance.
        const int steps = 64;
        var reach = double.IsFinite(ray.Distance)
            ? ray.Distance
            : capsule.Bounds.Size.Length + (ray.From - capsule.Position).Length;

        if (capsule.Contains(ray.From))
        {
            return new Hit(0, ray.From.Clone(), -ray.Direction, inside: true);
        }

        var previous = 0.0;
        for (var i = 1; i <= steps; i++)
        {
            var along = reach * i / steps;
            if (capsule.Contains(ray.At(along)))
            {
                return Landed(ray, capsule, previous, along);
            }

            previous = along;
        }

        return null;
    }

    /// <summary>Bisects between the last step outside and the first one inside.</summary>
    private static Hit Landed(Ray ray, Capsule capsule, double outside, double inside)
    {
        var low = outside;
        var high = inside;
        for (var j = 0; j < 24; j++)
        {
            var middle = (low + high) * 0.5;
            if (capsule.Contains(ray.At(middle)))
            {
                high = middle;
            }
            else
            {
                low = middle;
            }
        }

        var point = ray.At(high);
        var (a, b) = capsule.Segment;
        var axis = Shapes.NearestOnSegment(a, b, point);
        var offset = point - axis;
        return new Hit(high, point, offset.Length2 < 1e-18 ? -ray.Direction : offset.Normalized());
    }
}

/// <summary>
/// A grid of buckets, for finding which shapes are near enough to test
/// properly. Every pair is what a scene of a thousand shapes cannot afford:
/// half a million tests to find the four that touch. A grid turns that into
/// "look in my own cell and its neighbours", which is a handful.
/// A grid and not a tree because the things in it move. A tree has to be
/// rebuilt or refitted when they do. A grid is rebuilt by dropping every shape
/// into a bucket, which is one pass and no allocation per shape beyond the
/// bucket lists.
/// </summary>
public sealed class Broadphase(double cellSize = 4)
{
    // Ordered, because pairs come out in the order the buckets were first filled.
    private readonly OrderedDictionary<long, List<int>> _buckets = [];
    private readonly List<Aabb> _bounds = [];

    /// <summary>
    /// How wide a bucket is. About the size of the largest common shape. Much
    /// smaller and a big shape lands in dozens of buckets, much larger and every
    /// bucket holds everything.
    /// </summary>
    public double CellSize { get; } = cellSize;

    public int Length => _bounds.Count;

    public void Clear()
    {
        _buckets.Clear();
        _bounds.Clear();
    }

    /// <summary>Puts <paramref name="shape"/> in, under the index it will be reported by.</summary>
    public int Add(Shape shape) => AddBounds(shape.Bounds);

    public int AddBounds(Aabb bounds)
    {
        var index = _bounds.Count;
        _bounds.Add(bounds);
        ForEachCell(bounds, key =>
        {
            if (!_buckets.TryGetValue(key, out var bucket))
            {
                bucket = [];
                _buckets.Add(key, bucket);
            }

            bucket.Add(index);
        });
        return index;
    }

    /// <summary>
    /// Every pair whose boxes overlap, each reported once. A shape that spans
    /// several buckets meets the same neighbour in each of them, so pairs are
    /// kept in a set. Reporting a collision four times because the two things
    /// were large is the classic fault here, and it shows up as objects being
    /// pushed apart four times as hard.
    /// </summary>
    public List<(int, int)> Pairs()
    {
        var seen = new HashSet<long>();
        var found = new List<(int, int)>();

        foreach (var bucket in _buckets.Values)
        {
            for (var i = 0; i < bucket.Count; i++)
            {
                for (var j = i + 1; j < bucket.Count; j++)
                {
                    if (bucket[i] == bucket[j])
                    {
                        continue;
                    }

                    var a = bucket[i] < bucket[j] ? bucket[i] : bucket[j];
                    var b = bucket[i] < bucket[j] ? bucket[j] : bucket[i];
                    if (!seen.Add(((long)a * 1000003) + b))
                    {
                        continue;
                    }

                    if (_bounds[a].Overlaps(_bounds[b]))
                    {
                        found.Add((a, b));
                    }
                }
            }
        }

        return found;
    }

    /// <summary>Everything whose box overlaps <paramref name="box"/>.</summary>
    public List<int> Near(Aabb box)
    {
        var found = new HashSet<int>();
        ForEachCell(box, key =>
        {
            if (!_buckets.TryGetValue(key, out var bucket))
            {
                return;
            }

            foreach (var index in bucket)
            {
                if (_bounds[index].Overlaps(box))
                {
                    found.Add(index);
                }
            }
        });
        var sorted = found.ToList();
        sorted.Sort();
        return sorted;
    }

    private void ForEachCell(Aabb box, Action<long> visit)
    {
        var x0 = Doubles.Floor(box.Minimum.X / CellSize);
        var x1 = Doubles.Floor(box.Maximum.X / CellSize);
        var y0 = Doubles.Floor(box.Minimum.Y / CellSize);
        var y1 = Doubles.Floor(box.Maximum.Y / CellSize);
        var z0 = Doubles.Floor(box.Minimum.Z / CellSize);
        var z1 = Doubles.Floor(box.Maximum.Z / CellSize);

        for (var z = z0; z <= z1; z++)
        {
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    visit(Key(x, y, z));
                }
            }
        }
    }

    /// <summary>
    /// Three cell coordinates as one integer. Not a string. A grid is rebuilt
    /// every frame and every shape hashes at least one of these, and building
    /// strings for that is most of the frame.
    /// Mixed in sequence and not XORed together, which is how this is usually
    /// written and is wrong: <c>x*a ^ y*b ^ z*c</c> collides constantly on the
    /// symmetric coordinates a grid is full of, so one shape lands in the same
    /// bucket several times and is paired with itself. The arithmetic wraps at 64
    /// bits, as Dart's does.
    /// </summary>
    private static long Key(long x, long y, long z)
    {
        var h = x * 73856093;
        h = ((h ^ (h >> 15)) * 2246822519) + (y * 19349663);
        h = ((h ^ (h >> 13)) * 3266489917) + (z * 83492791);
        return h ^ (h >> 16);
    }
}

/// <summary>
/// What a body is allowed to touch. Two masks and not one: what something
/// is, and what it cares about. A bullet is on the bullet layer
/// and cares about walls and people. A wall is on the wall layer and cares
/// about nothing, because a wall does not initiate anything. One mask forces
/// every pair to be described twice and the two descriptions drift apart.
/// </summary>
public sealed class Layers(long @is = 1, long cares = 0xFFFFFFFF)
{
    /// <summary>The layers this belongs to.</summary>
    public long Is { get; } = @is;

    /// <summary>The layers it wants to be told about.</summary>
    public long Cares { get; } = cares;

    /// <summary>
    /// Whether either of the pair cares about the other. Either, not both: a
    /// bullet that cares about walls should hit a wall that cares about nothing,
    /// and requiring both would mean every passive thing in the world listing
    /// everything that might touch it.
    /// </summary>
    public static bool Interact(Layers a, Layers b) => (a.Cares & b.Is) != 0 || (b.Cares & a.Is) != 0;
}
