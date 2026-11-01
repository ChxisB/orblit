namespace Orblit.Rig.Tests;

public class FaceTests
{
    /// <summary>A head with a jaw, two eyes and an upper lid on the left. Eyes point along +Z.</summary>
    private static Armature Head() => new(
    [
        new Bone("head", new Vector3(0, 1.6, 0), new Vector3(0, 1.8, 0)),
        new Bone("jaw", new Vector3(0, 1.7, 0.02), new Vector3(0, 1.64, 0.12), parent: "head"),
        new Bone("eye.L", new Vector3(0.03, 1.74, 0.05), new Vector3(0.03, 1.74, 0.08), parent: "head"),
        new Bone("eye.R", new Vector3(-0.03, 1.74, 0.05), new Vector3(-0.03, 1.74, 0.08), parent: "head"),
        new Bone("lid_upper.L", new Vector3(0.03, 1.75, 0.05), new Vector3(0.03, 1.76, 0.08), parent: "head"),
    ]);

    private static GeneratedRig Face(string eyeForLid = "eye.L")
    {
        var meta = new MetaRig(Head());
        meta.Assign("jaw", new JawRig());
        meta.Assign("eye.L", new EyeRig());
        meta.Assign("eye.R", new EyeRig());
        meta.Assign("lid_upper.L", new EyelidRig(eyeForLid));
        return new RigGenerator().Generate(meta);
    }

    /// <summary>Which way a bone points, in armature space.</summary>
    private static Vector3 Forward(Pose pose, string bone) => (pose.TailOf(bone) - pose.HeadOf(bone)).Normalized();

    /// <summary>How far a bone has turned from where it rests, in radians.</summary>
    private static double Departure(Pose pose, string bone)
    {
        var rest = Quaternion.Identity();
        pose.BaseOf(bone).Decompose(Vector3.Zero(), rest, Vector3.Zero());

        var now = Quaternion.Identity();
        pose.WorldOf(bone).Decompose(Vector3.Zero(), now, Vector3.Zero());

        var local = rest.Normalized().Conjugated() * now.Normalized();
        local.Normalize();
        return 2 * Math.Acos(Doubles.Clamp(Math.Abs(local.W), 0.0, 1.0));
    }

    // a jaw

    [Fact]
    public void CannotBePushedFurtherThanAJawGoes()
    {
        var pose = Face().Pose;

        pose["jaw"].Rotation = Quaternion.AxisAngle(new Vector3(1, 0, 0), Math.PI);
        pose.Evaluate();

        Assert.True(Departure(pose, "ORG-jaw") <= (Math.PI / 5) + 1e-6);
    }

    [Fact]
    public void IsLimitedFromWhereItRestsNotFromTheIdentity()
    {
        var pose = Face().Pose;
        pose.Evaluate();

        Assert.Equal(0, Departure(pose, "ORG-jaw"), 1e-6);

        const double opening = Math.PI / 12;
        pose["jaw"].Rotation = Quaternion.AxisAngle(new Vector3(1, 0, 0), opening);
        pose.Evaluate();

        Assert.Equal(opening, Departure(pose, "ORG-jaw"), 1e-6);
    }

    [Fact]
    public void StillMovesWithinItsRange()
    {
        var rig = Face();
        rig.Pose.Evaluate();
        var restTail = rig.Pose.TailOf("DEF-jaw").Clone();

        rig.Pose["jaw"].Rotation = Quaternion.AxisAngle(new Vector3(1, 0, 0), Math.PI / 12);
        rig.Pose.Evaluate();

        Assert.True((rig.Pose.TailOf("DEF-jaw") - restTail).Length > 1e-3);
    }

    // eyes

    [Fact]
    public void ShareOneControlSoACharacterLooksSomewhereInOneMove()
    {
        var rig = Face();

        Assert.True(rig.Armature.Contains(EyeRig.MasterName));
        Assert.Equal(EyeRig.MasterName, rig.Armature["eye_target.L"]?.Parent);
        Assert.Equal(EyeRig.MasterName, rig.Armature["eye_target.R"]?.Parent);
    }

    [Fact]
    public void FollowTheSharedControlWhenItMoves()
    {
        var pose = Face().Pose;
        pose.Evaluate();

        var before = Forward(pose, "ORG-eye.L");
        Assert.True(before.Z > 0.9, "should start looking ahead");

        pose[EyeRig.MasterName].Location.SetValues(1.5, 0, 0);
        pose.Evaluate();

        var after = Forward(pose, "ORG-eye.L");
        Assert.True(after.X > 0.3);
        Assert.True(after.Z < before.Z);
    }

    [Fact]
    public void StayParallelByDefaultSoAnUnposedRigIsTheRestPose()
    {
        var pose = Face().Pose;
        pose.Evaluate();

        Assert.Equal(1, Forward(pose, "ORG-eye.L").Z, 1e-6);
        Assert.Equal(1, Forward(pose, "ORG-eye.R").Z, 1e-6);

        pose[EyeRig.MasterName].Location.SetValues(0.8, 0, 0);
        pose.Evaluate();

        var left = Forward(pose, "ORG-eye.L");
        var right = Forward(pose, "ORG-eye.R");
        Assert.Equal(left.X, right.X, 1e-9);
    }

    [Fact]
    public void ConvergeOnOnePointWhenFocusIsTurnedUp()
    {
        var pose = Face().Pose;
        pose.SetProperty(EyeRig.FocusProperty, 1);
        pose.Evaluate();

        pose[EyeRig.MasterName].Location.SetValues(0.8, 0, 0);
        pose.Evaluate();

        var left = Forward(pose, "ORG-eye.L");
        var right = Forward(pose, "ORG-eye.R");

        Assert.True(left.X > 0);
        Assert.True(right.X > left.X);
    }

    [Fact]
    public void FocusStartsOffSoItChangesNothingUntilAsked()
    {
        Assert.Equal(0, Face().Properties[EyeRig.FocusProperty]);
    }

    // an eyelid

    [Fact]
    public void FollowsTheEyePartOfTheWayNotAllOfIt()
    {
        var pose = Face().Pose;
        pose.Evaluate();
        var restLid = Forward(pose, "ORG-lid_upper.L").Clone();

        pose[EyeRig.MasterName].Location.SetValues(1.5, 0, 0);
        pose.Evaluate();

        var lid = Forward(pose, "ORG-lid_upper.L");
        var eye = Forward(pose, "ORG-eye.L");

        Assert.True((lid - restLid).Length > 1e-3);
        Assert.True(lid.X < eye.X);
    }

    [Fact]
    public void AnEyeThatIsNotInTheMetaRigIsReportedNotIgnored()
    {
        var rig = Face("eye.middle");

        Assert.True(rig.HasProblems);
        Assert.Contains("eye.middle", string.Concat(rig.Problems.Select(problem => problem.ToString())));
    }

    [Fact]
    public void TheLidIsStillBuiltWhenTheEyeItNamesIsMissing()
    {
        var rig = Face("eye.middle");

        Assert.True(rig.Armature.Contains("lid_upper.L"));
        Assert.True(rig.Armature.Contains("DEF-lid_upper.L"));
    }

    [Fact]
    public void TheWholeFaceHidesTogether()
    {
        var members = Face().Collections.MembersOf(FaceRigTypes.FaceCollection);

        Assert.Contains("jaw", members);
        Assert.Contains("eye_target.L", members);
        Assert.Contains(EyeRig.MasterName, members);
        Assert.Contains("lid_upper.L", members);
    }
}
