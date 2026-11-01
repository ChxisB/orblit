namespace Orblit.Rig.Tests;

public class StretchTests
{
    /// <summary>A two-bone chain along +Y, each bone a metre long, plus a goal to reach for.</summary>
    private static Pose Limb(Vector3 goal) => new(new Armature(
    [
        new Bone("upper", new Vector3(0, 0, 0), new Vector3(0, 1, 0)),
        new Bone("lower", new Vector3(0, 1, 0), new Vector3(0, 2, 0), parent: "upper", connected: true),
        new Bone("goal", goal, goal + new Vector3(0, 0.2, 0)),
        new Bone("pole", new Vector3(0, 1, 2), new Vector3(0, 1.2, 2)),
    ]));

    private static IkChain Chain(double stretch = 0, string? stretchProperty = null) =>
        new("upper", "lower", "goal", pole: "pole", stretch: stretch, stretchProperty: stretchProperty);

    // the solver

    [Fact]
    public void DoesNotStretchALimbThatCanAlreadyReach()
    {
        var solution = Ik.SolveTwoBoneIk(
            root: Vector3.Zero(),
            pole: new Vector3(0, 1, 2),
            target: new Vector3(0, 1.5, 0),
            upperLength: 1,
            lowerLength: 1,
            stretch: 1);
        Assert.Equal(1, solution.Stretch);
    }

    [Fact]
    public void StopsShortWhenStretchIsOff()
    {
        var solution = Ik.SolveTwoBoneIk(
            root: Vector3.Zero(),
            pole: new Vector3(0, 1, 2),
            target: new Vector3(0, 3, 0),
            upperLength: 1,
            lowerLength: 1);
        Assert.Equal(1, solution.Stretch);
        Assert.False(solution.Reachable);
        // The solver stops a whisker inside full extension on purpose, so the tolerance is wider than that margin.
        Assert.Equal(2, (solution.End - Vector3.Zero()).Length, 1e-3);
    }

    [Fact]
    public void ReachesAnythingWhenStretchIsFull()
    {
        var solution = Ik.SolveTwoBoneIk(
            root: Vector3.Zero(),
            pole: new Vector3(0, 1, 2),
            target: new Vector3(0, 3, 0),
            upperLength: 1,
            lowerLength: 1,
            stretch: 1);
        Assert.True(solution.Reachable);
        Assert.Equal(1.5, solution.Stretch, 1e-9);
        Assert.True((solution.End - new Vector3(0, 3, 0)).Length < 1e-9);
    }

    [Fact]
    public void PartialStretchClosesPartOfTheGapNotAllOfIt()
    {
        var solution = Ik.SolveTwoBoneIk(
            root: Vector3.Zero(),
            pole: new Vector3(0, 1, 2),
            target: new Vector3(0, 3, 0),
            upperLength: 1,
            lowerLength: 1,
            stretch: 0.5);
        // Half of the way from 2 to 3.
        Assert.Equal(2.5, solution.End.Y, 1e-9);
        Assert.False(solution.Reachable);
    }

    // a chain that has to bend

    [Fact]
    public void LandsItsTailOnAGoalOffTheLimbsOwnLine()
    {
        var pose = Limb(goal: new Vector3(1, 1, 0));
        pose.AddIkChain(Chain());
        pose.Evaluate();

        Assert.True(pose.TailOf("lower").DistanceTo(new Vector3(1, 1, 0)) < 1e-6);
    }

    [Fact]
    public void KeepsTheLowerBoneJoinedToTheUpper()
    {
        var pose = Limb(goal: new Vector3(1, 1, 0));
        pose.AddIkChain(Chain());
        pose.Evaluate();

        Assert.True(pose.TailOf("upper").DistanceTo(pose.HeadOf("lower")) < 1e-6);
    }

    // a stretching chain

    [Fact]
    public void LandsItsTailOnATargetItCouldNotOtherwiseReach()
    {
        var pose = Limb(goal: new Vector3(0, 3, 0));
        pose.AddIkChain(Chain(stretch: 1));
        pose.Evaluate();

        Assert.Equal(3, pose.TailOf("lower").Y, 1e-6);
    }

    [Fact]
    public void NarrowsAsItLengthensRatherThanGettingFatter()
    {
        var pose = Limb(goal: new Vector3(0, 3, 0));
        pose.AddIkChain(Chain(stretch: 1));
        pose.Evaluate();

        var scale = pose["upper"].Scale;
        Assert.Equal(1.5, scale.Y, 1e-9);
        // Volume preserved: the cross-section is the inverse square root of the stretch.
        Assert.Equal(1 / Math.Sqrt(1.5), scale.X, 1e-9);
        Assert.True(scale.X < 1);
        Assert.Equal(scale.X, scale.Z, 1e-12);
    }

    [Fact]
    public void TheLowerBoneIsNotStretchedTwiceByInheritedScale()
    {
        var pose = Limb(goal: new Vector3(0, 3, 0));
        pose.AddIkChain(Chain(stretch: 1));
        pose.Evaluate();

        // Scale is inherited, so the lower bone carries its own factor of one. Scaling it as well would put its tail at 3.75.
        Assert.Equal(1, pose["lower"].Scale.Y);
        Assert.Equal(1.5, pose.HeadOf("lower").Y, 1e-6);
        Assert.Equal(3, pose.TailOf("lower").Y, 1e-6);
    }

    [Fact]
    public void GoesBackToItsOwnLengthWhenTheTargetComesIntoReach()
    {
        var pose = Limb(goal: new Vector3(0, 3, 0));
        pose.AddIkChain(Chain(stretch: 1));
        pose.Evaluate();

        Assert.True(pose["upper"].Scale.Y > 1.4);

        // A stretch left over from the frame before would leave the limb permanently long.
        pose["goal"].Location.SetValues(0, -1.5, 0);
        pose.Evaluate();

        Assert.Equal(1, pose["upper"].Scale.Y, 1e-6);
        Assert.Equal(1.5, pose.TailOf("lower").Y, 1e-3);
    }

    [Fact]
    public void AnAnimatorCanTurnStretchOffThroughAProperty()
    {
        var pose = Limb(goal: new Vector3(0, 3, 0));
        pose.SetProperty("arm_stretch", 0);
        pose.AddIkChain(Chain(stretch: 1, stretchProperty: "arm_stretch"));
        pose.Evaluate();

        Assert.Equal(1, pose["upper"].Scale.Y, 1e-9);
        // Full extension, within the solver's stabilising margin.
        Assert.Equal(2, pose.TailOf("lower").Y, 1e-3);
    }

    // the stretch-to constraint

    [Fact]
    public void ReachesATargetAndKeepsItsVolume()
    {
        var pose = new Pose(new Armature(
        [
            new Bone("hose", new Vector3(0, 0, 0), new Vector3(0, 1, 0)),
            new Bone("end", new Vector3(0, 2, 0), new Vector3(0, 2.2, 0)),
        ]));
        pose.Constrain("hose", new StretchTo("end", restLength: 1));
        pose.Evaluate();

        Assert.Equal(2, pose.TailOf("hose").Y, 1e-6);

        var scale = Vector3.Zero();
        pose.WorldOf("hose").Decompose(Vector3.Zero(), Quaternion.Identity(), scale);
        Assert.Equal(2, scale.Y, 1e-6);
        Assert.Equal(1 / Math.Sqrt(2), scale.X, 1e-6);
    }

    [Fact]
    public void AimsSidewaysAsWellAsStretching()
    {
        var pose = new Pose(new Armature(
        [
            new Bone("hose", new Vector3(0, 0, 0), new Vector3(0, 1, 0)),
            new Bone("end", new Vector3(3, 0, 0), new Vector3(3.2, 0, 0)),
        ]));
        pose.Constrain("hose", new StretchTo("end", restLength: 1));
        pose.Evaluate();

        var tail = pose.TailOf("hose");
        Assert.Equal(3, tail.X, 1e-6);
        Assert.Equal(0, tail.Y, 1e-6);
    }

    [Fact]
    public void LeavesTheCrossSectionAloneWhenVolumeIsOff()
    {
        var pose = new Pose(new Armature(
        [
            new Bone("hose", new Vector3(0, 0, 0), new Vector3(0, 1, 0)),
            new Bone("end", new Vector3(0, 2, 0), new Vector3(0, 2.2, 0)),
        ]));
        pose.Constrain("hose", new StretchTo("end", restLength: 1, volume: 0));
        pose.Evaluate();

        var scale = Vector3.Zero();
        pose.WorldOf("hose").Decompose(Vector3.Zero(), Quaternion.Identity(), scale);
        Assert.Equal(2, scale.Y, 1e-6);
        Assert.Equal(1, scale.X, 1e-6);
    }
}
