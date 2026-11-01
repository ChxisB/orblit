using System.Diagnostics;
using Orblit.VectorMath;

namespace Orblit.Collide;

/// <summary>A box in two dimensions, lined up with the axes.</summary>
public sealed class Rect2(Vector2 minimum, Vector2 maximum)
{
    public Vector2 Minimum { get; } = minimum;

    public Vector2 Maximum { get; } = maximum;

    public static Rect2 Around(Vector2 centre, Vector2 size) => new(centre - (size * 0.5), centre + (size * 0.5));

    public Vector2 Centre => (Minimum + Maximum) * 0.5;

    public Vector2 Size => Maximum - Minimum;

    public bool Overlaps(Rect2 other) =>
        Minimum.X <= other.Maximum.X
        && Maximum.X >= other.Minimum.X
        && Minimum.Y <= other.Maximum.Y
        && Maximum.Y >= other.Minimum.Y;

    public bool Contains(Vector2 point) =>
        point.X >= Minimum.X
        && point.X <= Maximum.X
        && point.Y >= Minimum.Y
        && point.Y <= Maximum.Y;

    public Vector2 NearestTo(Vector2 to) => new(
        Doubles.Clamp(to.X, Minimum.X, Maximum.X),
        Doubles.Clamp(to.Y, Minimum.Y, Maximum.Y));
}

/// <summary>
/// A hitbox: a circle or a rectangle. Two shapes, and that is the whole list
/// on purpose. Every 2D game's collision is these plus a tile grid, and the
/// third shape anybody asks for, a rotated rectangle, is better served by a
/// circle for the body and a separate test for the swing.
/// </summary>
public abstract class Hitbox
{
    private protected Hitbox()
    {
    }

    public abstract Rect2 Bounds { get; }

    public abstract Vector2 Position { get; }

    public abstract bool Contains(Vector2 point);
}

public sealed class Circle(Vector2 position, double radius) : Hitbox
{
    public override Vector2 Position { get; } = position;

    public double Radius { get; } = radius;

    public override Rect2 Bounds => new(Position - Vector2.All(Radius), Position + Vector2.All(Radius));

    public override bool Contains(Vector2 point) => (point - Position).Length2 <= Radius * Radius;
}

public sealed class Rectangle : Hitbox
{
    public Rectangle(Vector2 position, Vector2 size)
    {
        Position = position;
        Half = size * 0.5;
    }

    public override Vector2 Position { get; }

    public Vector2 Half { get; }

    public Vector2 Size => Half * 2;

    public Rect2 Rect => new(Position - Half, Position + Half);

    public override Rect2 Bounds => Rect;

    public override bool Contains(Vector2 point) => Rect.Contains(point);
}

/// <summary>Two hitboxes touching, and the way out.</summary>
public sealed class Touch(Vector2 point, Vector2 normal, double depth)
{
    public Vector2 Point { get; } = point;

    public Vector2 Normal { get; } = normal;

    public double Depth { get; } = depth;
}

public static class Hitboxes
{
    /// <summary>Whether two hitboxes touch, and where.</summary>
    public static Touch? Touching(Hitbox a, Hitbox b) => (a, b) switch
    {
        (Circle x, Circle y) => Circles(x, y),
        (Circle x, Rectangle y) => CircleRect(x, y),
        (Rectangle x, Circle y) => Flip(CircleRect(y, x)),
        (Rectangle x, Rectangle y) => Rects(x, y),
        _ => throw new UnreachableException(),
    };

    private static Touch? Flip(Touch? found) =>
        found is null ? null : new Touch(found.Point, -found.Normal, found.Depth);

    private static Touch? Circles(Circle a, Circle b)
    {
        var offset = a.Position - b.Position;
        var distance = offset.Length;
        var reach = a.Radius + b.Radius;
        if (distance >= reach)
        {
            return null;
        }

        var normal = distance < 1e-9 ? new Vector2(0, 1) : offset / distance;
        return new Touch(b.Position + (normal * b.Radius), normal, reach - distance);
    }

    private static Touch? CircleRect(Circle a, Rectangle b)
    {
        var near = b.Rect.NearestTo(a.Position);
        var offset = a.Position - near;
        var distance = offset.Length;
        if (distance >= a.Radius)
        {
            return null;
        }

        if (distance > 1e-9)
        {
            return new Touch(near, offset / distance, a.Radius - distance);
        }

        // Centre inside: out through the nearest edge.
        var local = a.Position - b.Position;
        var across = b.Half.X - Math.Abs(local.X);
        var up = b.Half.Y - Math.Abs(local.Y);
        var normal = across < up ? new Vector2(Facing.Of(local.X), 0) : new Vector2(0, Facing.Of(local.Y));
        return new Touch(a.Position, normal, Math.Min(across, up) + a.Radius);
    }

    private static Touch? Rects(Rectangle a, Rectangle b)
    {
        var offset = a.Position - b.Position;
        var across = a.Half.X + b.Half.X - Math.Abs(offset.X);
        if (across <= 0)
        {
            return null;
        }

        var up = a.Half.Y + b.Half.Y - Math.Abs(offset.Y);
        if (up <= 0)
        {
            return null;
        }

        var normal = across < up ? new Vector2(Facing.Of(offset.X), 0) : new Vector2(0, Facing.Of(offset.Y));
        return new Touch(b.Rect.NearestTo(a.Position), normal, Math.Min(across, up));
    }
}

/// <summary>One thing in a 2D world.</summary>
// F2: more than four parameters, and the three after the hitbox are optional and called by name.
public sealed class Body(long id, Hitbox hitbox, bool passthrough = false, long @is = 1, long cares = 0xFFFFFFFF)
{
    /// <summary>Whatever the game calls it.</summary>
    public long Id { get; } = id;

    public Hitbox Hitbox { get; set; } = hitbox;

    /// <summary>
    /// Whether things pass through it. A trigger: it reports touches and resolves
    /// none of them. A door's threshold, a pickup, the bottom of a pit.
    /// Distinguished from a solid body and not left to the callback, because "did
    /// it touch" and "did it stop" are separate questions and a game that answers
    /// them in one place gets a pickup that blocks the player.
    /// </summary>
    public bool Passthrough { get; } = passthrough;

    /// <summary>What it is.</summary>
    public long Is { get; } = @is;

    /// <summary>What it wants to be told about.</summary>
    public long Cares { get; } = cares;
}

/// <summary>A touch between two bodies, as reported.</summary>
public sealed class Collision(long a, long b, Touch touch)
{
    public long A { get; } = a;

    public long B { get; } = b;

    public Touch Touch { get; } = touch;
}

/// <summary>
/// A 2D world that says what is touching what, and when it started and
/// stopped. The three events are the whole of what a game asks for, and only
/// the first is easy. Started and stopped need the previous frame remembered. A
/// world that only reports what is touching now leaves every caller keeping its
/// own set of what was touching before, and every caller keeps it slightly
/// differently.
/// </summary>
public sealed class World2(double cellSize = 8)
{
    private const long PairSize = 1000003;

    private readonly OrderedDictionary<long, Body> _bodies = [];

    // An ordered set, because the pairs that ended come out in the order they were found.
    private OrderedDictionary<long, bool> _touching = [];

    public double CellSize { get; } = cellSize;

    public int Length => _bodies.Count;

    public Body? this[long id] => _bodies.TryGetValue(id, out var body) ? body : null;

    public void Add(Body body) => _bodies[body.Id] = body;

    public void Remove(long id) => _bodies.Remove(id);

    public void Clear()
    {
        _bodies.Clear();
        _touching = [];
    }

    /// <summary>
    /// Works out what is touching, and reports the three events. Called once a
    /// frame. Everything it reports is about this call: <c>Began</c> is what was
    /// not touching last time, <c>Ended</c> is what was and is not now, and
    /// <c>Touching</c> is everything, so a caller that only wants the current
    /// state does not have to add the first two together.
    /// </summary>
    public (List<Collision> Touching, List<Collision> Began, List<(long, long)> Ended) Step()
    {
        var buckets = Bucketed();
        var now = new OrderedDictionary<long, bool>();
        var found = new List<Collision>();
        var began = new List<Collision>();
        var seen = new HashSet<long>();

        foreach (var bucket in buckets.Values)
        {
            for (var i = 0; i < bucket.Count; i++)
            {
                for (var j = i + 1; j < bucket.Count; j++)
                {
                    if (bucket[i] == bucket[j])
                    {
                        continue;
                    }

                    var a = Math.Min(bucket[i], bucket[j]);
                    var b = Math.Max(bucket[i], bucket[j]);
                    var key = Pair(a, b);

                    // A body spanning several cells meets the same neighbour in each of
                    // them. Reporting that four times is the classic fault, and it
                    // reads as a pickup collected four times.
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    var collision = Meet(a, b);
                    if (collision is null)
                    {
                        continue;
                    }

                    found.Add(collision);
                    now[key] = true;
                    if (!_touching.ContainsKey(key))
                    {
                        began.Add(collision);
                    }
                }
            }
        }

        var ended = EndedIn(now);
        _touching = now;
        return (found, began, ended);
    }

    /// <summary>
    /// Pushes solid bodies apart, leaving passthrough ones alone. Half the depth
    /// each, which is the honest answer when neither has a mass: moving one all
    /// the way makes the order they were added in visible.
    /// </summary>
    public void Separate(IEnumerable<Collision> collisions)
    {
        foreach (var collision in collisions)
        {
            var a = this[collision.A];
            var b = this[collision.B];
            if (a is null || b is null)
            {
                continue;
            }

            if (a.Passthrough || b.Passthrough)
            {
                continue;
            }

            var push = collision.Touch.Normal * (collision.Touch.Depth * 0.5);
            a.Hitbox = Moved(a.Hitbox, push);
            b.Hitbox = Moved(b.Hitbox, -push);
        }
    }

    /// <summary>Everything whose hitbox contains <paramref name="point"/>.</summary>
    public List<long> At(Vector2 point)
    {
        var found = _bodies.Where(entry => entry.Value.Hitbox.Contains(point)).Select(entry => entry.Key).ToList();
        found.Sort();
        return found;
    }

    /// <summary>Everything overlapping <paramref name="area"/>.</summary>
    public List<long> Inside(Rect2 area)
    {
        var found = _bodies.Where(entry => entry.Value.Hitbox.Bounds.Overlaps(area)).Select(entry => entry.Key).ToList();
        found.Sort();
        return found;
    }

    /// <summary>
    /// Two ids as one key. An id below 0 or above 1000002 can pair with the wrong
    /// body, and <see cref="Unpair"/> of a negative key is wrong. This pairing is
    /// kept as it is for compatibility.
    /// </summary>
    private static long Pair(long a, long b) => (a * PairSize) + b;

    /// <summary>Integer-style division and remainder: the quotient truncates and the remainder is never negative.</summary>
    private static (long, long) Unpair(long key)
    {
        var remainder = key % PairSize;
        return (key / PairSize, remainder < 0 ? remainder + PairSize : remainder);
    }

    private static Hitbox Moved(Hitbox hitbox, Vector2 by) => hitbox switch
    {
        Circle c => new Circle(c.Position + by, c.Radius),
        Rectangle r => new Rectangle(r.Position + by, r.Size),
        _ => throw new UnreachableException(),
    };

    private OrderedDictionary<long, List<long>> Bucketed()
    {
        var buckets = new OrderedDictionary<long, List<long>>();
        var ids = _bodies.Keys.ToList();
        ids.Sort();

        foreach (var id in ids)
        {
            ForEachCell(_bodies[id].Hitbox.Bounds, key =>
            {
                if (!buckets.TryGetValue(key, out var bucket))
                {
                    bucket = [];
                    buckets.Add(key, bucket);
                }

                bucket.Add(id);
            });
        }

        return buckets;
    }

    /// <summary>The touch between two bodies, or null when their layers, boxes or shapes say no.</summary>
    private Collision? Meet(long a, long b)
    {
        var one = _bodies[a];
        var two = _bodies[b];
        if ((one.Cares & two.Is) == 0 && (two.Cares & one.Is) == 0)
        {
            return null;
        }

        if (!one.Hitbox.Bounds.Overlaps(two.Hitbox.Bounds))
        {
            return null;
        }

        var touch = Hitboxes.Touching(one.Hitbox, two.Hitbox);
        return touch is null ? null : new Collision(a, b, touch);
    }

    private List<(long, long)> EndedIn(OrderedDictionary<long, bool> now)
    {
        var ended = new List<(long, long)>();
        foreach (var key in _touching.Keys)
        {
            if (!now.ContainsKey(key))
            {
                ended.Add(Unpair(key));
            }
        }

        return ended;
    }

    private void ForEachCell(Rect2 box, Action<long> visit)
    {
        var x0 = Doubles.Floor(box.Minimum.X / CellSize);
        var x1 = Doubles.Floor(box.Maximum.X / CellSize);
        var y0 = Doubles.Floor(box.Minimum.Y / CellSize);
        var y1 = Doubles.Floor(box.Maximum.Y / CellSize);
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                // Mixed and not XORed: XOR collides on the symmetric coordinates
                // a grid is full of, and a body that lands in one bucket twice is a
                // body paired with itself.
                var h = x * 73856093;
                h = ((h ^ (h >> 15)) * 2246822519) + (y * 19349663);
                visit(h ^ (h >> 16));
            }
        }
    }
}
