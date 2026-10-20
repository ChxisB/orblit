namespace Orblit.Rig.Tests;

public class GeneratorTests
{
    private static readonly RigGenerator Generator = new();

    /// <summary>An arm hanging down and bent slightly forward, which tells the generator which way the elbow goes.</summary>
    private static MetaRig ArmMetaRig(string side = ".L")
    {
        var armature = new Armature(
        [
            new Bone($"upper_arm{side}", new Vector3(0, 0, 0), new Vector3(0.4, -2, 0)),
            new Bone($"forearm{side}", new Vector3(0.4, -2, 0), new Vector3(0, -3.9, 0), parent: $"upper_arm{side}", connected: true),
            new Bone($"hand{side}", new Vector3(0, -3.9, 0), new Vector3(0, -4.4, 0), parent: $"forearm{side}", connected: true),
        ]);
        var meta = new MetaRig(armature);
        meta.Assign($"upper_arm{side}", new LimbRig());
        return meta;
    }

    private static void ExpectVector(Vector3 actual, Vector3 expected, double tolerance = 1e-4)
    {
        Assert.True(Math.Abs(actual.X - expected.X) <= tolerance, $"x of {actual}");
        Assert.True(Math.Abs(actual.Y - expected.Y) <= tolerance, $"y of {actual}");
        Assert.True(Math.Abs(actual.Z - expected.Z) <= tolerance, $"z of {actual}");
    }

    // a copied bone

    [Fact]
    public void BecomesAControlAnOriginalAndADeformBone()
    {
        var meta = new MetaRig(new Armature([new Bone("jaw", Vector3.Zero(), new Vector3(0, 0, 1))]));
        meta.Assign("jaw", new CopyRig());

        var rig = Generator.Generate(meta);

        Assert.True(rig.Armature.Contains("jaw"));
        Assert.True(rig.Armature.Contains("ORG-jaw"));
        Assert.True(rig.Armature.Contains("DEF-jaw"));
        Assert.Contains("jaw", rig.Controls);
        Assert.True(new[] { "DEF-jaw" }.SequenceEqual(rig.DeformBones), "only the deform bone deforms; the rest is machinery");
    }

    [Fact]
    public void TheControlDrivesTheDeformBoneThroughTheOriginal()
    {
        var meta = new MetaRig(new Armature([new Bone("jaw", Vector3.Zero(), new Vector3(0, 1, 0))]));
        meta.Assign("jaw", new CopyRig());

        var rig = Generator.Generate(meta);
        rig.Pose["jaw"].Rotation = Quaternion.AxisAngle(new Vector3(0, 0, 1), Math.PI / 2);
        rig.Pose.Evaluate();

        ExpectVector(rig.Pose.TailOf("DEF-jaw"), new Vector3(-1, 0, 0));
    }

    // a limb

    [Fact]
    public void GeneratesTheBonesALimbNeeds()
    {
        var rig = Generator.Generate(ArmMetaRig());

        foreach (var name in new[]
        {
            "ORG-upper_arm.L",
            "ORG-forearm.L",
            "ORG-hand.L",
            "DEF-upper_arm.L",
            "DEF-forearm.L",
            "DEF-hand.L",
            "upper_arm_fk.L",
            "forearm_fk.L",
            "hand_fk.L",
            "hand_ik.L",
            "upper_arm_pole.L",
            "MCH-upper_arm_ik.L",
            "MCH-forearm_ik.L",
        })
        {
            Assert.True(rig.Armature.Contains(name), $"missing {name}");
        }
    }

    [Fact]
    public void OnlyTheControlsAreOfferedToAnAnimator()
    {
        var rig = Generator.Generate(ArmMetaRig());

        foreach (var name in new[] { "upper_arm_fk.L", "forearm_fk.L", "hand_fk.L", "hand_ik.L", "upper_arm_pole.L" })
        {
            Assert.Contains(name, rig.Controls);
        }

        foreach (var control in rig.Controls)
        {
            Assert.False(control.StartsWith("MCH-", StringComparison.Ordinal));
            Assert.False(control.StartsWith("DEF-", StringComparison.Ordinal));
            Assert.False(control.StartsWith("ORG-", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void CarriesASwitchBetweenTheTwoWaysOfDrivingIt()
    {
        var rig = Generator.Generate(ArmMetaRig());
        Assert.True(
            rig.Properties["upper_arm_ik_fk.L"] == 1,
            "blocking a shot happens on forward controls, so that is where the switch starts");
    }

    [Fact]
    public void ForwardControlsDriveTheDeformBones()
    {
        var rig = Generator.Generate(ArmMetaRig());
        var pose = rig.Pose;
        pose.SetProperty("upper_arm_ik_fk.L", 1);

        pose.Evaluate();
        var before = pose.TailOf("DEF-forearm.L").Clone();

        pose["upper_arm_fk.L"].Rotation = Quaternion.AxisAngle(new Vector3(0, 0, 1), 0.6);
        pose.Evaluate();

        Assert.True(
            (pose.TailOf("DEF-forearm.L") - before).Length > 0.5,
            "turning the shoulder should swing the forearm below it");
    }

    [Fact]
    public void TheInverseGoalDrivesTheDeformBones()
    {
        var rig = Generator.Generate(ArmMetaRig());
        var pose = rig.Pose;
        pose.SetProperty("upper_arm_ik_fk.L", 0);

        pose["hand_ik.L"].Location = new Vector3(1.0, 0.6, 0);
        pose.Evaluate();

        var hand = pose.HeadOf("DEF-hand.L");
        var goal = pose.HeadOf("hand_ik.L");
        ExpectVector(hand, goal, tolerance: 1e-3);
    }

    [Fact]
    public void TheChainKeepsItsBoneLengthsWhileReaching()
    {
        var rig = Generator.Generate(ArmMetaRig());
        var pose = rig.Pose;
        pose.SetProperty("upper_arm_ik_fk.L", 0);

        pose["hand_ik.L"].Location = new Vector3(0.8, 1.2, 0.3);
        pose.Evaluate();

        var upper = pose.HeadOf("MCH-forearm_ik.L") - pose.HeadOf("MCH-upper_arm_ik.L");
        var rest = rig.Armature["MCH-upper_arm_ik.L"]!.Length;
        Assert.True(
            Math.Abs(upper.Length - rest) <= 1e-3,
            "a limb that stretches to reach is a different feature, and not one anybody asked for by accident");
    }

    [Fact]
    public void TheSwitchHandsTheLimbOverRatherThanSnapping()
    {
        var rig = Generator.Generate(ArmMetaRig());
        var pose = rig.Pose;

        pose["hand_ik.L"].Location = new Vector3(1.2, 0.8, 0);
        pose["upper_arm_fk.L"].Rotation = Quaternion.AxisAngle(new Vector3(0, 0, 1), 0.9);

        pose.SetProperty("upper_arm_ik_fk.L", 1);
        pose.Evaluate();
        var forward = pose.HeadOf("DEF-hand.L").Clone();

        pose.SetProperty("upper_arm_ik_fk.L", 0);
        pose.Evaluate();
        var inverse = pose.HeadOf("DEF-hand.L").Clone();

        pose.SetProperty("upper_arm_ik_fk.L", 0.5);
        pose.Evaluate();
        var between = pose.HeadOf("DEF-hand.L");

        Assert.True(
            (forward - inverse).Length > 0.5,
            "the two ways of driving it should disagree, or the test proves nothing");
        Assert.True((between - forward).Length > 1e-3);
        Assert.True((between - inverse).Length > 1e-3);
    }

    [Fact]
    public void ThePoleSitsOffTheWayTheElbowAlreadyBends()
    {
        var rig = Generator.Generate(ArmMetaRig());
        rig.Pose.Evaluate();

        var pole = rig.Pose.HeadOf("upper_arm_pole.L");
        var elbow = rig.Pose.HeadOf("ORG-forearm.L");

        Assert.True(pole.X > elbow.X);
    }

    [Fact]
    public void MirrorsGenerateIndependentlyWithTheirOwnNames()
    {
        var meta = ArmMetaRig();
        var right = ArmMetaRig(side: ".R");
        foreach (var bone in right.Armature.Bones)
        {
            meta.Armature.Add(bone);
        }

        meta.Assign("upper_arm.R", new LimbRig());

        var rig = Generator.Generate(meta);

        Assert.True(rig.Armature.Contains("hand_ik.L"));
        Assert.True(rig.Armature.Contains("hand_ik.R"));
        Assert.Contains("upper_arm_ik_fk.L", rig.Properties.Keys);
        Assert.Contains("upper_arm_ik_fk.R", rig.Properties.Keys);
        Assert.Equal("hand_ik.R", BoneNaming.Mirror("hand_ik.L"));
    }

    // a finger

    private static MetaRig FingerMetaRig()
    {
        var armature = new Armature(
        [
            new Bone("f_index_01.L", Vector3.Zero(), new Vector3(0, 1, 0)),
            new Bone("f_index_02.L", new Vector3(0, 1, 0), new Vector3(0, 1.8, 0), parent: "f_index_01.L", connected: true),
            new Bone("f_index_03.L", new Vector3(0, 1.8, 0), new Vector3(0, 2.4, 0), parent: "f_index_02.L", connected: true),
        ]);
        var meta = new MetaRig(armature);
        meta.Assign("f_index_01.L", new FingerRig());
        return meta;
    }

    [Fact]
    public void OneControlCurlsEverySegment()
    {
        var rig = Generator.Generate(FingerMetaRig());
        var pose = rig.Pose;

        pose.Evaluate();
        var straight = pose.TailOf("DEF-f_index_03.L").Clone();

        Assert.True(rig.Controls.Contains("f_index_curl.L"), "the control is named for the finger, not for its first knuckle");
        pose["f_index_curl.L"].Rotation = Quaternion.AxisAngle(new Vector3(0, 0, 1), 0.5);
        pose.Evaluate();

        Assert.True(
            (pose.TailOf("DEF-f_index_03.L") - straight).Length > 0.3,
            "animating three knuckles separately is possible and nobody does it");
    }

    [Fact]
    public void TheSegmentsStillExistForAPoseThatNeedsThem()
    {
        var rig = Generator.Generate(FingerMetaRig());
        Assert.Equal(3, rig.DeformBones.Count());
    }

    // a spine

    private static MetaRig SpineMetaRig()
    {
        var armature = new Armature(
        [
            new Bone("spine", Vector3.Zero(), new Vector3(0, 1, 0)),
            new Bone("spine_01", new Vector3(0, 1, 0), new Vector3(0, 2, 0), parent: "spine", connected: true),
            new Bone("spine_02", new Vector3(0, 2, 0), new Vector3(0, 3, 0), parent: "spine_01", connected: true),
        ]);
        var meta = new MetaRig(armature);
        meta.Assign("spine", new SpineRig());
        return meta;
    }

    [Fact]
    public void GivesAnAnimatorATorsoHipsAndAChest()
    {
        var rig = Generator.Generate(SpineMetaRig());
        Assert.Contains("spine_torso", rig.Controls);
        Assert.Contains("hips", rig.Controls);
        Assert.Contains("chest", rig.Controls);
    }

    [Fact]
    public void TurningTheChestBendsTheTopOfTheSpineMost()
    {
        var rig = Generator.Generate(SpineMetaRig());
        var pose = rig.Pose;

        pose.Evaluate();
        var lowerBefore = pose.TailOf("DEF-spine").Clone();
        var upperBefore = pose.TailOf("DEF-spine_02").Clone();

        pose["chest"].Rotation = Quaternion.AxisAngle(new Vector3(0, 0, 1), 0.5);
        pose.Evaluate();

        var lowerMoved = (pose.TailOf("DEF-spine") - lowerBefore).Length;
        var upperMoved = (pose.TailOf("DEF-spine_02") - upperBefore).Length;

        Assert.True(
            upperMoved > lowerMoved,
            "a spine bends proportionally, which is what posing each vertebra by hand never quite achieves");
    }

    // the generator

    [Fact]
    public void AChainStopsWhereAnotherRigBegins()
    {
        var meta = ArmMetaRig();
        meta.Assign("hand.L", new CopyRig());
        var rig = Generator.Generate(meta);

        Assert.False(rig.Armature.Contains("hand"));
        Assert.Contains(
            "at least 3",
            string.Concat(rig.Problems.Select(problem => problem.Message)));
    }

    [Fact]
    public void ProblemsAreCollectedNotThrownAtTheFirstOne()
    {
        var meta = new MetaRig(new Armature([new Bone("lonely", Vector3.Zero(), new Vector3(0, 1, 0))]));
        meta.Assign("lonely", new LimbRig());
        meta.Assign("ghost", new CopyRig());

        var rig = Generator.Generate(meta);

        Assert.True(
            rig.Problems.Count == 2,
            "an artist fixing a rig wants the whole list; one problem per generate turns a five minute fix into an afternoon");
        Assert.True(rig.HasProblems);
    }

    [Fact]
    public void ABoneNobodyClaimedIsReportedRatherThanDropped()
    {
        var meta = ArmMetaRig();
        meta.Armature.Add(new Bone("forgotten", new Vector3(9, 0, 0), new Vector3(9, 1, 0)));

        var rig = Generator.Generate(meta);
        Assert.True(
            rig.Unassigned.Contains("forgotten"),
            "an unclaimed bone is almost always a chain somebody forgot to mark");
    }

    [Fact]
    public void GeneratingTwiceGivesTheSameRig()
    {
        var first = Generator.Generate(ArmMetaRig());
        var second = Generator.Generate(ArmMetaRig());

        Assert.True(
            first.Armature.EvaluationOrder.SequenceEqual(second.Armature.EvaluationOrder),
            "a generated rig that differs run to run cannot be diffed");
    }

    [Fact]
    public void AnEmptyMetaRigGeneratesAnEmptyRigRatherThanFailing()
    {
        var rig = Generator.Generate(new MetaRig(new Armature()));
        Assert.Equal(0, rig.Armature.Length);
        Assert.Empty(rig.Problems);
    }
}
