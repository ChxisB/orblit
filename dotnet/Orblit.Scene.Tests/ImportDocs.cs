using System.Text;
using Orblit.Json;
using Orblit.Light;
using Orblit.Mesh;
using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

/// <summary>The scenes and documents the import tests read.</summary>
internal static class ImportDocs
{
    public static SceneEntity Entity(
        string id,
        IReadOnlyDictionary<string, SceneComponent> components,
        string? parent = null,
        string? name = null,
        bool visible = true) => new(id, name ?? id, parent, visible, components);

    public static Dictionary<string, SceneComponent> Parts(params (string Type, SceneComponent Component)[] pairs) =>
        pairs.ToDictionary(pair => pair.Type, pair => pair.Component);

    /// <summary>A scene with one of most things in it, to send round the trip.</summary>
    public static SceneDocument Populated() => new(
        "Old Town",
        new SceneSettings(
            sky: Tint.Hex(0x223344),
            ambient: 41000,
            timeOfDay: 17.25,
            dayCycle: true,
            hoursPerSecond: 0.125),
        [
            Entity(
                "yard",
                Parts((SceneComponents.Transform, new TransformComponent(
                    position: new Vector3(1.5, 0, -4),
                    rotation: new Vector3(0, 30, 0),
                    scale: new Vector3(2, 2, 2)))),
                name: "Yard"),
            Entity(
                "wall",
                Parts(
                    (SceneComponents.Transform, new TransformComponent(
                        position: new Vector3(0, 1, 0),
                        rotation: new Vector3(12, 34, 56))),
                    (SceneComponents.Mesh, new MeshComponent(
                        shape: Shape.Of(ShapeKind.Cube),
                        colour: Tint.Hex(0x8899AA),
                        castShadows: false,
                        sway: 0.4,
                        authored: true)),
                    (SceneComponents.Material, new MaterialComponent(
                        asset: "materials/brick.omat",
                        looks: new Dictionary<string, string> { ["winter"] = "materials/brick_snow.omat" }))),
                parent: "yard",
                name: "Wall"),
            Entity(
                "lamp",
                Parts(
                    (SceneComponents.Transform, new TransformComponent(position: new Vector3(0, 3, 0))),
                    (SceneComponents.Light, new LightComponent(
                        kind: LightType.Spot,
                        power: 60,
                        colour: Tint.Hex(0xFFE7C0),
                        spotSize: 70,
                        spotBlend: 0.3))),
                parent: "yard",
                name: "Lamp"),
            Entity("sun", Parts((SceneComponents.Light, new LightComponent(power: 1.2))), name: "Sun"),
            Entity(
                "eye",
                Parts((SceneComponents.Camera, new CameraComponent(fieldOfView: 42, near: 0.05, far: 250))),
                name: "Eye",
                visible: false),
            Entity(
                "capture",
                Parts((SceneComponents.Splats, new SplatsComponent(asset: "captures/street.osplat", budget: 400000))),
                name: "Capture"),
        ]);

    /// <summary>A node in a glTF nobody here wrote.</summary>
    // F2: a node is described by its parts, and each is named at the call.
    public static OrderedDictionary<string, object?> Foreign(
        string name,
        long[]? children = null,
        double[]? translation = null,
        double[]? rotation = null,
        double[]? scale = null,
        double[]? matrix = null,
        long? mesh = null,
        long? camera = null,
        long? light = null)
    {
        var node = Map(("name", name));
        Put(node, "children", children);
        Put(node, "translation", translation);
        Put(node, "rotation", rotation);
        Put(node, "scale", scale);
        Put(node, "matrix", matrix);
        if (mesh is not null)
        {
            node["mesh"] = mesh;
        }

        if (camera is not null)
        {
            node["camera"] = camera;
        }

        if (light is not null)
        {
            node["extensions"] = Map(("KHR_lights_punctual", Map(("light", light))));
        }

        return node;
    }

    /// <summary>A node a scene wrote, before components carried a version.</summary>
    public static OrderedDictionary<string, object?> Recorded(string id, long[]? children = null, JsonMap? components = null)
    {
        var node = Map(("name", id));
        Put(node, "children", children);
        node["extras"] = Map(("orblit", Map(("id", id), ("components", components ?? Map()))));
        return node;
    }

    /// <summary>A whole glTF document as the bytes a reader is handed.</summary>
    public static byte[] Document(params (string Key, object? Value)[] pairs) =>
        Encoding.UTF8.GetBytes(JsonText.Encode(With(Map(("asset", Map(("version", "2.0")))), pairs)));

    public static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

    public static MeshComponent MeshOf(SceneEntity entity) => Assert.IsType<MeshComponent>(entity[SceneComponents.Mesh]);

    private static void Put<T>(OrderedDictionary<string, object?> node, string key, T[]? values)
    {
        if (values is not null)
        {
            node[key] = values.Select(static one => (object?)one).ToList();
        }
    }
}
