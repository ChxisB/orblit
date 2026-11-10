using System.Numerics;
using Orblit.Editor.Bridge;

namespace Orblit.Editor.App;

/// <summary>What the gizmo on the selection offers. The toolbar's mode buttons choose it.</summary>
internal enum GizmoMode
{
    Transform,
    Move,
    Rotate,
    Scale,
    Select,
}

/// <summary>What a drag on a part of the gizmo changes.</summary>
internal enum GizmoPart
{
    /// <summary>The position, along an axis.</summary>
    Move,

    /// <summary>One scale value.</summary>
    Stretch,

    /// <summary>All three scale values.</summary>
    StretchAll,

    /// <summary>The rotation, about an axis through the pivot.</summary>
    Turn,

    /// <summary>The rotation, about the direction of the view.</summary>
    Spin,

    /// <summary>The rotation, about the axis a drag across the view makes.</summary>
    Trackball,
}

/// <summary>A part of the gizmo under the pointer. The axis means nothing for the parts that have none.</summary>
internal readonly record struct GizmoHit(GizmoPart Part, GizmoAxis Axis);

/// <summary>One pointer move of a drag that began on a gizmo part, and whether the snap key was down for it.</summary>
internal readonly record struct GizmoDrag(GizmoHit Hit, FrameMove Move, bool Inverts);

/// <summary>What the viewport shows of the gizmo: the mode, whether the trackball is on, and what was drawn.</summary>
internal readonly record struct GizmoShown(GizmoMode Mode, bool Trackball, GizmoHandles? Handles, GizmoRings? Rings);

/// <summary>
/// The hit zones of the gizmo, in control pixels. Each mode takes presses in its own places, and this is the one
/// place that says where.
/// </summary>
internal static class GizmoZones
{
    // A line takes a press this near, in control pixels. A press this near the pivot takes none, so a press
    // on the pivot still picks. Scale mode puts its center handle there.
    const float GrabRadius = 8;
    const float PivotRadius = 10;
    const float TipGrab = 10;

    /// <summary>The part a press at <paramref name="at"/> takes in a mode, or null for none. The nearest line wins among lines.</summary>
    public static GizmoHit? HitAt(GizmoShown shown, Vector2 at) => shown.Mode switch
    {
        GizmoMode.Transform => TipAt(shown.Handles, at) ?? ShaftAt(shown.Handles, at, GizmoPart.Move) ?? RingAt(shown.Rings, at),
        GizmoMode.Move => ShaftAt(shown.Handles, at, GizmoPart.Move),
        GizmoMode.Scale => CentreAt(shown.Handles, at) ?? ShaftAt(shown.Handles, at, GizmoPart.Stretch),
        GizmoMode.Rotate => RingAt(shown.Rings, at) ?? OuterAt(shown.Rings, at) ?? InsideAt(shown.Rings, shown.Trackball, at),
        _ => null,
    };

    /// <summary>Whether a mode puts handles on the selection.</summary>
    public static bool HasHandles(GizmoMode mode) => mode is GizmoMode.Transform or GizmoMode.Move or GizmoMode.Scale;

    /// <summary>Whether a mode puts rotate rings on the selection.</summary>
    public static bool HasRings(GizmoMode mode) => mode is GizmoMode.Transform or GizmoMode.Rotate;

    /// <summary>Whether a mode has the part, so a drag that began in another mode is stale.</summary>
    public static bool Offers(GizmoMode mode, GizmoPart part) => mode switch
    {
        GizmoMode.Transform => part is GizmoPart.Move or GizmoPart.Stretch or GizmoPart.Turn,
        GizmoMode.Move => part == GizmoPart.Move,
        GizmoMode.Scale => part is GizmoPart.Stretch or GizmoPart.StretchAll,
        GizmoMode.Rotate => part is GizmoPart.Turn or GizmoPart.Spin or GizmoPart.Trackball,
        _ => false,
    };

    static GizmoHit? CentreAt(GizmoHandles? handles, Vector2 at) =>
        handles is { } shown && Vector2.Distance(at, shown.Origin) < PivotRadius ? new GizmoHit(GizmoPart.StretchAll, GizmoAxis.X) : null;

    static GizmoHit? TipAt(GizmoHandles? handles, Vector2 at)
    {
        if (handles is not { } shown || Vector2.Distance(at, shown.Origin) < PivotRadius)
        {
            return null;
        }

        GizmoHit? grabbed = null;
        var nearest = TipGrab;
        foreach (var axis in Enum.GetValues<GizmoAxis>())
        {
            var distance = Vector2.Distance(at, shown.Tip(axis));
            if (distance < nearest)
            {
                nearest = distance;
                grabbed = new GizmoHit(GizmoPart.Stretch, axis);
            }
        }
        return grabbed;
    }

    static GizmoHit? ShaftAt(GizmoHandles? handles, Vector2 at, GizmoPart part)
    {
        if (handles is not { } shown || Vector2.Distance(at, shown.Origin) < PivotRadius)
        {
            return null;
        }

        GizmoHit? grabbed = null;
        var nearest = GrabRadius;
        foreach (var axis in Enum.GetValues<GizmoAxis>())
        {
            // A handle that shows as a dot has no line. Its distance is NaN, which is never near.
            var distance = DistanceToSegment(at, shown.Origin, shown.Tip(axis));
            if (distance < nearest)
            {
                nearest = distance;
                grabbed = new GizmoHit(part, axis);
            }
        }
        return grabbed;
    }

    static GizmoHit? RingAt(GizmoRings? rings, Vector2 at)
    {
        if (rings is not { } shown || Vector2.Distance(at, shown.Origin) < PivotRadius)
        {
            return null;
        }

        GizmoHit? grabbed = null;
        var nearest = GrabRadius;
        foreach (var axis in Enum.GetValues<GizmoAxis>())
        {
            var distance = DistanceToRing(at, shown.Of(axis));
            if (distance < nearest)
            {
                nearest = distance;
                grabbed = new GizmoHit(GizmoPart.Turn, axis);
            }
        }
        return grabbed;
    }

    // A default GizmoRings has no outer ring at all, so an empty or missing one offers nothing.
    static GizmoHit? OuterAt(GizmoRings? rings, Vector2 at) =>
        rings is { Outer.Length: > 0 } shown && Vector2.Distance(at, shown.Origin) >= PivotRadius && DistanceToRing(at, shown.Outer) < GrabRadius
            ? new GizmoHit(GizmoPart.Spin, GizmoAxis.X)
            : null;

    // The ball is the disc inside the outer ring, as far out as the ring is on average.
    static GizmoHit? InsideAt(GizmoRings? rings, bool trackball, Vector2 at)
    {
        if (!trackball || rings is not { Outer.Length: > 0 } shown)
        {
            return null;
        }

        var distance = Vector2.Distance(at, shown.Origin);
        return distance >= PivotRadius && distance < shown.Outer.Average(point => Vector2.Distance(point, shown.Origin))
            ? new GizmoHit(GizmoPart.Trackball, GizmoAxis.X)
            : null;
    }

    static float DistanceToRing(Vector2 point, Vector2[] ring) =>
        Enumerable.Range(0, ring.Length).Min(i => DistanceToSegment(point, ring[i], ring[(i + 1) % ring.Length]));

    static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var along = end - start;
        var share = Math.Clamp(Vector2.Dot(point - start, along) / along.LengthSquared(), 0f, 1f);
        return Vector2.Distance(point, start + along * share);
    }
}
