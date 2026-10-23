using Orblit.Rig;
using static Orblit.Motion.Tests.ClipSamples;

namespace Orblit.Motion.Tests;

public class LiveRetargetTests
{
    /// <summary>A stride, an arm swing and a bob of the hips, on the bones of <paramref name="who"/>.</summary>
    private static ClipDocument Stride(Person who)
    {
        var hips = who.Slots[HumanoidBone.Hips];
        var arm = who.Slots[HumanoidBone.LeftUpperArm];
        var thigh = who.Slots[HumanoidBone.RightUpperLeg];
        var spine = who.Slots[HumanoidBone.Spine];
        var rest = who.Locals[who.IndexOf(hips)].Position;
        var armRest = who.Locals[who.IndexOf(arm)].Turn;
        return new ClipDocument(
            "Stride",
            1,
            channels:
            [
                Moves(hips, (0, rest), (0.5, rest + new Vector3(0.05, -0.08, 0.2)), (1, rest)),
                Turns(arm, (0, armRest), (0.5, About(1, 0.2, 0, 50)), (1, armRest)),
                Turns(thigh, (0, Quaternion.Identity()), (0.3, About(1, 0, 0.1, -35)), (0.7, About(1, 0, 0, 25)), (1, Quaternion.Identity())),
                Turns(spine, (0, Quaternion.Identity()), (0.5, About(0.3, 1, 0, 20)), (1, Quaternion.Identity())),
            ]);
    }

    [Fact]
    public void TurnsEachBoneTheWayTheBakedClipDoesAtItsKeys()
    {
        var from = People.Build(People.Capture);
        var to = People.Build(People.Mannequin, height: 1.2, armsDown: 0.7);
        var clip = Stride(from);
        var live = new LiveRetarget(from.Skeleton, to.Skeleton, humanoid: true);
        var baked = ClipRetarget.Of(clip, from.Skeleton, to.Skeleton, humanoid: true);
        Assert.Equal(baked.Problems, live.Problems);
        foreach (var at in new[] { 0.0, 0.3, 0.5, 0.7, 1.0 })
        {
            var frame = live.Follow(clip.SampleAt(at, false).Bones[string.Empty]);
            var expected = baked.Clip.SampleAt(at, false).Bones.GetValueOrDefault(string.Empty);
            foreach (var (key, value) in frame)
            {
                var made = expected?.GetValueOrDefault(key);
                var turn = made?.Rotation ?? to.Locals[to.IndexOf(key)].Turn;

                // An angle from acos, so a rounding error of 1e-16 reads as 1e-8.
                Assert.True(TurnBetween(value.Rotation!, turn) < 1e-6, $"{key} at {at}");
                if (made?.Position is { } place)
                {
                    Assert.True((value.Position! - place).Length < 1e-9, $"{key} at {at}");
                }
            }
        }
    }

    [Fact]
    public void AnswersARestPoseForASourceThatSaysNothing()
    {
        var from = People.Build(People.Capture);
        var to = People.Build(People.Mannequin, height: 1.2);
        var live = new LiveRetarget(from.Skeleton, to.Skeleton, humanoid: true);
        var frame = live.Follow(new Dictionary<string, BoneLocal>());
        Assert.Equal(to.Slots.Count, frame.Count);
        foreach (var (key, value) in frame)
        {
            var rest = to.Locals[to.IndexOf(key)].Turn;
            Assert.True(TurnBetween(value.Rotation!, rest) < 1e-6);
            Assert.Null(value.Position);
        }
    }

    [Fact]
    public void PutsALimbWhereASourceOnItsOwnCannotBeMovedByName()
    {
        // Not a person, so only the names given can pair the two.
        var from = People.Build(People.Capture);
        var to = People.Build(People.Mannequin);
        var live = new LiveRetarget(from.Skeleton, to.Skeleton, new Dictionary<string, string> { ["hand_l"] = "mixamorig:LeftHand" });
        Assert.Contains("hand_l", live.Follow(new Dictionary<string, BoneLocal>()).Keys);
        Assert.ThrowsAny<ArgumentException>(
            () => new LiveRetarget(from.Skeleton, to.Skeleton, new Dictionary<string, string> { ["nowhere"] = "mixamorig:LeftHand" }));
    }

    [Fact]
    public void IsTheSourceFootAsLargeAsTheTargetIs()
    {
        var from = People.Build(People.Capture);
        var to = People.Build(People.Mannequin, height: 0.9);
        var live = new LiveRetarget(from.Skeleton, to.Skeleton, humanoid: true);
        var frame = new Dictionary<string, BoneLocal>
        {
            ["mixamorig:RightUpLeg"] = new(rotation: About(1, 0, 0, -40)),
            ["mixamorig:RightLeg"] = new(rotation: About(1, 0, 0, 30)),
        };
        var foot = from.OriginOf("mixamorig:RightFoot", frame);
        var size = to.Skeleton.Reach / from.Skeleton.Reach;
        var point = live.PointOf("mixamorig:RightFoot", frame);
        Assert.True((point - (foot * size)).Length < 1e-9);
        Assert.True(point.Z > 0.05);
    }

    [Fact]
    public void ReachesATargetLegToWhereTheSourceFootIs()
    {
        var from = People.Build(People.Capture);
        var to = People.Build(People.Mannequin, height: 1.2);
        var live = new LiveRetarget(from.Skeleton, to.Skeleton, humanoid: true);
        var frame = new Dictionary<string, BoneLocal>
        {
            ["mixamorig:LeftUpLeg"] = new(rotation: About(1, 0, 0, -40)),
            ["mixamorig:LeftLeg"] = new(rotation: About(1, 0, 0, 30)),
        };
        var world = to.World();
        Vector3 At(string name) => world[to.IndexOf(name)].GetTranslation();
        var armature = new Armature(
        [
            new Bone("thigh_l", At("thigh_l"), At("calf_l")),
            new Bone("calf_l", At("calf_l"), At("foot_l"), parent: "thigh_l", connected: true),
        ]);
        var pose = new Pose(armature);
        pose.Evaluate();
        IReadOnlyDictionary<string, BoneLocal>? source = frame;
        var ik = new TwoBoneIk("thigh_l", "calf_l", live.Pinned("mixamorig:LeftFoot", () => source));

        ik.Solve(pose, new ModifierFrame());
        pose.Evaluate();
        var want = live.PointOf("mixamorig:LeftFoot", frame);
        Assert.True((pose.TailOf("calf_l") - want).Length < 1e-6);

        // With no source frame the limb is left as it was.
        source = null;
        var before = pose.TailOf("calf_l");
        ik.Solve(pose, new ModifierFrame());
        pose.Evaluate();
        Assert.True((pose.TailOf("calf_l") - before).Length < 1e-9);
    }

    [Fact]
    public void RefusesABoneTheSourceDoesNotHave()
    {
        var from = People.Build(People.Capture);
        var to = People.Build(People.Mannequin);
        var live = new LiveRetarget(from.Skeleton, to.Skeleton);
        Assert.ThrowsAny<ArgumentException>(() => live.PointOf("nowhere", new Dictionary<string, BoneLocal>()));
        Assert.ThrowsAny<ArgumentException>(() => live.Pinned("nowhere", () => null));
    }
}
