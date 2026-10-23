using Orblit.Rig;

namespace Orblit.Motion;

/// <summary>
/// The nodes of a glTF document: what each is called, how each hangs from the next and where each stands in
/// its parent. A glTF comes from any tool on earth, so every value in one may be missing or the wrong type,
/// and none of that is a reason to stop reading.
/// </summary>
internal sealed class GltfNodes
{
    private const string Unnamed = "<unknown>";

    private readonly Dictionary<long, long> _parentOf = [];

    public GltfNodes(JsonMap json)
    {
        Json = json;
        Nodes = Maps(json.GetValueOrDefault("nodes"));
        for (var at = 0; at < Nodes.Count; at++)
        {
            foreach (var child in Ints(Nodes[at].GetValueOrDefault("children")).Where(child => child >= 0 && child < Nodes.Count))
            {
                _parentOf[child] = at;
            }
        }
    }

    public JsonMap Json { get; }

    public IReadOnlyList<JsonMap> Nodes { get; }

    public static List<JsonMap> Maps(object? raw) => raw is JsonList list ? [.. list.OfType<JsonMap>()] : [];

    public static List<long> Ints(object? raw) => raw is JsonList list ? [.. list.OfType<long>()] : [];

    /// <summary>A whole number that is not negative, or null. Wide, because an index may be anything a file says.</summary>
    public static long? Index(object? raw) => raw is long and >= 0 ? (long)raw : null;

    /// <summary>An index as an <see cref="int"/>. One past the int range is past the end of any list too, so it stays out of range.</summary>
    public static int Saturate(long index) => (int)Math.Min(index, int.MaxValue);

    /// <summary>The node <paramref name="at"/> hangs from, by the children lists, or null for a root.</summary>
    public long? ParentOf(long at) => _parentOf.TryGetValue(at, out var parent) ? parent : null;

    /// <summary>
    /// The names of <paramref name="joints"/> as a model's skeleton names them, no two alike. One function for
    /// everything that names joints, so an imported clip and the model it plays on agree about which bone is
    /// which.
    /// </summary>
    public List<string> JointNames(IReadOnlyList<long> joints) => BoneNaming.Unique([.. joints.Select(NameOf)]);

    /// <summary>The id a scene read from this file gives node <paramref name="at"/>.</summary>
    public string EntityOf(long at)
    {
        var ours = (Nodes[Saturate(at)].GetValueOrDefault("extras") as JsonMap)?.GetValueOrDefault("orblit") as JsonMap;
        return ours?.GetValueOrDefault("id") is string { Length: > 0 } id ? id : $"node-{at}";
    }

    /// <summary>A node's transform in its parent.</summary>
    /// <exception cref="ClipFormatException">When the document names a node it does not have.</exception>
    public Matrix4 LocalOf(long at)
    {
        var node = at >= 0 && at < Nodes.Count
            ? Nodes[(int)at]
            : throw new ClipFormatException($"The document names node {at}, which it does not have.");
        if (Floats(node.GetValueOrDefault("matrix"), 16) is { } matrix)
        {
            return MatrixOf(matrix);
        }

        var position = Floats(node.GetValueOrDefault("translation"), 3);
        var scale = Floats(node.GetValueOrDefault("scale"), 3);
        return Matrix4.Compose(
            position is null ? Vector3.Zero() : new Vector3(position[0], position[1], position[2]),
            TurnOf(Floats(node.GetValueOrDefault("rotation"), 4)),
            scale is null ? Vector3.All(1) : new Vector3(scale[0], scale[1], scale[2]));
    }

    /// <summary>A node's transform in the world.</summary>
    public Matrix4 WorldOf(long at)
    {
        var world = LocalOf(at);
        var seen = new HashSet<long> { at };
        for (var up = ParentOf(at); up is { } above && seen.Add(above); up = ParentOf(above))
        {
            world = LocalOf(above).Multiplied(world);
        }

        return world;
    }

    /// <summary>
    /// A node's name as the renderer reads it, and so as a model's skeleton names its joints: its own, or the
    /// name of the mesh, light or camera it carries, or <c>&lt;unknown&gt;</c>. Anything else and an unnamed
    /// joint would be one bone to the clip and another to the model it plays on.
    /// </summary>
    private string NameOf(long at)
    {
        if (at < 0 || at >= Nodes.Count)
        {
            return Unnamed;
        }

        var node = Nodes[(int)at];
        if (node.GetValueOrDefault("name") is string name)
        {
            return name;
        }

        foreach (var (index, all) in Carried(node))
        {
            if (index is { } found && found < all.Count && all[(int)found].GetValueOrDefault("name") is string carried)
            {
                return carried;
            }
        }

        return Unnamed;
    }

    /// <summary>What a node carries, each as its index and the list it indexes, in the order a name is taken from.</summary>
    private List<(long? Index, List<JsonMap> All)> Carried(JsonMap node)
    {
        var lights = (node.GetValueOrDefault("extensions") as JsonMap)?.GetValueOrDefault("KHR_lights_punctual") as JsonMap;
        var allLights = (Json.GetValueOrDefault("extensions") as JsonMap)?.GetValueOrDefault("KHR_lights_punctual") as JsonMap;
        return
        [
            (Index(node.GetValueOrDefault("mesh")), Maps(Json.GetValueOrDefault("meshes"))),
            (lights is null ? null : Index(lights.GetValueOrDefault("light")), Maps(allLights?.GetValueOrDefault("lights"))),
            (Index(node.GetValueOrDefault("camera")), Maps(Json.GetValueOrDefault("cameras"))),
        ];
    }

    /// <summary>Exactly <paramref name="count"/> finite numbers, or null.</summary>
    private static double[]? Floats(object? raw, int count)
    {
        if (raw is not JsonList { Count: var length } list || length != count)
        {
            return null;
        }

        var numbers = list.Select(JsonNumbers.Finite).ToArray();
        return numbers.Any(static number => number is null) ? null : [.. numbers.Select(static number => number!.Value)];
    }

    /// <summary>A turn that does not scale, or none when there is nothing to turn by.</summary>
    private static Quaternion TurnOf(double[]? turn)
    {
        var turned = turn is null ? null : new Quaternion(turn[0], turn[1], turn[2], turn[3]);
        return turned is null || turned.Length2 < 1e-18 ? Quaternion.Identity() : turned.Normalized();
    }

    /// <summary>Sixteen numbers in column order, as a node's <c>matrix</c> lists them.</summary>
    private static Matrix4 MatrixOf(double[] values)
    {
        var matrix = Matrix4.Zero();
        for (var i = 0; i < 16; i++)
        {
            matrix[i] = values[i];
        }

        return matrix;
    }
}
