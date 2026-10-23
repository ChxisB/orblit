using static Orblit.Motion.Tests.ClipSamples;
using static Orblit.Motion.Tests.Rigs;

namespace Orblit.Motion.Tests;

public class RetargetClipTests
{
    [Fact]
    public void StandsEachBoneWhereItsDriverStoodAtAnyMoment()
    {
        var from = Source();
        var to = Target();
        var clip = Walk();
        var result = ClipRetarget.Of(clip, from.Skeleton, to.Skeleton);
        Assert.Empty(result.Problems);
        ExpectSamePose(from, to, clip, result.Clip, [0, 0.1, 0.4, 0.65, 0.9, 1.2, 1.5, 2]);
    }

    [Fact]
    public void KeepsItsKeysAndItsHoldsWhenTheBonesLineUp()
    {
        var result = ClipRetarget.Of(Walk(), Source().Skeleton, Target().Skeleton);
        foreach (var name in new[] { "hips", "chest" })
        {
            var made = Assert.IsType<ClipChannel<Quaternion>>(result.Clip.ChannelFor(string.Empty, "rotation", name));
            var original = Assert.IsType<ClipChannel<Quaternion>>(Walk().ChannelFor(string.Empty, "rotation", name));
            Assert.Equal([0, 0.4, 0.9, 1.5], made.Keys.Select(static key => key.At));
            Assert.Equal(original.Keys.Select(static key => key.Hold), made.Keys.Select(static key => key.Hold));
        }
    }

    [Fact]
    public void CarriesCubicSlopesThroughAsItCarriesTheValues()
    {
        var from = Source();
        var to = Target();
        var clip = new ClipDocument(
            "Swing",
            1,
            channels:
            [
                new ClipChannel<Quaternion>(
                    string.Empty,
                    "rotation",
                    ChannelKind.Rotation,
                    [
                        new Key<Quaternion>(
                            0,
                            Tip(0, 0, -90),
                            Hold.Curve,
                            slopeOut: new Slope<Quaternion>(new Quaternion(0.3, -0.2, 0.5, 0.1))),
                        new Key<Quaternion>(
                            0.5,
                            Tip(40, 20, -60),
                            Hold.Curve,
                            slopeIn: new Slope<Quaternion>(new Quaternion(-0.4, 0.1, 0.2, 0.3)),
                            slopeOut: new Slope<Quaternion>(new Quaternion(0.1, 0.4, -0.3, 0.2))),
                        new Key<Quaternion>(
                            1,
                            Tip(-30, 50, -120),
                            Hold.Curve,
                            slopeIn: new Slope<Quaternion>(new Quaternion(0.2, 0.2, -0.1, -0.4))),
                    ],
                    "armL"),
            ]);
        var result = ClipRetarget.Of(clip, from.Skeleton, to.Skeleton);
        ExpectSamePose(from, to, clip, result.Clip, [0, 0.13, 0.37, 0.5, 0.71, 0.98, 1]);
    }

    [Fact]
    public void WorksOutABoneTheTargetHasNoParentForAtItsKeys()
    {
        var from = Source();
        var to = Target(spine: null);
        var clip = Walk();
        var result = ClipRetarget.Of(clip, from.Skeleton, to.Skeleton);
        ExpectSamePose(from, to, clip, result.Clip, [0, 0.4, 0.9, 1.5]);
        var chest = Assert.IsType<ClipChannel<Quaternion>>(result.Clip.ChannelFor(string.Empty, "rotation", "chest"));
        Assert.Equal([0, 0.4, 0.9, 1.5], chest.Keys.Select(static key => key.At));
        Assert.Contains("\"spine\"", Assert.Single(result.Problems));
    }

    [Fact]
    public void StaysWithinAFewDegreesBetweenTheKeysOfAWorkedOutBone()
    {
        var from = Source();
        var to = Target(spine: null);
        var clip = Walk();
        var result = ClipRetarget.Of(clip, from.Skeleton, to.Skeleton);

        // Straight lines between keys, so the middle of a span is a little off.
        ExpectSamePose(from, to, clip, result.Clip, [0.2, 0.65, 1.2], within: 0.1);
    }

    [Fact]
    public void KeepsABoneUnderAParentNothingDrivesWhereItRests()
    {
        var from = Source();
        var to = Target(spine: "lumbar");
        var clip = Walk();
        var result = ClipRetarget.Of(clip, from.Skeleton, to.Skeleton);
        Assert.Null(result.Clip.ChannelFor(string.Empty, "rotation", "lumbar"));
        ExpectSamePose(from, to, clip, result.Clip, [0, 0.4, 0.9, 1.5]);
    }

    [Fact]
    public void LeavesABoneTheClipNeverMovesStandingAsItRests()
    {
        var result = ClipRetarget.Of(Walk(), Source().Skeleton, Target().Skeleton);
        Assert.Null(result.Clip.ChannelFor(string.Empty, "rotation", "handL"));
        Assert.NotNull(result.Clip.ChannelFor(string.Empty, "rotation", "head"));
    }

    [Fact]
    public void MovesTheHipsAsMuchFurtherAsTheTargetIsBigger()
    {
        var from = Source();
        var to = Target();
        var clip = new ClipDocument(
            "Bob",
            1,
            channels:
            [
                Moves("hips", (0, new Vector3(0, 1, 0)), (0.5, new Vector3(0.4, 0.9, 1.5)), (1, new Vector3(0, 1, 3))),
            ]);
        var result = ClipRetarget.Of(clip, from.Skeleton, to.Skeleton);
        var size = to.Reach / from.Reach;
        var fromRest = from.Pose()[0].GetTranslation();
        var toRest = to.Pose()[0].GetTranslation();
        foreach (var time in new[] { 0.0, 0.25, 0.5, 0.8, 1.0 })
        {
            var moved = from.Pose(clip, time)[0].GetTranslation() - fromRest;
            var carried = to.Pose(result.Clip, time)[0].GetTranslation() - toRest;
            Assert.Equal(0, (carried - (moved * size)).Length, 1e-9);
        }
    }

    [Fact]
    public void MovesABoneBelowTheHipsAsMuchFurtherAndNoMore()
    {
        var from = Source();
        var to = Target();
        var clip = new ClipDocument(
            "Lunge",
            1,
            channels:
            [
                Moves("spine", (0, new Vector3(0, 0.2, 0)), (0.5, new Vector3(0.1, 0.25, 0.3)), (1, new Vector3(0, 0.2, 0))),
            ]);
        var result = ClipRetarget.Of(clip, from.Skeleton, to.Skeleton);
        var size = to.Reach / from.Reach;
        var fromRest = InParent(from, null, 0);
        var toRest = InParent(to, null, 0);

        // At a moment the source is at rest the target must be at its own rest.
        Assert.Equal(0, (InParent(to, result.Clip, 0) - toRest).Length, 1e-9);
        foreach (var time in new[] { 0.25, 0.5, 0.8 })
        {
            var moved = InParent(from, clip, time) - fromRest;
            var carried = InParent(to, result.Clip, time) - toRest;

            // The target's own frame is turned from the source's, so only the lengths are comparable.
            Assert.Equal(moved.Length * size, carried.Length, 1e-9);
        }
    }

    [Fact]
    public void LeavesOutAPositionThatStaysAtRest()
    {
        var clip = new ClipDocument(
            "Held",
            1,
            channels: [Moves("spine", (0, new Vector3(0, 0.2, 0)), (1, new Vector3(0, 0.2, 0)))]);
        var result = ClipRetarget.Of(clip, Source().Skeleton, Target().Skeleton);
        Assert.Empty(result.Clip.Channels);
    }

    [Fact]
    public void CarriesAScaleAsTheChangeFromRest()
    {
        var clip = new ClipDocument(
            "Squash",
            1,
            channels: [Scaled("chest", (0, new Vector3(1, 1, 1)), (1, new Vector3(1.5, 0.5, 1)))]);
        var result = ClipRetarget.Of(clip, Source().Skeleton, Target().Skeleton);
        var made = Assert.IsType<ClipChannel<Vector3>>(result.Clip.ChannelFor(string.Empty, "scale", "chest"));
        var last = made.Keys[^1].Value;
        Assert.Equal(1.5, last.X, 1e-12);
        Assert.Equal(0.5, last.Y, 1e-12);
        Assert.Equal(1, last.Z, 1e-12);
    }

    [Fact]
    public void UsesTheBonesItIsToldTo()
    {
        var from = Source();
        var renamed = new Rig(["pelvis", "back", "ribs", "skull", "shoulder", "palm"], from.Parents, Target().Rest);
        var result = ClipRetarget.Of(
            Walk(),
            from.Skeleton,
            renamed.Skeleton,
            new Dictionary<string, string>
            {
                ["pelvis"] = "hips",
                ["back"] = "spine",
                ["ribs"] = "chest",
                ["skull"] = "head",
                ["shoulder"] = "armL",
                ["palm"] = "handL",
            });
        Assert.NotNull(result.Clip.ChannelFor(string.Empty, "rotation", "skull"));
        Assert.Null(result.Clip.ChannelFor(string.Empty, "rotation", "head"));
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void RefusesABoneNeitherSkeletonHas() =>
        Assert.Throws<ArgumentException>(() => ClipRetarget.Of(
            Walk(),
            Source().Skeleton,
            Target().Skeleton,
            new Dictionary<string, string> { ["wing"] = "hips" }));

    [Fact]
    public void KeepsWhatIsNotABoneAndTheMarksNameAndLength()
    {
        var clip = Walk().CopyWith(
            channels:
            [
                .. Walk().Channels,
                new ClipChannel<Vector3>(
                    "door",
                    "transform.position",
                    ChannelKind.Vector,
                    [new Key<Vector3>(0, new Vector3(0, 0, 0)), new Key<Vector3>(1, new Vector3(1, 0, 0))]),
            ]);
        var result = ClipRetarget.Of(clip, Source().Skeleton, Target().Skeleton);
        Assert.NotNull(result.Clip.ChannelFor("door", "transform.position"));
        Assert.Equal(1.5, result.Clip.Duration);
        Assert.Equal(["step"], result.Clip.Marks.Select(static mark => mark.Name));
        Assert.Equal("Walk", result.Clip.Name);
    }

    [Fact]
    public void DoesNotTouchTheBonesOfAnotherTarget()
    {
        var clip = new ClipDocument(
            "Two",
            1,
            channels:
            [
                new ClipChannel<Quaternion>(
                    "other",
                    "rotation",
                    ChannelKind.Rotation,
                    [new Key<Quaternion>(0, Tip(0, 0, 0)), new Key<Quaternion>(1, Tip(10, 20, 30))],
                    "hips"),
            ]);
        var result = ClipRetarget.Of(clip, Source().Skeleton, Target().Skeleton);
        var made = Assert.IsType<ClipChannel<Quaternion>>(result.Clip.ChannelFor("other", "rotation", "hips"));
        Assert.Equal(Tip(10, 20, 30).X, made.Keys[^1].Value.X);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void SaysWhichMotionItCouldNotCarry()
    {
        var clip = Walk().CopyWith(
            channels: [.. Walk().Channels, Turns("tail", (0, Tip(0, 0, 0)), (1, Tip(10, 0, 0)))]);
        var result = ClipRetarget.Of(clip, Source().Skeleton, Chain("hips", "spine"));
        Assert.Equal(2, result.Problems.Count);
        Assert.StartsWith("The clip moves \"tail\"", result.Problems[0]);
        Assert.Contains("\"chest\"", result.Problems[1]);
        Assert.Contains("\"armL\"", result.Problems[1]);
    }

    [Fact]
    public void StaysQuietAboutRootMotionAClipNeverHad()
    {
        var result = ClipRetarget.Of(Walk(), Source().Skeleton, Chain("a"));
        Assert.Null(result.Clip.RootMotion);
        Assert.DoesNotContain(result.Problems, static line => line.Contains("root", StringComparison.Ordinal));
    }

    [Fact]
    public void MovesRootMotionToTheBoneThatCarriesIt()
    {
        var clip = Walk().CopyWith(rootMotion: new RootMotion(bone: "hips", turns: true, rises: true));
        var result = ClipRetarget.Of(clip, Source().Skeleton, Target().Skeleton);
        var root = result.Clip.RootMotion!;
        Assert.Equal("hips", root.Bone);
        Assert.True(root.Turns);
        Assert.True(root.Rises);

        // The target's armature stands its Z axis up, so up is Z on the way in.
        Assert.Equal(0, root.Up.X, 1e-9);
        Assert.Equal(0, root.Up.Y, 1e-9);
        Assert.Equal(1, root.Up.Z, 1e-9);
    }

    [Fact]
    public void FollowsRootMotionToARenamedBone()
    {
        var from = Source();
        var renamed = new Rig(["pelvis", .. from.Names.Skip(1)], from.Parents, from.Rest);
        var clip = Walk().CopyWith(rootMotion: new RootMotion(bone: "hips"));
        var result = ClipRetarget.Of(
            clip,
            from.Skeleton,
            renamed.Skeleton,
            new Dictionary<string, string> { ["pelvis"] = "hips" });
        Assert.Equal("pelvis", result.Clip.RootMotion!.Bone);
    }

    [Fact]
    public void DropsRootMotionItHasNoBoneToCarry()
    {
        var clip = Walk().CopyWith(rootMotion: new RootMotion(bone: "hips"));
        var result = ClipRetarget.Of(clip, Source().Skeleton, Chain("a"));
        Assert.Null(result.Clip.RootMotion);
        Assert.Contains("root motion", result.Problems[^1], StringComparison.Ordinal);
    }

    [Fact]
    public void TurnsAClipThatStaysAtRestIntoOneThatStaysAtRest()
    {
        var from = Source();
        var to = Target();
        var still = new ClipDocument(
            "Still",
            1,
            channels: [.. from.Names.Select((name, i) => Turns(name, (0, from.Rest[i].Turn), (1, from.Rest[i].Turn)))]);
        var result = ClipRetarget.Of(still, from.Skeleton, to.Skeleton);
        ExpectSamePose(from, to, still, result.Clip, [0, 0.5]);
        var rest = to.Pose();
        var posed = to.Pose(result.Clip, 0.5);
        for (var i = 0; i < rest.Count; i++)
        {
            Assert.True(posed[i].GetTranslation().DistanceTo(rest[i].GetTranslation()) < 1e-9);
        }

        Assert.Equal(1, TurnAt(result.Clip, "chest", 0).Length, 1e-12);
    }

    /// <summary>Where the spine stands from its parent, seen from the parent's own rotation.</summary>
    private static Vector3 InParent(Rig rig, ClipDocument? clip, double time)
    {
        var pose = rig.Pose(clip, time);
        var away = pose[1].GetTranslation() - pose[0].GetTranslation();
        var turn = TurnOf(pose[0]);
        turn.Transpose();
        return turn.Transform(away);
    }

    private static ClipChannel<Vector3> Scaled(string bone, params (double At, Vector3 Place)[] keys) =>
        new(
            string.Empty,
            "scale",
            ChannelKind.Vector,
            [.. keys.Select(key => new Key<Vector3>(key.At, key.Place, Hold.Linear))],
            bone);
}
