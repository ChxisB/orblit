using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class SceneDiffTests
{
    private static readonly IReadOnlyDictionary<string, SceneComponent> NoComponents =
        new Dictionary<string, SceneComponent>();

    // what changed

    [Fact]
    public void NothingBetweenADocumentAndItself()
    {
        var document = DocumentOf([Thing("a"), Thing("b")]);
        Assert.True(SceneDiff.Between(document, document).IsEmpty);
    }

    [Fact]
    public void AFieldAsOneOperationRatherThanTheWholeEntity()
    {
        var a = DocumentOf([Thing("crate", components: Parts((SceneComponents.Transform, new TransformComponent())))]);
        var b = DocumentOf([Thing(
            "crate",
            components: Parts((SceneComponents.Transform, new TransformComponent(position: new Vector3(0, 3, 0)))))]);

        var diff = SceneDiff.Between(a, b);
        Assert.Equal("position", Assert.IsType<SetField>(Assert.Single(diff.Operations)).Field);
        ExpectRoundTrip(a, b);
    }

    [Fact]
    public void AComponentGainedWhichIsNotTheSameAsAFieldChanging()
    {
        var a = DocumentOf([Thing("lamp")]);
        var b = DocumentOf([Thing("lamp", components: Parts((SceneComponents.Light, new LightComponent(power: 40))))]);

        var diff = SceneDiff.Between(a, b);
        Assert.Null(Assert.IsType<SetComponent>(Assert.Single(diff.Operations)).From);
        ExpectRoundTrip(a, b);
    }

    [Fact]
    public void AComponentLostAndUndoingItBringsBackWhatItHeld()
    {
        var a = DocumentOf([Thing(
            "lamp",
            components: Parts((SceneComponents.Light, new LightComponent(power: 40, spotSize: 12))))]);
        var b = DocumentOf([Thing("lamp")]);

        ExpectRoundTrip(a, b);

        var back = SceneDiff.Between(a, b).Inverse.ApplyTo(b);
        var light = Assert.IsType<LightComponent>(back["lamp"]![SceneComponents.Light]);
        Assert.Equal(40, light.Power);
        Assert.Equal(12, light.SpotSize);
    }

    [Fact]
    public void ARename()
    {
        var a = DocumentOf([Thing("crate", name: "Crate")]);
        var b = DocumentOf([Thing("crate", name: "Barrel")]);

        Assert.IsType<SetEntityName>(Assert.Single(SceneDiff.Between(a, b).Operations));
        ExpectRoundTrip(a, b);
    }

    [Fact]
    public void HidingSomething()
    {
        var a = DocumentOf([Thing("crate")]);
        var b = DocumentOf([Thing("crate", visible: false)]);

        Assert.IsType<SetVisible>(Assert.Single(SceneDiff.Between(a, b).Operations));
        ExpectRoundTrip(a, b);
    }

    [Fact]
    public void TheScenesOwnSettingsAndItsName()
    {
        var a = DocumentOf([], name: "Scene");
        var b = DocumentOf(
            [],
            name: "Courtyard",
            settings: new SceneSettings(ambient: 4000, timeOfDay: 21, dayCycle: true));

        Assert.Equal(4, SceneDiff.Between(a, b).Operations.Count);
        ExpectRoundTrip(a, b);
    }

    [Fact]
    public void NamingALayerIsOneSettingAndItGoesBack()
    {
        var a = DocumentOf([]);
        var b = DocumentOf([], settings: new SceneSettings(layerNames: ["World", "", "Player"]));
        var operation = Assert.Single(SceneDiff.Between(a, b).Operations);

        Assert.Equal(SetSetting.LayerNames, Assert.IsType<SetSetting>(operation).Field);
        ExpectRoundTrip(a, b);
    }

    [Fact]
    public void RenamingALayerThatAlreadyHadAName()
    {
        var a = DocumentOf([], settings: new SceneSettings(layerNames: ["World"]));
        var b = DocumentOf([], settings: new SceneSettings(layerNames: ["Ground"]));

        Assert.Single(SceneDiff.Between(a, b).Operations);
        ExpectRoundTrip(a, b);
    }

    // the tree

    [Fact]
    public void AddingAnEntityPutsItWhereItBelongsNotAtTheEnd()
    {
        var a = DocumentOf([Thing("a"), Thing("c")]);
        var b = DocumentOf([Thing("a"), Thing("b"), Thing("c")]);

        Assert.Equal(["a", "b", "c"], SceneDiff.Between(a, b).ApplyTo(a).Entities.Select(e => e.Id));
        ExpectRoundTrip(a, b);
    }

    [Fact]
    public void AddingAtTheHead() =>
        ExpectRoundTrip(DocumentOf([Thing("b")]), DocumentOf([Thing("a"), Thing("b")]));

    [Fact]
    public void RemovingSeveralInARowAndPuttingThemAllBack() =>
        ExpectRoundTrip(
            DocumentOf([Thing("a"), Thing("b"), Thing("c"), Thing("d")]),
            DocumentOf([Thing("a"), Thing("d")]));

    [Fact]
    public void Reparenting()
    {
        var a = DocumentOf([Thing("props"), Thing("crate")]);
        var b = DocumentOf([Thing("props"), Thing("crate", parent: "props")]);

        Assert.IsType<Reparent>(Assert.Single(SceneDiff.Between(a, b).Operations));
        ExpectRoundTrip(a, b);
    }

    [Fact]
    public void ReorderingSiblings()
    {
        var a = DocumentOf([Thing("props"), Thing("crate", parent: "props"), Thing("barrel", parent: "props")]);
        var b = DocumentOf([Thing("props"), Thing("barrel", parent: "props"), Thing("crate", parent: "props")]);

        Assert.Equal(
            ["barrel", "crate"],
            SceneDiff.Between(a, b).ApplyTo(a).SiblingsOf("props").Select(e => e.Id));
        ExpectRoundTrip(a, b);
    }

    [Fact]
    public void AReversalOfEverything() =>
        ExpectRoundTrip(
            DocumentOf([Thing("a"), Thing("b"), Thing("c"), Thing("d")]),
            DocumentOf([Thing("d"), Thing("c"), Thing("b"), Thing("a")]));

    [Fact]
    public void AllOfItAtOnce()
    {
        var a = DocumentOf([
            Thing("group"),
            Thing(
                "crate",
                name: "Crate",
                parent: "group",
                components: Parts(
                    (SceneComponents.Transform, new TransformComponent()),
                    (SceneComponents.Mesh, new MeshComponent(asset: "crate.glb")))),
            Thing("lamp", components: Parts((SceneComponents.Light, new LightComponent(power: 100)))),
            Thing("old"),
        ]);
        var b = DocumentOf(
            [
                Thing("new"),
                Thing(
                    "lamp",
                    components: Parts(
                        (SceneComponents.Light, new LightComponent(power: 250)),
                        (SceneComponents.Mesh, new MeshComponent(asset: "lamp.glb")))),
                Thing("group"),
                Thing(
                    "crate",
                    name: "Barrel",
                    visible: false,
                    components: Parts(
                        (SceneComponents.Transform, new TransformComponent(position: new Vector3(1, 0, 2))))),
            ],
            name: "Courtyard");

        ExpectRoundTrip(a, b);
    }

    // a diff as something that can be stored

    [Fact]
    public void SurvivesBeingWrittenOutAndReadBack()
    {
        var a = DocumentOf([
            Thing("crate", components: Parts((SceneComponents.Transform, new TransformComponent()))),
            Thing("gone"),
        ]);
        var b = DocumentOf([
            Thing(
                "crate",
                name: "Barrel",
                components: Parts(
                    (SceneComponents.Transform, new TransformComponent(position: new Vector3(4, 0, 0))),
                    (SceneComponents.Mesh, new MeshComponent()))),
            Thing("added"),
        ]);

        var diff = SceneDiff.Between(a, b);
        var stored = SceneDiff.FromJson(diff.ToJson());

        Assert.Equal(diff.Operations.Count, stored.Operations.Count);
        Assert.Equal(b.Encode(), stored.ApplyTo(a).Encode());
        Assert.Equal(a.Encode(), stored.Inverse.ApplyTo(stored.ApplyTo(a)).Encode());
    }

    [Fact]
    public void AnUnknownComponentDiffsByItsFieldsLikeAnyOther()
    {
        var a = DocumentOf([Thing("thing", components: Parts(("buoyancy", Buoyancy(1.0))))]);
        var b = DocumentOf([Thing("thing", components: Parts(("buoyancy", Buoyancy(9.0))))]);

        Assert.IsType<SetField>(Assert.Single(SceneDiff.Between(a, b).Operations));
        ExpectRoundTrip(a, b);
    }

    private static UnknownComponent Buoyancy(double displacement) =>
        new("buoyancy", Map(("displacement", displacement)));

    private static SceneDocument DocumentOf(
        IReadOnlyList<SceneEntity> entities,
        string name = "Scene",
        SceneSettings? settings = null) => new(name, settings, entities);

    private static SceneEntity Thing(
        string id,
        string? name = null,
        string? parent = null,
        bool visible = true,
        IReadOnlyDictionary<string, SceneComponent>? components = null) =>
        new(id, name ?? id, parent, visible, components ?? NoComponents);

    private static OrderedDictionary<string, SceneComponent> Parts(params (string Type, SceneComponent Component)[] pairs)
    {
        var parts = new OrderedDictionary<string, SceneComponent>();
        foreach (var (type, component) in pairs)
        {
            parts[type] = component;
        }

        return parts;
    }

    private static void ExpectRoundTrip(SceneDocument a, SceneDocument b) =>
        DiffAssert.RoundTrips(SceneDiff.Between(a, b), a, b);
}
