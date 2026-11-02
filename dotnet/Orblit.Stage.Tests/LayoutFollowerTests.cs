using Orblit.Scene;
using Orblit.VectorMath;
using static Orblit.Stage.Tests.Documents;

namespace Orblit.Stage.Tests;

public class LayoutFollowerTests
{
    [Fact]
    public void ATallWindowShowsThePortraitLayout()
    {
        var (follower, view) = Follow(Scene());

        follower.Fit(390, 844);

        Assert.Equal(LayoutShape.Portrait, follower.Shape);
        Assert.Equal(70, view.Camera.FieldOfView);
    }

    [Fact]
    public void AWideWindowShowsTheLandscapeLayout()
    {
        var (follower, view) = Follow(Scene());

        follower.Fit(844, 390);

        Assert.Equal(LayoutShape.Landscape, follower.Shape);
        Assert.Equal(40, view.Camera.FieldOfView);
    }

    [Fact]
    public void NothingIsShownUntilTheWindowHasASize()
    {
        var shown = new List<SceneDiff>();

        var follower = new LayoutFollower(Scene(), shown.Add);

        Assert.Null(follower.Shape);
        Assert.Empty(shown);
    }

    [Fact]
    public void ATurnSwitchesLayoutAndKeepsWhatBothShare()
    {
        var (follower, view) = Follow(Scene());
        follower.Fit(390, 844);
        var crate = Assert.Single(view.Scene.Objects);

        follower.Fit(844, 390);

        Assert.Equal(40, view.Camera.FieldOfView);
        Assert.Equal(crate.Key, Assert.Single(view.Scene.Objects).Key);
    }

    [Fact]
    public void ATurnThereAndBackIsTheSceneItStartedAs()
    {
        var (follower, view) = Follow(Scene());
        follower.Fit(390, 844);
        var upright = ViewShape.Of(view);

        follower.Fit(844, 390);
        follower.Fit(390, 844);

        Assert.Equal(upright, ViewShape.Of(view));
    }

    [Fact]
    public void AResizeThatKeepsTheShapeShowsNothingAgain()
    {
        var shown = new List<SceneDiff>();
        var follower = new LayoutFollower(Scene(), shown.Add);

        follower.Fit(390, 844);
        follower.Fit(400, 860);
        follower.Fit(844, 390);
        follower.Fit(850, 400);

        Assert.Equal(2, shown.Count);
    }

    [Fact]
    public void TheFirstFitHandsOverWhatTurnsTheBaseSceneIntoTheLayout()
    {
        var scene = Scene();
        var shown = new List<SceneDiff>();
        var follower = new LayoutFollower(scene, shown.Add);

        follower.Fit(390, 844);

        Assert.True(SceneDiff.Between(scene.InLayout(LayoutShape.Portrait), Assert.Single(shown).ApplyTo(scene)).IsEmpty);
    }

    [Fact]
    public void ATurnLeavesAloneWhatTheGameAddedToTheView()
    {
        var (follower, view) = Follow(Scene());
        follower.Fit(390, 844);
        view.Apply(new SceneDiff([new AddEntity(Crate("spawned").ToJson(), null)]));

        follower.Fit(844, 390);

        Assert.True(view.Document.Contains("spawned"));
        Assert.Equal(40, view.Camera.FieldOfView);
    }

    [Fact]
    public void ATurnPutsBackAFieldOnlyTheLayoutItLeftHadSet()
    {
        var scene = new SceneDocument(
            entities: [Crate("crate"), Camera(50)],
            layouts: SceneLayouts.None.With(LayoutShape.Portrait, FieldOfViewOf(70)));
        var (follower, view) = Follow(scene);
        follower.Fit(390, 844);

        follower.Fit(844, 390);

        Assert.Equal(50, view.Camera.FieldOfView);
    }

    [Fact]
    public void ALayoutThatFailsToShowIsNotRecordedAsShown()
    {
        var attempts = 0;
        var follower = new LayoutFollower(Scene(), _ =>
        {
            if (attempts++ == 0)
            {
                throw new InvalidOperationException("The view was not ready.");
            }
        });

        Assert.Throws<InvalidOperationException>(() => follower.Fit(390, 844));
        Assert.Null(follower.Shape);

        follower.Fit(390, 844);
        Assert.Equal(LayoutShape.Portrait, follower.Shape);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public void ASceneWithNoLayoutsLooksTheSameHeldEitherWay()
    {
        var plain = new SceneDocument(entities: [Crate("crate"), Camera(50)]);
        var (follower, view) = Follow(plain);
        var expected = ViewShape.Of(new DocumentView(plain));

        follower.Fit(390, 844);
        Assert.Equal(expected, ViewShape.Of(view));

        follower.Fit(844, 390);
        Assert.Equal(expected, ViewShape.Of(view));
    }

    private static (LayoutFollower Follower, DocumentView View) Follow(SceneDocument scene)
    {
        var view = new DocumentView(scene);
        return (new LayoutFollower(scene, view.Apply), view);
    }

    private static SceneEntity Camera(double fieldOfView) => Thing(
        "cam",
        components: Parts(
            (SceneComponents.Transform, new TransformComponent(position: new Vector3(0, 2, 10))),
            (SceneComponents.Camera, new CameraComponent(fieldOfView))));

    private static SceneDocument Scene() => new(
        entities: [Crate("crate"), Camera(50)],
        layouts: SceneLayouts.None
            .With(LayoutShape.Portrait, FieldOfViewOf(70))
            .With(LayoutShape.Landscape, FieldOfViewOf(40)));

    private static SceneDiff FieldOfViewOf(double to) =>
        new([new SetField("cam", SceneComponents.Camera, "fieldOfView", 50.0, to)]);
}
