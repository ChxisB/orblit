using Orblit.Json;
using Orblit.Mesh;
using static Orblit.Scene.Tests.ImportDocs;
using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class GltfPointerGraftTests
{
    private const string PointerExtension = "KHR_animation_pointer";
    private const string Lights = "KHR_lights_punctual";

    // Source numbering: nodes Body 0, Eye 1, Lamp 2. One camera, one material, one mesh, one light.
    private static readonly string[] Aimed =
    [
        "/nodes/1/scale",
        "/nodes/0/extensions/KHR_node_visibility/visible",
        "/materials/0/pbrMetallicRoughness/baseColorFactor",
        "/cameras/0/perspective/yfov",
        "/meshes/0/weights",
        "/extensions/KHR_lights_punctual/lights/0/intensity",
    ];

    // Names no array the graft counts, or no index at all. The pointer is the file's own business.
    private static readonly string[] Foreign =
    [
        "/nodes/one/scale",
        "/nodes/-1/scale",
        "/widgets/0/size",
        "nodes/0/scale",
    ];

    private static OrderedDictionary<string, object?> Pointing(string pointer) => Map(
        ("sampler", 0L),
        ("target", Map(
            ("path", "pointer"),
            ("extensions", Map((PointerExtension, Map(("pointer", pointer))))))));

    private static OrderedDictionary<string, object?> Accessor(long count, string type) =>
        Map(("componentType", 5126L), ("count", count), ("type", type));

    private static byte[] Rig()
    {
        var channels = new List<object?> { Map(("sampler", 0L), ("target", Map(("node", 1L), ("path", "translation")))) };
        channels.AddRange(Aimed.Concat(Foreign).Select(Pointing));

        return GltfFiles.GlbBytes(
            Map(
                ("asset", Map(("version", "2.0"))),
                ("extensionsUsed", List(PointerExtension, Lights)),
                ("extensions", Map((Lights, Map(("lights", List(Map(("type", "point")))))))),
                ("nodes", List(
                    Map(("name", "Body"), ("mesh", 0L)),
                    Map(("name", "Eye"), ("camera", 0L)),
                    Map(("name", "Lamp"), ("extensions", Map((Lights, Map(("light", 0L)))))))),
                ("cameras", List(Map(("type", "perspective"), ("perspective", Map(("yfov", 0.8), ("znear", 0.1)))))),
                ("materials", List(Map(("name", "Paint")))),
                ("meshes", List(Map(("primitives", List(Map(("attributes", Map(("POSITION", 0L))), ("material", 0L))))))),
                ("accessors", List(Accessor(3, "VEC3"), Accessor(2, "SCALAR"), Accessor(2, "VEC3"))),
                ("animations", List(Map(
                    ("samplers", List(Map(("input", 1L), ("output", 2L)))),
                    ("channels", channels))))),
            []);
    }

    /// <summary>Two objects draw the one animated model, which a graft copies in full each time.</summary>
    private static JsonMap Exported()
    {
        var scene = new SceneDocument(entities:
        [
            Entity("a", Parts((SceneComponents.Mesh, new MeshComponent(asset: "rig.glb")))),
            Entity("b", Parts((SceneComponents.Mesh, new MeshComponent(asset: "rig.glb")))),
        ]);
        var written = scene.WriteAs(SceneFormat.Glb, files: new Dictionary<string, byte[]> { ["rig.glb"] = Rig() });

        Assert.Empty(written.Problems);
        return GltfFiles.GlbChunks(written.First.Bytes)!.Value.Json;
    }

    private static IEnumerable<JsonMap> ChannelsOf(JsonMap header, int animation)
    {
        var animations = Assert.IsAssignableFrom<JsonList>(header["animations"]);
        var channels = Assert.IsAssignableFrom<JsonList>(Assert.IsAssignableFrom<JsonMap>(animations[animation])["channels"]);
        return channels.Cast<JsonMap>();
    }

    private static string? PointerOf(JsonMap channel)
    {
        var target = Assert.IsAssignableFrom<JsonMap>(channel["target"]);
        return target.GetValueOrDefault("extensions") is JsonMap extensions
            && extensions.GetValueOrDefault(PointerExtension) is JsonMap pointer
                ? pointer["pointer"] as string
                : null;
    }

    private static long PlainNodeOf(JsonMap header, int animation) =>
        Convert.ToInt64(Assert.IsAssignableFrom<JsonMap>(ChannelsOf(header, animation).First()["target"])["node"]);

    private static List<string?> PointersIn(JsonMap header, int animation) =>
        [.. ChannelsOf(header, animation).Skip(1).Select(PointerOf)];

    [Fact]
    public void TheFirstCopyKeepsThePointersItCameWith()
    {
        var header = Exported();

        var pointers = PointersIn(header, 0);

        // Nothing sat before it in the file, so only the nodes have moved, and those are the plain channel's to say.
        var eye = PlainNodeOf(header, 0);
        Assert.Equal($"/nodes/{eye}/scale", pointers[0]);
        Assert.Equal($"/nodes/{eye - 1}/extensions/KHR_node_visibility/visible", pointers[1]);
        Assert.Equal(Aimed[2..], pointers.Skip(2).Take(Aimed.Length - 2));
    }

    [Fact]
    public void TheSecondCopyPointsAtItsOwnNodesMaterialCameraMeshAndLight()
    {
        var header = Exported();

        var pointers = PointersIn(header, 1);

        var eye = PlainNodeOf(header, 1);
        Assert.NotEqual(PlainNodeOf(header, 0), eye);
        Assert.Equal($"/nodes/{eye}/scale", pointers[0]);
        Assert.Equal($"/nodes/{eye - 1}/extensions/KHR_node_visibility/visible", pointers[1]);
        Assert.Equal("/materials/1/pbrMetallicRoughness/baseColorFactor", pointers[2]);
        Assert.Equal("/cameras/1/perspective/yfov", pointers[3]);
        Assert.Equal("/meshes/1/weights", pointers[4]);
        Assert.Equal("/extensions/KHR_lights_punctual/lights/1/intensity", pointers[5]);
    }

    [Fact]
    public void APointerThatNamesNothingTheGraftCountsIsLeftAlone()
    {
        var header = Exported();

        Assert.Equal(Foreign, PointersIn(header, 0).Skip(Aimed.Length));
        Assert.Equal(Foreign, PointersIn(header, 1).Skip(Aimed.Length));
    }
}
