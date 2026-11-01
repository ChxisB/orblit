using Orblit.VectorMath;

namespace Orblit.Mesh;

/// <summary>
/// A shape drawn as an outline and then pulled up.
/// </summary>
/// <remarks>
/// The other way of making geometry from the primitives: instead of choosing a box and squashing it,
/// somebody draws the plan of a room, a path or a pond and gives it a height. Almost everything in a
/// level that is not a box is one of these, and doing it with box primitives means twenty of them and
/// a seam at every join.
/// <para>
/// The points are kept, not just the mesh they made. That is the whole difference between this and
/// drawing a shape once: a wall can be moved a week later by dragging the corner it belongs to, and
/// the room closes up again.
/// </para>
/// </remarks>
public sealed class PolyShape(List<Vector3>? points = null, double height = 2.0, bool flipped = false)
{
    /// <summary>
    /// The outline, in order, in the object's own space.
    /// </summary>
    /// <remarks>
    /// Any plane, not just the ground: they are projected onto the plane they best fit, so a shape
    /// drawn on a sloping roof stays on it.
    /// </remarks>
    public List<Vector3> Points { get; } = points ?? [];

    /// <summary>
    /// How far it is pulled up from the outline, along the plane's normal. Negative pulls it the
    /// other way. Nought leaves a flat surface, which is a legitimate thing to want and is how a
    /// floor is made.
    /// </summary>
    public double Height { get; set; } = height;

    /// <summary>
    /// Whether the outline was drawn the other way round.
    /// </summary>
    /// <remarks>
    /// A plan drawn clockwise and one drawn anticlockwise are the same room, and somebody drawing
    /// one is not thinking about winding. This is worked out rather than asked for. The flag is here
    /// so it can be overridden when the guess is wrong, which happens on a shape drawn nearly
    /// edge-on.
    /// </remarks>
    public bool Flipped { get; set; } = flipped;

    public bool IsDrawable => Points.Count >= 3;

    /// <summary>
    /// The plane the points best lie on: a middle, and a direction.
    /// </summary>
    /// <remarks>
    /// Newell's method over the outline, the same as a face's normal and for the same reason: points
    /// drawn by hand are never quite in a plane, and the cross product of any two of them is
    /// whichever answer those two happen to give.
    /// </remarks>
    public (Vector3 Centre, Vector3 Normal) Plane
    {
        get
        {
            var centre = Vector3.Zero();
            foreach (var at in Points)
            {
                centre.Add(at);
            }

            if (Points.Count > 0)
            {
                centre.Scale(1.0 / Points.Count);
            }

            var normal = Vector3.Zero();
            for (var i = 0; i < Points.Count; i++)
            {
                var here = Points[i];
                var next = Points[(i + 1) % Points.Count];
                normal.X += (here.Y - next.Y) * (here.Z + next.Z);
                normal.Y += (here.Z - next.Z) * (here.X + next.X);
                normal.Z += (here.X - next.X) * (here.Y + next.Y);
            }

            return (centre, normal.Length2 < 1e-20 ? new Vector3(0, 1, 0) : normal.Normalized());
        }
    }

    /// <summary>
    /// The shape as geometry: a top, a bottom and a wall between them.
    /// </summary>
    /// <remarks>
    /// An empty mesh below three points, because two points are a line and a line is not a thing
    /// that can be drawn, and something degenerate instead would put a shape in the scene that
    /// renders as nothing and cannot be selected.
    /// <para>
    /// Built here rather than by extruding a cap, because an extrude leaves the bottom open. A room
    /// somebody drew is a solid.
    /// </para>
    /// </remarks>
    public Mesh Build()
    {
        var mesh = new Mesh();
        if (!IsDrawable)
        {
            return mesh;
        }

        var normal = Plane.Normal;

        // Which way is up for this shape. A plan drawn clockwise and one drawn anticlockwise are the
        // same room, and a negative height is the same shape pulled the other way, so both are
        // settled here, once, by turning the outline over rather than by two more cases further
        // down.
        var reverse = Flipped != (Height < 0);
        var outline = reverse ? Enumerable.Reverse(Points).ToList() : [.. Points];
        var up = reverse ? -normal : normal;
        var far = Math.Abs(Height);

        foreach (var at in outline)
        {
            mesh.AddVertex(at);
        }

        var count = outline.Count;

        if (far < 1e-9)
        {
            // Flat, which is how a floor or a lake is made and is a legitimate thing to ask for.
            mesh.AddFace([.. Enumerable.Range(0, count)]);
            return mesh;
        }

        foreach (var at in outline)
        {
            mesh.AddVertex(at + (up * far));
        }

        // The face at the outline is the underside, so it is wound the other way.
        mesh.AddFace([.. Enumerable.Range(0, count).Reverse()]);
        mesh.AddFace([.. Enumerable.Range(count, count)]);

        // And the wall: one quad an edge, wound to face outwards.
        for (var i = 0; i < count; i++)
        {
            var next = (i + 1) % count;
            mesh.AddFace([i, next, count + next, count + i]);
        }

        return mesh;
    }

    /// <summary>
    /// Where a point on the drawing plane is, given a ray.
    /// </summary>
    /// <remarks>
    /// Used while drawing: the pointer is a ray into the world and a point has to land on the plane
    /// being drawn on. Null when the ray runs parallel to it, which is somebody looking along the
    /// surface they are drawing on and is a question with no answer.
    /// </remarks>
    // F2: four positional parameters, and a call reads as origin, direction, plane point, plane normal.
    public static Vector3? OnPlane(Vector3 origin, Vector3 direction, Vector3 planePoint, Vector3 planeNormal)
    {
        var slope = planeNormal.Dot(direction);
        if (Math.Abs(slope) < 1e-9)
        {
            return null;
        }

        var away = planeNormal.Dot(planePoint - origin) / slope;

        // Behind the camera is not on the plane in front of it.
        return away < 0 ? null : origin + (direction * away);
    }

    public PolyShape Copy() => new([.. Points.Select(at => at.Clone())], Height, Flipped);

    public OrderedDictionary<string, object?> ToJson()
    {
        var json = new OrderedDictionary<string, object?>
        {
            ["points"] = Points.SelectMany(at => new object?[] { at.X, at.Y, at.Z }).ToList(),
            ["height"] = Height,
        };
        JsonValues.Put(json, Flipped, "flipped", true);
        return json;
    }

    public static PolyShape? FromJson(object? value)
    {
        if (JsonValues.Map(value) is not { } map
            || !map.TryGetValue("points", out var rawPoints)
            || JsonValues.List(rawPoints) is not { } raw)
        {
            return null;
        }

        var points = new List<Vector3>();
        for (var i = 0; i + 2 < raw.Count; i += 3)
        {
            if (JsonValues.Num(raw[i]) is { } x && JsonValues.Num(raw[i + 1]) is { } y && JsonValues.Num(raw[i + 2]) is { } z)
            {
                points.Add(new Vector3(x, y, z));
            }
        }

        map.TryGetValue("height", out var rawHeight);
        map.TryGetValue("flipped", out var rawFlipped);
        return new PolyShape(points, JsonValues.Num(rawHeight) ?? 2.0, JsonValues.IsTrue(rawFlipped));
    }
}

/// <summary>The checks made while somebody draws an outline.</summary>
public static class Drawing
{
    /// <summary>
    /// How far apart two points have to be for the second to count as a new one.
    /// </summary>
    /// <remarks>
    /// Somebody clicking twice in the same place means one point. Without this a double-click
    /// leaves a zero-length edge, which is a wall of no width that nothing can be done with
    /// afterwards.
    /// </remarks>
    internal const double SamePoint = 1e-4;

    /// <summary>
    /// Whether adding <paramref name="at"/> to <paramref name="points"/> would close the outline
    /// rather than extend it: a click back on the first point, which is how somebody says they are
    /// finished.
    /// </summary>
    public static bool ClosesOutline(List<Vector3> points, Vector3 at, double reach = 0.15) =>
        points.Count >= 3 && (points[0] - at).Length < reach;

    /// <summary>Whether a point is far enough from the last one to be worth adding.</summary>
    public static bool IsNewPoint(List<Vector3> points, Vector3 at) =>
        points.Count == 0 || (points[^1] - at).Length > SamePoint;

    /// <summary>
    /// The area an outline encloses, seen from its own plane.
    /// </summary>
    /// <remarks>
    /// Signed, so its sign says which way round the outline was drawn. Used to decide whether a
    /// shape needs turning over, and to refuse one that encloses nothing.
    /// </remarks>
    public static double SignedAreaOf(List<Vector3> points, Vector3 normal)
    {
        if (points.Count < 3)
        {
            return 0;
        }

        var total = Vector3.Zero();
        for (var i = 0; i < points.Count; i++)
        {
            total.Add(points[i].Cross(points[(i + 1) % points.Count]));
        }

        return total.Dot(normal) / 2;
    }

    /// <summary>
    /// Whether an outline crosses itself.
    /// </summary>
    /// <remarks>
    /// A figure of eight is not a room, and the triangulation of one is a mess of overlapping faces
    /// rather than an error, so it is worth saying no before the shape exists rather than leaving
    /// somebody to work out why it looks wrong.
    /// </remarks>
    public static bool OutlineCrosses(List<Vector3> points, Vector3 normal)
    {
        if (points.Count < 4)
        {
            return false;
        }

        // Flattened onto the plane, because "crosses" is a question about the drawing and not about
        // the world.
        var away = Math.Abs(normal.X) < 0.9 ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0);
        var u = normal.Cross(away).Normalized();
        var v = normal.Cross(u).Normalized();
        var flat = points.Select(at => new Vector2(at.Dot(u), at.Dot(v))).ToList();

        for (var i = 0; i < flat.Count; i++)
        {
            for (var j = i + 1; j < flat.Count; j++)
            {
                if (AreNeighbours(i, j, flat.Count))
                {
                    continue;
                }

                if (SegmentsCross(flat[i], flat[(i + 1) % flat.Count], flat[j], flat[(j + 1) % flat.Count]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>How far a point is from a line between two others, in a plane.</summary>
    public static double DistanceToSegment(Vector2 at, Vector2 a, Vector2 b)
    {
        var along = b - a;
        var length2 = along.Length2;
        if (length2 < 1e-12)
        {
            return (at - a).Length;
        }

        var t = Doubles.Clamp((at - a).Dot(along) / length2, 0.0, 1.0);
        return (at - (a + (along * t))).Length;
    }

    /// <summary>The smallest turn between two directions, for the tools that care.</summary>
    public static double TurnBetween(Vector2 a, Vector2 b) => Math.Atan2((a.X * b.Y) - (a.Y * b.X), a.Dot(b));

    /// <summary>Neighbouring edges share a corner and always "meet" there.</summary>
    private static bool AreNeighbours(int i, int j, int count) =>
        j == i || (j + 1) % count == i || j == (i + 1) % count;

    private static bool SegmentsCross(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
    {
        var d1 = Side(a1, a2, b1);
        var d2 = Side(a1, a2, b2);
        var d3 = Side(b1, b2, a1);
        var d4 = Side(b1, b2, a2);

        // Strictly opposite sides both ways. Touching at a point is not crossing: an outline that
        // comes back to graze itself is odd but drawable, and refusing it would refuse a lot of
        // legitimate shapes to catch one.
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    private static double Side(Vector2 a, Vector2 b, Vector2 at) => ((b.X - a.X) * (at.Y - a.Y)) - ((b.Y - a.Y) * (at.X - a.X));
}
