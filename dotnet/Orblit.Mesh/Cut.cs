using Orblit.VectorMath;

namespace Orblit.Mesh;

/// <summary>
/// Where a drawn point falls on a face.
/// </summary>
/// <remarks>
/// Three answers rather than two, because a point on a corner and a point on an edge are cut
/// differently: one uses a vertex that already exists and the other has to make one, and treating a
/// corner as an edge point at nought leaves a duplicate vertex on top of the original.
/// </remarks>
public abstract record OnFace
{
    private protected OnFace()
    {
    }
}

/// <summary>On an existing corner, by its position in the face.</summary>
public sealed record AtCorner(int Corner) : OnFace;

/// <summary>Along the edge that leaves corner <see cref="Corner"/>, a fraction <see cref="Along"/> of the way.</summary>
public sealed record AlongEdge(int Corner, double Along) : OnFace;

/// <summary>Somewhere in the middle.</summary>
public sealed record InsideFace : OnFace;

/// <summary>
/// Cutting a face into more of them.
/// </summary>
/// <remarks>
/// The move for everything a primitive cannot start: a doorway in a wall, a step across a floor, an
/// edge to grab where there was not one. Without it a shape can only be made out of whole faces, and
/// anything else means going back to a modelling package.
/// </remarks>
public static class MeshCut
{
    /// <summary>A vertex a cut has put into an edge: after which corner, how far along, and its index.</summary>
    private readonly record struct Insert(int After, double Along, int Vertex);

    extension(Mesh mesh)
    {
        /// <summary>
        /// Where <paramref name="at"/> falls on <paramref name="face"/>, given how near counts as on.
        /// </summary>
        /// <remarks>
        /// <paramref name="reach"/> is in metres, and generous on purpose: somebody drawing on a wall
        /// means the edge they aimed at, and a cut that lands a millimetre inside leaves a sliver of
        /// a face nobody can select.
        /// </remarks>
        public OnFace WhereOn(Face face, Vector3 at, double reach = 0.02)
        {
            var points = mesh.PointsOf(face);
            if (points.Count < 3)
            {
                return new InsideFace();
            }

            var nearestCorner = NearestCorner(points, at, reach);
            if (nearestCorner >= 0)
            {
                return new AtCorner(nearestCorner);
            }

            var (edge, along) = NearestEdge(points, at, reach);
            return edge >= 0 ? new AlongEdge(edge, along) : new InsideFace();
        }

        /// <summary>
        /// Cuts <paramref name="face"/> along <paramref name="path"/>, replacing it with the faces
        /// that result.
        /// </summary>
        /// <remarks>
        /// The path is a list of points in the object's own space. Two shapes of cut are understood,
        /// and they are the two anybody draws:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// <b>Edge to edge.</b> The first and last points are on the face's boundary and the rest
        /// are wherever. The face becomes two, one either side of the line.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// <b>A closed loop inside it.</b> The face becomes the loop, plus what is left around it,
        /// joined by a slit from one to the other, because a face here is a ring of corners and has
        /// nowhere to put a hole.
        /// </description>
        /// </item>
        /// </list>
        /// Returns the new faces, or an empty list when the path does not cut anything, which is a
        /// refusal to be reported and not a failure: a path that starts and ends in the middle of a
        /// face does not divide it, and doing something anyway would leave geometry nobody asked for.
        /// </remarks>
        public List<Face> CutFace(Face face, List<Vector3> path, double reach = 0.02)
        {
            var at = mesh.Faces.IndexOf(face);
            if (at < 0 || path.Count < 2 || face.Vertices.Count < 3)
            {
                return [];
            }

            var closed = (path[0] - path[^1]).Length < reach && path.Count >= 4;
            return closed ? CutLoop(mesh, at, face, path) : CutAcross(mesh, at, face, path, reach);
        }
    }

    /// <summary>
    /// The signed area a loop of points encloses, seen along <paramref name="normal"/>.
    /// </summary>
    /// <remarks>
    /// Its sign says which way round the loop goes, which is what decides whether a face made from
    /// it points the same way as the one it was drawn on.
    /// </remarks>
    public static double SignedLoopArea(List<Vector3> loop, Vector3 normal)
    {
        if (loop.Count < 3)
        {
            return 0;
        }

        var total = Vector3.Zero();
        for (var i = 0; i < loop.Count; i++)
        {
            total.Add(loop[i].Cross(loop[(i + 1) % loop.Count]));
        }

        return total.Dot(normal) / 2;
    }

    /// <summary>
    /// Whether a point is inside a face, seen from the face's own plane.
    /// </summary>
    /// <remarks>
    /// The crossing rule, which is right for the concave faces an editor makes as well as the convex
    /// ones: a face cut twice is rarely convex afterwards.
    /// </remarks>
    public static bool PointInsideOutline(List<Vector2> outline, Vector2 at)
    {
        var inside = false;
        for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
        {
            var a = outline[i];
            var b = outline[j];
            if ((a.Y > at.Y) == (b.Y > at.Y))
            {
                continue;
            }

            var crossing = ((b.X - a.X) * (at.Y - a.Y) / (b.Y - a.Y)) + a.X;
            if (at.X < crossing)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>A face's corners on its own plane, and the axes that got them there.</summary>
    public static (List<Vector2> Flat, Vector3 U, Vector3 V) FlattenFace(List<Vector3> points, Vector3 normal)
    {
        var (u, v) = FaceUv.AxesFor(normal);
        return ([.. points.Select(at => new Vector2(at.Dot(u), at.Dot(v)))], u, v);
    }

    /// <summary>The first corner nearer than <paramref name="reach"/>, taking the nearest of them, or -1.</summary>
    private static int NearestCorner(List<Vector3> points, Vector3 at, double reach)
    {
        var nearestCorner = -1;
        var nearest = reach;
        for (var i = 0; i < points.Count; i++)
        {
            var away = (points[i] - at).Length;
            if (away < nearest)
            {
                nearest = away;
                nearestCorner = i;
            }
        }

        return nearestCorner;
    }

    /// <summary>The edge nearest <paramref name="at"/> within <paramref name="reach"/> and how far along it, or edge -1.</summary>
    private static (int Edge, double Along) NearestEdge(List<Vector3> points, Vector3 at, double reach)
    {
        var bestEdge = -1;
        var bestAlong = 0.0;
        var bestAway = reach;
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var along = points[(i + 1) % points.Count] - a;
            var length2 = along.Length2;
            if (length2 < 1e-12)
            {
                continue;
            }

            var t = Doubles.Clamp((at - a).Dot(along) / length2, 0.0, 1.0);
            var away = (at - (a + (along * t))).Length;
            if (away >= bestAway)
            {
                continue;
            }

            bestAway = away;
            bestEdge = i;
            bestAlong = t;
        }

        return (bestEdge, bestAlong);
    }

    /// <summary>A path from one edge of a face to another: the face becomes two.</summary>
    private static List<Face> CutAcross(Mesh mesh, int at, Face face, List<Vector3> path, double reach)
    {
        var entry = mesh.WhereOn(face, path[0], reach);
        var exit = mesh.WhereOn(face, path[^1], reach);
        if (entry is InsideFace || exit is InsideFace)
        {
            return [];
        }

        // The outline with the two ends put into it, so both are corners of it.
        var outline = new List<int>(face.Vertices);
        var inserts = new List<Insert>();

        var first = PlaceEnd(mesh, face, entry, path[0], inserts);
        var last = PlaceEnd(mesh, face, exit, path[^1], inserts);
        if (first < 0 || last < 0 || first == last)
        {
            return [];
        }

        PutIn(outline, inserts);
        var from = outline.IndexOf(first);
        var to = outline.IndexOf(last);
        if (from < 0 || to < 0)
        {
            return [];
        }

        return Halves(mesh, at, face, outline, (from, to), path);
    }

    /// <summary>The face as two, one on each side of the path, with the path's inner points shared by both.</summary>
    private static List<Face> Halves(Mesh mesh, int at, Face face, List<int> outline, (int From, int To) ends, List<Vector3> path)
    {
        // Everything strictly between the two ends becomes new corners, shared by both halves: one
        // vertex each, not two, or the cut is a crack.
        var middle = new List<int>();
        for (var i = 1; i < path.Count - 1; i++)
        {
            middle.Add(mesh.AddVertex(path[i]));
        }

        List<int> one = [.. Walk(outline, ends.From, ends.To), .. Enumerable.Reverse(middle)];
        List<int> two = [.. Walk(outline, ends.To, ends.From), .. middle];
        return one.Count < 3 || two.Count < 3 ? [] : Replace(mesh, at, face, [one, two]);
    }

    /// <summary>Puts the new vertices into the outline, later edges first so inserting into one does not move the next.</summary>
    private static void PutIn(List<int> outline, List<Insert> inserts)
    {
        var along = Comparer<double>.Create(Doubles.Compare);
        foreach (var insert in inserts.OrderByDescending(item => item.After).ThenByDescending(item => item.Along, along))
        {
            outline.Insert(insert.After + 1, insert.Vertex);
        }
    }

    /// <summary>
    /// The index a cut's end point comes to: the face's own corner, or a new vertex put into an
    /// edge. -1 for a point in the middle.
    /// </summary>
    // F5: making the vertex and handing back its index is one step.
    private static int PlaceEnd(Mesh mesh, Face face, OnFace where, Vector3 point, List<Insert> inserts)
    {
        switch (where)
        {
            case AtCorner(var corner):
                return face.Vertices[corner];
            case AlongEdge(var corner, var along):
                var made = mesh.AddVertex(point);
                inserts.Add(new Insert(corner, along, made));
                return made;
            default:
                return -1;
        }
    }

    /// <summary>The outline's points from <paramref name="start"/> round to <paramref name="end"/>, both included.</summary>
    private static List<int> Walk(List<int> outline, int start, int end)
    {
        var walked = new List<int>();
        var i = start;
        while (true)
        {
            walked.Add(outline[i]);
            if (i == end)
            {
                break;
            }

            i = (i + 1) % outline.Count;
        }

        return walked;
    }

    /// <summary>
    /// A closed loop drawn inside a face: the loop becomes a face of its own, and what is left
    /// surrounds it.
    /// </summary>
    /// <remarks>
    /// Joined by a slit rather than left as a hole, because a face here is a ring of corners with
    /// nowhere to put one. The slit runs between the two nearest corners, which puts it where it is
    /// least visible, and it is invisible anyway once the inner face is extruded or painted, which
    /// is what somebody drew it for.
    /// </remarks>
    private static List<Face> CutLoop(Mesh mesh, int at, Face face, List<Vector3> path)
    {
        // The closing point is the opening one said twice.
        var loop = path.GetRange(0, path.Count - 1);
        if (loop.Count < 3)
        {
            return [];
        }

        // Wound the same way as the face, so the inner face faces the same way.
        var wound = SignedLoopArea(loop, mesh.NormalOf(face)) < 0 ? Enumerable.Reverse(loop).ToList() : loop;

        var inner = wound.Select(mesh.AddVertex).ToList();

        // A second set for the ring, so the two faces do not share corners along the slit. They
        // meet there and are not joined there.
        var ringInner = wound.Select(mesh.AddVertex).ToList();

        var (bestOuter, bestInner) = NearestPair(mesh, face.Vertices, wound);

        // Round the outside, in through the slit, round the inside the other way, and back out. One
        // face, and it has the loop cut out of it.
        var outline = face.Vertices;
        List<int> ring =
        [
            .. Enumerable.Range(0, outline.Count + 1).Select(i => outline[(bestOuter + i) % outline.Count]),
            .. Enumerable.Range(0, wound.Count + 1).Select(j => ringInner[(bestInner - j + (wound.Count * 2)) % wound.Count]),
        ];

        return Replace(mesh, at, face, [ring, inner]);
    }

    /// <summary>The outline position and loop position that sit nearest each other.</summary>
    private static (int Outer, int Inner) NearestPair(Mesh mesh, List<int> outline, List<Vector3> wound)
    {
        var bestOuter = 0;
        var bestInner = 0;
        var bestAway = double.PositiveInfinity;
        for (var i = 0; i < outline.Count; i++)
        {
            for (var j = 0; j < wound.Count; j++)
            {
                var away = (mesh.Positions[outline[i]] - wound[j]).Length;
                if (away >= bestAway)
                {
                    continue;
                }

                bestAway = away;
                bestOuter = i;
                bestInner = j;
            }
        }

        return (bestOuter, bestInner);
    }

    /// <summary>
    /// Puts new faces where an old one was, keeping what it wore.
    /// </summary>
    /// <remarks>
    /// In its place rather than appended, so the order somebody made things in survives a cut, and
    /// material and smoothing carry over, because a cut divides a surface rather than making a
    /// different one. The texture rule carries too, but drawn coordinates do not: they belonged to
    /// corners that are no longer all there.
    /// </remarks>
    private static List<Face> Replace(Mesh mesh, int at, Face face, List<List<int>> outlines)
    {
        var uv = face.Uv.IsManual ? face.Uv.CopyWith(clearManual: true) : face.Uv;
        var made = outlines.Select(corners => new Face(corners, face.Material, face.Smooth, uv)).ToList();

        mesh.Faces.RemoveAt(at);
        mesh.Faces.InsertRange(at, made);
        return made;
    }
}
