namespace Orblit.Motion.Tests;

// A leg hanging from a hip, with the hip hung from something that is turned and scaled, as a rig out of another
// tool is.
public class LimbTests
{
    private static readonly Dictionary<string, BoneLocal> NoPose = [];

    private readonly RestSkeleton _skeleton = Leg();

    [Fact]
    public void PutsTheFootOnATargetTheLegCanReach()
    {
        var target = new Vector3(0.4, -1.3, 0.5);
        var reach = LimbReach.Of(_skeleton, NoPose, "thigh", "shin", "foot", target);
        Assert.True(reach.Reachable);

        var posed = _skeleton.PosedOrigins(WithReach(NoPose, reach));

        ExpectNear(posed[3], target);
        ExpectNear(reach.End, target);
    }

    [Fact]
    public void KeepsEachBoneAsLongAsItWas()
    {
        var reach = LimbReach.Of(_skeleton, NoPose, "thigh", "shin", "foot", new Vector3(-0.3, -1.1, 0.2));

        var was = _skeleton.PosedOrigins(NoPose);
        var now = _skeleton.PosedOrigins(WithReach(NoPose, reach));

        Assert.Equal((was[2] - was[1]).Length, (now[2] - now[1]).Length, 1e-9);
        Assert.Equal((was[3] - was[2]).Length, (now[3] - now[2]).Length, 1e-9);
    }

    [Fact]
    public void KeepsTheKneeOnTheSideItWasOn()
    {
        var reach = LimbReach.Of(_skeleton, NoPose, "thigh", "shin", "foot", new Vector3(0, -1.2, 0.6));

        var posed = _skeleton.PosedOrigins(WithReach(NoPose, reach));

        Assert.True(posed[2].Z > 0);
    }

    [Fact]
    public void BendsAStraightLegTheWayTheRestBendsIt()
    {
        var straight = new Dictionary<string, BoneLocal>
        {
            ["shin"] = new(position: new Vector3(0, -1, 0)),
            ["foot"] = new(position: new Vector3(0, -1, 0)),
        };

        var reach = LimbReach.Of(_skeleton, straight, "thigh", "shin", "foot", new Vector3(0, -1.5, 0.3));

        var posed = _skeleton.PosedOrigins(WithReach(straight, reach));
        ExpectNear(posed[3], new Vector3(0, -1.5, 0.3));
        Assert.True(posed[2].Z > 0.5);
    }

    [Fact]
    public void BendsTowardsAPoleWhenGivenOne()
    {
        var reach = LimbReach.Of(
            _skeleton,
            NoPose,
            "thigh",
            "shin",
            "foot",
            new Vector3(0, -1.2, 0.6),
            pole: new Vector3(0, -0.5, -3));

        var posed = _skeleton.PosedOrigins(WithReach(NoPose, reach));

        Assert.True(posed[2].Z < 0);
        ExpectNear(posed[3], new Vector3(0, -1.2, 0.6));
    }

    [Fact]
    public void StretchesTheLimbStraightAtATargetPastItsReach()
    {
        var reach = LimbReach.Of(_skeleton, NoPose, "thigh", "shin", "foot", new Vector3(0, -5, 0));

        Assert.False(reach.Reachable);
        var posed = _skeleton.PosedOrigins(WithReach(NoPose, reach));
        Assert.Equal(0, posed[3].X, 1e-3);
        Assert.True(posed[3].Y < -1.9);
        ExpectNear(posed[3], reach.End, 1e-3);
    }

    [Fact]
    public void WorksFromThePoseItIsGivenNotTheRest()
    {
        var pose = new Dictionary<string, BoneLocal>
        {
            ["hips"] = new(new Vector3(0.2, 0.1, 0), Quaternion.AxisAngle(new Vector3(0, 1, 0), 0.7)),
            ["thigh"] = new(rotation: Quaternion.AxisAngle(new Vector3(1, 0, 0), -0.4)),
        };
        var target = new Vector3(0.5, -0.9, 0.4);

        var reach = LimbReach.Of(_skeleton, pose, "thigh", "shin", "foot", target);

        var posed = _skeleton.PosedOrigins(WithReach(pose, reach));
        ExpectNear(posed[3], target);
    }

    [Fact]
    public void ReachesThroughWhateverTheHipsHangFrom()
    {
        var turned = Leg(Matrix4.Compose(
            new Vector3(2, 1, -3),
            Quaternion.AxisAngle(new Vector3(1, 0, 0), Math.PI / 2),
            Vector3.All(0.5)));
        var target = turned.PosedOrigins(NoPose)[0] + new Vector3(0.2, 0, 0.3);

        var reach = LimbReach.Of(turned, NoPose, "thigh", "shin", "foot", target);

        var posed = turned.PosedOrigins(WithReach(NoPose, reach));
        ExpectNear(posed[3], target);
    }

    [Fact]
    public void SaysWhatIsWrongWithAChainThatIsNotOne()
    {
        Assert.Throws<ArgumentException>(
            () => LimbReach.Of(_skeleton, NoPose, "thigh", "foot", "shin", Vector3.Zero()));
        Assert.Throws<ArgumentException>(
            () => LimbReach.Of(_skeleton, NoPose, "thigh", "shin", "toe", Vector3.Zero()));
    }

    private static Matrix4 At(double x, double y, double z) =>
        Matrix4.Compose(new Vector3(x, y, z), Quaternion.Identity(), Vector3.All(1));

    private static RestSkeleton Leg(Matrix4? above = null) => new(
        ["hips", "thigh", "shin", "foot"],
        [-1, 0, 1, 2],
        // Hips at the origin, then a thigh and a shin one long each, bent a little forward at the knee so the leg
        // has a side to bend on.
        [At(0, 0, 0), At(0, 0, 0), At(0, -1, 0.1), At(0, -1, -0.1)],
        above);

    private static void ExpectNear(Vector3 got, Vector3 want, double within = 1e-6) =>
        Assert.True((got - want).Length < within, $"{got} is not {want}");

    // What a clip would hold once the two turns are keyed: they replace the bones' rotations and leave the rest
    // of each bone as it was.
    private static Dictionary<string, BoneLocal> WithReach(Dictionary<string, BoneLocal> pose, LimbReach reach) =>
        new(pose)
        {
            ["thigh"] = new(
                pose.GetValueOrDefault("thigh")?.Position,
                reach.Root,
                pose.GetValueOrDefault("thigh")?.Scale),
            ["shin"] = new(
                pose.GetValueOrDefault("shin")?.Position,
                reach.Mid,
                pose.GetValueOrDefault("shin")?.Scale),
        };
}
