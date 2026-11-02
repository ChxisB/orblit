using Orblit.Json;
using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class SceneLayoutsTests
{
    // which layout a window wants

    [Theory]
    [InlineData(390, 844, LayoutShape.Portrait)]
    [InlineData(844, 390, LayoutShape.Landscape)]
    [InlineData(600, 600, LayoutShape.Landscape)]
    [InlineData(0, 0, LayoutShape.Landscape)]
    public void TallerThanWideIsPortraitAndAnythingElseIsLandscape(double width, double height, LayoutShape expected) =>
        Assert.Equal(expected, LayoutShapes.Of(width, height));

    // what a layout may hold

    [Fact]
    public void ALayoutRefusesAnEditThatIsNotAnAppearance()
    {
        SceneOp[] refused =
        [
            new SetEntityName("cam", "Camera", "Eye"),
            new Reparent("hud", null, "cam"),
            new SetSetting(SetSetting.Ambient, 1.0, 2.0),
        ];

        foreach (var op in refused)
        {
            Assert.Throws<ArgumentException>(() => new SceneLayouts(new SceneDiff([op]), SceneDiff.None));
            Assert.Throws<ArgumentException>(() => new SceneLayouts(SceneDiff.None, new SceneDiff([op])));
        }
    }

    [Fact]
    public void NothingIsEmptyAndSomethingIsNot()
    {
        Assert.True(SceneLayouts.None.IsEmpty);
        Assert.False(SceneLayouts.None.With(LayoutShape.Portrait, WiderView(70)).IsEmpty);
    }

    [Fact]
    public void WithReplacesOneShapeAndLeavesTheOther()
    {
        var both = SceneLayouts.None
            .With(LayoutShape.Portrait, WiderView(70))
            .With(LayoutShape.Landscape, WiderView(40));

        var cleared = both.With(LayoutShape.Portrait, SceneDiff.None);

        Assert.True(cleared[LayoutShape.Portrait].IsEmpty);
        Assert.Same(both[LayoutShape.Landscape], cleared[LayoutShape.Landscape]);
        Assert.Single(both[LayoutShape.Portrait].Operations);
    }

    // reading a layout on

    [Fact]
    public void InLayoutMovesACameraAndLeavesTheBaseAlone()
    {
        var scene = Base().CopyWith(layouts: SceneLayouts.None.With(LayoutShape.Portrait, WiderView(70)));

        var tall = scene.InLayout(LayoutShape.Portrait);

        Assert.Equal(70, Assert.IsType<CameraComponent>(tall["cam"]![SceneComponents.Camera]).FieldOfView);
        Assert.Equal(50, Assert.IsType<CameraComponent>(scene["cam"]![SceneComponents.Camera]).FieldOfView);
        Assert.True(tall.Layouts.IsEmpty);
    }

    [Fact]
    public void ALayoutThatSaysNothingLeavesTheSceneAsItIs()
    {
        var scene = Base().CopyWith(layouts: SceneLayouts.None.With(LayoutShape.Portrait, WiderView(70)));

        Assert.Equal(Base().Encode(), scene.InLayout(LayoutShape.Landscape).Encode());
    }

    [Fact]
    public void AnEditCanHideAndShowAnObject()
    {
        var hidden = new SceneDiff([new SetVisible("hud", true, false)]);
        var scene = Base().CopyWith(layouts: SceneLayouts.None.With(LayoutShape.Landscape, hidden));

        Assert.False(scene.InLayout(LayoutShape.Landscape)["hud"]!.Visible);
        Assert.True(scene.InLayout(LayoutShape.Portrait)["hud"]!.Visible);
    }

    [Fact]
    public void AnEditThatFollowsAnotherSeesItsResult()
    {
        var diff = new SceneDiff(
        [
            new SetComponent("hud", SceneComponents.Light, null, new LightComponent(power: 40).ToJson()),
            new SetField("hud", SceneComponents.Light, "power", 40.0, 90.0),
        ]);
        var layouts = SceneLayouts.None.With(LayoutShape.Portrait, diff);
        var scene = Base().CopyWith(layouts: layouts);

        var light = Assert.IsType<LightComponent>(scene.InLayout(LayoutShape.Portrait)["hud"]![SceneComponents.Light]);

        Assert.Equal(90, light.Power);
        Assert.Equal(0, layouts.CountMissed(scene, LayoutShape.Portrait));
    }

    // an object that is not there

    [Fact]
    public void AnObjectThatIsGoneIsSkippedAndCounted()
    {
        // An id inside a folded prefab instance is in this position too: the document holds the
        // instance and not its parts, so nothing by that id is there to change.
        var diff = new SceneDiff(
        [
            new SetVisible("crate/lid", true, false),
            new SetField("cam", SceneComponents.Camera, "fieldOfView", 50.0, 70.0),
            new SetField("hud", SceneComponents.Camera, "fieldOfView", 50.0, 70.0),
        ]);
        var layouts = SceneLayouts.None.With(LayoutShape.Portrait, diff);
        var scene = Base().CopyWith(layouts: layouts);

        var tall = scene.InLayout(LayoutShape.Portrait);

        Assert.Equal(70, Assert.IsType<CameraComponent>(tall["cam"]![SceneComponents.Camera]).FieldOfView);
        Assert.Null(tall["hud"]![SceneComponents.Camera]);
        Assert.Equal(2, layouts.CountMissed(scene, LayoutShape.Portrait));
    }

    [Fact]
    public void NothingIsMissedWhenEveryObjectIsThere()
    {
        var scene = Base().CopyWith(layouts: SceneLayouts.None.With(LayoutShape.Portrait, WiderView(70)));

        Assert.Equal(0, scene.Layouts.CountMissed(scene, LayoutShape.Portrait));
    }

    // writing and reading

    [Fact]
    public void ASceneWithNoLayoutsIsWrittenAtThePlainVersion()
    {
        var parsed = Assert.IsAssignableFrom<JsonMap>(JsonText.Decode(Base().Encode()));

        Assert.Equal((long)SceneDocument.PlainVersion, parsed["formatVersion"]);
        Assert.False(parsed.ContainsKey("layouts"));
    }

    [Fact]
    public void ASceneWithLayoutsIsWrittenAtTheCurrentVersion()
    {
        var scene = Base().CopyWith(layouts: SceneLayouts.None.With(LayoutShape.Portrait, WiderView(70)));

        var parsed = Assert.IsAssignableFrom<JsonMap>(JsonText.Decode(scene.Encode()));

        Assert.Equal((long)SceneDocument.FormatVersion, parsed["formatVersion"]);
        Assert.True(parsed.ContainsKey("layouts"));
    }

    [Fact]
    public void LayoutsComeBackAsTheyWentOut()
    {
        var scene = Base().CopyWith(layouts: SceneLayouts.None
            .With(LayoutShape.Portrait, WiderView(70))
            .With(LayoutShape.Landscape, new SceneDiff([new SetVisible("hud", true, false)])));

        var load = SceneDocument.Decode(scene.Encode());

        Assert.Empty(load.Problems);
        Assert.Equal(scene.Encode(), load.Document.Encode());
        var field = Assert.IsType<SetField>(Assert.Single(load.Document.Layouts[LayoutShape.Portrait].Operations));
        Assert.Equal(70.0, field.To);
        Assert.IsType<SetVisible>(Assert.Single(load.Document.Layouts[LayoutShape.Landscape].Operations));
    }

    [Fact]
    public void ASceneWithOneShapeWritesOnlyThatShape()
    {
        var scene = Base().CopyWith(layouts: SceneLayouts.None.With(LayoutShape.Landscape, WiderView(40)));

        var parsed = Assert.IsAssignableFrom<JsonMap>(JsonText.Decode(scene.Encode()));
        var layouts = Assert.IsAssignableFrom<JsonMap>(parsed["layouts"]);

        Assert.Equal(["landscape"], layouts.Keys);
    }

    [Fact]
    public void LayoutsAreTheLastThingInTheFile()
    {
        var scene = Base().CopyWith(layouts: SceneLayouts.None.With(LayoutShape.Portrait, WiderView(70)));

        var parsed = Assert.IsAssignableFrom<JsonMap>(JsonText.Decode(scene.Encode()));

        Assert.Equal("layouts", parsed.Keys.Last());
    }

    [Fact]
    public void AFileAtTheOldVersionLoadsWithNoLayouts()
    {
        var load = SceneDocument.Decode(Base().Encode());

        Assert.Empty(load.Problems);
        Assert.True(load.Document.Layouts.IsEmpty);
    }

    [Fact]
    public void AFileNewerThanTheFormatIsStillRefused()
    {
        var file = JsonText.Encode(Map(("formatVersion", (long)SceneDocument.FormatVersion + 1), ("entities", List())));

        Assert.Throws<SceneFormatException>(() => SceneDocument.Decode(file));
    }

    [Fact]
    public void AFileThatPutsTheWrongThingInALayoutLosesOnlyThatThing()
    {
        var file = JsonText.Encode(Map(
            ("formatVersion", (long)SceneDocument.FormatVersion),
            ("name", "Scene"),
            ("entities", List()),
            ("layouts", Map(("portrait", List(
                Map(("op", "name"), ("id", "cam"), ("from", "a"), ("to", "b")),
                Map(("op", "visible"), ("id", "hud"), ("from", true), ("to", false))))))));

        var load = SceneDocument.Decode(file);

        Assert.IsType<SetVisible>(Assert.Single(load.Document.Layouts[LayoutShape.Portrait].Operations));
        Assert.Contains("portrait", Assert.Single(load.Problems));
    }

    [Fact]
    public void LayoutsThatAreNotAnObjectAreIgnored()
    {
        var file = JsonText.Encode(Map(
            ("formatVersion", (long)SceneDocument.FormatVersion),
            ("entities", List()),
            ("layouts", "wide")));

        var load = SceneDocument.Decode(file);

        Assert.True(load.Document.Layouts.IsEmpty);
    }

    // a copy keeps what it was not asked to change

    [Fact]
    public void ACopyKeepsTheLayoutsUnlessItIsGivenOthers()
    {
        var layouts = SceneLayouts.None.With(LayoutShape.Portrait, WiderView(70));
        var scene = Base().CopyWith(layouts: layouts);

        Assert.Same(layouts, scene.CopyWith(name: "Renamed").Layouts);
        Assert.Same(layouts, scene.WithEntity("hud", null).Layouts);
        Assert.True(scene.CopyWith(layouts: SceneLayouts.None).Layouts.IsEmpty);
    }

    private static SceneDiff WiderView(double fieldOfView) =>
        new([new SetField("cam", SceneComponents.Camera, "fieldOfView", 50.0, fieldOfView)]);

    private static SceneDocument Base() => new(
        "Scene",
        entities:
        [
            new SceneEntity(
                "cam",
                "Camera",
                components: Parts(
                    (SceneComponents.Transform, new TransformComponent(position: new Vector3(0, 2, 10))),
                    (SceneComponents.Camera, new CameraComponent(50)))),
            new SceneEntity("hud", "Hud"),
        ]);

    private static Dictionary<string, SceneComponent> Parts(params (string Type, SceneComponent Component)[] pairs) =>
        pairs.ToDictionary(pair => pair.Type, pair => pair.Component);
}
