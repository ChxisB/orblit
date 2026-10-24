using Orblit.Filament;
using Orblit.Light;
using Orblit.Scene;
using Orblit.VectorMath;
using Orblit.Weather;
using static Orblit.Stage.Tests.Documents;

namespace Orblit.Stage.Tests;

public class DocumentViewTests
{
    private static void ExpectSameAsRebuilt(DocumentView moved, SceneDocument wanted) =>
        Assert.Equal(ViewShape.Of(new DocumentView(wanted)), ViewShape.Of(moved));

    private static SceneEntity Placed(string id, Vector3 at) => Thing(
        id,
        components: Parts((SceneComponents.Transform, new TransformComponent(position: at))));

    [Fact]
    public void AMeshEntityIsAnObjectAndALightEntityIsALight()
    {
        var view = new DocumentView(Of(
            Crate("crate"),
            Thing("sun", components: Parts((SceneComponents.Light, new LightComponent(power: 2))))));

        Assert.Single(view.Scene.Objects);
        Assert.Equal(RenderLightKind.Directional, Assert.Single(view.Scene.Lights).Kind);
    }

    [Fact]
    public void ALampThatIsBothIsBothWithDifferentKeys()
    {
        var view = new DocumentView(Of(Thing(
            "lamp",
            components: Parts(
                (SceneComponents.Mesh, new MeshComponent()),
                (SceneComponents.Light, new LightComponent(LightType.Point))))));

        Assert.NotEqual(Assert.Single(view.Scene.Lights).Key, Assert.Single(view.Scene.Objects).Key);
    }

    [Fact]
    public void AChildIsPlacedByItsParentAsWellAsByItself()
    {
        var view = new DocumentView(Of(
            Placed("group", new Vector3(10, 0, 0)),
            Crate("crate", "group", new Vector3(0, 0, 5))));

        Near.Vector(Assert.Single(view.Scene.Objects).Transform.GetTranslation(), new Vector3(10, 0, 5));
    }

    [Fact]
    public void HidingAGroupHidesWhatIsInsideIt()
    {
        var view = new DocumentView(Of(Thing("group", visible: false), Crate("crate", "group")));

        Assert.False(Assert.Single(view.Scene.Objects).Visible);
    }

    [Fact]
    public void AHiddenLightIsLeftOutRatherThanSentDark()
    {
        var view = new DocumentView(Of(Thing(
            "sun",
            visible: false,
            components: Parts((SceneComponents.Light, new LightComponent())))));

        Assert.Empty(view.Scene.Lights);
    }

    [Fact]
    public void TheListsComeOutInDocumentOrder()
    {
        var view = new DocumentView(Of(Crate("a"), Crate("b"), Crate("c")));
        var keys = view.Scene.Objects.Select(one => one.Key).ToList();

        view.Replace(Of(Crate("c"), Crate("a"), Crate("b")));

        Assert.Equal([keys[2], keys[0], keys[1]], view.Scene.Objects.Select(one => one.Key));
    }

    [Fact]
    public void WeatherBecomesTheAirAndWhatIsFallingOutOfIt()
    {
        var view = new DocumentView(Of(Thing(
            "weather",
            components: Parts((
                SceneComponents.Weather,
                new WeatherComponent(WeatherState.Of(WeatherCondition.Storm) with { Rain = 0.8 }, WeatherCondition.Storm))))));

        Assert.True(view.Scene.Fog.Density > 0);
        Assert.Equal(0.8, view.Scene.Precipitation.Amount);
    }

    [Fact]
    public void ASceneWithNoWeatherInItHasClearAir()
    {
        var view = new DocumentView(Of(Crate("a")));

        Assert.Equal(0, view.Scene.Fog.Density);
        Assert.Equal(0, view.Scene.Precipitation.Amount);
    }

    [Fact]
    public void ProjectPathsAreRootedAndAbsoluteOnesAreLeftAlone()
    {
        var view = new DocumentView(
            Of(
                Thing("a", components: Parts((SceneComponents.Mesh, new MeshComponent("models/a.glb")))),
                Thing("b", components: Parts((SceneComponents.Mesh, new MeshComponent("/tmp/b.glb"))))),
            projectRoot: "/projects/one");

        Assert.Equal("/projects/one/models/a.glb", view.Scene.Objects[0].Mesh);
        Assert.Equal("/tmp/b.glb", view.Scene.Objects[1].Mesh);
    }

    [Fact]
    public void AFieldChanging()
    {
        var after = Of(Crate("crate", at: new Vector3(0, 4, 0)));

        var view = new DocumentView(Of(Crate("crate")));
        view.Replace(after);

        Assert.Equal(4, Assert.Single(view.Scene.Objects).Transform.GetTranslation().Y);
        ExpectSameAsRebuilt(view, after);
    }

    [Fact]
    public void AParentMovingTakesItsChildrenWithIt()
    {
        var after = Of(Placed("group", new Vector3(10, 0, 0)), Crate("crate", "group", new Vector3(0, 0, 5)));

        var view = new DocumentView(Of(Thing("group"), Crate("crate", "group", new Vector3(0, 0, 5))));
        view.Replace(after);

        Near.Vector(Assert.Single(view.Scene.Objects).Transform.GetTranslation(), new Vector3(10, 0, 5));
        ExpectSameAsRebuilt(view, after);
    }

    [Fact]
    public void Reparenting()
    {
        var before = Of(Placed("a", new Vector3(3, 0, 0)), Placed("b", new Vector3(0, 7, 0)), Crate("crate", "a"));
        var after = before.WithEntity("crate", before["crate"]!.CopyWith(parent: "b"));

        var view = new DocumentView(before);
        view.Replace(after);

        Near.Vector(Assert.Single(view.Scene.Objects).Transform.GetTranslation(), new Vector3(0, 7, 0));
        ExpectSameAsRebuilt(view, after);
    }

    [Fact]
    public void AddingAndRemoving()
    {
        var after = Of(Crate("a"), Crate("c"));

        var view = new DocumentView(Of(Crate("a"), Crate("b")));
        view.Replace(after);

        Assert.Equal(2, view.Scene.Objects.Count);
        ExpectSameAsRebuilt(view, after);
    }

    [Fact]
    public void AComponentGainedAndLost()
    {
        var before = Of(Crate("lamp"));
        var after = Of(Thing(
            "lamp",
            components: Parts(
                (SceneComponents.Transform, new TransformComponent()),
                (SceneComponents.Light, new LightComponent(power: 60)))));

        var view = new DocumentView(before);
        view.Replace(after);
        Assert.Empty(view.Scene.Objects);
        Assert.Single(view.Scene.Lights);
        ExpectSameAsRebuilt(view, after);

        view.Replace(before);
        Assert.Empty(view.Scene.Lights);
        ExpectSameAsRebuilt(view, before);
    }

    [Fact]
    public void TheScenesOwnSettings()
    {
        var before = Of(Crate("a"));
        var after = before.CopyWith(settings: new SceneSettings(sky: Tint.Hex(0x884422), ambient: 900));

        var view = new DocumentView(before);
        view.Replace(after);

        Assert.Equal(900, view.Scene.Sky.Ambient);
        ExpectSameAsRebuilt(view, after);
    }

    [Fact]
    public void KeysSurviveAnEditSoTheRendererKeepsWhatItBuilt()
    {
        var view = new DocumentView(Of(Crate("crate")));
        var key = Assert.Single(view.Scene.Objects).Key;

        view.Replace(Of(Crate("crate", at: new Vector3(1, 1, 1))));

        Assert.Equal(key, Assert.Single(view.Scene.Objects).Key);
    }

    [Fact]
    public void AnEmptyDiffChangesNothingAtAll()
    {
        var view = new DocumentView(Of(Crate("a")));
        var before = Assert.Single(view.Scene.Objects).Key;

        view.Apply(SceneDiff.None);

        Assert.Equal(before, Assert.Single(view.Scene.Objects).Key);
    }
}
