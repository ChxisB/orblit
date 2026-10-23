namespace Orblit.Motion.Tests;

/// <summary>What the shared fixture cannot say: what a result shares with its inputs, and what it answers for a gap.</summary>
public class NamesAddedTests
{
    [Fact]
    public void AnEmptyNameCleansToNothingOnNoSide()
    {
        Assert.Equal(("", (string?)null), BoneNames.CleanName(""));
    }

    [Fact]
    public void FoundBonesAreReadOnlyAndComeInTheOrderTheyWereFound()
    {
        var found = HumanoidMap.Of(People.Build(People.Capture).Skeleton);

        Assert.True(((IDictionary<HumanoidBone, string>)found.Bones).IsReadOnly);
        Assert.Equal(HumanoidBone.Hips, found.Bones.Keys.First());
        Assert.Equal(HumanoidBone.LeftUpperLeg, found.Bones.Keys.ElementAt(1));
    }

    [Fact]
    public void ProblemsAreReadOnly()
    {
        var found = HumanoidMap.Of(new RestSkeleton(["only"], [-1], [Matrix4.Identity()]));

        Assert.NotEmpty(found.Problems);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)found.Problems).Add("late"));
    }

    [Fact]
    public void ASlotTheSkeletonLacksAnswersNull()
    {
        var found = HumanoidMap.Of(People.Build(People.Capture, shoulders: false).Skeleton);

        Assert.Null(found[HumanoidBone.LeftShoulder]);
        Assert.Equal("mixamorig:Hips", found[HumanoidBone.Hips]);
    }

    [Fact]
    public void AReachChangesNothingItWasGiven()
    {
        var person = People.Build(People.Capture);
        var arm = new[] { HumanoidBone.LeftUpperArm, HumanoidBone.LeftLowerArm, HumanoidBone.LeftHand }.Select(slot => person.Slots[slot]).ToArray();
        var turned = Quaternion.AxisAngle(new Vector3(0, 0, 1), 0.4);
        var pose = new Dictionary<string, BoneLocal> { [arm[0]] = new(null, turned, null) };
        var target = new Vector3(0.5, 1.2, 0.3);
        var pole = new Vector3(0, 0, 2);

        LimbReach.Of(person.Skeleton, pose, arm[0], arm[1], arm[2], target, pole);

        Assert.Equal([arm[0]], pose.Keys);
        Assert.Equal(0.4, AngleOf(pose[arm[0]].Rotation!), 1e-12);
        Assert.Equal((0.5, 1.2, 0.3), (target.X, target.Y, target.Z));
        Assert.Equal((0.0, 0.0, 2.0), (pole.X, pole.Y, pole.Z));
    }

    private static double AngleOf(Quaternion q) => 2 * Math.Acos(Math.Clamp(q.W, -1, 1));
}
