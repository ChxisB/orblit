using Orblit.Json;
using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class SceneDocumentTests
{
    // reading a file

    [Fact]
    public void RefusesSomethingThatIsNotJsonAtAll() =>
        Assert.Throws<SceneFormatException>(() => SceneDocument.Decode("not a scene"));

    [Fact]
    public void RefusesJsonThatIsNotAnObject() =>
        Assert.Throws<SceneFormatException>(() => SceneDocument.Decode("[1, 2, 3]"));

    [Fact]
    public void RefusesAFileThatDoesNotSayWhatVersionItIs() =>
        Assert.Throws<SceneFormatException>(() => SceneDocument.Decode("{\"entities\": []}"));

    [Fact]
    public void RefusesAFileFromANewerOrblitRatherThanGuessing()
    {
        var error = Assert.Throws<SceneFormatException>(() => SceneDocument.Decode(
            JsonText.Encode(Map(("formatVersion", (long)SceneDocument.FormatVersion + 1), ("entities", List())))));

        Assert.Contains("newer Orblit", error.Message);
    }

    [Fact]
    public void KeepsTheEntitiesItCanReadAndNamesTheOnesItCannot()
    {
        var load = SceneDocument.Decode(SceneOf(
            Entity("a"),
            Map(("name", "no id of its own")),
            Entity("a", name: "the second a"),
            Entity("b")));

        Assert.Equal(["a", "b"], load.Document.Entities.Select(e => e.Id));
        Assert.Equal(2, load.Problems.Count);
        Assert.Contains("has no id", load.Problems[0]);
        Assert.Contains("share the id \"a\"", load.Problems[^1]);
    }

    [Fact]
    public void AParentThatIsNotInTheFileBecomesTheTopLevel()
    {
        var load = SceneDocument.Decode(SceneOf(Entity("child", name: "Lamp", parent: "gone")));

        Assert.Null(load.Document["child"]!.Parent);
        Assert.Contains("\"Lamp\" belonged to something", Assert.Single(load.Problems));
    }

    [Fact]
    public void ALoopOfParentsIsCutRatherThanHangingTheOutliner()
    {
        var load = SceneDocument.Decode(SceneOf(Entity("a", parent: "b"), Entity("b", parent: "a")));

        // One link is enough to cut: the first entity walked finds itself and is broken out, which
        // leaves the other hanging from a root rather than from a loop. Breaking both would be the
        // wrong answer, it would scatter a nested group across the top level to fix one bad link.
        Assert.Single(load.Document.Roots);
        Assert.Contains("was inside itself", Assert.Single(load.Problems));

        foreach (var entity in load.Document.Entities)
        {
            var walked = new HashSet<string> { entity.Id };
            var current = load.Document[entity.Id]!.Parent;
            while (current is not null)
            {
                Assert.True(walked.Add(current), "a loop survived");
                current = load.Document[current]!.Parent;
            }
        }
    }

    // writing a file

    [Fact]
    public void SavingTheSameSceneTwiceGivesIdenticalBytes()
    {
        var load = SceneDocument.Decode(SceneOf(
            Entity("lamp", components: Map(
                (SceneComponents.Light, new LightComponent().ToJson()),
                (SceneComponents.Transform, new TransformComponent().ToJson()))),
            Entity("floor", components: Map((SceneComponents.Mesh, new MeshComponent(sway: 0.4).ToJson())))));

        var once = load.Document.Encode();
        var twice = SceneDocument.Decode(once).Document.Encode();
        var thrice = SceneDocument.Decode(twice).Document.Encode();

        Assert.Equal(once, twice);
        Assert.Equal(once, thrice);
    }

    [Fact]
    public void ComponentsAreWrittenInAFixedOrderNotTheOrderRead()
    {
        var load = SceneDocument.Decode(SceneOf(Entity("thing", components: Map(
            (SceneComponents.Data, new DataComponent(["a.json"]).ToJson()),
            (SceneComponents.Mesh, new MeshComponent().ToJson()),
            (SceneComponents.Transform, new TransformComponent().ToJson())))));

        var written = load.Document.Encode();
        Assert.True(written.IndexOf("\"transform\"", StringComparison.Ordinal) < written.IndexOf("\"mesh\"", StringComparison.Ordinal));
        Assert.True(written.IndexOf("\"mesh\"", StringComparison.Ordinal) < written.IndexOf("\"data\"", StringComparison.Ordinal));
    }

    [Fact]
    public void HiddenIsWrittenAndVisibleIsNot()
    {
        var shown = new SceneEntity("a", "a");
        var hidden = shown.CopyWith(visible: false);

        Assert.False(shown.ToJson().ContainsKey("visible"));
        Assert.Equal(false, hidden.ToJson()["visible"]);
    }

    // components

    [Fact]
    public void OneThisVersionHasNeverHeardOfSurvivesARoundTrip()
    {
        var load = SceneDocument.Decode(SceneOf(Entity("thing", components: Map(
            ("buoyancy", Map(("displacement", 3.5), ("notes", "from a newer editor")))))));

        var component = load.Document["thing"]!["buoyancy"];
        Assert.IsType<UnknownComponent>(component);
        Assert.Equal(3.5, component.ToJson()["displacement"]);
        Assert.Equal(
            "from a newer editor",
            SceneDocument.Decode(load.Document.Encode()).Document["thing"]!["buoyancy"]!.ToJson()["notes"]);
    }

    [Fact]
    public void ALightWritesEveryFieldIncludingTheOnesItIsNotUsing()
    {
        var json = new LightComponent().ToJson();

        foreach (var key in new[] { "kind", "power", "spotSize", "sunAngle", "body" })
        {
            Assert.Contains(key, json.Keys);
        }
    }

    [Fact]
    public void AnEntityIsWhatItHasSoALampCanBeBothAtOnce()
    {
        var lamp = new SceneEntity("lamp", "Lamp", components: new Dictionary<string, SceneComponent>
        {
            [SceneComponents.Mesh] = new MeshComponent(asset: "lamp.glb"),
            [SceneComponents.Light] = new LightComponent(power: 40),
        });

        Assert.True(lamp.Has(SceneComponents.Mesh));
        Assert.True(lamp.Has(SceneComponents.Light));
    }

    // settings

    [Fact]
    public void AFileThatSaysNothingAboutItsSkyGetsANewScenes()
    {
        var load = SceneDocument.Decode(SceneOf());
        Assert.Equal(0x1A / 255.0, load.Document.Settings.Sky.Red, 0.001);
    }

    [Fact]
    public void ASkyItCannotReadFallsBackRatherThanGoingBlack()
    {
        var load = SceneDocument.Decode(OldFile(Map(("sky", "not a colour"))));
        Assert.Equal(0x59 / 255.0, load.Document.Settings.Sky.Red, 0.001);
    }

    [Fact]
    public void ASceneWithNoLayerNamesDoesNotWriteTheKey()
    {
        var document = new SceneDocument("Scene");
        var read = SceneDocument.Decode(document.Encode());

        Assert.DoesNotContain("layerNames", Assert.IsAssignableFrom<JsonMap>(JsonText.Decode(document.Encode())).Keys);
        Assert.Empty(read.Document.Settings.LayerNames);
    }

    [Fact]
    public void LayerNamesAreWrittenAndReadBack()
    {
        var document = new SceneDocument("Scene", new SceneSettings(layerNames: ["World", "", "Player"]));
        var read = SceneDocument.Decode(document.Encode()).Document.Settings;

        Assert.Equal(["World", "", "Player"], read.LayerNames);
        Assert.Equal("World", read.NameOf(0));
        Assert.Equal("Player", read.NameOf(2));
    }

    [Fact]
    public void ALayerWithNoNameOrPastTheLastHasNone()
    {
        var settings = new SceneSettings(layerNames: ["World", "", "Player"]);

        Assert.Null(settings.NameOf(1));
        Assert.Null(settings.NameOf(3));
        Assert.Null(settings.NameOf(-1));
        Assert.Null(settings.NameOf(SceneSettings.LayerCount));
    }

    [Fact]
    public void ANameThatIsNotTextLeavesItsLayerUnnamed()
    {
        var load = SceneDocument.Decode(OldFile(Map(("layerNames", List("World", 7L, null, "Player")))));

        Assert.Equal(["World", "", "", "Player"], load.Document.Settings.LayerNames);
    }

    [Fact]
    public void OnlyThirtyTwoLayersCanBeNamed()
    {
        var load = SceneDocument.Decode(OldFile(Map(
            ("layerNames", Enumerable.Range(0, 40).Select(i => (object?)$"Layer {i}").ToList()))));

        Assert.Equal(32, load.Document.Settings.LayerNames.Count);
        Assert.Equal("Layer 31", load.Document.Settings.NameOf(31));
    }

    [Fact]
    public void LayerNamesThatAreNotAListAreIgnored()
    {
        var load = SceneDocument.Decode(OldFile(Map(("layerNames", "World"))));

        Assert.Empty(load.Document.Settings.LayerNames);
    }

    /// <summary>A scene with no layouts, written the way the encoder writes one.</summary>
    private static string SceneOf(params object?[] entities) => JsonText.Encode(Map(
        ("formatVersion", (long)SceneDocument.PlainVersion),
        ("name", "Scene"),
        ("entities", entities)));

    private static OrderedDictionary<string, object?> Entity(
        string id,
        string? name = null,
        string? parent = null,
        JsonMap? components = null)
    {
        var map = Map(("id", id), ("name", name ?? id));
        if (parent is not null)
        {
            map["parent"] = parent;
        }

        map["components"] = components ?? Map();
        return map;
    }

    private static string OldFile(JsonMap rest) => JsonText.Encode(With(
        Map(("formatVersion", 4L)),
        [.. rest.Select(pair => (pair.Key, pair.Value)), ("entities", List())]));
}
