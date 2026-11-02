using Orblit.Json;
using Orblit.Light;
using Orblit.Weather;
using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class SceneMigrationTests
{
    private static readonly double SunSphere = 1000 / (4 * Math.PI);

    // the shape that predates versions

    [Fact]
    public void NamesWhatItCouldNotRecoverInsteadOfStackingItSilently()
    {
        var load = SceneDocument.Decode(OldScene(1,
            ("name", "Old"),
            ("entities", List(
                Map(("name", "Key"), ("components", List("Light", "Transform"))),
                Map(("name", "Eye"), ("components", List("Camera"))),
                Map(("name", "Crate"), ("components", List("MeshRenderer")))))));

        var document = load.Document;
        Assert.Equal("Old", document.Name);
        Assert.Equal(["legacy0", "legacy1", "legacy2"], document.Entities.Select(e => e.Id));
        Assert.True(document["legacy0"]!.Has(SceneComponents.Light));
        Assert.True(document["legacy1"]!.Has(SceneComponents.Camera));
        Assert.True(document["legacy2"]!.Has(SceneComponents.Mesh));
        Assert.Contains("all at the origin", Assert.Single(load.Problems));
    }

    // version one, where a light was stated in watts

    [Fact]
    public void ASunKeepsItsBrightnessByLosingTheSphereItWasSpreadOn()
    {
        var load = SceneDocument.Decode(OldScene(1, ("objects", List(
            Map(("id", "sun"), ("kind", "light"), ("lightType", "sun"), ("power", 1000.0))))));

        var light = Assert.IsType<LightComponent>(load.Document["sun"]![SceneComponents.Light]);
        Assert.Equal(SunSphere, light.Power, 1e-9);
        Assert.Contains("watts per square metre", Assert.Single(load.Problems));
    }

    [Fact]
    public void AnUnnamedLightWasDrawnAsASunSoItIsConvertedAsOne()
    {
        var load = SceneDocument.Decode(OldScene(1, ("objects", List(Map(("id", "light"), ("kind", "light"))))));

        var light = Assert.IsType<LightComponent>(load.Document["light"]![SceneComponents.Light]);
        Assert.Equal(LightType.Sun, light.Kind);
        // The default power of a thousand, over the same sphere.
        Assert.Equal(SunSphere, light.Power, 1e-9);
    }

    [Fact]
    public void APointLightIsLeftAloneBecauseItAlwaysWasABulb()
    {
        var load = SceneDocument.Decode(OldScene(1, ("objects", List(
            Map(("id", "bulb"), ("kind", "light"), ("lightType", "point"), ("power", 60.0))))));

        var light = Assert.IsType<LightComponent>(load.Document["bulb"]![SceneComponents.Light]);
        Assert.Equal(60.0, light.Power);
    }

    // version two, where the air was a block on the scene

    [Fact]
    public void FogBecomesAWeatherEntityThatCanHoldAChange()
    {
        var load = SceneDocument.Decode(OldScene(2,
            ("objects", List()),
            ("fog", Map(
                ("colour", "#405060"),
                ("density", 0.4),
                ("height", 12.0),
                ("falloff", 0.3),
                ("mist", 0.5),
                ("mistSize", 20.0),
                ("mistSpeed", 0.25)))));

        var weather = Assert.IsType<WeatherComponent>(load.Document["weather"]![SceneComponents.Weather]);
        Assert.Equal(WeatherCondition.Misty, weather.Condition);
        Assert.Equal(0.4, weather.Air.FogDensity);
        Assert.Equal(0.5, weather.Air.Mist);
        // Nothing in the old shape said anything about cloud.
        Assert.Equal(0, weather.Air.CloudCover);
        // The old drift was a rate, not a speed: four metres a second per unit.
        Assert.Equal(1.0, weather.Air.WindSpeed, 1e-9);
        Assert.Contains("now a Weather object", Assert.Single(load.Problems));
    }

    [Fact]
    public void FogWithNoMistInItIsHazyRatherThanMisty()
    {
        var load = SceneDocument.Decode(OldScene(2,
            ("objects", List()),
            ("fog", Map(("density", 0.2)))));

        var weather = Assert.IsType<WeatherComponent>(load.Document["weather"]![SceneComponents.Weather]);
        Assert.Equal(WeatherCondition.Hazy, weather.Condition);
    }

    [Fact]
    public void ASceneWithNoFogInItGainsNothing()
    {
        var load = SceneDocument.Decode(OldScene(2,
            ("objects", List(Map(("id", "crate"), ("kind", "mesh")))),
            ("fog", Map(("density", 0.0)))));

        Assert.Equal(["crate"], load.Document.Entities.Select(e => e.Id));
        Assert.Empty(load.Problems);
    }

    [Fact]
    public void TheInventedEntityDoesNotTakeAnIdSomethingElseIsUsing()
    {
        var load = SceneDocument.Decode(OldScene(2,
            ("objects", List(Map(("id", "weather"), ("kind", "mesh")))),
            ("fog", Map(("density", 0.3)))));

        Assert.Equal(["weather", "weather2"], load.Document.Entities.Select(e => e.Id));
        Assert.True(load.Document["weather2"]!.Has(SceneComponents.Weather));
    }

    // version three, where an object said what it was

    [Fact]
    public void AMeshBecomesAnEntityWithATransformAndAMesh()
    {
        var load = SceneDocument.Decode(OldScene(3, ("objects", List(Map(
            ("id", "floor"),
            ("name", "Floor"),
            ("kind", "mesh"),
            ("position", List(0.0, -1.0, 0.0)),
            ("scale", List(30.0, 1.0, 30.0)),
            ("colour", "#8899AA"),
            ("mesh", "models/floor.glb"),
            ("material", "materials/stone.omat"),
            ("castShadows", false),
            ("sway", 0.25),
            ("data", List("scripts/floor.js")))))));

        var entity = load.Document["floor"]!;
        Assert.Equal("Floor", entity.Name);

        var transform = Assert.IsType<TransformComponent>(entity[SceneComponents.Transform]);
        Assert.Equal(-1.0, transform.Position.Y);
        Assert.Equal(30.0, transform.Scale.X);

        var mesh = Assert.IsType<MeshComponent>(entity[SceneComponents.Mesh]);
        Assert.Equal("models/floor.glb", mesh.Asset);
        Assert.False(mesh.CastShadows);
        Assert.True(mesh.ReceiveShadows);
        Assert.Equal(0.25, mesh.Sway);
        Assert.Equal(0x88 / 255.0, mesh.Colour.Red, 0.001);

        Assert.Equal("materials/stone.omat", Assert.IsType<MaterialComponent>(entity[SceneComponents.Material]).Asset);
        Assert.Equal(["scripts/floor.js"], Assert.IsType<DataComponent>(entity[SceneComponents.Data]).Paths);
        Assert.Empty(load.Problems);
    }

    [Fact]
    public void AShapeIsDrawnTooSoItGetsAMeshComponentAsWell()
    {
        var load = SceneDocument.Decode(OldScene(3, ("objects", List(Map(("id", "box"), ("kind", "shape"))))));

        Assert.True(load.Document["box"]!.Has(SceneComponents.Mesh));
    }

    [Fact]
    public void ALightKeepsEveryFieldIncludingTheOnesItIsNotUsing()
    {
        var load = SceneDocument.Decode(OldScene(3, ("objects", List(Map(
            ("id", "spot"),
            ("kind", "light"),
            ("lightType", "spot"),
            ("power", 60.0),
            ("spotSize", 30.0),
            ("colour", "#FFEEDD"),
            ("castShadows", false))))));

        var light = Assert.IsType<LightComponent>(load.Document["spot"]![SceneComponents.Light]);
        Assert.Equal(LightType.Spot, light.Kind);
        Assert.Equal(60.0, light.Power);
        Assert.Equal(30.0, light.SpotSize);
        Assert.False(light.CastShadows);
        // A spot has no sun angle, and it is written anyway, so flipping this light to a sun and
        // back does not lose what it was.
        Assert.Equal(0.526, light.SunAngle);
        Assert.Equal(0xDD / 255.0, light.Colour.Blue, 0.001);

        // A light is not drawn, so it gets no mesh.
        Assert.False(load.Document["spot"]!.Has(SceneComponents.Mesh));
    }

    [Fact]
    public void AGroupIsAPlaceInTheTreeAndNothingElse()
    {
        var load = SceneDocument.Decode(OldScene(3, ("objects", List(
            Map(("id", "props"), ("kind", "group")),
            Map(("id", "crate"), ("kind", "mesh"), ("parent", "props"))))));

        Assert.Equal([SceneComponents.Transform], load.Document["props"]!.Components.Keys);
        Assert.Equal("crate", Assert.Single(load.Document.ChildrenOf("props")).Id);
    }

    [Fact]
    public void AKindThisEditorDoesNotKnowIsDroppedAndNamed()
    {
        var load = SceneDocument.Decode(OldScene(3, ("objects", List(
            Map(("id", "crate"), ("kind", "mesh")),
            Map(("id", "portal"), ("kind", "wormhole"))))));

        Assert.Equal(["crate"], load.Document.Entities.Select(e => e.Id));
        Assert.Contains("\"wormhole\"", Assert.Single(load.Problems));
    }

    [Fact]
    public void HiddenSurvivesTheConversion()
    {
        var load = SceneDocument.Decode(OldScene(3, ("objects", List(
            Map(("id", "crate"), ("kind", "mesh"), ("visible", false))))));

        Assert.False(load.Document["crate"]!.Visible);
    }

    [Fact]
    public void TheSceneKeepsItsOwnSettingsAcrossTheConversion()
    {
        var load = SceneDocument.Decode(OldScene(3,
            ("name", "Courtyard"),
            ("sky", "#223344"),
            ("ambient", 12000.0),
            ("time", Map(("hour", 18.5), ("cycle", true), ("hoursPerSecond", 0.25))),
            ("objects", List())));

        var settings = load.Document.Settings;
        Assert.Equal("Courtyard", load.Document.Name);
        Assert.Equal(0x33 / 255.0, settings.Sky.Green, 0.001);
        Assert.Equal(12000.0, settings.Ambient);
        Assert.Equal(18.5, settings.TimeOfDay);
        Assert.True(settings.DayCycle);
        Assert.Equal(0.25, settings.HoursPerSecond);
    }

    [Fact]
    public void AFileWithNoObjectsListInItIsRefused() =>
        Assert.Throws<SceneFormatException>(() => SceneDocument.Decode(OldScene(3, ("name", "Nothing"))));

    // what a drawable is made of

    [Fact]
    public void AShapeSaysTheGeometryIsThisDocumentsToEdit()
    {
        var load = SceneDocument.Decode(OldScene(3, ("objects", List(
            Map(("id", "arch"), ("kind", "shape")),
            Map(("id", "model"), ("kind", "mesh"), ("mesh", "models/tree.glb"))))));

        var arch = Assert.IsType<MeshComponent>(load.Document["arch"]![SceneComponents.Mesh]);
        var model = Assert.IsType<MeshComponent>(load.Document["model"]![SceneComponents.Mesh]);
        Assert.True(arch.Authored);
        Assert.False(model.Authored);

        // And it survives the file, which is the whole point of stating it: guessed from which
        // fields are filled in, an untouched shape looks exactly like a referenced model that names
        // nothing.
        var again = SceneDocument.Decode(load.Document.Encode()).Document;
        Assert.True(Assert.IsType<MeshComponent>(again["arch"]![SceneComponents.Mesh]).Authored);
        Assert.False(Assert.IsType<MeshComponent>(again["model"]![SceneComponents.Mesh]).Authored);
    }

    [Fact]
    public void APlacedModelSaysNothingSoItStaysOutOfTheDiff() =>
        Assert.False(new MeshComponent().ToJson().ContainsKey("authored"));

    // one object on its own, for a clipboard or a prefab

    [Fact]
    public void RunsTheSameChainAFileDoes()
    {
        var notes = new List<string>();
        var entity = SceneMigrations.Entity(
            Map(("id", "sun"), ("kind", "light"), ("lightType", "sun"), ("power", 1000.0)),
            1,
            notes);

        var light = Assert.IsType<LightComponent>(SceneEntity.FromJson(entity!)![SceneComponents.Light]);
        // Version two's conversion, applied to something that was never in a file, which is exactly
        // what a prefab written that long ago is.
        Assert.Equal(SunSphere, light.Power, 1e-9);
        Assert.NotEmpty(notes);
    }

    [Fact]
    public void ACurrentOneIsHandedBackAsItCame()
    {
        var already = Map(
            ("id", "crate"),
            ("name", "Crate"),
            ("components", Map(("mesh", new MeshComponent().ToJson()))));

        Assert.Same(already, SceneMigrations.Entity(already, SceneDocument.FormatVersion));
        Assert.Same(already, SceneMigrations.Entity(already, SceneDocument.PlainVersion));
    }

    [Fact]
    public void AVersionFourOneIsMarkedAsTheCopyItWas()
    {
        var entity = SceneMigrations.Entity(
            Map(
                ("id", "post"),
                ("name", "Post"),
                ("components", Map(("prefab", Map(("asset", "props/lamp.oprefab")))))),
            4);

        var link = Assert.IsType<PrefabComponent>(SceneEntity.FromJson(entity!)![SceneComponents.Prefab]);
        Assert.Equal(PrefabState.Stamped, link.State);
    }

    [Fact]
    public void SomethingWithNothingUsableInItComesBackNull() =>
        Assert.Null(SceneMigrations.Entity(Map(("kind", "mesh")), 3));

    // version four, where an instance was a copy of its prefab

    [Fact]
    public void EveryPartOfACopyIsMarkedSoItCanBeRelinked()
    {
        var load = SceneDocument.Decode(OldScene(4, ("entities", List(
            Map(
                ("id", "o1"),
                ("name", "Lamp"),
                ("components", Map(("prefab", Map(("asset", "props/lamp.oprefab")))))),
            Map(
                ("id", "o2"),
                ("name", "Bulb"),
                ("parent", "o1"),
                ("components", Map(("prefab", Map(("asset", "props/lamp.oprefab")))))),
            Map(("id", "o3"), ("name", "Crate"))))));

        PrefabState? StateOf(string id) => (load.Document[id]![SceneComponents.Prefab] as PrefabComponent)?.State;

        Assert.Equal(PrefabState.Stamped, StateOf("o1"));
        Assert.Equal(PrefabState.Stamped, StateOf("o2"));
        Assert.Null(StateOf("o3"));
        // Nothing is lost by it: a copy is a copy until its prefab is read.
        Assert.Equal("o2", Assert.Single(load.Document.ChildrenOf("o1")).Id);
    }

    // every version arrives at the same place

    [Fact]
    public void AVersionOneFileRunsTheWholeChain()
    {
        var load = SceneDocument.Decode(OldScene(1,
            ("objects", List(Map(("id", "sun"), ("kind", "light"), ("lightType", "sun"), ("power", 1000.0)))),
            ("fog", Map(("density", 0.3), ("mist", 0.2)))));

        var light = Assert.IsType<LightComponent>(load.Document["sun"]![SceneComponents.Light]);
        // Converted by version two...
        Assert.Equal(SunSphere, light.Power, 1e-9);
        // ...given a home by version three...
        Assert.True(load.Document["weather"]!.Has(SceneComponents.Weather));
        // ...and made components by version four.
        Assert.True(load.Document["sun"]!.Has(SceneComponents.Transform));
    }

    [Fact]
    public void AConvertedSceneSavesAtThePlainVersionAndReloadsUnchanged()
    {
        var load = SceneDocument.Decode(OldScene(3, ("objects", List(
            Map(("id", "crate"), ("kind", "mesh"), ("position", List(1.0, 2.0, 3.0)))))));

        var saved = load.Document.Encode();
        var parsed = Assert.IsAssignableFrom<JsonMap>(JsonText.Decode(saved));
        Assert.Equal((long)SceneDocument.PlainVersion, parsed["formatVersion"]);

        var again = SceneDocument.Decode(saved);
        Assert.Empty(again.Problems);
        Assert.Equal(saved, again.Document.Encode());
    }

    /// <summary>A file at an older version, as one was actually written.</summary>
    private static string OldScene(long version, params (string Key, object? Value)[] rest) =>
        JsonText.Encode(With(Map(("formatVersion", version)), rest));
}
