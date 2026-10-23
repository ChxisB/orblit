using static Orblit.Motion.Tests.ClipSamples;
using static Orblit.Motion.Tests.People;

namespace Orblit.Motion.Tests;

public class RetargetRestTests
{
    private const string Arm = "mixamorig:LeftArm";
    private const string Forearm = "mixamorig:LeftForeArm";

    private readonly Person _tpose = Build(Capture);
    private readonly Person _apose = Build(Capture, armsDown: 0.7);
    private readonly ClipDocument _raise = ClipOf(
        Turns(Arm, (0, Quaternion.Identity()), (0.5, About(0, 0, 1, 40)), (1, Quaternion.Identity())));

    [Fact]
    public void FixRestPointsTheTargetArmWhereTheSourceArmPoints()
    {
        var want = Pointing(_tpose, Arm, Forearm, _raise);
        var got = Pointing(_apose, Arm, Forearm, Retarget(fix: true));
        Assert.Equal(1, got.Dot(want), 1e-9);
    }

    [Fact]
    public void FixRestLeavesTheArmShortOfThatWithoutIt()
    {
        var want = Pointing(_tpose, Arm, Forearm, _raise);
        var got = Pointing(_apose, Arm, Forearm, Retarget(fix: false));

        // The arm carries 40 degrees of motion but starts 0.7 radians low.
        Assert.Equal(Math.Cos(0.7), got.Dot(want), 1e-6);
    }

    [Fact]
    public void FixRestGivesTheArmAKeyEvenWhenTheSourceNeverMovesIt()
    {
        var hips = _tpose.Locals[_tpose.IndexOf("mixamorig:Hips")].Position;
        var still = ClipOf(Moves("mixamorig:Hips", (0, hips), (1, hips + new Vector3(0, 0.1, 0))));
        var result = ClipRetarget.Of(still, _tpose.Skeleton, _apose.Skeleton, fixRest: true).Clip;
        var got = Pointing(_apose, Arm, Forearm, result);
        Assert.Equal(1, got.Dot(new Vector3(1, 0, 0)), 1e-9);
        var armChannel = Assert.Single(result.Channels, static channel => channel.Bone == Arm && channel.Property == "rotation");
        Assert.Single(Assert.IsType<ClipChannel<Quaternion>>(armChannel).Keys);
    }

    [Fact]
    public void FixRestAddsNothingBetweenTwoSkeletonsThatRestTheSame()
    {
        var twin = Build(Capture);
        var plain = ClipRetarget.Of(_raise, _tpose.Skeleton, twin.Skeleton).Clip;
        var fixedRest = ClipRetarget.Of(_raise, _tpose.Skeleton, twin.Skeleton, fixRest: true).Clip;
        Assert.Equal(plain.Channels.Count, fixedRest.Channels.Count);
        for (var i = 0; i < plain.Channels.Count; i++)
        {
            Assert.Equal(plain.Channels[i].Bone, fixedRest.Channels[i].Bone);
            Assert.Equal(plain.Channels[i].BoxedKeys.Count, fixedRest.Channels[i].BoxedKeys.Count);
        }
    }

    [Fact]
    public void FixRestSaysSoWhenABonePointsTheOppositeWayToItsDriver()
    {
        var backwards = Build(Capture, armsDown: 3.0);
        var result = ClipRetarget.Of(_raise, _tpose.Skeleton, backwards.Skeleton, fixRest: true);
        Assert.Contains(result.Problems, static text => text.Contains("LeftArm", StringComparison.Ordinal));
    }

    [Fact]
    public void HumanoidCarriesAClipBetweenRigsThatShareNoNames()
    {
        var from = Build(spine: 3);
        var to = Build(height: 1.2, rightFirst: true, spine: 1);
        var clip = ClipOf(
            Turns(
                from.Slots[HumanoidBone.LeftUpperArm],
                (0, Quaternion.Identity()),
                (0.5, About(0, 0, 1, 50)),
                (1, Quaternion.Identity())));
        var result = ClipRetarget.Of(clip, from.Skeleton, to.Skeleton, humanoid: true);
        Assert.Empty(result.Problems);
        var upper = to.Slots[HumanoidBone.LeftUpperArm];
        var lower = to.Slots[HumanoidBone.LeftLowerArm];
        var got = Pointing(to, upper, lower, result.Clip);
        var want = Pointing(from, from.Slots[HumanoidBone.LeftUpperArm], from.Slots[HumanoidBone.LeftLowerArm], clip);
        Assert.Equal(1, got.Dot(want), 1e-9);
    }

    [Fact]
    public void HumanoidSaysWhatWasNotAPersonInTheSourceOrTheTarget()
    {
        var chain = new RestSkeleton(
            ["a", "b", "c"],
            [-1, 0, 1],
            [.. Enumerable.Range(0, 3).Select(static i => Matrix4.TranslationValues(0, i * 0.3, 0))]);
        var result = ClipRetarget.Of(ClipOf(), chain, Build(Capture).Skeleton, humanoid: true);
        Assert.Contains(result.Problems, static text => text.StartsWith("In the source skeleton: ", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Problems, static text => text.StartsWith("In the target", StringComparison.Ordinal));
    }

    private static ClipDocument ClipOf(params ClipChannel[] channels) => new("Move", 1, channels: channels);

    /// <summary>Which way <paramref name="bone"/> points in the world: from its origin to its child's.</summary>
    private static Vector3 Pointing(Person who, string bone, string child, ClipDocument clip)
    {
        var world = who.World(clip.SampleAt(0.5).Bones.GetValueOrDefault(string.Empty));
        var from = world[who.IndexOf(bone)].GetTranslation();
        var to = world[who.IndexOf(child)].GetTranslation();
        return (to - from).Normalized();
    }

    private ClipDocument Retarget(bool fix) =>
        ClipRetarget.Of(_raise, _tpose.Skeleton, _apose.Skeleton, fixRest: fix).Clip;
}
