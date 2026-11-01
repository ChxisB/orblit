using Orblit.Light;
using Orblit.Mesh;

namespace Orblit.Scene;

/// <summary>
/// Writes a scene as one glTF document: a node per entity, and the components glTF has a word for
/// written as what it calls them.
/// </summary>
internal sealed class GltfExporter
{
    private static readonly string[] Sections = ["nodes", "meshes", "materials", "cameras", "textures", "images", "samplers"];

    private readonly SceneDocument _scene;
    private readonly GltfOutput _output;
    private readonly GltfMaterials _materials;

    /// <summary>
    /// Node index by entity id, filled before anything is written so that a parent can name a child it
    /// has not reached yet.
    /// </summary>
    private readonly Dictionary<string, long> _node = [];

    /// <summary>Models already copied in, by the path the scene names them.</summary>
    private readonly Dictionary<string, Grafted> _graftedAt = [];

    /// <summary>
    /// Look names, in the order the scene first mentions them. Their positions are the variant
    /// indices every primitive's mappings count into.
    /// </summary>
    private readonly List<string> _looks = [];

    public GltfExporter(SceneDocument scene, MaterialLibrary? library, IReadOnlyDictionary<string, byte[]> files)
    {
        _scene = scene;
        _output = new GltfOutput(files);
        _materials = new GltfMaterials(_output, new GltfTextures(_output), library);
    }

    public GltfScene Write()
    {
        var children = Nodes();
        foreach (var entity in _scene.Entities)
        {
            Fill(entity, children.GetValueOrDefault(entity.Id) ?? []);
        }

        var json = _output.Json;
        json["scene"] = 0L;
        json["scenes"] = new List<object?> { SceneEntry() };

        if (_looks.Count > 0)
        {
            _output.Document("KHR_materials_variants")["variants"] =
                new List<object?>(_looks.Select(static look => (object?)new OrderedDictionary<string, object?> { ["name"] = look }));
            _output.Used.Add("KHR_materials_variants");
        }

        var binary = _output.Buffer.Bytes;
        if (_output.Buffer.Views.Count > 0)
        {
            json["bufferViews"] = _output.Buffer.Views;
        }

        if (_output.Buffer.Accessors.Count > 0)
        {
            json["accessors"] = _output.Buffer.Accessors;
        }

        if (binary.Length > 0)
        {
            json["buffers"] = new List<object?> { new OrderedDictionary<string, object?> { ["byteLength"] = (long)binary.Length } };
        }

        Finish();
        return new GltfScene(json, binary, _output.Problems);
    }

    /// <summary>An empty node for each entity, and the children each parent will name.</summary>
    private Dictionary<string, List<long>> Nodes()
    {
        foreach (var entity in _scene.Entities)
        {
            _node[entity.Id] = _output.Add("nodes", new());
        }

        var children = new Dictionary<string, List<long>>();
        foreach (var entity in _scene.Entities)
        {
            if (entity.Parent is { } parent && _node.ContainsKey(parent))
            {
                if (!children.TryGetValue(parent, out var named))
                {
                    named = children[parent] = [];
                }

                named.Add(_node[entity.Id]);
            }
        }

        return children;
    }

    private OrderedDictionary<string, object?> SceneEntry()
    {
        var roots = _scene.Entities
            .Where(entity => entity.Parent is null || !_node.ContainsKey(entity.Parent))
            .Select(entity => (object?)_node[entity.Id])
            .ToList();
        var entry = new OrderedDictionary<string, object?> { ["name"] = _scene.Name };

        // An empty list is not an empty scene in this format, it is a mistake: every array glTF has
        // must have something in it.
        if (roots.Count > 0)
        {
            entry["nodes"] = roots;
        }

        // The settings are not a node and have nowhere in the format to go, so they ride on the
        // scene's extras, which is where a re-import looks for them, and which every other loader
        // ignores exactly as it should. The version rides with them, so a re-import knows what the
        // components on each node meant when they were written.
        entry["extras"] = new OrderedDictionary<string, object?>
        {
            ["orblit"] = new OrderedDictionary<string, object?>
            {
                ["formatVersion"] = (long)SceneDocument.FormatVersion,
                ["settings"] = _scene.Settings.ToJson(),
            },
        };
        return entry;
    }

    private void Finish()
    {
        foreach (var name in Sections)
        {
            if (_output.Section(name).Count == 0)
            {
                _output.Json.Remove(name);
            }
        }

        if (_output.Used.Count > 0)
        {
            _output.Json["extensionsUsed"] = Union(_output.Used);
        }
    }

    /// <summary>The names plus whatever a grafted model already declared, without repeats.</summary>
    private List<object?> Union(IEnumerable<string> names)
    {
        var ours = _output.Json.GetValueOrDefault("extensionsUsed") as IReadOnlyList<object?> ?? [];
        return [.. ours, .. names.Where(name => !ours.Contains(name))];
    }

    private void Fill(SceneEntity entity, List<long> children)
    {
        var node = (OrderedDictionary<string, object?>)_output.Section("nodes")[(int)_node[entity.Id]]!; // Nodes() made it
        node["name"] = entity.Name;

        if (entity[SceneComponents.Transform] is TransformComponent transform)
        {
            Place(node, transform);
        }

        IReadOnlyList<long> extra = [];
        if (entity[SceneComponents.Mesh] is MeshComponent mesh)
        {
            extra = Draw(node, entity, mesh, entity[SceneComponents.Material] as MaterialComponent);
        }

        if (entity[SceneComponents.Light] is LightComponent light)
        {
            Shine(node, light);
        }

        if (entity[SceneComponents.Camera] is CameraComponent camera)
        {
            Frame(node, camera);
        }

        if (children.Count > 0 || extra.Count > 0)
        {
            node["children"] = new List<object?>([.. children.Select(static c => (object?)c), .. extra.Select(static c => (object?)c)]);
        }

        node["extras"] = ExtrasOf(entity);
    }

    /// <summary>
    /// Everything this package knows about an entity that glTF has no word for: which components it
    /// has, what each one holds, whether it is hidden. Written back exactly as the scene file states
    /// it. That is what makes exporting and importing again give the same scene rather than a
    /// flattened photograph of one.
    /// </summary>
    private static OrderedDictionary<string, object?> ExtrasOf(SceneEntity entity)
    {
        var components = new OrderedDictionary<string, object?>();
        foreach (var (key, component) in entity.Components)
        {
            components[key] = component.ToJson();
        }

        var orblit = new OrderedDictionary<string, object?> { ["id"] = entity.Id };
        if (!entity.Visible)
        {
            orblit["visible"] = false;
        }

        orblit["components"] = components;
        return new OrderedDictionary<string, object?> { ["orblit"] = orblit };
    }

    private static void Place(OrderedDictionary<string, object?> node, TransformComponent transform)
    {
        if (Differs(transform.Position, 0))
        {
            node["translation"] = Values.VectorToJson(transform.Position);
        }

        if (Differs(transform.Scale, 1))
        {
            node["scale"] = Values.VectorToJson(transform.Scale);
        }

        var euler = transform.Rotation;
        if (Differs(euler, 0))
        {
            // The same order the scene is drawn in, Z then Y then X, turned into the quaternion glTF
            // stores. Composing it here rather than writing three numbers means a loader that knows
            // nothing about Orblit's convention still puts the object the way round it was.
            var rotation = Matrix4.RotationZ(Doubles.Radians(euler.Z));
            rotation.Multiply(Matrix4.RotationY(Doubles.Radians(euler.Y)));
            rotation.Multiply(Matrix4.RotationX(Doubles.Radians(euler.X)));
            var q = Quaternion.FromRotation(rotation.GetRotation());
            q.Normalize();
            node["rotation"] = Values.NumbersToJson([q.X, q.Y, q.Z, q.W]);
        }
    }

    private static bool Differs(Vector3 value, double rest) => value.X != rest || value.Y != rest || value.Z != rest;

    /// <summary>
    /// Gives <paramref name="node"/> something to draw, returning any extra nodes it needs.
    /// </summary>
    /// <remarks>
    /// A model imported from a file is the awkward case: glTF cannot reference another document, so
    /// the whole of it is copied in and its roots become this node's children.
    /// </remarks>
    private IReadOnlyList<long> Draw(
        OrderedDictionary<string, object?> node,
        SceneEntity entity,
        MeshComponent mesh,
        MaterialComponent? wearing)
    {
        if (mesh.Asset is { } asset && mesh.Geometry is null)
        {
            return Graft(entity, asset);
        }

        Primitives(node, entity, mesh, wearing);
        return [];
    }

    private IReadOnlyList<long> Graft(SceneEntity entity, string asset)
    {
        if (_graftedAt.TryGetValue(asset, out var already) && already.Shareable)
        {
            return GltfGraft.Regraft(_output.Json, already);
        }

        if (!_output.Files.TryGetValue(asset, out var bytes))
        {
            _output.Problems.Add(
                $"{entity.Name} draws {asset} and its bytes were not supplied, so the export places an empty node where the model goes.");
            return [];
        }

        if (GltfFiles.GlbChunks(bytes) is not { } chunks)
        {
            _output.Problems.Add(
                $"{entity.Name} draws {asset}, which is not a GLB. Only a model that carries its own bytes can be copied into an export.");
            return [];
        }

        try
        {
            var grafted = GltfGraft.Graft(_output.Json, _output.Buffer, chunks.Json, chunks.Binary, asset);
            _graftedAt[asset] = grafted;
            return grafted.Roots;
        }
        catch (GraftException failure)
        {
            _output.Problems.Add($"{entity.Name}: {failure.Message}");
            return [];
        }
    }

    private void Primitives(
        OrderedDictionary<string, object?> node,
        SceneEntity entity,
        MeshComponent mesh,
        MaterialComponent? wearing)
    {
        if (DrawnTriangles(mesh) is not { } triangles)
        {
            return;
        }

        // One view over every face, and an accessor per run into a stretch of it. The builder narrows
        // the width to what the numbers need and says which it chose, so the offsets below are right
        // whichever it picked.
        var buffer = _output.Buffer;
        var indices = buffer.AddIndexView(triangles.Indices);
        var attributes = Attributes(triangles);

        var slot = _materials.Slot(wearing?.Asset, mesh.Colour);
        var mappings = Mappings(wearing, mesh.Colour);

        Tangents(attributes, triangles, wearing);

        var primitives = new List<object?>();
        foreach (var run in triangles.Runs)
        {
            var primitive = new OrderedDictionary<string, object?>
            {
                ["attributes"] = attributes,
                ["indices"] = (long)buffer.AddAccessor(indices.View, indices.ComponentType, run.Count, "SCALAR", run.Start * indices.Width),
                ["mode"] = 4L,
                ["material"] = slot,
            };
            if (mappings is not null)
            {
                primitive["extensions"] = Variants(mappings);
            }

            // Which of the mesh's painted surfaces this stretch of faces wore. Not turned into a
            // material here, because what a surface means is the material system's to say and this
            // package keeps it as the file wrote it, but kept, so importing again can.
            if (triangles.Groups.Count > 0)
            {
                primitive["extras"] = Surface(run.Material);
            }

            primitives.Add(primitive);
        }

        _output.Section("meshes").Add(new OrderedDictionary<string, object?> { ["name"] = entity.Name, ["primitives"] = primitives });
        node["mesh"] = (long)_output.Section("meshes").Count - 1;
    }

    private static Triangles? DrawnTriangles(MeshComponent mesh)
    {
        var triangles = (mesh.Geometry ?? mesh.Shape?.Build())?.Triangulate();
        return triangles is { Indices.Length: > 0, Positions.Length: > 0 } ? triangles : null;
    }

    /// <summary>
    /// A normal map is read in the texture's own frame, and a mesh that does not say which way that
    /// frame runs leaves every renderer to guess. They guess differently, so the bumps lean one way
    /// here and the other way in the next tool along. Worked out only when something actually wears a
    /// normal map: it is four more floats a vertex, and most things do not.
    /// </summary>
    private void Tangents(OrderedDictionary<string, object?> attributes, Triangles triangles, MaterialComponent? wearing)
    {
        if (attributes.ContainsKey("NORMAL") && attributes.ContainsKey("TEXCOORD_0") && _materials.NeedsTangents(wearing))
        {
            attributes["TANGENT"] = (long)_output.Buffer.AddFloats(MeshTangents.TangentsOf(triangles), "VEC4", GltfTarget.ArrayBuffer);
        }
    }

    private OrderedDictionary<string, object?> Attributes(Triangles triangles)
    {
        var buffer = _output.Buffer;
        var attributes = new OrderedDictionary<string, object?> { ["POSITION"] = (long)buffer.AddPositions(triangles.Positions) };
        if (triangles.Normals.Length > 0)
        {
            attributes["NORMAL"] = (long)buffer.AddFloats(triangles.Normals, "VEC3", GltfTarget.ArrayBuffer);
        }

        if (triangles.Uvs.Length > 0)
        {
            attributes["TEXCOORD_0"] = (long)buffer.AddFloats(triangles.Uvs, "VEC2", GltfTarget.ArrayBuffer);
        }

        return attributes;
    }

    private static OrderedDictionary<string, object?> Variants(List<object?> mappings) => new()
    {
        ["KHR_materials_variants"] = new OrderedDictionary<string, object?> { ["mappings"] = mappings },
    };

    private static OrderedDictionary<string, object?> Surface(int material) => new()
    {
        ["orblit"] = new OrderedDictionary<string, object?> { ["surface"] = (long)material },
    };

    /// <summary>The per-primitive variant mappings, or null when this entity has none.</summary>
    private List<object?>? Mappings(MaterialComponent? wearing, Tint colour)
    {
        if (wearing is null || wearing.Looks.Count == 0)
        {
            return null;
        }

        var mappings = new List<object?>();
        foreach (var (name, path) in wearing.Looks)
        {
            mappings.Add(new OrderedDictionary<string, object?>
            {
                ["material"] = _materials.Slot(path, colour),
                ["variants"] = new List<object?> { Look(name) },
            });
        }

        return mappings;
    }

    private long Look(string name)
    {
        var at = _looks.IndexOf(name);
        if (at >= 0)
        {
            return at;
        }

        _looks.Add(name);
        return _looks.Count - 1;
    }

    private void Shine(OrderedDictionary<string, object?> node, LightComponent light)
    {
        var document = _output.Document("KHR_lights_punctual");
        if (!document.TryGetValue("lights", out var held))
        {
            held = document["lights"] = new List<object?>();
        }

        var lights = (List<object?>)held!; // made just above or by a graft, as a list
        lights.Add(LightEntry(light));

        if (!node.TryGetValue("extensions", out var extensions))
        {
            extensions = node["extensions"] = new OrderedDictionary<string, object?>();
        }

        ((OrderedDictionary<string, object?>)extensions!)["KHR_lights_punctual"] = // made just above as a map
            new OrderedDictionary<string, object?> { ["light"] = (long)lights.Count - 1 };
        _output.Used.Add("KHR_lights_punctual");
    }

    private static OrderedDictionary<string, object?> LightEntry(LightComponent light)
    {
        var colour = light.Colour.Linear;

        // A sun is stated per square metre and arrives as parallel rays, so its strength is an
        // illuminance; everything else is a source radiating into the whole sphere, so its strength
        // is an intensity. Both go through the one conversion this engine has, which is the same
        // simplification Blender's exporter makes, so a scene comes back the brightness it left.
        var intensity = light.Kind == LightType.Sun
            ? Photometry.IrradianceToLux(light.Power)
            : Photometry.LumensToCandela(Photometry.WattsToLumens(light.Power));

        var outer = Math.Min(Doubles.Radians(light.SpotSize) / 2, Math.PI / 2);
        var entry = new OrderedDictionary<string, object?>
        {
            ["type"] = light.Kind switch
            {
                LightType.Sun => "directional",
                LightType.Spot => "spot",
                LightType.Point or LightType.Area => "point",
                _ => throw new ArgumentOutOfRangeException(nameof(light), light.Kind, "Not a kind of light."),
            },
            ["color"] = Values.VectorToJson(colour),
            ["intensity"] = intensity,
        };

        if (light.Kind == LightType.Spot)
        {
            entry["spot"] = new OrderedDictionary<string, object?>
            {
                ["innerConeAngle"] = outer * (1 - Doubles.Clamp(light.SpotBlend, 0.0, 1.0)),
                ["outerConeAngle"] = outer,
            };
        }

        // An area light is a shape that emits, and a punctual light is a point. Writing it as a point
        // keeps the light; what is lost is the softness its size gave the shadow, and the size is
        // kept so importing can give it back.
        if (light.Kind == LightType.Area)
        {
            entry["extras"] = new OrderedDictionary<string, object?>
            {
                ["orblit"] = new OrderedDictionary<string, object?> { ["kind"] = "area", ["sourceRadius"] = light.SourceRadius },
            };
        }

        return entry;
    }

    private void Frame(OrderedDictionary<string, object?> node, CameraComponent camera)
    {
        var perspective = new OrderedDictionary<string, object?>
        {
            ["yfov"] = Doubles.Radians(camera.FieldOfView),
            ["znear"] = camera.Near,
        };
        if (camera.Far > camera.Near)
        {
            perspective["zfar"] = camera.Far;
        }

        // Orblit states the angle across whichever side of the frame is shorter, which is the
        // vertical one on every screen anybody ships. glTF states the vertical angle outright, so on
        // a landscape frame they are the same number, and the original is kept either way.
        node["camera"] = _output.Add("cameras", new OrderedDictionary<string, object?>
        {
            ["type"] = "perspective",
            ["perspective"] = perspective,
            ["extras"] = new OrderedDictionary<string, object?>
            {
                ["orblit"] = new OrderedDictionary<string, object?> { ["fieldOfView"] = camera.FieldOfView, ["across"] = "shorter" },
            },
        });
    }
}
