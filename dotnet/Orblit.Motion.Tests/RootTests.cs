using Orblit.Scene;

namespace Orblit.Motion.Tests;

public class RootTests
{
    private static readonly Vector3 Up = new(0, 1, 0);

    [Fact]
    public void AcrossTheGroundGoesToTheCharacterAndTheBobStaysInThePose()
    {
        var clip = Walk();
        ExpectVector(clip.RootStepBetween(0, 1).Position, new Vector3(0, 0, 2));
        ExpectVector(clip.RootStepBetween(0, 0.5).Position, new Vector3(0, 0, 1));
        ExpectTurn(clip.RootStepBetween(0, 1).Rotation, Quaternion.Identity());

        var held = clip.SampleAt(0.5, inPlace: true).BoneOf(string.Empty, "hips");
        ExpectVector(held!.Position, new Vector3(0, 1.2, 0));
        var authored = clip.SampleAt(0.5).BoneOf(string.Empty, "hips");
        ExpectVector(authored!.Position, new Vector3(0, 1.2, 1));
    }

    [Fact]
    public void TakesUpAndDownTooWhenTheRootRises()
    {
        var clip = Walk(rises: true);
        ExpectVector(clip.RootStepBetween(0, 0.5).Position, new Vector3(0, 0.2, 1));
        var held = clip.SampleAt(0.5, inPlace: true).BoneOf(string.Empty, "hips");
        ExpectVector(held!.Position, Vector3.Zero());
    }

    [Fact]
    public void IsMeasuredAlongTheUpTheClipNames()
    {
        var clip = Walk().CopyWith(rootMotion: new RootMotion(bone: "hips", up: new Vector3(0, 0, 2)));

        // With Z up, walking along Z is climbing, and the bob is the walk.
        ExpectVector(clip.RootStepBetween(0, 0.5).Position, new Vector3(0, 0.2, 0));
    }

    [Fact]
    public void GoesBackwardsWhenPlayedBackwards() =>
        ExpectVector(Walk().RootStepBetween(1, 0.5).Position, new Vector3(0, 0, -1));

    [Fact]
    public void StaysInThePoseUnlessTheRootTurns()
    {
        var clip = Walk(turn: 90);
        ExpectTurn(clip.RootStepBetween(0, 1).Rotation, Quaternion.Identity());
        var held = clip.SampleAt(1, inPlace: true).BoneOf(string.Empty, "hips");
        ExpectTurn(held!.Rotation, Yaw(90));
    }

    [Fact]
    public void GoesToTheCharacterWhenItDoesTheLeanKept()
    {
        var clip = Walk(turns: true, turn: 90, lean: 20);
        ExpectTurn(clip.RootStepBetween(0, 1).Rotation, Yaw(90));
        ExpectTurn(clip.RootStepBetween(0, 0.5).Rotation, Yaw(45));
        foreach (var at in new[] { 0.0, 0.3, 1.0 })
        {
            var held = clip.SampleAt(at, inPlace: true).BoneOf(string.Empty, "hips");
            ExpectTurn(held!.Rotation, Pitch(20));
        }
    }

    [Fact]
    public void CarriesEachStepForwardTheWayTheCharacterNowFaces()
    {
        var clip = Walk(turns: true, turn: 90);

        // From half way, the hips face 45 degrees round towards X, so what is
        // straight along Z in the clip is forward and to the character's -X.
        var step = clip.RootStepBetween(0.5, 1);
        ExpectVector(step.Position, new Vector3(-Math.Sqrt(0.5), 0, Math.Sqrt(0.5)));
        ExpectTurn(step.Rotation, Yaw(45));
    }

    [Fact]
    public void TwoStepsOneAfterTheOtherAreTheStepOverBoth()
    {
        var clip = Walk(turns: true, turn: 120, lean: 10);
        var whole = clip.RootStepBetween(0.1, 0.9);
        var parts = clip.RootStepBetween(0.1, 0.4).Then(clip.RootStepBetween(0.4, 0.9));
        ExpectVector(parts.Position, whole.Position);
        ExpectTurn(parts.Rotation, whole.Rotation);
    }

    [Fact]
    public void ARootOnItsBackHasNoHeadingToGive()
    {
        var clip = Walk(turns: true).CopyWith(channels:
        [
            new ClipChannel<Quaternion>(
                string.Empty,
                "rotation",
                ChannelKind.Rotation,
                [new Key<Quaternion>(0, Pitch(180), Hold.Linear), new Key<Quaternion>(1, Pitch(180))],
                "hips"),
        ]);

        ExpectTurn(clip.RootStepBetween(0, 1).Rotation, Quaternion.Identity());
    }

    [Fact]
    public void PlayedALoopThatWrapsMidStepStillTakesTheWholeStep()
    {
        var player = new ClipPlayer(Walk());
        player.Play();
        player.Advance(0.75);
        var wrapped = player.Advance(0.5);
        ExpectVector(wrapped.Moved.Position, new Vector3(0, 0, 1));
        Assert.Equal(0.25, player.At, 1e-12);
        var held = wrapped.Frame.BoneOf(string.Empty, "hips")!;
        ExpectVector(held.Position, new Vector3(0, 1.1, 0));
    }

    [Fact]
    public void PlayedATurningLoopGoesOnFromWhereTheLastLapLeftIt()
    {
        var clip = Walk(turns: true, turn: 90);
        var player = new ClipPlayer(clip);
        player.Play();
        var moved = player.Advance(1.5).Moved;
        var expected = clip.RootStepBetween(0, 1).Then(clip.RootStepBetween(0, 0.5));
        ExpectVector(moved.Position, expected.Position);
        ExpectTurn(moved.Rotation, Yaw(135));
    }

    [Fact]
    public void PlayedBackwardsWalksBackwards()
    {
        var player = new ClipPlayer(Walk(), speed: -1);
        player.Seek(1);
        player.Play();
        ExpectVector(player.Advance(0.5).Moved.Position, new Vector3(0, 0, -1));
    }

    [Fact]
    public void PlayedABounceComesBackTheWayItWent()
    {
        var player = new ClipPlayer(Walk(), WhenDone.Bounce);
        player.Play();
        ExpectVector(player.Advance(1.5).Moved.Position, new Vector3(0, 0, 1));
    }

    [Fact]
    public void PlayedAStepManyLapsLongGoesTheWholeWay()
    {
        var player = new ClipPlayer(Walk());
        player.Play();
        var moved = player.Advance(1000.25).Moved;
        Assert.Equal(2000.5, moved.Position.Z, 1e-6);
        Assert.True(Math.Abs(moved.Position.X) + Math.Abs(moved.Position.Y) < 1e-9);
    }

    [Fact]
    public void PlayedATurningStepManyLapsLongTurnsTheWholeWay()
    {
        // A quarter turn a lap: a thousand laps is two hundred and fifty times round, and back where it
        // started facing.
        var clip = Walk(turns: true, turn: 90);
        var player = new ClipPlayer(clip);
        player.Play();
        var moved = player.Advance(1000).Moved;
        ExpectTurn(moved.Rotation, Quaternion.Identity());
    }

    [Fact]
    public void AClipWithNoRootMotionGoesNowhereAndHoldsNothing()
    {
        var clip = Walk().CopyWith(clearRootMotion: true);
        Assert.True(clip.RootStepBetween(0, 1).IsNone);
        var frame = clip.SampleAt(0.5, inPlace: true);
        ExpectVector(frame.BoneOf(string.Empty, "hips")!.Position, new Vector3(0, 1.2, 1));
    }

    [Fact]
    public void AnEntityRootCanTurnInDegrees()
    {
        var clip = new ClipDocument(
            "Cart",
            1,
            rootMotion: new RootMotion(target: "cart", turns: true),
            channels:
            [
                new ClipChannel<Vector3>(
                    "cart",
                    "transform.position",
                    ChannelKind.Vector,
                    [new Key<Vector3>(0, Vector3.Zero(), Hold.Linear), new Key<Vector3>(1, new Vector3(4, 1, 0))]),

                // Pitched, then turned: Z, then Y, then X is the order a transform
                // composes, so the X is applied first.
                new ClipChannel<Vector3>(
                    "cart",
                    "transform.rotation",
                    ChannelKind.Vector,
                    [new Key<Vector3>(0, new Vector3(10, 0, 0), Hold.Linear), new Key<Vector3>(1, new Vector3(10, 60, 0))]),
            ]);

        var step = clip.RootStepBetween(0, 1);
        ExpectVector(step.Position, new Vector3(4, 0, 0));
        ExpectTurn(step.Rotation, Yaw(60));

        var frame = clip.SampleAt(1, inPlace: true);
        ExpectVector(Assert.IsType<Vector3>(frame.ValueOf("cart", "transform.position")), new Vector3(0, 1, 0));
        var degrees = Assert.IsType<Vector3>(frame.ValueOf("cart", "transform.rotation"));
        ExpectTurn(Quaternion.FromRotation(TransformComponent.RotationOf(degrees)), Pitch(10));
    }

    [Fact]
    public void StepsComposeTheWayACharacterTakesThem()
    {
        var first = new RootStep(new Vector3(0, 0, 1), Yaw(90));
        var second = new RootStep(new Vector3(0, 0, 1));
        var both = first.Then(second);

        // A quarter turn about Y takes Z to X.
        ExpectVector(both.Position, new Vector3(1, 0, 1));
        ExpectTurn(both.Rotation, Yaw(90));
        Assert.True(new RootStep().IsNone);
        Assert.False(first.IsNone);
    }

    private static Quaternion Yaw(double degrees) => Quaternion.AxisAngle(Up, Doubles.Radians(degrees));

    private static Quaternion Pitch(double degrees) => Quaternion.AxisAngle(new Vector3(1, 0, 0), Doubles.Radians(degrees));

    private static void ExpectVector(Vector3? actual, Vector3 expected)
    {
        Assert.NotNull(actual);
        Assert.True((actual - expected).Length < 1e-9, $"{actual} is not {expected}");
    }

    /// <summary>The same turn: a rotation and its negation turn a thing alike.</summary>
    private static void ExpectTurn(Quaternion? actual, Quaternion expected)
    {
        Assert.NotNull(actual);
        var dot = (actual.X * expected.X) + (actual.Y * expected.Y) + (actual.Z * expected.Z) + (actual.W * expected.W);
        Assert.True(Math.Abs(Math.Abs(dot) - 1) < 1e-9, $"{actual} is not {expected}");
    }

    /// <summary>
    /// A second of walking two metres along Z, bobbing up in the middle, with its hips turning through
    /// <paramref name="turn"/> degrees as it goes and leaning <paramref name="lean"/>.
    /// </summary>
    // F2: five optional parameters, called by name.
    private static ClipDocument Walk(
        bool rises = false,
        bool turns = false,
        double turn = 0,
        double lean = 0,
        WhenDone whenDone = WhenDone.Loop) => new(
        "Walk",
        1,
        whenDone,
        rootMotion: new RootMotion(bone: "hips", turns: turns, rises: rises),
        channels:
        [
            new ClipChannel<Vector3>(
                string.Empty,
                "position",
                ChannelKind.Vector,
                [
                    new Key<Vector3>(0, new Vector3(0, 1, 0), Hold.Linear),
                    new Key<Vector3>(0.5, new Vector3(0, 1.2, 1), Hold.Linear),
                    new Key<Vector3>(1, new Vector3(0, 1, 2)),
                ],
                "hips"),
            new ClipChannel<Quaternion>(
                string.Empty,
                "rotation",
                ChannelKind.Rotation,
                [
                    new Key<Quaternion>(0, Pitch(lean), Hold.Linear),
                    new Key<Quaternion>(1, Yaw(turn) * Pitch(lean)),
                ],
                "hips"),
        ]);
}
