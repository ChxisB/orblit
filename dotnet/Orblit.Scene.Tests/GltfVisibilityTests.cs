using Orblit.Json;
using Orblit.Light;
using Orblit.Mesh;
using static Orblit.Scene.Tests.ImportDocs;
using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class GltfVisibilityTests
{
    private const string Visibility = "KHR_node_visibility";

    private static OrderedDictionary<string, object?> Marked(OrderedDictionary<string, object?> node, bool visible)
    {
        var extensions = node.GetValueOrDefault("extensions") as OrderedDictionary<string, object?> ?? Map();
        extensions[Visibility] = Map(("visible", visible));
        node["extensions"] = extensions;
        return node;
    }

    private static OrderedDictionary<string, object?> WrittenHere() => Map(
        ("nodes", List(0L)),
        ("extras", Map(("orblit", Map(("formatVersion", 5L))))));

    private static SceneDocument Hiding(params SceneEntity[] entities) => new(entities: entities);

    private static JsonMap NodeOf(SceneDocument scene, int at)
    {
        var header = GltfFiles.GlbChunks(scene.WriteAs(SceneFormat.Glb).First.Bytes)!.Value.Json;
        return Assert.IsAssignableFrom<JsonMap>(Assert.IsAssignableFrom<JsonList>(header["nodes"])[at]);
    }

    private static IEnumerable<string> Used(SceneDocument scene)
    {
        var header = GltfFiles.GlbChunks(scene.WriteAs(SceneFormat.Glb).First.Bytes)!.Value.Json;
        return header.TryGetValue("extensionsUsed", out var used)
            ? Assert.IsAssignableFrom<JsonList>(used).Cast<string>()
            : [];
    }

    // a file from somewhere else

    [Fact]
    public void ANodeTheFileHidesComesInHidden()
    {
        var read = SceneImport.ReadSceneFrom(Document(("nodes", List(Marked(Foreign("Prop"), visible: false)))));

        Assert.False(read.Document.Entities[0].Visible);
    }

    [Fact]
    public void ANodeTheFileShowsOrSaysNothingAboutComesInShown()
    {
        var read = SceneImport.ReadSceneFrom(Document(
            ("nodes", List(Marked(Foreign("Shown"), visible: true), Foreign("Silent")))));

        Assert.All(read.Document.Entities, one => Assert.True(one.Visible, one.Name));
    }

    [Fact]
    public void AChildKeepsItsOwnFlagBecauseTheViewAlreadyHidesWhatSitsUnderAHiddenParent()
    {
        var read = SceneImport.ReadSceneFrom(Document(
            ("scene", 0L),
            ("scenes", List(Map(("nodes", List(0L))))),
            ("nodes", List(Marked(Foreign("Crate", children: [1]), visible: false), Marked(Foreign("Lid"), visible: true)))));

        Assert.False(read.Document.Entities[0].Visible);
        Assert.True(read.Document.Entities[1].Visible);
    }

    [Fact]
    public void ALightKeepsItsLightWhenTheSameNodeIsHidden()
    {
        var sun = Map(("type", "directional"), ("color", List(1.0, 1.0, 1.0)), ("intensity", 1.0));
        var read = SceneImport.ReadSceneFrom(Document(
            ("extensions", Map(("KHR_lights_punctual", Map(("lights", List(sun)))))),
            ("nodes", List(Marked(Foreign("Sun", light: 0), visible: false)))));

        var entity = read.Document.Entities[0];
        Assert.False(entity.Visible);
        Assert.IsType<LightComponent>(entity["light"]);
    }

    [Fact]
    public void AnExtensionThatIsNotAFlagIsIgnored()
    {
        var node = Foreign("Odd");
        node["extensions"] = Map((Visibility, Map(("visible", "no"))));
        var read = SceneImport.ReadSceneFrom(Document(("nodes", List(node))));

        Assert.True(read.Document.Entities[0].Visible);
    }

    // a file this editor wrote

    [Fact]
    public void WhenTheFileAndOurRecordDisagreeTheFileWins()
    {
        // Another tool toggled the node after the scene was saved, and the extension is the part it knows.
        var read = SceneImport.ReadSceneFrom(Document(
            ("scenes", List(WrittenHere())),
            ("nodes", List(Marked(Recorded("lamp"), visible: false)))));

        Assert.True(read.WasWrittenHere);
        Assert.False(read.Document["lamp"]!.Visible);
    }

    [Fact]
    public void WhenTheFileShowsWhatOurRecordHidesTheFileWins()
    {
        var node = Marked(Recorded("lamp"), visible: true);
        var extras = Assert.IsType<OrderedDictionary<string, object?>>(node["extras"]);
        var ours = Assert.IsType<OrderedDictionary<string, object?>>(extras["orblit"]);
        ours["visible"] = false;

        var read = SceneImport.ReadSceneFrom(Document(("scenes", List(WrittenHere())), ("nodes", List(node))));

        Assert.True(read.Document["lamp"]!.Visible);
    }

    // a file this editor writes

    [Fact]
    public void AHiddenObjectIsWrittenWithTheStandardFlagAndTheFileSaysItUsesIt()
    {
        var scene = Hiding(Entity("eye", Parts(), visible: false));

        var flag = Assert.IsAssignableFrom<JsonMap>(Assert.IsAssignableFrom<JsonMap>(NodeOf(scene, 0)["extensions"])[Visibility]);
        Assert.Equal(false, flag["visible"]);
        Assert.Contains(Visibility, Used(scene));
    }

    [Fact]
    public void AShownObjectIsWrittenWithoutIt()
    {
        var scene = Hiding(Entity("wall", Parts()));

        Assert.False(NodeOf(scene, 0).ContainsKey("extensions"));
        Assert.DoesNotContain(Visibility, Used(scene));
    }

    [Fact]
    public void AHiddenLightKeepsItsLightExtensionBesideTheFlag()
    {
        var scene = Hiding(Entity("sun", Parts((SceneComponents.Light, new LightComponent(power: 1.2))), visible: false));

        var extensions = Assert.IsAssignableFrom<JsonMap>(NodeOf(scene, 0)["extensions"]);
        Assert.True(extensions.ContainsKey(Visibility));
        Assert.True(extensions.ContainsKey("KHR_lights_punctual"));
    }

    [Fact]
    public void AHiddenObjectStillComesBackHiddenAfterTheTrip()
    {
        var read = SceneImport.ReadSceneFrom(
            Hiding(Entity("eye", Parts(), visible: false), Entity("wall", Parts())).WriteAs(SceneFormat.Glb).First.Bytes);

        Assert.False(read.Document["eye"]!.Visible);
        Assert.True(read.Document["wall"]!.Visible);
    }
}
