using System.Buffers.Binary;
using System.Text;
using Orblit.Json;
using Orblit.VectorMath;

namespace Orblit.Mesh;

/// <summary>
/// The formats a mesh can leave in. Not because the engine needs them, since it reads its own
/// <c>.glb</c>, but because a shape blocked out here is often the start of something that gets
/// finished somewhere else.
/// </summary>
public enum MeshFormat
{
    /// <summary>Wavefront OBJ. The one that keeps faces as drawn, and carries materials in a <c>.mtl</c>.</summary>
    Obj,

    /// <summary>Binary glTF, which is what the engine itself loads.</summary>
    Glb,

    /// <summary>Stereolithography. Triangles and nothing else. What a printer takes.</summary>
    Stl,

    /// <summary>Polygon file format. Triangles with their normals and texture coordinates.</summary>
    Ply,
}

public static class MeshFormats
{
    extension(MeshFormat format)
    {
        public string Label => format switch
        {
            MeshFormat.Obj => "OBJ",
            MeshFormat.Glb => "glTF binary",
            MeshFormat.Stl => "STL",
            MeshFormat.Ply => "PLY",
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

        /// <summary>The file extension, which is the member's name in lower case.</summary>
        public string Extension => EnumNames.Of(format);

        /// <summary>Whether the format carries materials of its own.</summary>
        public bool HasMaterials => format is MeshFormat.Obj or MeshFormat.Glb;

        /// <summary>Whether writing it produces a second file beside the first.</summary>
        public bool HasSidecar => format == MeshFormat.Obj;
    }
}

/// <summary>One file, and what it is called.</summary>
/// <param name="name">
/// The file name, extension and all. Relative: whoever asked decides where it goes.
/// </param>
/// <param name="bytes">The contents.</param>
public sealed class Written(string name, byte[] bytes)
{
    public string Name { get; } = name;

    public byte[] Bytes { get; } = bytes;

    /// <summary>The contents as text. Throws <see cref="FormatException"/> when they are not UTF-8.</summary>
    public string Text => DartText.Decode(Bytes);
}

/// <summary>Writing a mesh out.</summary>
public static class MeshExport
{
    private const double EmptyNormal = 1e-20;

    extension(Mesh mesh)
    {
        /// <summary>
        /// This mesh in <paramref name="format"/>, as the files it takes. A list because one of them
        /// is two files: an OBJ that mentions materials is useless without the <c>.mtl</c> it names.
        /// </summary>
        public List<Written> WriteAs(
            MeshFormat format,
            string name = "mesh",
            IReadOnlyList<GlbMaterial>? materials = null,
            bool binary = true) => format switch
            {
                MeshFormat.Obj => mesh.ToObj(name, materials),
                MeshFormat.Glb => [new Written($"{name}.glb", mesh.ToGlb(name, materials))],
                MeshFormat.Stl => [new Written($"{name}.stl", mesh.ToStl(name, binary))],
                MeshFormat.Ply => [new Written($"{name}.ply", mesh.ToPly(binary))],
                _ => throw new ArgumentOutOfRangeException(nameof(format)),
            };

        /// <summary>
        /// Wavefront OBJ, and the material library it names. Faces are written as they are, so a quad
        /// stays a quad, because OBJ is the only common format that can hold one.
        /// </summary>
        public List<Written> ToObj(string name = "mesh", IReadOnlyList<GlbMaterial>? materials = null)
        {
            materials ??= [];
            var text = new StringBuilder();
            Line(text, "# Written by Orblit");
            Line(text, $"# {mesh.VertexCount} vertices, {mesh.FaceCount} faces");
            if (materials.Count > 0)
            {
                Line(text, $"mtllib {name}.mtl");
            }

            Line(text, $"o {name}");
            foreach (var at in mesh.Positions)
            {
                Line(text, $"v {Triple(at)}");
            }

            // Coordinates and normals are per corner and not per vertex, because that is what they
            // are: a cube's corner has three normals and belongs to three faces.
            var corners = CornersOf(mesh);
            corners.Uvs.ForEach(line => Line(text, line));
            corners.Normals.ForEach(line => Line(text, line));
            WriteFaces(text, mesh, corners.PerFace, materials);

            var files = new List<Written> { new($"{name}.obj", Encoding.UTF8.GetBytes(text.ToString())) };
            if (materials.Count > 0)
            {
                files.Add(new Written($"{name}.mtl", Encoding.UTF8.GetBytes(Mtl(materials))));
            }

            return files;
        }

        /// <summary>
        /// Stereolithography: triangles, each with the direction it faces. Everything else is thrown
        /// away, which is not a shortcoming of the writer. It is what the format is.
        /// </summary>
        public byte[] ToStl(string name = "mesh", bool binary = true)
        {
            var tris = mesh.Triangulate();
            var facets = Enumerable.Range(0, tris.TriangleCount).Select(i => FacetAt(tris, i)).ToList();
            return binary ? StlBinary(name, facets) : Encoding.UTF8.GetBytes(StlText(name, facets));
        }

        /// <summary>Polygon file format: corners with their normals and coordinates, and the corners each triangle uses.</summary>
        public byte[] ToPly(bool binary = true)
        {
            var tris = mesh.Triangulate();
            var header = Encoding.UTF8.GetBytes(PlyHeaderText(tris, binary));
            return binary ? [.. header, .. PlyBinaryBody(tris)] : [.. header, .. Encoding.UTF8.GetBytes(PlyAsciiBody(tris))];
        }
    }

    /// <summary>
    /// The box a model occupies, without loading the model. glTF requires every POSITION accessor to
    /// carry its own minimum and maximum, so a reader can frame or pick against a file it has not
    /// decoded. The file's own node transforms are not applied.
    /// </summary>
    public static (Vector3 Min, Vector3 Max)? BoundsOfGlb(byte[] glb) => BoundsOfDocument(GltfFiles.GlbChunks(glb)?.Json);

    /// <summary>The same, for a <c>.gltf</c>, which keeps its JSON in the open.</summary>
    public static (Vector3 Min, Vector3 Max)? BoundsOfGltf(string gltf) => BoundsOfDocument(Document(gltf));

    private static void Line(StringBuilder text, string line) => text.Append(line).Append('\n');

    private static string Triple(Vector3 at) => $"{Number(at.X)} {Number(at.Y)} {Number(at.Z)}";

    /// <summary>
    /// A number as a text format wants it: short, and never in exponent form. <c>1e-7</c> is legal
    /// in every one of these formats and refused by a surprising number of the things that read them.
    /// </summary>
    private static string Number(double value)
    {
        if (!double.IsFinite(value))
        {
            return "0";
        }

        if (value == Math.Round(value, MidpointRounding.AwayFromZero) && Math.Abs(value) < 1e9)
        {
            return Doubles.ToStringAsFixed(value, 1);
        }

        // Trailing zeros off, but never the whole fractional part: "1." is not a number to some readers.
        var trimmed = Doubles.ToStringAsFixed(value, 6).TrimEnd('0');
        return trimmed.EndsWith('.') ? trimmed + "0" : trimmed;
    }

    /// <summary>A name with the spaces taken out, because these formats separate by them.</summary>
    private static string Word(string name)
    {
        var cleaned = new StringBuilder();
        var inSpace = false;
        foreach (var c in DartText.Trim(name))
        {
            if (!DartText.IsPatternSpace(c))
            {
                cleaned.Append(c);
            }
            else if (!inSpace)
            {
                cleaned.Append('_');
            }

            inSpace = DartText.IsPatternSpace(c);
        }

        return cleaned.Length == 0 ? "unnamed" : cleaned.ToString();
    }

    private readonly record struct ObjCorner(int Uv, int Normal);

    private sealed record ObjCorners(List<string> Uvs, List<string> Normals, List<ObjCorner[]> PerFace);

    private static ObjCorners CornersOf(Mesh mesh)
    {
        var result = new ObjCorners([], [], []);
        foreach (var face in mesh.Faces)
        {
            var normal = mesh.NormalOf(face);
            var coordinates = face.Uv.ForFace(mesh.PointsOf(face), normal);
            var corners = new ObjCorner[face.Length];
            for (var i = 0; i < face.Length; i++)
            {
                var uv = i < coordinates.Count ? coordinates[i] : Vector2.Zero();
                result.Uvs.Add($"vt {Number(uv.X)} {Number(uv.Y)}");
                result.Normals.Add($"vn {Triple(normal)}");
                corners[i] = new ObjCorner(result.Uvs.Count, result.Normals.Count);
            }

            result.PerFace.Add(corners);
        }

        return result;
    }

    private static void WriteFaces(StringBuilder text, Mesh mesh, List<ObjCorner[]> perFace, IReadOnlyList<GlbMaterial> materials)
    {
        // Grouped by material, so a reader makes one object a material rather than switching back
        // and forth down the file. A stable sort, which Dart's is only up to 32 faces (decision 75).
        var order = Enumerable.Range(0, mesh.FaceCount).OrderBy(i => mesh.Faces[i].Material);
        var wearing = -1;
        foreach (var at in order)
        {
            var face = mesh.Faces[at];
            if (face.Material != wearing)
            {
                wearing = face.Material;
                Line(text, $"usemtl {Word(MaterialName(wearing, materials))}");
                Line(text, face.Smooth ? "s 1" : "s off");
            }

            // One-based, which OBJ has been since 1986.
            var parts = perFace[at].Select((corner, i) => $"{face.Vertices[i] + 1L}/{corner.Uv}/{corner.Normal}");
            Line(text, $"f {string.Join(' ', parts)}");
        }
    }

    private static string MaterialName(int at, IReadOnlyList<GlbMaterial> materials) =>
        at >= 0 && at < materials.Count ? materials[at].Name : $"material{at}";

    private static string Mtl(IReadOnlyList<GlbMaterial> materials)
    {
        var text = new StringBuilder();
        Line(text, "# Written by Orblit");
        foreach (var one in materials)
        {
            var c = one.Colour;
            var smooth = 1 - one.Roughness;
            var metal = Number(one.Metallic);
            Line(text, $"newmtl {Word(one.Name)}");
            Line(text, $"Kd {Number(c[0])} {Number(c[1])} {Number(c[2])}");

            // Roughness the other way round, which is what an exponent is. The mapping is a
            // convention and not a conversion: OBJ predates anything physically based.
            Line(text, $"Ns {Number((smooth * smooth * 900) + 1)}");
            Line(text, $"Ks {metal} {metal} {metal}");

            // The physically based pair, which Blender writes and ReadObj trusts over the two lines above.
            Line(text, $"Pr {Number(one.Roughness)}");
            Line(text, $"Pm {metal}");
            Line(text, $"Ke {Number(one.Emissive[0])} {Number(one.Emissive[1])} {Number(one.Emissive[2])}");
            if (c.Count > 3 && c[3] < 1)
            {
                Line(text, $"d {Number(c[3])}");
            }

            Line(text, "illum 2");
        }

        return text.ToString();
    }

    private readonly record struct Facet(Vector3 Normal, Vector3 A, Vector3 B, Vector3 C);

    private static Facet FacetAt(Triangles tris, int i)
    {
        var a = Corner(tris, tris.Indices[i * 3]);
        var b = Corner(tris, tris.Indices[(i * 3) + 1]);
        var c = Corner(tris, tris.Indices[(i * 3) + 2]);
        var normal = (b - a).Cross(c - a);
        if (normal.Length2 > EmptyNormal)
        {
            normal.Normalize();
        }

        return new Facet(normal, a, b, c);
    }

    private static Vector3 Corner(Triangles tris, uint at)
    {
        var start = (long)at * 3;
        return new Vector3(tris.Positions[start], tris.Positions[start + 1], tris.Positions[start + 2]);
    }

    private static string StlText(string name, List<Facet> facets)
    {
        var text = new StringBuilder();
        Line(text, $"solid {Word(name)}");
        foreach (var facet in facets)
        {
            Line(text, $"  facet normal {Triple(facet.Normal)}");
            Line(text, "    outer loop");
            foreach (var at in new[] { facet.A, facet.B, facet.C })
            {
                Line(text, $"      vertex {Triple(at)}");
            }

            Line(text, "    endloop");
            Line(text, "  endfacet");
        }

        Line(text, $"endsolid {Word(name)}");
        return text.ToString();
    }

    private static byte[] StlBinary(string name, List<Facet> facets)
    {
        var bytes = new byte[84 + (facets.Count * 50)];

        // Eighty bytes of anything, by convention not starting with "solid": a binary file that
        // does is read as text by half the readers there are. A long name is cut at 80 UTF-16 units
        // and then at 80 bytes, as the Dart writer does.
        Encoding.UTF8.GetBytes($"Orblit {name}".PadRight(80)[..80]).AsSpan(0, 80).CopyTo(bytes);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(80), (uint)facets.Count);

        var at = 84;
        foreach (var facet in facets)
        {
            foreach (var point in new[] { facet.Normal, facet.A, facet.B, facet.C })
            {
                at = PutFloats(bytes, at, point.X, point.Y, point.Z);
            }

            // Two bytes of attribute that nothing reads.
            at += 2;
        }

        return bytes;
    }

    // F5: writes the floats and hands back where the next one goes, as a stream's position would.
    private static int PutFloats(byte[] bytes, int at, params double[] values)
    {
        foreach (var value in values)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(at), Float32.Quiet((float)value));
            at += 4;
        }

        return at;
    }

    private static string PlyHeaderText(Triangles tris, bool binary)
    {
        var text = new StringBuilder();
        Line(text, "ply");
        Line(text, binary ? "format binary_little_endian 1.0" : "format ascii 1.0");
        Line(text, "comment Written by Orblit");
        Line(text, $"element vertex {tris.VertexCount}");
        foreach (var property in new[] { "x", "y", "z", "nx", "ny", "nz", "s", "t" })
        {
            Line(text, $"property float {property}");
        }

        Line(text, $"element face {tris.TriangleCount}");

        // uchar for the count and int for the indices, which is the combination every reader
        // handles. A list of any other pair is legal and half of them will not open it.
        Line(text, "property list uchar int vertex_indices");
        Line(text, "end_header");
        return text.ToString();
    }

    private static string PlyAsciiBody(Triangles tris)
    {
        var text = new StringBuilder();
        for (var i = 0; i < tris.VertexCount; i++)
        {
            var row = new[]
            {
                tris.Positions[i * 3], tris.Positions[(i * 3) + 1], tris.Positions[(i * 3) + 2],
                tris.Normals[i * 3], tris.Normals[(i * 3) + 1], tris.Normals[(i * 3) + 2],
                tris.Uvs[i * 2], tris.Uvs[(i * 2) + 1],
            };
            Line(text, string.Join(' ', row.Select(value => Number(value))));
        }

        for (var i = 0; i < tris.TriangleCount; i++)
        {
            Line(text, $"3 {tris.Indices[i * 3]} {tris.Indices[(i * 3) + 1]} {tris.Indices[(i * 3) + 2]}");
        }

        return text.ToString();
    }

    private static byte[] PlyBinaryBody(Triangles tris)
    {
        var body = new byte[(tris.VertexCount * 32) + (tris.TriangleCount * 13)];
        var at = 0;
        for (var i = 0; i < tris.VertexCount; i++)
        {
            at = PutFloats(
                body,
                at,
                tris.Positions[i * 3],
                tris.Positions[(i * 3) + 1],
                tris.Positions[(i * 3) + 2],
                tris.Normals[i * 3],
                tris.Normals[(i * 3) + 1],
                tris.Normals[(i * 3) + 2],
                tris.Uvs[i * 2],
                tris.Uvs[(i * 2) + 1]);
        }

        for (var i = 0; i < tris.TriangleCount; i++)
        {
            body[at++] = 3;
            for (var corner = 0; corner < 3; corner++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(body.AsSpan(at), (int)tris.Indices[(i * 3) + corner]);
                at += 4;
            }
        }

        return body;
    }

    /// <summary>A glTF document, or null if that is not what the text is.</summary>
    private static IReadOnlyDictionary<string, object?>? Document(string json)
    {
        try
        {
            return JsonValues.Map(DartJson.Decode(json));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static (Vector3 Min, Vector3 Max)? BoundsOfDocument(IReadOnlyDictionary<string, object?>? document)
    {
        if (document is null)
        {
            return null;
        }

        var accessors = JsonValues.List(document.GetValueOrDefault("accessors"));
        var meshes = JsonValues.List(document.GetValueOrDefault("meshes"));
        if (accessors is null || meshes is null)
        {
            return null;
        }

        double[] min = [double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity];
        double[] max = [double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity];
        var found = false;
        foreach (var (low, high) in PositionBoxes(accessors, meshes))
        {
            found = true;
            for (var axis = 0; axis < 3; axis++)
            {
                min[axis] = Math.Min(min[axis], low[axis]);
                max[axis] = Math.Max(max[axis], high[axis]);
            }
        }

        return found ? (new Vector3(min[0], min[1], min[2]), new Vector3(max[0], max[1], max[2])) : null;
    }

    /// <summary>
    /// The minimum and maximum of each accessor a primitive uses for POSITION. Not every accessor with
    /// a minimum and a maximum: that would fold in normals, which live between minus one and one and
    /// would swallow anything smaller than that.
    /// </summary>
    private static IEnumerable<(double[] Low, double[] High)> PositionBoxes(IReadOnlyList<object?> accessors, IReadOnlyList<object?> meshes)
    {
        foreach (var mesh in meshes)
        {
            var primitives = JsonValues.List(JsonValues.Map(mesh)?.GetValueOrDefault("primitives"));
            foreach (var primitive in primitives ?? [])
            {
                if (PositionBoxOf(primitive, accessors) is { } box)
                {
                    yield return box;
                }
            }
        }
    }

    private static (double[] Low, double[] High)? PositionBoxOf(object? primitive, IReadOnlyList<object?> accessors)
    {
        var attributes = JsonValues.Map(JsonValues.Map(primitive)?.GetValueOrDefault("attributes"));
        return attributes?.GetValueOrDefault("POSITION") is long at && at >= 0 && at < accessors.Count
            ? BoxOf(JsonValues.Map(accessors[(int)at]))
            : null;
    }

    /// <summary>The numbers in an accessor's <c>min</c> and <c>max</c>, or null unless both list only numbers and name three or more.</summary>
    private static (double[] Low, double[] High)? BoxOf(IReadOnlyDictionary<string, object?>? accessor)
    {
        var low = JsonValues.List(accessor?.GetValueOrDefault("min"));
        var high = JsonValues.List(accessor?.GetValueOrDefault("max"));
        if (low is null || high is null || low.Count < 3 || high.Count < 3)
        {
            return null;
        }

        var lows = low.Select(JsonValues.Num).ToList();
        var highs = high.Select(JsonValues.Num).ToList();
        return lows.Contains(null) || highs.Contains(null)
            ? null
            : ([lows[0]!.Value, lows[1]!.Value, lows[2]!.Value], [highs[0]!.Value, highs[1]!.Value, highs[2]!.Value]);
    }
}
