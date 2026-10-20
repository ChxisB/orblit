using System.Diagnostics;
using Orblit.VectorMath;

namespace Orblit.Collide;

/// <summary>A box lined up with the axes.</summary>
public sealed class Aabb(Vector3 minimum, Vector3 maximum)
{
    public Vector3 Minimum { get; } = minimum;

    public Vector3 Maximum { get; } = maximum;

    public static Aabb Around(Vector3 centre, Vector3 size) => new(centre - (size * 0.5), centre + (size * 0.5));

    public Vector3 Centre => (Minimum + Maximum) * 0.5;

    public Vector3 Size => Maximum - Minimum;

    public bool Overlaps(Aabb other) =>
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

    /// <summary>The point inside this box nearest <paramref name="to"/>.</summary>
    public Vector3 NearestTo(Vector3 to) => new(
        Doubles.Clamp(to.X, Minimum.X, Maximum.X),
        Doubles.Clamp(to.Y, Minimum.Y, Maximum.Y),
        Doubles.Clamp(to.Z, Minimum.Z, Maximum.Z));

    public Aabb Union(Aabb other) => new(
        new Vector3(
            Math.Min(Minimum.X, other.Minimum.X),
            Math.Min(Minimum.Y, other.Minimum.Y),
            Math.Min(Minimum.Z, other.Minimum.Z)),
        new Vector3(
            Math.Max(Maximum.X, other.Maximum.X),
            Math.Max(Maximum.Y, other.Maximum.Y),
            Math.Max(Maximum.Z, other.Maximum.Z)));

    public Aabb GrownBy(double margin) => new(Minimum - Vector3.All(margin), Maximum + Vector3.All(margin));
}

/// <summary>
/// Something that can be hit, overlapped and asked about.
/// A short list, and it stays short. Every shape here has a closed-form
/// distance to a point, which is what makes every query exact and not
/// iterative. A shape without one (a cone, a torus, a mesh) would need every
/// query to grow a numerical path beside the exact one. Anything more
/// complicated is better made of several of these.
/// </summary>
public abstract class Shape
{
    private protected Shape()
    {
    }

    /// <summary>A box around it, for the broadphase.</summary>
    public abstract Aabb Bounds { get; }

    /// <summary>Where it is.</summary>
    public abstract Vector3 Position { get; }

    /// <summary>The point on or inside this shape nearest <paramref name="to"/>.</summary>
    public abstract Vector3 NearestTo(Vector3 to);

    /// <summary>Whether <paramref name="point"/> is inside.</summary>
    public abstract bool Contains(Vector3 point);

    /// <summary>This shape moved.</summary>
    public abstract Shape MovedTo(Vector3 to);
}

public sealed class Sphere(Vector3 position, double radius) : Shape
{
    public override Vector3 Position { get; } = position;

    public double Radius { get; } = radius;

    public override Aabb Bounds => new(Position - Vector3.All(Radius), Position + Vector3.All(Radius));

    public override Vector3 NearestTo(Vector3 to)
    {
        var offset = to - Position;
        var distance = offset.Length;
        if (distance <= Radius || distance < 1e-12)
        {
            return to.Clone();
        }

        return Position + (offset * (Radius / distance));
    }

    public override bool Contains(Vector3 point) => (point - Position).Length2 <= Radius * Radius;

    public override Sphere MovedTo(Vector3 to) => new(to, Radius);
}

public sealed class Box : Shape
{
    public Box(Vector3 position, Vector3 size)
    {
        Position = position;
        Half = size * 0.5;
    }

    public override Vector3 Position { get; }

    /// <summary>Half its extent on each axis.</summary>
    public Vector3 Half { get; }

    public Vector3 Size => Half * 2;

    public override Aabb Bounds => new(Position - Half, Position + Half);

    public override Vector3 NearestTo(Vector3 to) => Bounds.NearestTo(to);

    public override bool Contains(Vector3 point) => Bounds.Contains(point);

    public override Box MovedTo(Vector3 to) => new(to, Size);
}

/// <summary>
/// A cylinder with a hemisphere on each end.
/// What a person is, for the purposes of walking into things. A box catches on
/// every corner and a sphere cannot stand up. A capsule slides along walls,
/// steps over small things and does not tip over, which is most of what a
/// character controller is asking of a shape.
/// </summary>
public sealed class Capsule(Vector3 position, double height, double radius) : Shape
{
    /// <summary>The centre of the whole shape.</summary>
    public override Vector3 Position { get; } = position;

    /// <summary>Its total height, ends included. Below twice the radius it is a sphere.</summary>
    public double Height { get; } = height;

    public double Radius { get; } = radius;

    /// <summary>The two ends of the line inside it.</summary>
    public (Vector3, Vector3) Segment
    {
        get
        {
            var half = Math.Max((Height * 0.5) - Radius, 0.0);
            return (Position - new Vector3(0, half, 0), Position + new Vector3(0, half, 0));
        }
    }

    public override Aabb Bounds
    {
        get
        {
            var (low, high) = Segment;
            return new Aabb(low - Vector3.All(Radius), high + Vector3.All(Radius));
        }
    }

    public override Vector3 NearestTo(Vector3 to)
    {
        var (low, high) = Segment;
        var axis = Shapes.NearestOnSegment(low, high, to);
        var offset = to - axis;
        var distance = offset.Length;
        if (distance <= Radius || distance < 1e-12)
        {
            return to.Clone();
        }

        return axis + (offset * (Radius / distance));
    }

    public override bool Contains(Vector3 point)
    {
        var (low, high) = Segment;
        return (point - Shapes.NearestOnSegment(low, high, point)).Length2 <= Radius * Radius;
    }

    public override Capsule MovedTo(Vector3 to) => new(to, Height, Radius);
}

/// <summary>
/// Two shapes touching, and what it would take to separate them.
/// The pair of normal and depth is what makes a contact usable and not merely
/// informative. A test that says only yes leaves the caller to work out how
/// far to push, and every caller works it out slightly differently.
/// </summary>
public sealed class Contact(Vector3 point, Vector3 normal, double depth)
{
    /// <summary>Where they meet.</summary>
    public Vector3 Point { get; } = point;

    /// <summary>Which way the first would have to move to come apart, unit length.</summary>
    public Vector3 Normal { get; } = normal;

    /// <summary>How far into each other they are.</summary>
    public double Depth { get; } = depth;

    public override string ToString() => FormattableString.Invariant($"Contact({Depth:F3} along {Normal} at {Point})");
}

/// <summary>What two shapes are doing to each other, in closed form.</summary>
public static class Shapes
{
    /// <summary>
    /// Whether two shapes are touching, and where. Null for no contact and not
    /// a contact of zero depth: "they are not touching" and "they are touching
    /// by nothing" want different code, and folding them together is how a
    /// resolver ends up dividing by a depth of nought.
    /// </summary>
    public static Contact? Contact(Shape a, Shape b)
    {
        // Every pair here reduces to a distance between two features (point to
        // point, point to box, segment to segment) because every shape has a
        // closed-form nearest point. That is why there is no table of a dozen
        // special cases below.
        return (a, b) switch
        {
            (Sphere x, Sphere y) => SphereSphere(x, y),
            (Sphere x, Box y) => SphereBox(x, y),
            (Box x, Sphere y) => Flip(SphereBox(y, x)),
            (Box x, Box y) => BoxBox(x, y),
            (Capsule x, Sphere y) => CapsuleSphere(x, y),
            (Sphere x, Capsule y) => Flip(CapsuleSphere(y, x)),
            (Capsule x, Capsule y) => CapsuleCapsule(x, y),
            (Capsule x, Box y) => CapsuleBox(x, y),
            (Box x, Capsule y) => Flip(CapsuleBox(y, x)),
            _ => throw new UnreachableException(),
        };
    }

    /// <summary>Whether they touch at all. Cheaper than asking where.</summary>
    public static bool Overlaps(Shape a, Shape b) => a.Bounds.Overlaps(b.Bounds) && Contact(a, b) is not null;

    /// <summary>The point on the segment <c>a</c> to <c>b</c> nearest <paramref name="to"/>.</summary>
    public static Vector3 NearestOnSegment(Vector3 a, Vector3 b, Vector3 to)
    {
        var along = b - a;
        var length2 = along.Length2;
        if (length2 < 1e-24)
        {
            return a.Clone();
        }

        var t = Doubles.Clamp((to - a).Dot(along) / length2, 0.0, 1.0);
        return a + (along * t);
    }

    /// <summary>The nearest pair of points on two segments.</summary>
    public static (Vector3, Vector3) NearestBetweenSegments(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
    {
        var da = a1 - a0;
        var db = b1 - b0;
        var r = a0 - b0;
        var aa = da.Length2;
        var bb = db.Length2;
        var rb = r.Dot(db);

        if (aa < 1e-24 && bb < 1e-24)
        {
            return (a0.Clone(), b0.Clone());
        }

        if (aa < 1e-24)
        {
            return (a0.Clone(), b0 + (db * Doubles.Clamp(rb / bb, 0.0, 1.0)));
        }

        var ra = r.Dot(da);
        if (bb < 1e-24)
        {
            return (a0 + (da * Doubles.Clamp(-ra / aa, 0.0, 1.0)), b0.Clone());
        }

        var ab = da.Dot(db);
        var denominator = (aa * bb) - (ab * ab);

        // Parallel segments have no single nearest pair, so one end is taken and
        // the other found against it. Any answer is as correct as any other and
        // this one is stable.
        var s = denominator < 1e-12 ? 0.0 : Doubles.Clamp(((ab * rb) - (ra * bb)) / denominator, 0.0, 1.0);
        var t = ((ab * s) + rb) / bb;

        if (t < 0)
        {
            t = 0;
            s = Doubles.Clamp(-ra / aa, 0.0, 1.0);
        }
        else if (t > 1)
        {
            t = 1;
            s = Doubles.Clamp((ab - ra) / aa, 0.0, 1.0);
        }

        return (a0 + (da * s), b0 + (db * t));
    }

    private static Contact? Flip(Contact? found) =>
        found is null ? null : new Contact(found.Point, -found.Normal, found.Depth);

    private static Contact? SphereSphere(Sphere a, Sphere b)
    {
        var offset = a.Position - b.Position;
        var distance = offset.Length;
        var reach = a.Radius + b.Radius;
        if (distance >= reach)
        {
            return null;
        }

        // Two spheres exactly on top of each other have no direction to separate
        // along. Up is as good as any, and it is at least stable. A random one
        // would jitter for as long as they stayed there.
        var normal = distance < 1e-9 ? new Vector3(0, 1, 0) : offset / distance;
        return new Contact(b.Position + (normal * b.Radius), normal, reach - distance);
    }

    private static Contact? SphereBox(Sphere a, Box b)
    {
        var near = b.Bounds.NearestTo(a.Position);
        var offset = a.Position - near;
        var distance = offset.Length;

        if (distance >= a.Radius)
        {
            return null;
        }

        if (distance > 1e-9)
        {
            return new Contact(near, offset / distance, a.Radius - distance);
        }

        // The centre is inside the box, so the nearest surface point is the centre
        // itself and there is no direction in it. The way out is the nearest face.
        var local = a.Position - b.Position;
        var left = b.Half.X - Math.Abs(local.X);
        var down = b.Half.Y - Math.Abs(local.Y);
        var back = b.Half.Z - Math.Abs(local.Z);

        var normal = new Vector3(Facing.Of(local.X), 0, 0);
        var depth = left;
        if (down < depth)
        {
            normal = new Vector3(0, Facing.Of(local.Y), 0);
            depth = down;
        }

        if (back < depth)
        {
            normal = new Vector3(0, 0, Facing.Of(local.Z));
            depth = back;
        }

        return new Contact(a.Position, normal, depth + a.Radius);
    }

    private static Contact? BoxBox(Box a, Box b)
    {
        var offset = a.Position - b.Position;
        var overlapX = a.Half.X + b.Half.X - Math.Abs(offset.X);
        if (overlapX <= 0)
        {
            return null;
        }

        var overlapY = a.Half.Y + b.Half.Y - Math.Abs(offset.Y);
        if (overlapY <= 0)
        {
            return null;
        }

        var overlapZ = a.Half.Z + b.Half.Z - Math.Abs(offset.Z);
        if (overlapZ <= 0)
        {
            return null;
        }

        // Out along the axis they overlap least on. Any other axis is a longer way
        // out, and pushing along it is what makes a stack of boxes explode.
        var normal = new Vector3(Facing.Of(offset.X), 0, 0);
        var depth = overlapX;
        if (overlapY < depth)
        {
            normal = new Vector3(0, Facing.Of(offset.Y), 0);
            depth = overlapY;
        }

        if (overlapZ < depth)
        {
            normal = new Vector3(0, 0, Facing.Of(offset.Z));
            depth = overlapZ;
        }

        return new Contact(b.Bounds.NearestTo(a.Position), normal, depth);
    }

    private static Contact? CapsuleSphere(Capsule a, Sphere b)
    {
        var (low, high) = a.Segment;
        var near = NearestOnSegment(low, high, b.Position);
        return SphereSphere(new Sphere(near, a.Radius), b);
    }

    private static Contact? CapsuleCapsule(Capsule a, Capsule b)
    {
        var (a0, a1) = a.Segment;
        var (b0, b1) = b.Segment;
        var (pa, pb) = NearestBetweenSegments(a0, a1, b0, b1);
        return SphereSphere(new Sphere(pa, a.Radius), new Sphere(pb, b.Radius));
    }

    private static Contact? CapsuleBox(Capsule a, Box b)
    {
        // The point on the capsule's axis nearest the box, found by stepping. The
        // nearest point on the box depends on the point on the axis and the other
        // way round, so there is no closed form. Four iterations settles it to
        // well under a millimetre for anything person-sized, and it converges
        // monotonically. This is not a search that can wander.
        var (low, high) = a.Segment;
        var onAxis = NearestOnSegment(low, high, b.Position);
        for (var i = 0; i < 4; i++)
        {
            var onBox = b.Bounds.NearestTo(onAxis);
            var next = NearestOnSegment(low, high, onBox);
            if ((next - onAxis).Length2 < 1e-14)
            {
                break;
            }

            onAxis = next;
        }

        return SphereBox(new Sphere(onAxis, a.Radius), b);
    }
}
