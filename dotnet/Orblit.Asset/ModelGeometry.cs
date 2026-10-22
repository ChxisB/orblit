using Orblit.VectorMath;

namespace Orblit.Asset;

/// <summary>
/// Walks the active scene of a glTF and bakes every mesh into one list of triangles in the model's
/// frame.
/// </summary>
internal sealed class ModelGeometry(GltfDocument document)
{
    // No real model nests this deep. The walk is recursive, and a .NET stack that runs out ends the
    // process, which an error cannot catch.
    private const int MaxDepth = 512;

    private static readonly string[] ListKeys = ["nodes", "scenes", "meshes", "bufferViews"];

    private static readonly double[] IdentityValues = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];

    private readonly List<GltfMap> _nodes = document.List("nodes");
    private readonly List<GltfMap> _meshes = document.List("meshes");
    private readonly List<double> _vertices = [];
    private readonly List<int> _indices = [];
    private readonly HashSet<int> _visiting = [];

    public CollisionMesh Read()
    {
        Validate();
        var scenes = document.List("scenes");
        if (scenes.Count > 0)
        {
            ReadScene(scenes);
        }
        else if (_nodes.Count > 0)
        {
            ReadRoots();
        }
        else
        {
            for (var i = 0; i < _meshes.Count; i++)
            {
                ReadMesh(i, Matrix4.Identity());
            }
        }

        return new CollisionMesh(_vertices, _indices);
    }

    private void ReadScene(List<GltfMap> scenes)
    {
        var selected = ReadIndex(GltfJson.Get(document.Json, "scene")) ?? 0;
        if (selected < 0 || selected >= scenes.Count)
        {
            throw new FormatException("Invalid scene.");
        }

        foreach (var node in Integers(GltfJson.Get(scenes[selected], "nodes")))
        {
            ReadNode(node, Matrix4.Identity());
        }
    }

    // A scene is not required, and a model without one is every node nothing else holds.
    private void ReadRoots()
    {
        var children = _nodes.SelectMany(node => Integers(GltfJson.Get(node, "children"))).ToHashSet();
        for (var i = 0; i < _nodes.Count; i++)
        {
            if (!children.Contains(i))
            {
                ReadNode(i, Matrix4.Identity());
            }
        }
    }

    private void Validate()
    {
        foreach (var key in ListKeys)
        {
            var list = GltfJson.Get(document.Json, key);
            if (list is not null && (list is not List<object?> entries || entries.Any(v => v is not GltfMap)))
            {
                throw new FormatException($"Invalid model {key}.");
            }
        }

        foreach (var view in document.List("bufferViews"))
        {
            if (HasExtension(view, "EXT_meshopt_compression") || HasExtension(view, "KHR_meshopt_compression"))
            {
                throw new FormatException("Compressed collision geometry is unsupported.");
            }
        }
    }

    private static bool HasExtension(GltfMap value, string name) =>
        GltfJson.Get(value, "extensions") is GltfMap extensions && extensions.ContainsKey(name);

    private void ReadNode(int index, Matrix4 parent)
    {
        if (index < 0 || index >= _nodes.Count || _visiting.Count >= MaxDepth || !_visiting.Add(index))
        {
            throw new FormatException("Invalid or cyclic model hierarchy.");
        }

        var node = _nodes[index];
        if (node.ContainsKey("skin"))
        {
            throw new FormatException("Skinned collision geometry is unsupported.");
        }

        var world = parent.Clone();
        world.Multiply(Transform(node));
        if (ReadIndex(GltfJson.Get(node, "mesh")) is { } mesh)
        {
            ReadMesh(mesh, world);
        }

        foreach (var child in Integers(GltfJson.Get(node, "children")))
        {
            ReadNode(child, world);
        }

        _visiting.Remove(index);
    }

    private void ReadMesh(int index, Matrix4 world)
    {
        if (index < 0 || index >= _meshes.Count)
        {
            throw new FormatException("Invalid mesh.");
        }

        if (GltfJson.Get(_meshes[index], "primitives") is not List<object?> primitives)
        {
            throw new FormatException("Missing mesh primitives.");
        }

        foreach (var value in primitives)
        {
            if (value is not GltfMap primitive)
            {
                throw new FormatException("Invalid primitive.");
            }

            ReadPrimitive(primitive, world);
        }
    }

    private void ReadPrimitive(GltfMap primitive, Matrix4 world)
    {
        var mode = ReadIndex(GltfJson.Get(primitive, "mode")) ?? 4;
        if (mode < 4 || mode > 6)
        {
            return;
        }

        var points = document.ReadVec3(PositionsOf(primitive));
        var count = points.Length / 3;
        var first = _vertices.Count / 3;
        AddPoints(points, world);
        var ids = ReadIndex(GltfJson.Get(primitive, "indices")) is { } accessor
            ? document.ReadIndices(accessor).Select(i => unchecked((int)i)).ToArray()
            : Enumerable.Range(0, count).ToArray();
        if (ids.Any(i => i < 0 || i >= count))
        {
            throw new FormatException("Collision index is outside its primitive.");
        }

        AddTriangles(first, Cut(ids, mode), world.Determinant() < 0);
    }

    // The accessor of a primitive's positions, after the refusals that need no geometry read.
    private static int PositionsOf(GltfMap primitive)
    {
        if (HasExtension(primitive, "KHR_draco_mesh_compression"))
        {
            throw new FormatException("Compressed collision geometry is unsupported.");
        }

        if (primitive.ContainsKey("targets"))
        {
            throw new FormatException("Morph collision geometry is unsupported.");
        }

        return GltfJson.Get(primitive, "attributes") is GltfMap attributes
            && GltfJson.IsInt(GltfJson.Get(attributes, "POSITION"))
                ? GltfJson.Saturated(attributes["POSITION"])
                : throw new FormatException("Missing collision positions.");
    }

    private void AddPoints(float[] points, Matrix4 world)
    {
        for (var i = 0; i < points.Length; i += 3)
        {
            var point = world.Transformed3(new Vector3(points[i], points[i + 1], points[i + 2]));
            _vertices.AddRange([point.X, point.Y, point.Z]);
        }
    }

    // A mirror turns every triangle inside out, so two of its corners swap to keep the front face out.
    private void AddTriangles(int first, List<int> triangles, bool mirrored)
    {
        for (var i = 0; i < triangles.Count; i += 3)
        {
            _indices.Add(first + triangles[i]);
            _indices.Add(first + triangles[i + (mirrored ? 2 : 1)]);
            _indices.Add(first + triangles[i + (mirrored ? 1 : 2)]);
        }
    }

    // Mode 4 is a list of triangles, 5 a strip and 6 a fan. A strip turns every second triangle over,
    // so each one is written the other way round to keep the winding the same.
    private static List<int> Cut(int[] ids, int mode)
    {
        if (mode == 4)
        {
            return ids.Length % 3 == 0 ? [.. ids] : throw new FormatException("Incomplete triangles.");
        }

        var triangles = new List<int>();
        for (var i = 2; i < ids.Length; i++)
        {
            var even = i % 2 == 0;
            triangles.Add(mode == 6 ? ids[0] : ids[i - (even ? 2 : 1)]);
            triangles.Add(ids[i - (mode == 6 || even ? 1 : 2)]);
            triangles.Add(ids[i]);
        }

        return triangles;
    }

    private static int? ReadIndex(object? raw) => raw is null
        ? null
        : GltfJson.IsInt(raw) ? GltfJson.Saturated(raw) : throw new FormatException("Expected an integer model index.");

    private static List<int> Integers(object? raw)
    {
        if (raw is null)
        {
            return [];
        }

        return raw is List<object?> list && list.All(GltfJson.IsInt)
            ? [.. list.Select(GltfJson.Saturated)]
            : throw new FormatException("Expected node indices.");
    }

    private static double[] Numbers(object? raw, double[] fallback)
    {
        if (raw is null)
        {
            return fallback;
        }

        if (raw is not List<object?> list || list.Count != fallback.Length)
        {
            throw new FormatException("Invalid model transform.");
        }

        var numbers = list.Select(GltfJson.AsNumber).OfType<double>().Where(double.IsFinite).ToArray();
        return numbers.Length == fallback.Length ? numbers : throw new FormatException("Invalid model transform.");
    }

    private static Matrix4 Transform(GltfMap node)
    {
        if (node.TryGetValue("matrix", out var raw))
        {
            var values = Numbers(raw, IdentityValues);
            var matrix = Matrix4.Zero();
            for (var i = 0; i < values.Length; i++)
            {
                matrix[i] = values[i];
            }

            return matrix;
        }

        var at = Numbers(GltfJson.Get(node, "translation"), [0, 0, 0]);
        var turn = Numbers(GltfJson.Get(node, "rotation"), [0, 0, 0, 1]);
        var scale = Numbers(GltfJson.Get(node, "scale"), [1, 1, 1]);
        var rotation = new Quaternion(turn[0], turn[1], turn[2], turn[3]);
        if (rotation.Length2 == 0)
        {
            throw new FormatException("Zero model rotation.");
        }

        rotation.Normalize();
        return Matrix4.Compose(new Vector3(at[0], at[1], at[2]), rotation, new Vector3(scale[0], scale[1], scale[2]));
    }
}
