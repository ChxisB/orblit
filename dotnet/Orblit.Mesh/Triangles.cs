using Orblit.VectorMath;

namespace Orblit.Mesh;

/// <summary>One stretch of <see cref="Triangles.Indices"/> and the material it wears.</summary>
public readonly record struct TriangleGroup(int Material, int Start, int Count);

/// <summary>
/// A mesh as a renderer takes it: triangles, with a normal and a texture coordinate on every
/// corner. The other direction from everything else here. Editing works on faces of any number of
/// corners because that is what somebody drew; drawing works on triangles with one normal each
/// because that is what a graphics card takes. This is the one place the two meet.
/// </summary>
public sealed class Triangles(
    float[] positions,
    float[] normals,
    float[] uvs,
    uint[] indices,
    IReadOnlyList<TriangleGroup>? groups = null)
{
    /// <summary>Three floats a vertex.</summary>
    public float[] Positions { get; } = positions;

    /// <summary>Three floats a vertex, in the same order.</summary>
    public float[] Normals { get; } = normals;

    /// <summary>Two floats a vertex.</summary>
    public float[] Uvs { get; } = uvs;

    /// <summary>
    /// Thirty-two bits each, because sixteen is not enough. A block world's surface is a few
    /// hundred thousand vertices, and at sixty-five thousand and thirty-six an unsigned short
    /// wraps: the file writes, the loader reads it without complaint, and the mesh draws nothing.
    /// Always thirty-two, rather than a second code path that only small meshes ever take.
    /// </summary>
    public uint[] Indices { get; } = indices;

    /// <summary>
    /// Which stretch of <see cref="Indices"/> wears which material. A run rather than a material
    /// per triangle: faces are sorted by material when they are triangulated, so every material's
    /// triangles end up together and one number a group says everything.
    /// </summary>
    public IReadOnlyList<TriangleGroup> Groups { get; } = groups ?? [];

    /// <summary>
    /// The groups as something to walk. A mesh nobody gave materials to is one run of everything,
    /// so there is no second case to write anywhere else.
    /// </summary>
    public IReadOnlyList<TriangleGroup> Runs =>
        Groups.Count == 0 ? [new TriangleGroup(0, 0, Indices.Length)] : Groups;

    public int VertexCount => Positions.Length / 3;

    public int TriangleCount => Indices.Length / 3;
}

/// <summary>Turning faces into triangles.</summary>
public static class MeshTriangles
{
    extension(Mesh mesh)
    {
        /// <summary>
        /// The mesh, ready to draw. Corners are not shared between faces unless the faces are
        /// smooth. A cube has eight corners and twenty-four vertices, because each one needs three
        /// different normals: sharing them is what makes a box look like a deflated ball.
        /// </summary>
        public Triangles Triangulate()
        {
            var soup = new Soup();

            // Smooth faces average their normals per shared corner, so a cylinder's sides look
            // round while its ends stay flat.
            var smoothed = SmoothNormals(mesh);

            // In material order, so each material's triangles are one run. Sorted here rather than
            // asked of the caller. Stable, so faces of one material keep their order.
            var group = -1;
            foreach (var face in mesh.Faces.OrderBy(one => one.Material))
            {
                if (face.Vertices.Count < 3)
                {
                    continue;
                }

                if (face.Material != group)
                {
                    group = face.Material;
                    soup.Groups.Add(new TriangleGroup(group, soup.Indices.Count, 0));
                }

                soup.Add(mesh, face, smoothed);
                var last = soup.Groups[^1];
                soup.Groups[^1] = last with { Count = soup.Indices.Count - last.Start };
            }

            return soup.ToTriangles();
        }
    }

    /// <summary>A normal per point, averaged over the smooth faces using it.</summary>
    private static Dictionary<int, Vector3> SmoothNormals(Mesh mesh)
    {
        var sums = new Dictionary<int, Vector3>();
        foreach (var face in mesh.Faces.Where(one => one.Smooth))
        {
            var normal = mesh.NormalOf(face);

            // Weighted by area, so a long thin face does not count as much as the broad one beside
            // it, which is what makes a cylinder's seam invisible.
            var weight = mesh.AreaOf(face);
            foreach (var index in face.Vertices)
            {
                if (!sums.TryGetValue(index, out var sum))
                {
                    sums[index] = sum = Vector3.Zero();
                }

                sum.Add(normal * weight);
            }
        }

        return sums.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.Length2 < 1e-20 ? new Vector3(0, 1, 0) : entry.Value.Normalized());
    }

    /// <summary>
    /// Cuts a face into triangles, whatever shape its outline is. A fan from the first corner is
    /// right only for a convex face, and the faces an editor makes are routinely not: an L-shaped
    /// room is concave the moment it is drawn. Ear clipping instead. Quadratic in the number of
    /// corners, which is nothing for the ten or twenty a face has, and it handles the keyhole
    /// outlines a cut produces. Returns positions within the face's own corner list, three at a
    /// time.
    /// </summary>
    public static List<int> CutUp(IReadOnlyList<Vector3> points, Vector3 normal)
    {
        var count = points.Count;
        if (count < 3)
        {
            return [];
        }

        if (count == 3)
        {
            return [0, 1, 2];
        }

        var flat = Flatten(points, normal);

        // Worked on anticlockwise whichever way it came in, and the result mapped back at the end:
        // one direction to reason about rather than two.
        var left = Enumerable.Range(0, count).ToList();
        if (TwiceArea(flat) < 0)
        {
            left.Reverse();
        }

        var output = new List<int>();

        // Every pass removes one corner or gives up. Without the bound a self-intersecting
        // outline, which a cut can leave and a drawing can be given, would spin here forever.
        var guard = (long)count * count;
        while (left.Count > 3 && guard-- > 0)
        {
            // No ear anywhere: the outline crosses itself or has no area. A fan over what is left
            // is wrong, but it is visible and it is not a crash, which beats leaving a hole where
            // a face should be.
            if (EarOf(flat, left) is not { } ear)
            {
                break;
            }

            var n = left.Count;
            output.AddRange([left[(ear - 1 + n) % n], left[ear], left[(ear + 1) % n]]);
            left.RemoveAt(ear);
        }

        for (var i = 1; i + 1 < left.Count; i++)
        {
            output.AddRange([left[0], left[i], left[i + 1]]);
        }

        return output;
    }

    /// <summary>A number the tests use to say two directions are the same.</summary>
    public static double AngleBetween(Vector3 a, Vector3 b) =>
        Math.Acos(Doubles.Clamp(a.Normalized().Dot(b.Normalized()), -1.0, 1.0));

    /// <summary>
    /// Flattened onto the face's own plane. The axes are right-handed about the normal, so a face
    /// wound anticlockwise about it is anticlockwise here too and its area is positive.
    /// </summary>
    private static List<Vector2> Flatten(IReadOnlyList<Vector3> points, Vector3 normal)
    {
        var axes = FaceUv.AxesFor(normal);
        return [.. points.Select(at => new Vector2(at.Dot(axes.U), at.Dot(axes.V)))];
    }

    private static double TwiceArea(List<Vector2> flat)
    {
        var area = 0.0;
        for (var i = 0; i < flat.Count; i++)
        {
            var a = flat[i];
            var b = flat[(i + 1) % flat.Count];
            area += (a.X * b.Y) - (b.X * a.Y);
        }

        return area;
    }

    private static double Cross(Vector2 a, Vector2 b, Vector2 c) =>
        ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));

    private static bool Same(Vector2 a, Vector2 b) => (a - b).Length2 < 1e-18;

    /// <summary>
    /// A corner standing exactly on one of the ear's own is not in the way of it. This is what
    /// makes a keyhole outline work at all: the slit is two pairs of coincident corners, and
    /// counting each as blocking its own triangle leaves the whole face with no ear anywhere.
    /// Otherwise, on an edge counts as in: a corner lying exactly on the far side of a candidate
    /// ear makes that ear unsafe, and clipping it would cross the outline.
    /// </summary>
    private static bool Inside(Vector2 a, Vector2 b, Vector2 c, Vector2 at) =>
        !(Same(at, a) || Same(at, b) || Same(at, c))
        && Cross(a, b, at) >= 0
        && Cross(b, c, at) >= 0
        && Cross(c, a, at) >= 0;

    /// <summary>The first corner of <paramref name="left"/> that can be clipped, or null when none can.</summary>
    private static int? EarOf(List<Vector2> flat, List<int> left)
    {
        var n = left.Count;
        for (var i = 0; i < n; i++)
        {
            var before = (i - 1 + n) % n;
            var after = (i + 1) % n;

            // A reflex corner is a dent, and the triangle across it is outside the face rather
            // than part of it.
            if (Cross(flat[left[before]], flat[left[i]], flat[left[after]]) > 0 && IsClear(flat, left, i))
            {
                return i;
            }
        }

        return null;
    }

    private static bool IsClear(List<Vector2> flat, List<int> left, int i)
    {
        var n = left.Count;
        var before = (i - 1 + n) % n;
        var after = (i + 1) % n;
        for (var j = 0; j < n; j++)
        {
            if (j != i && j != before && j != after
                && Inside(flat[left[before]], flat[left[i]], flat[left[after]], flat[left[j]]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The corners laid down so far, as lists a <see cref="Triangles"/> is made from.</summary>
    private sealed class Soup
    {
        private readonly List<float> _positions = [];
        private readonly List<float> _normals = [];
        private readonly List<float> _uvs = [];

        internal List<uint> Indices { get; } = [];

        internal List<TriangleGroup> Groups { get; } = [];

        internal void Add(Mesh mesh, Face face, Dictionary<int, Vector3> smoothed)
        {
            var normal = mesh.NormalOf(face);
            var first = _positions.Count / 3;

            // Whatever the face says its coordinates are: a rule projected flat, or the ones
            // somebody drew.
            var coordinates = face.Uv.ForFace(mesh.PointsOf(face), normal);
            for (var corner = 0; corner < face.Vertices.Count; corner++)
            {
                var index = face.Vertices[corner];
                var at = mesh.Positions[index];
                var n = face.Smooth ? smoothed.GetValueOrDefault(index) ?? normal : normal;
                var uv = corner < coordinates.Count ? coordinates[corner] : Vector2.Zero();
                _positions.AddRange([(float)at.X, (float)at.Y, (float)at.Z]);
                _normals.AddRange([(float)n.X, (float)n.Y, (float)n.Z]);
                _uvs.AddRange([(float)uv.X, (float)uv.Y]);
            }

            foreach (var corner in CutUp(mesh.PointsOf(face), normal))
            {
                Indices.Add(unchecked((uint)(first + corner)));
            }
        }

        internal Triangles ToTriangles() => new([.. _positions], [.. _normals], [.. _uvs], [.. Indices], Groups);
    }
}

/// <summary>
/// One material, as much of it as a <c>.glb</c> can carry. Deliberately glTF's own set and no
/// more. A material in the editor may describe things this cannot, such as a texture's tiling, an
/// alpha cut-off, or which way it faces, and those belong on the object because they are the
/// renderer's business rather than the file's.
/// </summary>
public sealed record GlbMaterial
{
    public string Name { get; init; } = "material";

    /// <summary>Linear red, green, blue and alpha.</summary>
    public IReadOnlyList<double> Colour { get; init; } = [0.8, 0.8, 0.8, 1.0];

    public double Metallic { get; init; }

    public double Roughness { get; init; } = 0.5;

    public IReadOnlyList<double> Emissive { get; init; } = [0.0, 0.0, 0.0];

    public bool DoubleSided { get; init; }

    /// <summary>
    /// Whether it is punched out by its alpha rather than blended. The two glTF answers that need
    /// no extra state; anything softer is a renderer setting and not a property of the file.
    /// </summary>
    public bool Cutout { get; init; }

    public OrderedDictionary<string, object?> ToGltf()
    {
        var json = new OrderedDictionary<string, object?>
        {
            ["name"] = Name,
            ["pbrMetallicRoughness"] = new OrderedDictionary<string, object?>
            {
                ["baseColorFactor"] = Boxed(Colour),
                ["metallicFactor"] = Metallic,
                ["roughnessFactor"] = Roughness,
            },
        };
        JsonValues.Put(json, Emissive.Any(one => one > 0), "emissiveFactor", Boxed(Emissive));
        JsonValues.Put(json, DoubleSided, "doubleSided", true);
        JsonValues.Put(json, Cutout, "alphaMode", "MASK");
        JsonValues.Put(json, Cutout, "alphaCutoff", 0.5);
        return json;
    }

    private static List<object?> Boxed(IReadOnlyList<double> values) => [.. values.Select(one => (object?)one)];
}

/// <summary>
/// Writing a mesh as a <c>.glb</c>, which the renderer already knows how to load. A shape made in
/// the editor becomes a file, and everything downstream treats it exactly as it treats a model
/// somebody exported from Blender. The alternative is a second path through the renderer for
/// geometry that came from here.
/// </summary>
public static class MeshGlb
{
    extension(Mesh mesh)
    {
        /// <summary>This mesh as a binary glTF file.</summary>
        public byte[] ToGlb(string name = "mesh", IReadOnlyList<GlbMaterial>? materials = null)
        {
            var tris = mesh.Triangulate();

            // Nothing to draw. An empty scene is a file every loader opens; a file whose accessors
            // count nought elements and whose buffer views are nought bytes long is one the
            // validator rejects, so the mesh nobody has drawn in yet leaves as the former.
            if (tris.Indices.Length == 0 || tris.VertexCount == 0)
            {
                return GltfFiles.GlbBytes(
                    new OrderedDictionary<string, object?>
                    {
                        ["asset"] = Asset(),
                        ["scene"] = 0L,
                        ["scenes"] = Scenes(),
                        ["nodes"] = new List<object?> { new OrderedDictionary<string, object?> { ["name"] = name } },
                    },
                    []);
            }

            return WriteGlb(tris, name, materials ?? []);
        }
    }

    private static byte[] WriteGlb(Triangles tris, string name, IReadOnlyList<GlbMaterial> materials)
    {
        // One primitive a material, which is what a glTF loader turns into one draw call each. A
        // face pointing at a material nobody listed falls back to the file's default rather than
        // to nothing: a wrong colour is recoverable and a missing surface is not.
        var found = tris.Runs.Where(group => group.Count > 0).ToList();

        // An empty mesh still has to be a valid file, and a file with no primitives is not one.
        List<TriangleGroup> used = found.Count == 0 ? [new TriangleGroup(0, 0, tris.Indices.Length)] : found;
        var wanted = used.Select(group => group.Material).ToHashSet();
        var listed = Enumerable.Range(0, materials.Count).Where(wanted.Contains).ToList();
        var slotOf = listed.Select((material, slot) => (material, slot)).ToDictionary(pair => pair.material, pair => pair.slot);

        // Indices first. A buffer view starts on a four-byte boundary, and four-byte indices
        // always end on one, so laying them down before the float attributes means nothing is
        // ever padded between the two.
        var buffer = new GltfBuffer();
        var indices = buffer.AddView<uint>(tris.Indices, GltfTarget.ElementArrayBuffer);

        // One accessor a group, all over that one view: the indices are already sorted so each
        // material's are a contiguous run, and a byte offset is cheaper than a second copy of them.
        var runs = used
            .Select(group => buffer.AddAccessor(indices, GltfComponent.UnsignedInt, group.Count, "SCALAR", byteOffset: group.Start * 4))
            .ToList();
        var position = buffer.AddPositions(tris.Positions);
        var normal = buffer.AddFloats(tris.Normals, "VEC3", GltfTarget.ArrayBuffer);
        var uv = buffer.AddFloats(tris.Uvs, "VEC2", GltfTarget.ArrayBuffer);

        var json = new OrderedDictionary<string, object?>
        {
            ["asset"] = Asset(),
            ["scene"] = 0L,
            ["scenes"] = Scenes(),
            ["nodes"] = new List<object?>
            {
                new OrderedDictionary<string, object?> { ["mesh"] = 0L, ["name"] = name },
            },
            ["meshes"] = new List<object?>
            {
                new OrderedDictionary<string, object?>
                {
                    ["name"] = name,
                    ["primitives"] = used
                        .Select((group, i) => (object?)Primitive(group, runs[i], slotOf, position, normal, uv))
                        .ToList(),
                },
            },
        };
        JsonValues.Put(json, listed.Count > 0, "materials", listed.Select(at => (object?)materials[at].ToGltf()).ToList());
        json["accessors"] = buffer.Accessors;
        json["bufferViews"] = buffer.Views;
        json["buffers"] = buffer.Buffers;
        return GltfFiles.GlbBytes(json, buffer.Bytes);
    }

    private static OrderedDictionary<string, object?> Primitive(
        TriangleGroup group,
        int indices,
        Dictionary<int, int> slotOf,
        int position,
        int normal,
        int uv)
    {
        var primitive = new OrderedDictionary<string, object?>
        {
            ["attributes"] = new OrderedDictionary<string, object?>
            {
                ["POSITION"] = (long)position,
                ["NORMAL"] = (long)normal,
                ["TEXCOORD_0"] = (long)uv,
            },
            ["indices"] = (long)indices,
            ["mode"] = 4L,
        };
        if (slotOf.TryGetValue(group.Material, out var slot))
        {
            primitive["material"] = (long)slot;
        }

        return primitive;
    }

    private static OrderedDictionary<string, object?> Asset() =>
        new() { ["version"] = "2.0", ["generator"] = "Orblit" };

    private static List<object?> Scenes() =>
        [new OrderedDictionary<string, object?> { ["nodes"] = new List<object?> { 0L } }];
}
