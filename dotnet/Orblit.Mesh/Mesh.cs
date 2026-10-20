using Orblit.VectorMath;

namespace Orblit.Mesh;

/// <summary>
/// One flat surface, named by the corners it joins. Any number of corners, not three: a cube has
/// six faces and not twelve triangles, and every operation here means something on a quad and
/// something fiddlier on the two triangles it was cut into.
/// </summary>
public sealed class Face(List<int> vertices, int material = 0, bool smooth = false, FaceUv? uv = null)
{
    /// <summary>
    /// Indices into the mesh's positions, going round the face. The order decides which way it
    /// faces: counter-clockwise seen from the front, as glTF, Filament and OpenGL have it.
    /// </summary>
    public List<int> Vertices { get; } = vertices;

    /// <summary>Which material this face wears. An index rather than a name.</summary>
    public int Material { get; set; } = material;

    /// <summary>How this face gets its texture coordinates. Automatic unless somebody drew them.</summary>
    public FaceUv Uv { get; set; } = uv ?? new FaceUv();

    /// <summary>Whether this face's normals are shared with its neighbours. Flat by default.</summary>
    public bool Smooth { get; set; } = smooth;

    public int Length => Vertices.Count;

    public bool IsTriangle => Vertices.Count == 3;

    public bool IsQuad => Vertices.Count == 4;

    public Face Copy() => new([.. Vertices], Material, Smooth, Uv);

    public OrderedDictionary<string, object?> ToJson()
    {
        var json = new OrderedDictionary<string, object?>
        {
            ["v"] = Vertices.Select(index => (object?)(long)index).ToList(),
        };
        if (Material != 0)
        {
            json["m"] = (long)Material;
        }

        if (Smooth)
        {
            json["s"] = true;
        }

        var uv = Uv.ToJson();
        if (uv.Count > 0)
        {
            json["uv"] = uv;
        }

        return json;
    }

    public static Face? FromJson(object? value)
    {
        if (JsonValues.Map(value) is not { } map
            || !map.TryGetValue("v", out var raw)
            || JsonValues.List(raw) is not { Count: >= 3 } list)
        {
            return null;
        }

        map.TryGetValue("m", out var material);
        map.TryGetValue("s", out var smooth);
        map.TryGetValue("uv", out var uv);
        return new Face(
            [.. list.Select(JsonValues.Whole).OfType<int>()],
            JsonValues.Whole(material) ?? 0,
            JsonValues.IsTrue(smooth),
            FaceUv.FromJson(uv));
    }
}

/// <summary>
/// Geometry somebody is building. Positions and faces, and nothing else. Not a half-edge
/// structure: those refuse to hold anything that is not a closed surface, and half of what
/// somebody builds in an editor, such as a wall, a floor or a shape mid-extrude, is not one.
/// </summary>
public sealed class Mesh(List<Vector3>? positions = null, List<Face>? faces = null)
{
    public List<Vector3> Positions { get; } = positions ?? [];

    public List<Face> Faces { get; } = faces ?? [];

    public int VertexCount => Positions.Count;

    public int FaceCount => Faces.Count;

    public bool IsEmpty => Faces.Count == 0;

    public Mesh Copy() => new(
        [.. Positions.Select(at => at.Clone())],
        [.. Faces.Select(face => face.Copy())]);

    /// <summary>The corners of a face, as points. A corner whose index is out of range is left out.</summary>
    public List<Vector3> PointsOf(Face face) =>
        [.. face.Vertices.Where(index => index >= 0 && index < Positions.Count).Select(index => Positions[index])];

    /// <summary>
    /// Which way a face points. Newell's method rather than the cross product of the first two
    /// edges: a quad whose corners are not quite in a plane has no single normal, and this is the
    /// average of all of them. A degenerate face has no direction, so it gets up.
    /// </summary>
    public Vector3 NormalOf(Face face)
    {
        var points = PointsOf(face);
        if (points.Count < 3)
        {
            return new Vector3(0, 1, 0);
        }

        var normal = Vector3.Zero();
        for (var i = 0; i < points.Count; i++)
        {
            var here = points[i];
            var next = points[(i + 1) % points.Count];
            normal.X += (here.Y - next.Y) * (here.Z + next.Z);
            normal.Y += (here.Z - next.Z) * (here.X + next.X);
            normal.Z += (here.X - next.X) * (here.Y + next.Y);
        }

        return normal.Length2 < 1e-20 ? new Vector3(0, 1, 0) : normal.Normalized();
    }

    public Vector3 CentreOf(Face face)
    {
        var points = PointsOf(face);
        if (points.Count == 0)
        {
            return Vector3.Zero();
        }

        var sum = Vector3.Zero();
        foreach (var point in points)
        {
            sum.Add(point);
        }

        sum.Scale(1.0 / points.Count);
        return sum;
    }

    /// <summary>
    /// How much surface a face has. Half the length of the sum of the corner cross products,
    /// which is exact for any face in a plane however bent its outline is.
    /// </summary>
    public double AreaOf(Face face)
    {
        var points = PointsOf(face);
        if (points.Count < 3)
        {
            return 0;
        }

        var total = Vector3.Zero();
        for (var i = 0; i < points.Count; i++)
        {
            total.Add(points[i].Cross(points[(i + 1) % points.Count]));
        }

        return total.Length / 2;
    }

    /// <summary>The box everything sits inside, as its smallest and largest corner.</summary>
    public (Vector3 Min, Vector3 Max) Bounds
    {
        get
        {
            if (Positions.Count == 0)
            {
                return (Vector3.Zero(), Vector3.Zero());
            }

            var min = Positions[0].Clone();
            var max = Positions[0].Clone();
            foreach (var at in Positions)
            {
                min.SetValues(Math.Min(min.X, at.X), Math.Min(min.Y, at.Y), Math.Min(min.Z, at.Z));
                max.SetValues(Math.Max(max.X, at.X), Math.Max(max.Y, at.Y), Math.Max(max.Z, at.Z));
            }

            return (min, max);
        }
    }

    /// <summary>Adds a point and says where it went.</summary>
    public int AddVertex(Vector3 at)
    {
        Positions.Add(at.Clone());
        return Positions.Count - 1;
    }

    /// <summary>Adds a face over points that are already there.</summary>
    public Face AddFace(List<int> vertices, int material = 0, bool smooth = false)
    {
        var face = new Face(vertices, material, smooth);
        Faces.Add(face);
        return face;
    }

    public void Transform(Matrix4 by)
    {
        foreach (var at in Positions)
        {
            at.SetFrom(by.Transformed3(at));
        }
    }

    /// <summary>
    /// Turns every face the other way. A mirrored shape needs it: reflecting the points turns the
    /// winding inside out, and a mesh that is inside out is invisible from the side somebody is
    /// looking at.
    /// </summary>
    public void Flip()
    {
        foreach (var face in Faces)
        {
            face.Vertices.Reverse();
        }
    }

    /// <summary>Everything from another mesh, added to this one.</summary>
    public void Merge(Mesh other)
    {
        var offset = Positions.Count;
        foreach (var at in other.Positions)
        {
            Positions.Add(at.Clone());
        }

        foreach (var face in other.Faces)
        {
            Faces.Add(new Face([.. face.Vertices.Select(index => index + offset)], face.Material, face.Smooth));
        }
    }

    public OrderedDictionary<string, object?> ToJson() => new()
    {
        ["positions"] = Positions.SelectMany(at => new object?[] { at.X, at.Y, at.Z }).ToList(),
        ["faces"] = Faces.Select(face => (object?)face.ToJson()).ToList(),
    };

    public static Mesh? FromJson(object? value)
    {
        if (JsonValues.Map(value) is not { } map
            || !map.TryGetValue("positions", out var rawPositions)
            || JsonValues.List(rawPositions) is not { } raw)
        {
            return null;
        }

        var positions = new List<Vector3>();
        for (var i = 0; i + 2 < raw.Count; i += 3)
        {
            positions.Add(new Vector3(
                JsonValues.NumOrZero(raw[i]),
                JsonValues.NumOrZero(raw[i + 1]),
                JsonValues.NumOrZero(raw[i + 2])));
        }

        map.TryGetValue("faces", out var rawFaces);
        return new Mesh(positions, FacesFrom(rawFaces, positions.Count));
    }

    /// <summary>The faces a file lists, less any that name a point the file does not have.</summary>
    private static List<Face> FacesFrom(object? raw, int pointCount)
    {
        var faces = new List<Face>();
        foreach (var entry in JsonValues.List(raw) ?? [])
        {
            if (Face.FromJson(entry) is { } face && face.Vertices.TrueForAll(i => i >= 0 && i < pointCount))
            {
                faces.Add(face);
            }
        }

        return faces;
    }
}
