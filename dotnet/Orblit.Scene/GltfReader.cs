using Orblit.Mesh;

namespace Orblit.Scene;

/// <summary>
/// Reads one glTF document back into a scene. This half handles the shape of the document and a
/// document this package wrote. The half for a document from somewhere else is in
/// <c>GltfReader.Foreign.cs</c>.
/// </summary>
internal sealed partial class GltfReader
{
    private readonly JsonMap _json;
    private readonly string _fallbackName;
    private readonly List<string> _problems = [];

    /// <summary>The document's bytes, read into the problems so a bad accessor is noted alongside everything else.</summary>
    private readonly GltfAccessors _numbers;

    private readonly List<JsonMap> _nodes;
    private readonly List<JsonMap> _materials;
    private readonly List<JsonMap> _cameras;
    private readonly List<JsonMap> _meshes;

    /// <summary>The node each node hangs from, by index.</summary>
    private readonly Dictionary<int, int> _parentOf = [];

    /// <summary>Which nodes became entities, and what each one's id is.</summary>
    private readonly Dictionary<int, string> _idOf = [];

    public GltfReader(JsonMap json, byte[]? binary, string fallbackName)
    {
        _json = json;
        _fallbackName = fallbackName;
        _numbers = new GltfAccessors(json, binary, _problems);
        _nodes = GltfJson.Maps(json.GetValueOrDefault("nodes"));
        _materials = GltfJson.Maps(json.GetValueOrDefault("materials"));
        _cameras = GltfJson.Maps(json.GetValueOrDefault("cameras"));
        _meshes = GltfJson.Maps(json.GetValueOrDefault("meshes"));
    }

    public SceneImported Read()
    {
        LinkChildren();

        var scene = SceneOf();
        var ours = _nodes.Any(node => GltfJson.OursOn(node) is not null);

        // Which nodes are entities is settled before any of them is built, because a parent has to be
        // known to be an entity before anything can be attached to it. In a document this package
        // wrote, the grafted copy of an imported model is a subtree of nodes that are not entities at
        // all and must not become any. The component that names the model is already on the node
        // above them.
        NameEntities(ours);

        if (!ours && _nodes.Count > 0)
        {
            _problems.Add(
                "This glTF was not written by Orblit, so the scene was worked out "
                + "from the parts of the format that have an equivalent here. "
                + "Materials, textures, skins and animation were not carried across.");
        }

        var version = StatedVersion(scene);
        var entities = new List<SceneEntity>();
        for (var at = 0; at < _nodes.Count; at++)
        {
            if (_idOf.TryGetValue(at, out var id))
            {
                entities.Add(ours ? Restore(at, id, version) : Interpret(at, id));
            }
        }

        var document = new SceneDocument(
            GltfJson.Text(scene?.GetValueOrDefault("name")) ?? _fallbackName,
            SettingsOf(scene),
            entities);
        return new SceneImported(document, _problems, ours);
    }

    private void LinkChildren()
    {
        for (var at = 0; at < _nodes.Count; at++)
        {
            foreach (var child in GltfJson.Ints(_nodes[at].GetValueOrDefault("children")))
            {
                // The first parent a node is named by keeps it.
                if (child >= 0 && child < _nodes.Count && child != at)
                {
                    _parentOf.TryAdd((int)child, at);
                }
            }
        }
    }

    private void NameEntities(bool ours)
    {
        for (var at = 0; at < _nodes.Count; at++)
        {
            if (ours && GltfJson.OursOn(_nodes[at]) is null)
            {
                continue;
            }

            _idOf[at] = ours ? OurId(at) : $"node-{at}";
        }
    }

    /// <summary>The scene the document points at, or its first, or none.</summary>
    private JsonMap? SceneOf()
    {
        var all = GltfJson.Maps(_json.GetValueOrDefault("scenes"));
        if (all.Count == 0)
        {
            return null;
        }

        return _json.GetValueOrDefault("scene") is long at and >= 0 && at < all.Count ? all[(int)at] : all[0];
    }

    private static SceneSettings SettingsOf(JsonMap? scene) =>
        scene is not null && GltfJson.OursOn(scene)?.GetValueOrDefault("settings") is JsonMap settings
            ? SceneSettings.FromJson(settings)
            : new SceneSettings();

    /// <summary>
    /// The scene format the components on each node were written in. A document from before the
    /// exporter wrote one down was written at version four, the one the exporter arrived at.
    /// </summary>
    private static long StatedVersion(JsonMap? scene) =>
        scene is not null && GltfJson.OursOn(scene)?.GetValueOrDefault("formatVersion") is long stated ? stated : 4;

    private string OurId(int at)
    {
        var id = GltfJson.Text(GltfJson.OursOn(_nodes[at])?.GetValueOrDefault("id"));

        // A node of ours with no id is a document somebody edited by hand. It still has its
        // components, so it is worth keeping under a made-up id rather than dropping. What it loses
        // is anything that pointed at it.
        if (string.IsNullOrEmpty(id))
        {
            _problems.Add($"Node {at} carried Orblit components but no id.");
            return $"node-{at}";
        }

        return id;
    }

    private SceneEntity Restore(int at, string id, long version)
    {
        var node = _nodes[at];

        // Only a node with a record was given an id, so the record is there.
        var ours = GltfJson.OursOn(node)!;

        var source = new OrderedDictionary<string, object?>
        {
            ["id"] = id,
            ["components"] = ours.GetValueOrDefault("components"),
        };
        var raw = SceneMigrations.Entity(source, version, _problems)?.GetValueOrDefault("components");

        var components = new OrderedDictionary<string, SceneComponent>();
        foreach (var (type, value) in Values.Object(raw))
        {
            if (value is JsonMap body)
            {
                components[type] = SceneComponents.Read(type, body);
            }
        }

        return new SceneEntity(
            id,
            GltfJson.Text(node.GetValueOrDefault("name")) ?? id,
            ParentId(at),
            GltfJson.ShownOn(node) ?? ours.GetValueOrDefault("visible") is not false,
            components);
    }

    /// <summary>
    /// The id of the nearest ancestor that is an entity. Nearest rather than immediate, because a
    /// document somebody else has been through may have nodes between two of ours. A tool that wrapped
    /// every object in an axis-correcting parent is the common one, and dropping the child out of the
    /// tree over it would be worse than reattaching it a level up.
    /// </summary>
    private string? ParentId(int at)
    {
        var seen = new HashSet<int> { at };
        int? walk = _parentOf.TryGetValue(at, out var first) ? first : null;
        while (walk is { } node && seen.Add(node))
        {
            if (_idOf.TryGetValue(node, out var id))
            {
                return id;
            }

            walk = _parentOf.TryGetValue(node, out var up) ? up : null;
        }

        return null;
    }
}
