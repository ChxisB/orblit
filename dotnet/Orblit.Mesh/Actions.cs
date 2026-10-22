using Orblit.VectorMath;

namespace Orblit.Mesh;

/// <summary>
/// The rest of what somebody does to geometry.
/// </summary>
/// <remarks>
/// Separated from <see cref="MeshEdits"/> only by size: those are the operations a shape is built
/// out of, and these are the ones somebody reaches for once it is built. Every one of them takes a
/// selection and returns what should be selected afterwards, because in a modelling tool the answer
/// to "what now" is nearly always "the thing that just appeared".
/// </remarks>
// F5: an action changes the mesh and returns what changed, so the caller can select it.
public static class MeshActions
{
    /// <summary>
    /// An edge, named by the two points it joins. Ordered smallest first, so an edge shared by two
    /// faces is one edge rather than two that happen to be the same.
    /// </summary>
    public static MeshEdge EdgeOf(int a, int b) => a < b ? (a, b) : (b, a);

    /// <summary>The selection without the faces on its edge.</summary>
    public static OrderedSet<Face> Shrink(IEnumerable<Face> which)
    {
        var chosen = new OrderedSet<Face>(which);
        if (chosen.Count < 2)
        {
            return [];
        }

        var outside = MeshEdits.EdgeCounts(chosen);

        // A face every one of whose edges is shared with another selected face is one in the
        // middle.
        return new OrderedSet<Face>(chosen.Where(face => MeshEdits.EdgesOf([face]).All(edge => outside.GetValueOrDefault(edge) > 1)));
    }

    extension(Mesh mesh)
    {
        // ---- looking around ----

        /// <summary>Which faces use an edge.</summary>
        public List<Face> FacesOn(MeshEdge edge) => [.. mesh.Faces.Where(face => HasEdge(face, edge))];

        /// <summary>Every edge in the mesh.</summary>
        public OrderedSet<MeshEdge> AllEdges => MeshEdits.EdgesOf(mesh.Faces);

        /// <summary>The edges nothing shares: the outline of a hole, or of a flat sheet.</summary>
        public OrderedSet<MeshEdge> OpenEdges =>
            new(MeshEdits.EdgeCounts(mesh.Faces).Where(entry => entry.Value < 2).Select(entry => entry.Key));

        /// <summary>Which faces touch a point.</summary>
        public List<Face> FacesAt(int vertex) => [.. mesh.Faces.Where(face => face.Vertices.Contains(vertex))];

        // ---- growing and shrinking a selection ----

        /// <summary>
        /// The faces next to the ones given.
        /// </summary>
        /// <remarks>
        /// <paramref name="withinAngle"/> in degrees keeps the growth on a flat area: growing across
        /// the corner of a box is rarely what somebody meant, and stopping at the fold is what makes
        /// this usable on a whole wall.
        /// </remarks>
        public OrderedSet<Face> Grow(IEnumerable<Face> which, double? withinAngle = null)
        {
            var chosen = new OrderedSet<Face>(which);
            if (chosen.Count == 0)
            {
                return chosen;
            }

            var edges = MeshEdits.EdgesOf(chosen);
            var grown = new OrderedSet<Face>(chosen);

            foreach (var face in mesh.Faces)
            {
                if (grown.Contains(face) || !MeshEdits.EdgesOf([face]).Any(edges.Contains))
                {
                    continue;
                }

                if (withinAngle is null || IsWithin(mesh, face, chosen, withinAngle.Value))
                {
                    grown.Add(face);
                }
            }

            return grown;
        }

        // ---- loops and rings ----

        /// <summary>
        /// The loop of edges running through this one.
        /// </summary>
        /// <remarks>
        /// A loop crosses a quad to the edge opposite and carries on. It stops at a triangle, at a
        /// hole and where it meets itself, which is what makes it useful: an edge loop is the line
        /// somebody wants to cut along, and one that wandered off through a triangle fan would not
        /// be.
        /// </remarks>
        public OrderedSet<MeshEdge> EdgeLoop(MeshEdge from)
        {
            var loop = new OrderedSet<MeshEdge> { from };

            foreach (var direction in new[] { 0, 1 })
            {
                var current = from;
                var pivot = direction == 0 ? from.Item1 : from.Item2;

                while (true)
                {
                    var next = AcrossQuad(mesh, current, pivot);
                    if (next is null || loop.Contains(next.Value))
                    {
                        break;
                    }

                    loop.Add(next.Value);
                    pivot = Onward(next.Value, pivot);
                    current = next.Value;
                }
            }

            return loop;
        }

        /// <summary>
        /// The ring of edges parallel to this one.
        /// </summary>
        /// <remarks>
        /// A ring crosses a quad to the edge <em>opposite</em> rather than continuing through a
        /// shared point. It is what somebody selects to put a cut all the way round a cylinder.
        /// </remarks>
        public OrderedSet<MeshEdge> EdgeRing(MeshEdge from)
        {
            var ring = new OrderedSet<MeshEdge> { from };
            var queue = new List<MeshEdge> { from };

            while (queue.Count > 0)
            {
                var edge = queue[^1];
                queue.RemoveAt(queue.Count - 1);
                foreach (var face in mesh.FacesOn(edge).Where(face => face.IsQuad))
                {
                    var opposite = OppositeEdge(face, edge);
                    if (opposite is null || ring.Contains(opposite.Value))
                    {
                        continue;
                    }

                    ring.Add(opposite.Value);
                    queue.Add(opposite.Value);
                }
            }

            return ring;
        }

        /// <summary>The faces along a loop, which is what an edge ring separates.</summary>
        public OrderedSet<Face> FaceLoop(Face from)
        {
            if (!from.IsQuad)
            {
                return [from];
            }

            var loop = new OrderedSet<Face> { from };
            for (var i = 0; i < 2; i++)
            {
                // The two directions across the quad: one pair of opposite edges each.
                var edge = EdgeOf(from.Vertices[i], from.Vertices[i + 1]);
                foreach (var next in mesh.EdgeRing(edge))
                {
                    loop.AddRange(mesh.FacesOn(next));
                }
            }

            return loop;
        }

        // ---- the actions ----

        /// <summary>
        /// Pulls every selected vertex together into one.
        /// </summary>
        /// <remarks>
        /// Regardless of distance, unlike welding: this is somebody saying "these are one point
        /// now", not "tidy up what is already touching".
        /// </remarks>
        public int Collapse(IEnumerable<int> which, bool toFirst = false)
        {
            var chosen = InRange(mesh, which);
            if (chosen.Count < 2)
            {
                return 0;
            }

            var at = toFirst ? mesh.Positions[chosen[0]].Clone() : AverageOf(mesh, chosen);
            foreach (var index in chosen)
            {
                mesh.Positions[index].SetFrom(at);
            }

            // Welding is what actually joins them; moving them together only makes them coincident,
            // and a face with three corners in one place is a face that draws nothing rather than
            // one that has gone.
            return mesh.Weld();
        }

        /// <summary>
        /// Splits a point into one per face using it, so the faces move apart.
        /// </summary>
        /// <remarks>
        /// The opposite of welding, and what somebody does before pulling one corner of a box away
        /// from the rest.
        /// </remarks>
        public int Split(IEnumerable<int> which)
        {
            var made = 0;
            foreach (var index in new OrderedSet<int>(which))
            {
                var touching = mesh.FacesAt(index);
                if (touching.Count < 2)
                {
                    continue;
                }

                // The first face keeps the original; every other gets its own copy.
                foreach (var face in touching.Skip(1))
                {
                    Replace(face, index, mesh.AddVertex(mesh.Positions[index]));
                    made++;
                }
            }

            return made;
        }

        /// <summary>
        /// Puts a face over a hole.
        /// </summary>
        /// <remarks>
        /// Finds the ring of open edges the given points sit on and closes it. A hole with a hundred
        /// sides becomes a face with a hundred corners, which is something this model can hold and a
        /// triangle-only one cannot.
        /// </remarks>
        public List<Face> FillHole(IEnumerable<int> around)
        {
            var open = mesh.OpenEdges;
            if (open.Count == 0)
            {
                return [];
            }

            var wanted = new OrderedSet<int>(around);
            var made = new List<Face>();
            var used = new OrderedSet<MeshEdge>();

            foreach (var start in open)
            {
                if (used.Contains(start) || (wanted.Count > 0 && !wanted.Contains(start.Item1) && !wanted.Contains(start.Item2)))
                {
                    continue;
                }

                var ring = BorderFrom(open, start, used);
                if (ring.Count >= 3)
                {
                    made.Add(mesh.AddFace(ring));
                }
            }

            // Wound to match whatever is around it rather than by luck: a patch facing the wrong
            // way is a hole that looks like it is still there.
            foreach (var face in made)
            {
                if (mesh.NormalOf(face).Dot(OutwardOf(mesh, face)) < 0)
                {
                    mesh.FlipFaces([face]);
                }
            }

            return made;
        }

        /// <summary>Cuts a corner off, leaving a face where the edge was.</summary>
        public List<Face> Bevel(IEnumerable<MeshEdge> which, double distance)
        {
            var chosen = new OrderedSet<MeshEdge>(which);
            if (chosen.Count == 0 || distance <= 0)
            {
                return [];
            }

            var made = new List<Face>();
            foreach (var edge in chosen)
            {
                var sides = mesh.FacesOn(edge);
                if (sides.Count != 2)
                {
                    continue;
                }

                // Each end of the edge is pulled back along both faces, and the four points that
                // leaves become the new face.
                var corners = new List<int>();
                foreach (var vertex in new[] { edge.Item1, edge.Item2 })
                {
                    corners.AddRange(sides.Select(face => PullBack(mesh, face, vertex, distance)));
                }

                var patch = mesh.AddFace([corners[0], corners[1], corners[3], corners[2]]);
                made.Add(patch);
                var neighbours = SumOfNormals(mesh, sides);
                if (mesh.NormalOf(patch).Dot(neighbours) < 0)
                {
                    mesh.FlipFaces([patch]);
                }
            }

            return made;
        }

        /// <summary>
        /// Puts a face between two open edges.
        /// </summary>
        /// <remarks>
        /// How two separate pieces are joined: extrude one edge towards the other, or bridge the gap
        /// directly.
        /// </remarks>
        public Face? Bridge(MeshEdge a, MeshEdge b)
        {
            if (a == b)
            {
                return null;
            }

            // Wound so the two edges are traversed in opposite directions, or the face comes out as
            // a bow tie.
            var near = (mesh.Positions[a.Item1] - mesh.Positions[b.Item1]).Length
                + (mesh.Positions[a.Item2] - mesh.Positions[b.Item2]).Length;
            var crossed = (mesh.Positions[a.Item1] - mesh.Positions[b.Item2]).Length
                + (mesh.Positions[a.Item2] - mesh.Positions[b.Item1]).Length;

            return crossed < near
                ? mesh.AddFace([a.Item1, a.Item2, b.Item1, b.Item2])
                : mesh.AddFace([a.Item1, a.Item2, b.Item2, b.Item1]);
        }

        /// <summary>Splits each edge into pieces.</summary>
        public List<int> SubdivideEdges(IEnumerable<MeshEdge> which, int into = 1)
        {
            var cuts = Math.Clamp(into, 1, 32);
            var made = new List<int>();

            foreach (var edge in new OrderedSet<MeshEdge>(which))
            {
                var a = mesh.Positions[edge.Item1];
                var b = mesh.Positions[edge.Item2];

                var fresh = new List<int>();
                for (var i = 1; i <= cuts; i++)
                {
                    fresh.Add(mesh.AddVertex(a + ((b - a) * ((double)i / (cuts + 1)))));
                }

                made.AddRange(fresh);

                // Put into every face using the edge, so nothing is left with a corner in the middle
                // of a neighbour's edge.
                foreach (var face in mesh.FacesOn(edge))
                {
                    InsertAlong(face, edge, fresh);
                }
            }

            return made;
        }

        /// <summary>
        /// Separates faces from the mesh, leaving them where they are.
        /// </summary>
        /// <remarks>
        /// Returns a mesh of its own. What somebody does before moving a piece away, and what makes
        /// a doorway out of a wall.
        /// </remarks>
        public Mesh Detach(IEnumerable<Face> which)
        {
            var chosen = new OrderedSet<Face>(which);
            if (chosen.Count == 0)
            {
                return new Mesh();
            }

            var taken = CopyOut(mesh, chosen);
            mesh.DeleteFaces(chosen);
            return taken;
        }

        /// <summary>Copies faces in place, as a mesh of their own.</summary>
        public Mesh DuplicateFaces(IEnumerable<Face> which) => CopyOut(mesh, which.Where(mesh.Faces.Contains));

        /// <summary>Joins faces into one, dropping the edges between them.</summary>
        public Face? MergeFaces(IEnumerable<Face> which)
        {
            var chosen = which.ToList();
            if (chosen.Count < 2)
            {
                return null;
            }

            // The border of the group, walked round to make one outline.
            var border = new OrderedSet<MeshEdge>(MeshEdits.EdgeCounts(chosen).Where(entry => entry.Value == 1).Select(entry => entry.Key));
            var ring = border.Count < 3 ? null : OutlineOf(border);
            if (ring is null || ring.Count < 3)
            {
                return null;
            }

            var material = chosen[0].Material;
            var smooth = chosen[0].Smooth;
            mesh.DeleteFaces(chosen);
            var made = mesh.AddFace(ring, material, smooth);

            // Facing the way the pieces did rather than whichever way the walk went.
            if (mesh.NormalOf(made).Dot(SumOfNormals(mesh, chosen)) < 0)
            {
                mesh.FlipFaces([made]);
            }

            return made;
        }

        /// <summary>
        /// Cuts faces down to triangles.
        /// </summary>
        /// <remarks>
        /// Named for the faces rather than for the mesh, because the mesh already has a triangulate,
        /// the one that hands geometry to the renderer. This is the one that changes what the
        /// geometry <em>is</em>.
        /// </remarks>
        public List<Face> TriangulateFaces(IEnumerable<Face> which)
        {
            var chosen = which.Where(mesh.Faces.Contains).ToList();
            var made = new List<Face>();

            foreach (var face in chosen)
            {
                if (face.IsTriangle)
                {
                    made.Add(face);
                    continue;
                }

                for (var i = 1; i + 1 < face.Vertices.Count; i++)
                {
                    made.Add(mesh.AddFace([face.Vertices[0], face.Vertices[i], face.Vertices[i + 1]], face.Material, face.Smooth));
                }

                mesh.Faces.Remove(face);
            }

            return made;
        }

        /// <summary>
        /// Turns faces the way most of them already point.
        /// </summary>
        /// <remarks>
        /// What fixes a shape that came in with some of its faces inside out, which is most of what
        /// is wrong with a mesh somebody was given.
        /// </remarks>
        public int ConformNormals(IEnumerable<Face>? which = null)
        {
            var chosen = (which ?? mesh.Faces).ToList();
            if (chosen.Count == 0)
            {
                return 0;
            }

            var middle = MiddleOf(mesh);
            var outward = mesh.Faces.Count(face => PointsOutOf(mesh, face, middle));
            var wantOutward = outward * 2 >= mesh.Faces.Count;

            var turned = 0;
            foreach (var face in chosen)
            {
                if (PointsOutOf(mesh, face, middle) == wantOutward)
                {
                    continue;
                }

                mesh.FlipFaces([face]);
                turned++;
            }

            return turned;
        }

        /// <summary>
        /// Moves the mesh so a point is at the origin, and says how far it moved.
        /// </summary>
        /// <remarks>
        /// The pivot is where an object turns and scales around, and a shape that turns around a
        /// corner two rooms away is one nobody can place.
        /// </remarks>
        public Vector3 CentrePivotOn(IEnumerable<int> which)
        {
            var chosen = InRange(mesh, which);
            var at = chosen.Count == 0 ? MiddleOf(mesh) : AverageOf(mesh, chosen);

            mesh.Transform(Matrix4.TranslationValues(-at.X, -at.Y, -at.Z));
            return at;
        }

        /// <summary>A mirrored copy, across whichever axes are asked for.</summary>
        public Mesh Mirrored(bool x = false, bool y = false, bool z = false)
        {
            var copy = mesh.Copy();
            if (!x && !y && !z)
            {
                return copy;
            }

            copy.Transform(Matrix4.Diagonal3Values(x ? -1 : 1, y ? -1 : 1, z ? -1 : 1));

            // Reflecting turns the winding inside out, and a mesh that is inside out is one that is
            // invisible from the side somebody is looking at.
            var flips = new[] { x, y, z }.Count(on => on);
            if (flips % 2 == 1)
            {
                copy.Flip();
            }

            return copy;
        }
    }

    private static bool HasEdge(Face face, MeshEdge edge)
    {
        var count = face.Vertices.Count;
        for (var i = 0; i < count; i++)
        {
            if (EdgeOf(face.Vertices[i], face.Vertices[(i + 1) % count]) == edge)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether <paramref name="face"/> lies within <paramref name="degrees"/> of any face already chosen.</summary>
    private static bool IsWithin(Mesh mesh, Face face, OrderedSet<Face> chosen, double degrees)
    {
        var limit = Math.Cos(Doubles.Clamp(degrees, 0, 180) * Math.PI / 180);
        return chosen.Any(already => mesh.NormalOf(face).Dot(mesh.NormalOf(already)) >= limit - 1e-9);
    }

    /// <summary>The point a loop travels on to, after crossing <paramref name="next"/>.</summary>
    private static int Onward(MeshEdge next, int pivot)
    {
        if (next.Item1 != pivot && next.Item2 != pivot)
        {
            return pivot;
        }

        return next.Item1 == pivot ? next.Item2 : next.Item1;
    }

    /// <summary>
    /// The next edge of a loop: the one on the far side of the quad, sharing the point the loop is
    /// travelling through.
    /// </summary>
    private static MeshEdge? AcrossQuad(Mesh mesh, MeshEdge edge, int through)
    {
        foreach (var face in mesh.FacesOn(edge).Where(face => face.IsQuad))
        {
            foreach (var other in MeshEdits.EdgesOf([face]))
            {
                if (other == edge || (other.Item1 != through && other.Item2 != through))
                {
                    continue;
                }

                // Two edges of a quad share the pivot; the loop wants the one that continues rather
                // than the one that turns the corner, which is the one whose faces do not include
                // the one we came through.
                if (mesh.FacesOn(other).Any(f => f != face))
                {
                    return other;
                }
            }
        }

        return null;
    }

    private static MeshEdge? OppositeEdge(Face face, MeshEdge edge)
    {
        var v = face.Vertices;
        for (var i = 0; i < 4; i++)
        {
            if (EdgeOf(v[i], v[(i + 1) % 4]) == edge)
            {
                return EdgeOf(v[(i + 2) % 4], v[(i + 3) % 4]);
            }
        }

        return null;
    }

    private static List<int> InRange(Mesh mesh, IEnumerable<int> which) =>
        [.. which.Where(index => index >= 0 && index < mesh.Positions.Count)];

    /// <summary>The mean of the points at <paramref name="indices"/>, a point listed twice counting twice.</summary>
    private static Vector3 AverageOf(Mesh mesh, List<int> indices)
    {
        var sum = Vector3.Zero();
        foreach (var index in indices)
        {
            sum.Add(mesh.Positions[index]);
        }

        return sum.Scaled(1.0 / indices.Count);
    }

    private static Vector3 SumOfNormals(Mesh mesh, IEnumerable<Face> which)
    {
        var sum = Vector3.Zero();
        foreach (var face in which)
        {
            sum.Add(mesh.NormalOf(face));
        }

        return sum;
    }

    private static Vector3 MiddleOf(Mesh mesh)
    {
        var (min, max) = mesh.Bounds;
        return (min + max) / 2;
    }

    private static bool PointsOutOf(Mesh mesh, Face face, Vector3 middle) =>
        mesh.NormalOf(face).Dot(mesh.CentreOf(face) - middle) > 0;

    /// <summary>Swaps every use of <paramref name="vertex"/> in a face for <paramref name="moved"/>.</summary>
    private static void Replace(Face face, int vertex, int moved)
    {
        for (var i = 0; i < face.Vertices.Count; i++)
        {
            if (face.Vertices[i] == vertex)
            {
                face.Vertices[i] = moved;
            }
        }
    }

    /// <summary>
    /// The way a patch over a hole should face. The neighbours agree when the hole is in a flat
    /// sheet. Round the lid of a box they point four different ways and cancel out entirely, so
    /// where they say nothing, face away from the middle of the shape, which is what the outside of
    /// a closed thing means.
    /// </summary>
    private static Vector3 OutwardOf(Mesh mesh, Face face)
    {
        var neighbours = MeshEdits.EdgesOf([face]).SelectMany(edge => mesh.FacesOn(edge).Where(other => other != face));
        var average = SumOfNormals(mesh, neighbours);
        return average.Length2 > 1e-12 ? average : mesh.CentreOf(face) - MiddleOf(mesh);
    }

    /// <summary>
    /// Walks the border from <paramref name="start"/> until it comes back, marking each edge it
    /// uses.
    /// </summary>
    private static List<int> BorderFrom(OrderedSet<MeshEdge> open, MeshEdge start, OrderedSet<MeshEdge> used)
    {
        var ring = new List<int> { start.Item1, start.Item2 };
        used.Add(start);
        var at = start.Item2;

        while (true)
        {
            var next = NextEdge(open, used, at);
            if (next.Item1 < 0)
            {
                break;
            }

            used.Add(next);
            at = next.Item1 == at ? next.Item2 : next.Item1;
            if (at == ring[0])
            {
                break;
            }

            ring.Add(at);
        }

        return ring;
    }

    /// <summary>
    /// The first edge not yet used that touches <paramref name="at"/>, or <c>(-1, -1)</c> for none.
    /// A point numbered below zero reads as none too, so a walk stops at one.
    /// </summary>
    private static MeshEdge NextEdge(OrderedSet<MeshEdge> edges, OrderedSet<MeshEdge> used, int at)
    {
        foreach (var edge in edges)
        {
            if (!used.Contains(edge) && (edge.Item1 == at || edge.Item2 == at))
            {
                return edge;
            }
        }

        return (-1, -1);
    }

    /// <summary>
    /// The points of a closed border in the order they are met, or null when the edges do not make
    /// one loop.
    /// </summary>
    private static List<int>? OutlineOf(OrderedSet<MeshEdge> border)
    {
        var first = border.First();
        var ring = new List<int> { first.Item1 };
        var at = first.Item2;
        var used = new OrderedSet<MeshEdge> { first };
        while (at != ring[0])
        {
            ring.Add(at);
            var next = NextEdge(border, used, at);
            if (next.Item1 < 0)
            {
                return null;
            }

            used.Add(next);
            at = next.Item1 == at ? next.Item2 : next.Item1;
        }

        return ring;
    }

    /// <summary>
    /// Moves one corner of a face towards its middle, by at most <paramref name="distance"/>, and
    /// says which new point it moved to.
    /// </summary>
    // F5: a bevel pulls the corner back and the caller needs the new point.
    private static int PullBack(Mesh mesh, Face face, int vertex, double distance)
    {
        var towards = mesh.CentreOf(face) - mesh.Positions[vertex];
        var step = towards.Length <= distance ? towards : towards.Normalized() * distance;
        var moved = mesh.AddVertex(mesh.Positions[vertex] + step);
        Replace(face, vertex, moved);
        return moved;
    }

    /// <summary>Puts the points of <paramref name="fresh"/> into a face, between the two ends of <paramref name="edge"/>.</summary>
    private static void InsertAlong(Face face, MeshEdge edge, List<int> fresh)
    {
        var count = face.Vertices.Count;
        for (var i = 0; i < count; i++)
        {
            var here = face.Vertices[i];
            var next = face.Vertices[(i + 1) % count];
            if (EdgeOf(here, next) != edge)
            {
                continue;
            }

            face.Vertices.InsertRange(i + 1, here == edge.Item1 ? fresh : Enumerable.Reverse(fresh));
            return;
        }
    }

    /// <summary>A mesh of its own holding <paramref name="faces"/>, each point once.</summary>
    private static Mesh CopyOut(Mesh mesh, IEnumerable<Face> faces)
    {
        var taken = new Mesh();
        var moveTo = new Dictionary<int, int>();
        foreach (var face in faces)
        {
            taken.AddFace([.. face.Vertices.Select(index => TakeVertex(mesh, taken, moveTo, index))], face.Material, face.Smooth);
        }

        return taken;
    }

    // F5: a lookup that adds on a miss.
    private static int TakeVertex(Mesh from, Mesh into, Dictionary<int, int> moveTo, int index)
    {
        if (!moveTo.TryGetValue(index, out var taken))
        {
            taken = into.AddVertex(from.Positions[index]);
            moveTo[index] = taken;
        }

        return taken;
    }
}
