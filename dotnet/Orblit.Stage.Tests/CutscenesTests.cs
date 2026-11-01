using Orblit.Filament;
using Orblit.Motion;
using Orblit.Scene;
using Orblit.Sequence;
using Orblit.VectorMath;

namespace Orblit.Stage.Tests;

public class CutscenesTests
{
    private static SceneEntity Camera(string id, Vector3 at, Vector3? turned = null, double fieldOfView = 50, bool visible = true) =>
        Documents.Thing(
            id,
            visible: visible,
            components: Documents.Parts(
                (SceneComponents.Transform, new TransformComponent(position: at, rotation: turned)),
                (SceneComponents.Camera, new CameraComponent(fieldOfView: fieldOfView))));

    /// <summary>The game's camera, two more for a cutscene to cut between, and a cube.</summary>
    private static SceneDocument Stage() => Documents.Of(
        Camera("game", new Vector3(0, 0, 10)),
        Camera("front", new Vector3(0, 0, 5), fieldOfView: 40, visible: false),
        Camera("side", new Vector3(5, 0, 0), turned: new Vector3(0, 90, 0), fieldOfView: 60, visible: false),
        Documents.Crate("cube"));

    /// <summary>The cube slides four metres, seen from the front and then the side, with the two blended for a second between.</summary>
    private static CutsceneDocument Intro(WhenDone whenDone = WhenDone.Hold) => new(
        new ClipDocument(
            "Intro",
            4,
            whenDone,
            channels:
            [
                new ClipChannel<Vector3>(
                    "cube",
                    "transform.position",
                    ChannelKind.Vector,
                    [new Key<Vector3>(0, Vector3.Zero(), hold: Hold.Linear), new Key<Vector3>(4, new Vector3(4, 0, 0))]),
            ],
            marks: [new Mark(3, "boom")]),
        shots: [new CutsceneShot("front", 0, 3), new CutsceneShot("side", 2, 2)],
        sounds: [new CutsceneSound("swell.ogg", 0, 4)]);

    private static Vector3 CubeAt(DocumentView view) =>
        ((TransformComponent)view.Document["cube"]![SceneComponents.Transform]!).Position;

    private static Vector3 FacingOf(RenderCamera camera) => (camera.Target - camera.Position).Normalized();

    private static void Close(Vector3 actual, Vector3 wanted) => Near.Vector(actual, wanted, tolerance: 1e-6);

    [Fact]
    public void AViewGivesACameraEntitysViewShownOrNot()
    {
        var view = new DocumentView(Stage());
        var side = view.CameraOf("side")!;

        Close(side.Position, new Vector3(5, 0, 0));
        Close(FacingOf(side), new Vector3(-1, 0, 0));
        Assert.Equal(60, side.FieldOfView);
        Assert.Null(view.CameraOf("cube"));
        Assert.Null(view.CameraOf("nobody"));
    }

    [Fact]
    public void AViewLooksThroughAnotherCameraUntilGivenItsOwnBack()
    {
        var view = new DocumentView(Stage());
        Close(view.Scene.Camera.Position, new Vector3(0, 0, 10));

        view.Through = view.CameraOf("front");
        Close(view.Scene.Camera.Position, new Vector3(0, 0, 5));

        view.Through = null;
        Close(view.Scene.Camera.Position, new Vector3(0, 0, 10));
    }

    [Fact]
    public void BlendingNoneOrNoneWithASayIsNothing()
    {
        Assert.Null(Cutscenes.BlendCameras([]));
        Assert.Null(Cutscenes.BlendCameras([(Front(), 0)]));
    }

    [Fact]
    public void BlendingOneIsThatOne()
    {
        var front = Front();

        Assert.Same(front, Cutscenes.BlendCameras([(front, 0.3), (Side(), 0)]));
    }

    [Fact]
    public void BlendingStandsBetweenThemAndLooksBetweenThem()
    {
        var between = Cutscenes.BlendCameras([(Front(), 0.5), (Side(), 0.5)])!;

        Close(between.Position, new Vector3(2.5, 0, 2.5));
        Close(FacingOf(between), new Vector3(-1, 0, -1).Normalized());
        Assert.Equal(50, between.FieldOfView, 1e-9);
    }

    [Fact]
    public void BlendingTakesTheRestOfTheLensFromTheHeaviest()
    {
        Assert.Equal(2, Cutscenes.BlendCameras([(Front(), 0.25), (Side(), 0.75)])!.Aperture);
        Assert.Equal(16, Cutscenes.BlendCameras([(Front(), 0.75), (Side(), 0.25)])!.Aperture);
    }

    [Fact]
    public void BlendingLooksTheFirstOnesWayWhenTwoLookExactlyOpposite()
    {
        var back = new RenderCamera { Position = new Vector3(0, 0, -5), Target = Vector3.Zero() };

        Close(FacingOf(Cutscenes.BlendCameras([(Front(), 0.5), (back, 0.5)])!), new Vector3(0, 0, -1));
    }

    [Fact]
    public void ACutsceneLooksThroughItsFirstShotAndKeysTheSceneWhenStarted()
    {
        var view = new DocumentView(Stage());
        var cutscenes = new Cutscenes(view, [Intro()]);
        cutscenes.Start("Intro");

        Assert.Equal("Intro", cutscenes.Playing);
        Close(view.Scene.Camera.Position, new Vector3(0, 0, 5));
        Close(CubeAt(view), Vector3.Zero());
    }

    [Fact]
    public void ACutsceneMovesTheCubeBlendsItsShotsAndHandsTheViewBack()
    {
        var view = new DocumentView(Stage());
        var cutscenes = new Cutscenes(view, [Intro()]);
        cutscenes.Start("Intro");

        var first = cutscenes.Advance(1);
        Close(CubeAt(view), new Vector3(1, 0, 0));
        Close(view.Scene.Camera.Position, new Vector3(0, 0, 5));
        Assert.Equal("swell.ogg", Assert.Single(first.Sounds).Sound);
        Assert.False(first.Ended);

        cutscenes.Advance(1.5);
        Close(view.Scene.Camera.Position, new Vector3(2.5, 0, 2.5));

        var last = cutscenes.Advance(2);
        Assert.Equal(["boom"], last.Marks.Select(static mark => mark.Name));
        Assert.True(last.Ended);
        Assert.Null(cutscenes.Playing);
        Assert.Null(view.Through);
        Close(view.Scene.Camera.Position, new Vector3(0, 0, 10));
    }

    [Fact]
    public void ACutsceneThatHoldsLeavesTheSceneWhereItPutIt()
    {
        var view = new DocumentView(Stage());
        var cutscenes = new Cutscenes(view, [Intro()]);
        cutscenes.Start("Intro");
        cutscenes.Advance(5);

        Close(CubeAt(view), new Vector3(4, 0, 0));
    }

    [Fact]
    public void ACutsceneThatReleasesPutsBackWhatItMoved()
    {
        var view = new DocumentView(Stage());
        var cutscenes = new Cutscenes(view, [Intro(WhenDone.Release)]);
        cutscenes.Start("Intro");
        cutscenes.Advance(5);

        Close(CubeAt(view), Vector3.Zero());
    }

    [Fact]
    public void AStoppedCutsceneEndsWhereItIsAndHandsTheViewBack()
    {
        var view = new DocumentView(Stage());
        var cutscenes = new Cutscenes(view, [Intro()]);
        cutscenes.Start("Intro");
        cutscenes.Advance(2);
        cutscenes.Stop();

        Assert.Null(cutscenes.Playing);
        Close(CubeAt(view), new Vector3(2, 0, 0));
        Close(view.Scene.Camera.Position, new Vector3(0, 0, 10));
        Assert.False(cutscenes.Advance(1).Ended);
    }

    [Fact]
    public void ACutsceneStartsFromAMarkNamedAfterIt()
    {
        var cutscenes = new Cutscenes(new DocumentView(Stage()), [Intro()]);
        cutscenes.StartFrom([new Mark(1, "footstep")]);
        Assert.Null(cutscenes.Playing);

        cutscenes.StartFrom([new Mark(1, "footstep"), new Mark(1, "Intro")]);
        Assert.Equal("Intro", cutscenes.Playing);
    }

    [Fact]
    public void ACutsceneIsRefusedWhenThereIsNoSuchCutscene()
    {
        var cutscenes = new Cutscenes(new DocumentView(Stage()), [Intro()]);

        Assert.Throws<ArgumentException>(() => cutscenes.Start("Outro"));
        Assert.True(cutscenes.Has("Intro"));
        Assert.False(cutscenes.Has("Outro"));
    }

    [Fact]
    public void CutscenesNeedNamesThatDifferOrOneCouldNeverBeStarted() =>
        Assert.Throws<ArgumentException>(() => new Cutscenes(new DocumentView(Stage()), [Intro(), Intro()]));

    [Fact]
    public void StartingACutsceneEndsTheOneRunningFirst()
    {
        var view = new DocumentView(Stage());
        var cutscenes = new Cutscenes(view, [Intro(WhenDone.Release), Other()]);
        cutscenes.Start("Intro");
        cutscenes.Advance(2);

        cutscenes.Start("Other");

        Assert.Equal("Other", cutscenes.Playing);
        Close(CubeAt(view), Vector3.Zero());
    }

    private static CutsceneDocument Other() => new(new ClipDocument("Other", 1));

    private static RenderCamera Front() =>
        new() { Position = new Vector3(0, 0, 5), Target = Vector3.Zero(), FieldOfView = 40 };

    private static RenderCamera Side() =>
        new() { Position = new Vector3(5, 0, 0), Target = Vector3.Zero(), FieldOfView = 60, Aperture = 2 };
}
