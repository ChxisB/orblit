using Orblit.VectorMath;

namespace Orblit.Asset;

/// <summary>One primitive of the model, with the world transform of the node that draws it.</summary>
internal sealed record BakePart(string Key, Matrix4 World, BakeSource Source);

/// <summary>The primitives a model draws, read from the nodes of its active scene.</summary>
internal sealed class BakeParts(GltfDocument document)
{
    private static readonly string[] Refused = ["EXT_meshopt_compression", "KHR_draco_mesh_compression"];

    private static readonly string[] FloatAttributes = ["POSITION", "NORMAL", "TANGENT"];

    private readonly List<GltfMap> _nodes = document.List("nodes");
    private readonly List<GltfMap> _meshes = document.List("meshes");
    private readonly List<BakePart> _parts = [];
    private readonly HashSet<int> _seen = [];

    /// <summary>Every part of the model, or null when anything in the model rules baking out.</summary>
    public static List<BakePart>? Read(GltfDocument document) => new BakeParts(document).Collect();

    private static Matrix4 Local(GltfMap node)
    {
        if (GltfJson.Get(node, "matrix") is List<object?> { Count: 16 } matrix)
        {
            var result = Matrix4.Zero();
            for (var i = 0; i < 16; i++)
            {
                result[i] = Number(matrix[i]);
            }

            return result;
        }

        return Matrix4.Compose(
            Vector(GltfJson.Get(node, "translation"), 0),
            Quaternion(GltfJson.Get(node, "rotation")),
            Vector(GltfJson.Get(node, "scale"), 1));
    }

    private static double Number(object? value) =>
        GltfJson.AsNumber(value) ?? throw new InvalidCastException($"{GltfJson.Display(value)} is not a number.");

    private static Vector3 Vector(object? list, double fallback) =>
        list is List<object?> { Count: 3 } values
            ? new Vector3(Number(values[0]), Number(values[1]), Number(values[2]))
            : Vector3.All(fallback);

    private static Quaternion Quaternion(object? list) =>
        list is List<object?> { Count: 4 } values
            ? new Quaternion(Number(values[0]), Number(values[1]), Number(values[2]), Number(values[3]))
            : VectorMath.Quaternion.Identity();

    private static List<int> Integers(object? list) =>
        list is List<object?> values ? [.. values.Where(GltfJson.IsInt).Select(GltfJson.Saturated)] : [];

    private List<BakePart>? Collect()
    {
        if (document.List("skins").Count > 0 || document.List("animations").Count > 0 || RequiresCompression())
        {
            return null;
        }

        foreach (var root in Roots())
        {
            if (!Visit(root, Matrix4.Identity()))
            {
                return null;
            }
        }

        return _parts;
    }

    private bool RequiresCompression() =>
        GltfJson.Get(document.Json, "extensionsRequired") is List<object?> required
        && required.Any(name => name is string text && Refused.Contains(text));

    private bool Visit(int index, Matrix4 parent)
    {
        if (index < 0 || index >= _nodes.Count || !_seen.Add(index))
        {
            return false;
        }

        var node = _nodes[index];
        if (GltfJson.Get(node, "extensions") is GltfMap extensions && extensions.ContainsKey("KHR_mesh_gpu_instancing"))
        {
            return false;
        }

        var world = parent.Multiplied(Local(node));
        var mesh = GltfJson.Get(node, "mesh");
        if (GltfJson.IsInt(mesh) && !AddMesh(GltfJson.Saturated(mesh), world))
        {
            return false;
        }

        return GltfJson.Get(node, "children") is not List<object?> children
            || children.All(child => GltfJson.IsInt(child) && Visit(GltfJson.Saturated(child), world));
    }

    private bool AddMesh(int mesh, Matrix4 world)
    {
        var primitives = mesh < _meshes.Count ? GltfJson.Get(_meshes[mesh], "primitives") : null;
        if (primitives is not List<object?> list)
        {
            return false;
        }

        foreach (var primitive in list)
        {
            if (PartOf(primitive, world) is not { } part)
            {
                return false;
            }

            _parts.Add(part);
        }

        return true;
    }

    private BakePart? PartOf(object? primitive, Matrix4 world)
    {
        if (primitive is not GltfMap map || GltfMerge.PrimitiveGroupKey(document, map) is not { } key)
        {
            return null;
        }

        var attributes = (GltfMap)map["attributes"]!;
        foreach (var name in FloatAttributes)
        {
            var index = GltfJson.Get(attributes, name);
            if (GltfJson.IsInt(index)
                && GltfJson.AsNumber(GltfJson.Get(document.Accessor(GltfJson.Saturated(index)), "componentType"))
                    != ComponentType.Float)
            {
                return null;
            }
        }

        return new BakePart(key, world, new BakeSource(document, map));
    }

    // The nodes a scene starts from. A model without a scene starts from every node nothing else holds.
    private List<int> Roots()
    {
        var scenes = document.List("scenes");
        if (scenes.Count > 0)
        {
            var scene = GltfJson.Get(document.Json, "scene");
            var picked = GltfJson.IsInt(scene) && GltfJson.Saturated(scene) < scenes.Count ? GltfJson.Saturated(scene) : 0;
            return Integers(GltfJson.Get(scenes[picked], "nodes"));
        }

        var children = _nodes.SelectMany(node => Integers(GltfJson.Get(node, "children"))).ToHashSet();
        return [.. Enumerable.Range(0, _nodes.Count).Where(i => !children.Contains(i))];
    }
}
