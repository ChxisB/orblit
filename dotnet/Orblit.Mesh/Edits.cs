using Orblit.VectorMath;

namespace Orblit.Mesh;

/// <summary>
/// The operations that change geometry. Free functions over a mesh and not methods on it, because
/// a mesh is a description and these are things somebody does to one.
/// </summary>
public static class MeshEdits
{
    extension(Mesh mesh)
    {
        /// <summary>
        /// Pulls faces out along their own normals. The move a modelling tool is built around:
        /// everything from a doorway to a chimney is a face pulled out and moved. New sides are made
        /// round the edge, wound so they face outwards, and the original face is carried to the end
        /// and not left behind inside the shape.
        /// </summary>
        /// <returns>
        /// The faces that ended up on the outside, which is what somebody wants selected
        /// afterwards: they are almost always about to extrude again.
        /// </returns>
        public List<Face> Extrude(IEnumerable<Face> which, double distance)
        {
            // Each face taken on its own. Extruding two faces that share an edge as one region is a
            // different operation, and doing it silently would weld two chimneys into a wall.
            var moved = new List<Face>();
            foreach (var face in Chosen(mesh, which))
            {
                var along = mesh.NormalOf(face) * distance;

                // A copy of every corner, moved. Shared corners are duplicated on purpose: pulling
                // one face should not drag its neighbours' vertices with it.
                var fresh = face.Vertices.ToList().Select(index => mesh.AddVertex(mesh.Positions[index] + along)).ToList();
                AddSides(mesh, face, fresh);
                Replace(face, fresh);
                moved.Add(face);
            }

            return moved;
        }

        /// <summary>
        /// Shrinks faces towards their own middle, leaving a border round each. What a window is:
        /// inset a wall, then extrude the middle inwards. Scaling the face would move the border
        /// too. This keeps the outer edge exactly where it was.
        /// </summary>
        public List<Face> Inset(IEnumerable<Face> which, double amount)
        {
            var inner = new List<Face>();
            foreach (var face in Chosen(mesh, which))
            {
                var centre = mesh.CentreOf(face);
                var fresh = new List<int>();
                foreach (var index in face.Vertices)
                {
                    var at = mesh.Positions[index];
                    var towards = centre - at;

                    // A face smaller than the inset asked for collapses to its middle and does not
                    // turn inside out, which is what scaling past the centre would do.
                    var step = towards.Length <= amount ? towards : towards.Normalized() * amount;
                    fresh.Add(mesh.AddVertex(at + step));
                }

                AddSides(mesh, face, fresh);
                Replace(face, fresh);
                inner.Add(face);
            }

            return inner;
        }

        /// <summary>
        /// Cuts every face into four by adding a point in the middle of each edge and one in the
        /// middle of the face. Quads out of quads, so the result is still something the other
        /// operations work on. A triangle becomes three quads for the same reason.
        /// </summary>
        public List<Face> Subdivide(IEnumerable<Face> which)
        {
            var made = new List<Face>();
            foreach (var face in Chosen(mesh, which))
            {
                var corners = face.Vertices.ToList();
                var count = corners.Count;

                var middle = mesh.AddVertex(mesh.CentreOf(face));
                var edges = Enumerable.Range(0, count)
                    .ToList()
                    .Select(i => mesh.AddVertex((mesh.Positions[corners[i]] + mesh.Positions[corners[(i + 1) % count]]) / 2))
                    .ToList();

                for (var i = 0; i < count; i++)
                {
                    var previous = (i - 1 + count) % count;
                    made.Add(mesh.AddFace([corners[i], edges[i], middle, edges[previous]], face.Material, face.Smooth));
                }

                mesh.Faces.Remove(face);
            }

            return made;
        }

        /// <summary>
        /// Takes faces out. The points they used stay, because another face may be using them and
        /// walking the whole mesh to find out costs more than a few unused vertices. <c>Compact</c>
        /// is what clears those up, when somebody wants it.
        /// </summary>
        public void DeleteFaces(IEnumerable<Face> which)
        {
            var going = which.ToHashSet();
            mesh.Faces.RemoveAll(going.Contains);
        }

        /// <summary>Turns the chosen faces the other way round.</summary>
        public void FlipFaces(IEnumerable<Face> which)
        {
            foreach (var face in which.Where(mesh.Faces.Contains))
            {
                face.Vertices.Reverse();
            }
        }

        /// <summary>
        /// Joins points that are in the same place, so faces share corners again. Extruding and
        /// merging both leave duplicates behind on purpose: an operation that welded as it went would
        /// drag neighbours around. This is the one that tidies up, and it is somebody's decision and
        /// not a side effect.
        /// </summary>
        /// <returns>How many points went.</returns>
        public int Weld(double within = 1e-4)
        {
            if (mesh.Positions.Count == 0)
            {
                return 0;
            }

            // Bucketed by rounded position and not compared with everything: a mesh with ten
            // thousand vertices is a hundred million comparisons, and this is an operation
            // somebody presses a button for.
            var cell = within <= 0 ? 1e-4 : within;
            var buckets = new Dictionary<(long, long, long), int>();
            var moveTo = new int[mesh.Positions.Count];
            var kept = new List<Vector3>();
            for (var i = 0; i < mesh.Positions.Count; i++)
            {
                var key = BucketOf(mesh.Positions[i], cell);
                if (!buckets.TryGetValue(key, out var already))
                {
                    already = kept.Count;
                    buckets[key] = already;
                    kept.Add(mesh.Positions[i]);
                }

                moveTo[i] = already;
            }

            var went = mesh.Positions.Count - kept.Count;
            if (went == 0)
            {
                return 0;
            }

            mesh.Positions.Clear();
            mesh.Positions.AddRange(kept);
            foreach (var face in mesh.Faces)
            {
                Replace(face, Tidy(face.Vertices.Select(index => moveTo[index]).ToList()));
            }

            // A face with fewer than three corners left is not a face.
            mesh.Faces.RemoveAll(face => face.Vertices.Count < 3);
            return went;
        }

        /// <summary>Throws away points nothing uses, and renumbers what is left.</summary>
        public int Compact()
        {
            var used = mesh.Faces.SelectMany(face => face.Vertices).ToHashSet();
            if (used.Count == mesh.Positions.Count)
            {
                return 0;
            }

            var moveTo = new Dictionary<int, int>();
            var kept = new List<Vector3>();
            for (var i = 0; i < mesh.Positions.Count; i++)
            {
                if (used.Contains(i))
                {
                    moveTo[i] = kept.Count;
                    kept.Add(mesh.Positions[i]);
                }
            }

            var went = mesh.Positions.Count - kept.Count;
            mesh.Positions.Clear();
            mesh.Positions.AddRange(kept);
            foreach (var face in mesh.Faces)
            {
                Replace(face, face.Vertices.Select(index => moveTo[index]).ToList());
            }

            return went;
        }

        /// <summary>Moves the chosen points.</summary>
        public void MoveVertices(IEnumerable<int> which, Vector3 by)
        {
            foreach (var index in which.Where(index => index >= 0 && index < mesh.Positions.Count))
            {
                mesh.Positions[index].Add(by);
            }
        }

        /// <summary>
        /// The faces on the outside of the shape: the ones with an edge nothing else shares. What
        /// tells somebody their extrude left a hole, and what a "select border" does.
        /// </summary>
        public List<Face> OpenFaces
        {
            get
            {
                var counts = EdgeCounts(mesh.Faces);
                return [.. mesh.Faces.Where(face => EdgesOf([face]).Any(edge => counts.GetValueOrDefault(edge) < 2))];
            }
        }
    }

    /// <summary>Every point a set of faces touches.</summary>
    public static OrderedSet<int> VerticesOf(IEnumerable<Face> which) => new(which.SelectMany(face => face.Vertices));

    /// <summary>
    /// The edges of a set of faces, each as its two points, smaller first so an edge shared by two
    /// faces is one edge and not two.
    /// </summary>
    public static OrderedSet<MeshEdge> EdgesOf(IEnumerable<Face> which)
    {
        var edges = new OrderedSet<MeshEdge>();
        foreach (var face in which)
        {
            var count = face.Vertices.Count;
            for (var i = 0; i < count; i++)
            {
                var a = face.Vertices[i];
                var b = face.Vertices[(i + 1) % count];
                edges.Add(a < b ? (a, b) : (b, a));
            }
        }

        return edges;
    }

    /// <summary>How many of <paramref name="which"/> use each edge, in the order the edges are first met.</summary>
    internal static OrderedDictionary<MeshEdge, int> EdgeCounts(IEnumerable<Face> which)
    {
        var counts = new OrderedDictionary<MeshEdge, int>();
        foreach (var face in which)
        {
            foreach (var edge in EdgesOf([face]))
            {
                counts[edge] = counts.GetValueOrDefault(edge) + 1;
            }
        }

        return counts;
    }

    /// <summary>The faces of <paramref name="which"/> that belong to the mesh, in the order given.</summary>
    private static List<Face> Chosen(Mesh mesh, IEnumerable<Face> which) => [.. which.Where(mesh.Faces.Contains)];

    /// <summary>The faces that join a face's edge to the same edge in <paramref name="fresh"/>, wound so they face outwards.</summary>
    private static void AddSides(Mesh mesh, Face face, List<int> fresh)
    {
        var count = face.Vertices.Count;
        for (var i = 0; i < count; i++)
        {
            var a = face.Vertices[i];
            var b = face.Vertices[(i + 1) % count];
            mesh.AddFace([a, b, fresh[(i + 1) % count], fresh[i]], face.Material);
        }
    }

    /// <summary>
    /// The face itself moves and a new one is not added: it keeps its material and whatever else
    /// was set on it, which is what somebody means by extruding this face.
    /// </summary>
    private static void Replace(Face face, List<int> vertices)
    {
        face.Vertices.Clear();
        face.Vertices.AddRange(vertices);
    }

    private static (long, long, long) BucketOf(Vector3 at, double cell) =>
        (Doubles.Round(at.X / cell), Doubles.Round(at.Y / cell), Doubles.Round(at.Z / cell));

    /// <summary>
    /// A corner that welded onto the one beside it leaves the face with a repeated point, which is
    /// an edge of no length.
    /// </summary>
    private static List<int> Tidy(List<int> moved) =>
        [.. moved.Where((point, i) => point != moved[(i + 1) % moved.Count])];
}

/// <summary>
/// Moving the parts of a mesh, and not the mesh. Separate from the operations above because these
/// change nothing about the shape's structure: the same vertices in the same faces, standing
/// somewhere else. That is what makes them safe to run on every frame of a drag.
/// </summary>
public static class MeshHandles
{
    extension(Mesh mesh)
    {
        /// <summary>
        /// Where a set of corners sits, on average. Null when none of them exist. Named for corners
        /// and not overloading <c>CentreOf</c>, which takes a face: a handle sits at the middle of
        /// what is selected, which may be two vertices of one face or every vertex of eight.
        /// </summary>
        public Vector3? CentreOfPoints(IEnumerable<int> which)
        {
            var sum = Vector3.Zero();
            var count = 0;
            foreach (var index in which.Where(index => index >= 0 && index < mesh.Positions.Count))
            {
                sum.Add(mesh.Positions[index]);
                count++;
            }

            if (count == 0)
            {
                return null;
            }

            sum.Scale(1.0 / count);
            return sum;
        }

        /// <summary>Moves corners by <paramref name="by"/>.</summary>
        public void MovePoints(IEnumerable<int> which, Vector3 by)
        {
            foreach (var index in which.Where(index => index >= 0 && index < mesh.Positions.Count))
            {
                mesh.Positions[index].Add(by);
            }
        }

        /// <summary>
        /// Turns corners about <paramref name="about"/>. A matrix and not a quaternion, deliberately:
        /// everything else in this engine goes through the matrix, so taking a quaternion here would
        /// leave a caller one honest-looking line away from elements turning the other way from the
        /// object they belong to. Hand it the quaternion's <c>AsRotationMatrix()</c>.
        /// </summary>
        public void TurnPoints(IEnumerable<int> which, Matrix3 turn, Vector3 about)
        {
            foreach (var index in which.Where(index => index >= 0 && index < mesh.Positions.Count))
            {
                mesh.Positions[index] = turn.Transformed(mesh.Positions[index] - about) + about;
            }
        }

        /// <summary>Scales corners about <paramref name="about"/>.</summary>
        // F2: positional parameters, and the call reads as which, by, about.
        public void ScalePoints(IEnumerable<int> which, Vector3 by, Vector3 about)
        {
            foreach (var index in which.Where(index => index >= 0 && index < mesh.Positions.Count))
            {
                var at = mesh.Positions[index] - about;
                mesh.Positions[index] = new Vector3(about.X + (at.X * by.X), about.Y + (at.Y * by.Y), about.Z + (at.Z * by.Z));
            }
        }

        /// <summary>
        /// The direction a set of faces collectively points. Averaged and normalised, so pulling two
        /// faces of a corner moves them along the corner and not each along its own wall. Null when
        /// the faces cancel each other out, the two sides of a flat sheet say, where there is no
        /// "out" to move along.
        /// </summary>
        public Vector3? NormalAcross(IEnumerable<Face> which)
        {
            var sum = Vector3.Zero();
            var count = 0;
            foreach (var face in which)
            {
                sum.Add(mesh.NormalOf(face));
                count++;
            }

            return count == 0 || sum.Length2 < 1e-12 ? null : sum.Normalized();
        }
    }
}

/// <summary>
/// Growing a shape outwards. For a boundary that has to sit a little outside the thing it belongs
/// to, which is nearly always what a collision shape wants, because a character standing exactly
/// on a surface is a character intersecting it half the time.
/// </summary>
public static class MeshShell
{
    extension(Mesh mesh)
    {
        /// <summary>
        /// A copy with every face moved <paramref name="by"/> metres along its own normal. Corners
        /// move further than faces do, and by exactly the right amount: a corner of a cube pushed a
        /// metre along its diagonal would only move each of its three faces out by a bit over half a
        /// metre. So each corner is pushed along the average of the faces meeting there, scaled by
        /// how far off square that average is, which puts every face exactly that far out and keeps
        /// the corner sharp.
        /// </summary>
        /// <remarks>
        /// Faces, corners and everything a face wears are unchanged; only the positions move. A
        /// negative distance shrinks, which is how somebody makes a boundary that sits inside a
        /// decorative shell.
        /// </remarks>
        public Mesh Grown(double by)
        {
            var result = mesh.Copy();
            if (by == 0 || mesh.Positions.Count == 0)
            {
                return result;
            }

            foreach (var (index, normals) in FacesMeetingAt(mesh))
            {
                var average = Vector3.Zero();
                foreach (var one in normals)
                {
                    average.Add(one);
                }

                // Corners of a shape folded back on itself can cancel out. Leaving those where they
                // are keeps the shape closed, which matters more than moving them somewhere arbitrary.
                if (average.Length2 < 1e-12)
                {
                    continue;
                }

                average.Normalize();

                // The face most side-on to the average decides the scale, so no face ends up
                // further out than asked for. Clamped, because a corner where two faces nearly
                // double back is a scale that runs away.
                var least = 1.0;
                foreach (var one in normals)
                {
                    var along = average.Dot(one);
                    if (along < least)
                    {
                        least = along;
                    }
                }

                var scale = least < 0.2 ? 5.0 : 1 / least;
                result.Positions[index] = mesh.Positions[index] + (average * (by * scale));
            }

            return result;
        }
    }

    /// <summary>Which faces meet at each corner, and which way each of them points.</summary>
    private static Dictionary<int, List<Vector3>> FacesMeetingAt(Mesh mesh)
    {
        var meeting = new Dictionary<int, List<Vector3>>();
        foreach (var face in mesh.Faces.Where(face => face.Vertices.Count >= 3))
        {
            var normal = mesh.NormalOf(face);
            foreach (var index in face.Vertices.Where(index => index >= 0 && index < mesh.Positions.Count))
            {
                if (!meeting.TryGetValue(index, out var normals))
                {
                    normals = [];
                    meeting[index] = normals;
                }

                normals.Add(normal);
            }
        }

        return meeting;
    }
}
