namespace Orblit.Rig.Tests;

/// <summary>
/// Where the port refuses what the fixture exporter only asserted. An assert is off outside tests and an
/// unknown bone was a null check, so none of these have a fixture twin.
/// </summary>
public class RefusalTests
{
    private static Pose OneBone()
    {
        var pose = new Pose(new Armature([new Bone("root", new Vector3(0, 0, 0), new Vector3(0, 1, 0))]));
        pose.Evaluate();
        return pose;
    }

    [Fact]
    public void AskingForAnUnknownBoneNamesIt()
    {
        var pose = OneBone();
        Assert.Throws<ArmatureException>(() => pose.BaseOf("tail"));
        Assert.Throws<ArmatureException>(() => pose.TailOf("tail"));
        Assert.Throws<ArmatureException>(() => pose.WorldOf("tail"));
        Assert.Throws<ArmatureException>(() => pose.HeadOf("tail"));
        Assert.Throws<ArmatureException>(() => pose.SkinningOf("tail"));
    }

    [Fact]
    public void TheErrorNamesTheBone()
    {
        var error = Assert.Throws<ArmatureException>(() => OneBone().BaseOf("tail"));
        Assert.Contains("\"tail\"", error.Message, StringComparison.Ordinal);
    }
}
