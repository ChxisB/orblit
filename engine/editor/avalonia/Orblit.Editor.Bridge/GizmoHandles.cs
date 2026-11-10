using System.Numerics;

namespace Orblit.Editor.Bridge;

/// <summary>An axis of the move gizmo.</summary>
public enum GizmoAxis
{
    X,
    Y,
    Z,
}

/// <summary>The space the gizmo's axes are in. Global axes are the world's. Local axes turn with the entity.</summary>
public enum GizmoSpace
{
    Global,
    Local,
}

/// <summary>An axis of the move gizmo, in a space.</summary>
public readonly record struct GizmoDirection(GizmoAxis Axis, GizmoSpace Space);

/// <summary>A ray in world space: where it starts and the unit direction it runs.</summary>
public readonly record struct Ray(Vector3 Origin, Vector3 Direction);

/// <summary>A pointer move in frame pixels, from where the pointer was to where it is.</summary>
public readonly record struct FrameMove(Vector2 From, Vector2 To)
{
    public Vector2 Delta => To - From;
}

/// <summary>Where the pivot of the selected entity and the tip of each axis' handle land in the frame, in pixels.</summary>
public readonly record struct GizmoHandles(Vector2 Origin, Vector2 X, Vector2 Y, Vector2 Z)
{
    public Vector2 Tip(GizmoAxis axis) => axis switch
    {
        GizmoAxis.X => X,
        GizmoAxis.Y => Y,
        _ => Z,
    };
}

/// <summary>
/// The rotate gizmo's rings, one closed polyline of frame points per axis, around the pivot. A ring runs in
/// the direction a turn about its axis takes a point, by the right hand rule. The first point is not
/// repeated at the end. The outer ring lies in the plane of the screen, and is empty when none was asked for.
/// </summary>
public readonly record struct GizmoRings(Vector2 Origin, Vector2[] X, Vector2[] Y, Vector2[] Z)
{
    public Vector2[] Outer { get; init; } = [];

    public Vector2[] Of(GizmoAxis axis) => axis switch
    {
        GizmoAxis.X => X,
        GizmoAxis.Y => Y,
        _ => Z,
    };
}

/// <summary>The steps a snapped drag moves in: world units for a move, degrees for a turn, and a share of the scale.</summary>
public readonly record struct GizmoSnap(float Move, float Turn, float Scale)
{
    public static readonly GizmoSnap Default = new(1f, 15f, 0.1f);

    public float TurnRadians => Turn * MathF.PI / 180f;
}

/// <summary>
/// The screen side of the gizmos. A handle always has the same length on screen. A drag is read from the rays
/// under the pointer before and after it moved, so the result is the same for one big move or many small ones.
/// </summary>
public static class GizmoMath
{
    /// <summary>The length of a handle on screen, in frame pixels.</summary>
    public const float HandleLength = 80f;

    /// <summary>The probe's world length as a share of the distance from the camera to the pivot.</summary>
    public const float ProbeShare = 0.1f;

    /// <summary>The number of points on a rotate ring.</summary>
    public const int RingPoints = 48;

    /// <summary>The outer ring's radius as a share of the radius of the three axis rings.</summary>
    public const float OuterShare = 1.15f;

    // An axis that points at the camera shows as a dot. It has no direction to drag along.
    const float MinProbe = 1f;

    // A basis column shorter than this is a collapsed axis, with no direction.
    const float MinColumn = 1e-12f;

    // A ray this close to parallel with an axis, as the sine of the angle between them, has no usable nearest point.
    const float MinSine = 0.02f;

    // A scale drag cannot start nearer the pivot than this share of the probe. The ratio there is all noise.
    const float MinStart = 0.01f;

    // A ring plane seen this close to edge on, as the cosine of the angle to its normal, turns by the screen tangent.
    const float MinFace = 0.15f;

    // A pointer nearer the pivot than this, in frame pixels, has no angle round it.
    const float MinSpin = 1f;

    // A scale value under this grows by a step instead of a ratio, so an axis scaled to nothing can leave zero.
    const float MinScale = 0.1f;

    // The radius is read off a probe, so a few more passes settle what perspective bends.
    const int RadiusPasses = 4;

    // Every sixth point of a ring, eight in all, is enough to average its reach.
    const int RadiusStride = 6;

    public static Vector3 Axis(GizmoAxis axis) => axis switch
    {
        GizmoAxis.X => Vector3.UnitX,
        GizmoAxis.Y => Vector3.UnitY,
        _ => Vector3.UnitZ,
    };

    /// <summary>
    /// The unit vector of an axis for an entity placed at <paramref name="place"/>. A local axis is a column of the
    /// basis, and falls back to the global axis when the entity is scaled to nothing along it.
    /// </summary>
    public static Vector3 Axis(GizmoDirection direction, Transform3D place)
    {
        var global = Axis(direction.Axis);
        if (direction.Space == GizmoSpace.Global)
        {
            return global;
        }

        var column = new Vector3(Vector3.Dot(place.Row0, global), Vector3.Dot(place.Row1, global), Vector3.Dot(place.Row2, global));
        return column.LengthSquared() < MinColumn ? global : Vector3.Normalize(column);
    }

    /// <summary>The tip of a handle, or the origin when the axis shows as a dot.</summary>
    public static Vector2 Tip(Vector2 origin, Vector2 probe)
    {
        var onScreen = probe - origin;
        return onScreen.Length() < MinProbe ? origin : origin + Vector2.Normalize(onScreen) * HandleLength;
    }

    /// <summary>
    /// A circle around a center in the plane across an axis, as <see cref="RingPoints"/> world points. It
    /// runs the way a turn about the axis takes a point, by the right hand rule.
    /// </summary>
    public static Vector3[] Ring(Vector3 center, Vector3 axis, float radius)
    {
        var across = Vector3.Normalize(Vector3.Cross(axis, MathF.Abs(axis.X) < 0.9f ? Vector3.UnitX : Vector3.UnitY));
        var other = Vector3.Cross(axis, across);
        var points = new Vector3[RingPoints];
        for (var i = 0; i < RingPoints; i++)
        {
            var angle = MathF.Tau * i / RingPoints;
            points[i] = center + radius * (MathF.Cos(angle) * across + MathF.Sin(angle) * other);
        }
        return points;
    }

    /// <summary>
    /// The angle in radians a drag of frame pixels turns an entity, for a pointer at <paramref name="point"/>.
    /// The drag counts along the ring's screen direction at the ring point nearest the pointer, and a ring's
    /// width on screen is <see cref="HandleLength"/>. Many small drags add up to one big one.
    /// </summary>
    public static float RingAngle(Vector2[] ring, Vector2 point, Vector2 pixels)
    {
        var nearest = 0;
        for (var i = 1; i < ring.Length; i++)
        {
            nearest = Vector2.DistanceSquared(ring[i], point) < Vector2.DistanceSquared(ring[nearest], point) ? i : nearest;
        }

        var along = ring[(nearest + 1) % ring.Length] - ring[(nearest + ring.Length - 1) % ring.Length];
        return along.LengthSquared() < MinColumn ? 0f : Vector2.Dot(pixels, Vector2.Normalize(along)) / HandleLength;
    }

    /// <summary>The placement turned about a line through the origin. The turn acts on each column of the basis, so the scale stays.</summary>
    public static Transform3D Turned(Transform3D place, Quaternion turn)
    {
        var x = Vector3.Transform(new Vector3(place.Row0.X, place.Row1.X, place.Row2.X), turn);
        var y = Vector3.Transform(new Vector3(place.Row0.Y, place.Row1.Y, place.Row2.Y), turn);
        var z = Vector3.Transform(new Vector3(place.Row0.Z, place.Row1.Z, place.Row2.Z), turn);
        return place with { Row0 = new Vector3(x.X, y.X, z.X), Row1 = new Vector3(x.Y, y.Y, z.Y), Row2 = new Vector3(x.Z, y.Z, z.Z) };
    }

    /// <summary>
    /// How far along a line through the pivot a ray passes nearest it, in world units from the pivot. Null when
    /// the ray runs along the line, because then every point of it is as near.
    /// </summary>
    public static float? Along(Ray ray, Vector3 pivot, Vector3 axis)
    {
        var offset = ray.Origin - pivot;
        var cosine = Vector3.Dot(ray.Direction, axis);
        var square = 1f - cosine * cosine;
        return square < MinSine * MinSine
            ? null
            : (Vector3.Dot(axis, offset) - cosine * Vector3.Dot(ray.Direction, offset)) / square;
    }

    /// <summary>The world distance a pointer move slides along an axis. A move along the view gives 0.</summary>
    public static float Distance(Ray from, Ray to, Vector3 pivot, Vector3 axis) =>
        Along(to, pivot, axis) is { } end && Along(from, pivot, axis) is { } start ? end - start : 0f;

    /// <summary>
    /// The number a scale value is multiplied by for a pointer move: how much farther from the pivot, along the
    /// axis, the pointer is after it than before. A start nearer the pivot than a hundredth of the probe gives 1.
    /// </summary>
    public static float ScaleFactor(Ray from, Ray to, Vector3 pivot, Vector3 axis, float probe) =>
        Along(from, pivot, axis) is { } start && MathF.Abs(start) >= MinStart * probe && Along(to, pivot, axis) is { } end ? end / start : 1f;

    /// <summary>
    /// The scale of an entity with one axis' value times a factor. A value under a tenth grows by a step of a tenth,
    /// so an axis scaled to nothing can be stretched again.
    /// </summary>
    public static Vector3 Scaled(Vector3 scale, GizmoAxis axis, float factor)
    {
        var mask = Axis(axis);
        var value = Vector3.Dot(scale, mask);
        var grows = MathF.Abs(value) < MinScale && MathF.Abs(factor) > 1f;
        var changed = grows ? value + (factor - 1f) * (value < 0f ? -MinScale : MinScale) : value * factor;
        return scale + mask * (changed - value);
    }

    /// <summary>
    /// What a pointer move multiplies all three scale values by: how much farther from the pivot on screen the
    /// pointer is after it than before, with a handle's length added to both so a drag from the pivot has a ratio.
    /// </summary>
    public static float UniformFactor(Vector2 origin, FrameMove move) =>
        (Vector2.Distance(move.To, origin) + HandleLength) / (Vector2.Distance(move.From, origin) + HandleLength);

    /// <summary>The scale with all three values times a factor. Each value takes the same zero escape as <see cref="Scaled"/>.</summary>
    public static Vector3 ScaledAll(Vector3 scale, float factor) =>
        Scaled(Scaled(Scaled(scale, GizmoAxis.X, factor), GizmoAxis.Y, factor), GizmoAxis.Z, factor);

    /// <summary>
    /// A drag total held to a step: the nearest multiple of it, with halves away from zero. A step that is not
    /// above zero leaves the total alone, so no step means no snap.
    /// </summary>
    public static float Stepped(float total, float step) =>
        step > 0f ? MathF.Round(total / step, MidpointRounding.AwayFromZero) * step : total;

    /// <summary>Each part of a vector held to a step, as <see cref="Stepped(float, float)"/> does one number.</summary>
    public static Vector3 Stepped(Vector3 total, float step) => new(Stepped(total.X, step), Stepped(total.Y, step), Stepped(total.Z, step));

    /// <summary>
    /// The angle in radians a pointer move sweeps round the pivot on screen. Clockwise on screen is positive, which
    /// is a turn about the direction of the view. A pointer on the pivot has no angle, so the result is 0.
    /// </summary>
    public static float Spun(Vector2 origin, FrameMove move)
    {
        var before = move.From - origin;
        var after = move.To - origin;
        return before.LengthSquared() < MinSpin * MinSpin || after.LengthSquared() < MinSpin * MinSpin
            ? 0f
            : MathF.Atan2(before.X * after.Y - before.Y * after.X, Vector2.Dot(before, after));
    }

    /// <summary>
    /// The turn a pointer move gives a ball under the pointer: about the axis that takes the front of the ball the
    /// way the pointer went, by the move's length over <see cref="HandleLength"/> radians. The view looks along
    /// <paramref name="forward"/>, with the world's up above it.
    /// </summary>
    public static Quaternion Rolled(Vector3 forward, FrameMove move)
    {
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.Cross(right, forward);
        var axis = Vector3.Cross(right * move.Delta.X - up * move.Delta.Y, forward);
        return axis.LengthSquared() < MinColumn ? Quaternion.Identity : Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), move.Delta.Length() / HandleLength);
    }

    /// <summary>
    /// The angle in radians a pointer move turns an entity about an axis through the pivot: the angle the two
    /// rays sweep where they cross the ring's plane. Null when the plane is seen too near edge on for that.
    /// </summary>
    public static float? Swept(Ray from, Ray to, Vector3 pivot, Vector3 axis)
    {
        if (OnPlane(from, pivot, axis) is not { } start || OnPlane(to, pivot, axis) is not { } end)
        {
            return null;
        }

        var before = start - pivot;
        var after = end - pivot;
        return MathF.Atan2(Vector3.Dot(axis, Vector3.Cross(before, after)), Vector3.Dot(before, after));
    }

    static Vector3? OnPlane(Ray ray, Vector3 pivot, Vector3 normal)
    {
        var facing = Vector3.Dot(ray.Direction, normal);
        return MathF.Abs(facing) < MinFace ? null : ray.Origin + ray.Direction * (Vector3.Dot(pivot - ray.Origin, normal) / facing);
    }

    /// <summary>
    /// The world radius for the three rings round a center so that their points are, on average,
    /// <see cref="HandleLength"/> pixels from the center on screen. A probe radius starts it, and perspective
    /// is settled in a few passes.
    /// </summary>
    public static float RingRadius(Func<Vector3, Vector2> project, Vector3 center, Vector3[] axes, float probe)
    {
        var origin = project(center);
        var radius = probe;
        for (var pass = 0; pass < RadiusPasses; pass++)
        {
            var reach = axes
                .SelectMany(axis => Ring(center, axis, radius).Where((_, i) => i % RadiusStride == 0))
                .Average(point => Vector2.Distance(project(point), origin));
            radius *= HandleLength / reach;
        }
        return radius;
    }
}
