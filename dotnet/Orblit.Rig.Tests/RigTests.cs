namespace Orblit.Rig.Tests;

public class RigTests
{
    private static void ExpectVector(Vector3 actual, Vector3 expected, double tolerance = 1e-5, string? reason = null)
    {
        var because = reason is null ? "" : $" ({reason})";
        Assert.True(Math.Abs(actual.X - expected.X) <= tolerance, $"x of {actual}{because}");
        Assert.True(Math.Abs(actual.Y - expected.Y) <= tolerance, $"y of {actual}{because}");
        Assert.True(Math.Abs(actual.Z - expected.Z) <= tolerance, $"z of {actual}{because}");
    }

    /// <summary>A two-bone arm along +Y, the simplest thing that has a hierarchy.</summary>
    private static Armature Arm() => new(
    [
        new Bone("upper", Vector3.Zero(), new Vector3(0, 2, 0)),
        new Bone("lower", new Vector3(0, 2, 0), new Vector3(0, 5, 0), parent: "upper", connected: true),
    ]);

    private static Quaternion QuarterTurnAboutZ() => Quaternion.AxisAngle(new Vector3(0, 0, 1), Math.PI / 2);

    private static Armature SourceAndTarget() => new(
    [
        new Bone("source", Vector3.Zero(), new Vector3(0, 1, 0)),
        new Bone("target", new Vector3(5, 0, 0), new Vector3(5, 1, 0)),
    ]);

    // a bone

    [Fact]
    public void MeasuresItselfBetweenItsEnds()
    {
        var bone = new Bone("b", Vector3.Zero(), new Vector3(0, 3, 0));
        Assert.Equal(3, bone.Length, 1e-6);
        ExpectVector(bone.Direction, new Vector3(0, 1, 0));
    }

    [Fact]
    public void PointingAlongPlusYIsTheIdentityOrientation()
    {
        // The convention the whole package rests on: a bone's own Y axis runs head to tail.
        var bone = new Bone("b", Vector3.Zero(), new Vector3(0, 1, 0));
        ExpectVector(bone.RestMatrix.Transformed3(new Vector3(0, 1, 0)), new Vector3(0, 1, 0));
    }

    [Fact]
    public void TurnsItsOwnYTowardsWhereItPoints()
    {
        var bone = new Bone("b", Vector3.Zero(), new Vector3(2, 0, 0));
        // Read through the matrix rather than the quaternion, because the quaternion rotation runs the other way.
        ExpectVector(bone.RestMatrix.Transformed3(new Vector3(0, 1, 0)), new Vector3(1, 0, 0));
    }

    [Fact]
    public void SitsAtItsHead()
    {
        var bone = new Bone("b", new Vector3(1, 2, 3), new Vector3(1, 5, 3));
        ExpectVector(bone.RestMatrix.GetTranslation(), new Vector3(1, 2, 3));
    }

    [Fact]
    public void RollTwistsAboutTheBoneRatherThanMovingIt()
    {
        var straight = new Bone("b", Vector3.Zero(), new Vector3(0, 1, 0));
        var rolled = new Bone("b", Vector3.Zero(), new Vector3(0, 1, 0), roll: Math.PI / 2);

        // Still pointing the same way, but turned about that direction, which decides where a knee bends.
        ExpectVector(
            rolled.RestMatrix.Transformed3(new Vector3(0, 1, 0)),
            straight.RestMatrix.Transformed3(new Vector3(0, 1, 0)));
        Assert.Equal(0, rolled.RestMatrix.Transformed3(new Vector3(1, 0, 0)).X, 1e-6);
    }

    [Fact]
    public void AZeroLengthBoneStillProducesAUsableMatrix()
    {
        var bone = new Bone("b", Vector3.Zero(), Vector3.Zero());
        Assert.Equal(0, bone.RestMatrix.GetTranslation().Length, 1e-6);
        // A degenerate bone falls back to an axis rather than filling the matrix with NaNs.
        Assert.Equal(1, bone.Direction.Length, 1e-6);
    }

    // an armature

    [Fact]
    public void EvaluatesParentsBeforeChildren()
    {
        var order = Arm().EvaluationOrder.ToList();
        Assert.True(order.IndexOf("upper") < order.IndexOf("lower"));
    }

    [Fact]
    public void RefusesABoneThatIsItsOwnAncestor()
    {
        var armature = new Armature(
        [
            new Bone("a", Vector3.Zero(), new Vector3(0, 1, 0), parent: "b"),
            new Bone("b", Vector3.Zero(), new Vector3(0, 1, 0), parent: "a"),
        ]);
        Assert.Throws<ArmatureException>(() => armature.EvaluationOrder);
    }

    [Fact]
    public void RefusesAParentThatIsNotThere()
    {
        var armature = new Armature([new Bone("a", Vector3.Zero(), new Vector3(0, 1, 0), parent: "ghost")]);
        Assert.Throws<ArmatureException>(() => armature.EvaluationOrder);
    }

    [Fact]
    public void RefusesTwoBonesWithTheSameName()
    {
        var armature = new Armature();
        armature.Add(new Bone("a", Vector3.Zero(), new Vector3(0, 1, 0)));
        Assert.Throws<ArmatureException>(() => armature.Add(new Bone("a", Vector3.Zero(), new Vector3(0, 1, 0))));
    }

    [Fact]
    public void RemovingABonePromotesItsChildrenRatherThanBreaking()
    {
        var armature = Arm();
        armature.Remove("upper");
        Assert.Null(armature["lower"]!.Parent);
        // A rig that loses a bone keeps evaluating, so the mistake shows up as a limb in the wrong place.
        Assert.Equal(["lower"], armature.EvaluationOrder);
    }

    [Fact]
    public void AChildRestsRelativeToItsParent()
    {
        // The lower bone starts two units up its parent's own axis.
        ExpectVector(Arm().RestLocalOf("lower").GetTranslation(), new Vector3(0, 2, 0));
    }

    // posing

    [Fact]
    public void AtRestABoneIsWhereItRests()
    {
        var pose = new Pose(Arm());
        pose.Evaluate();

        ExpectVector(pose.HeadOf("lower"), new Vector3(0, 2, 0));
        ExpectVector(pose.TailOf("lower"), new Vector3(0, 5, 0));
    }

    [Fact]
    public void AtRestSkinningChangesNothing()
    {
        var pose = new Pose(Arm());
        pose.Evaluate();
        // The rest pose is the pose the mesh was bound in, so it must move nothing.
        var point = pose.SkinningOf("lower").Transformed3(new Vector3(1, 3, 2));
        ExpectVector(point, new Vector3(1, 3, 2));
    }

    [Fact]
    public void TurningAParentCarriesItsChildren()
    {
        var pose = new Pose(Arm());

        // Fold the upper bone a quarter turn about Z: +Y becomes -X.
        pose["upper"].Rotation = QuarterTurnAboutZ();
        pose.Evaluate();

        ExpectVector(pose.TailOf("upper"), new Vector3(-2, 0, 0));
        ExpectVector(pose.HeadOf("lower"), new Vector3(-2, 0, 0), reason: "a connected child follows its parent to the joint");
        ExpectVector(pose.TailOf("lower"), new Vector3(-5, 0, 0));
    }

    [Fact]
    public void AChildTurnsAboutItsOwnJointNotTheRoot()
    {
        var pose = new Pose(Arm());
        pose["lower"].Rotation = QuarterTurnAboutZ();
        pose.Evaluate();

        // The elbow has not moved; the forearm has swung from it.
        ExpectVector(pose.HeadOf("lower"), new Vector3(0, 2, 0));
        ExpectVector(pose.TailOf("lower"), new Vector3(-3, 2, 0));
    }

    [Fact]
    public void ResettingPutsEverythingBack()
    {
        var pose = new Pose(Arm());
        pose["upper"].Rotation = Quaternion.AxisAngle(new Vector3(0, 0, 1), 1.2);
        pose.Evaluate();
        pose.Reset();
        pose.Evaluate();
        ExpectVector(pose.TailOf("lower"), new Vector3(0, 5, 0));
    }

    [Fact]
    public void OnlyDeformingBonesProduceSkinningMatrices()
    {
        var armature = Arm();
        armature["lower"]!.Deform = false;
        var pose = new Pose(armature);
        pose.Evaluate();

        // A generated rig has far more bones than deforming ones, and skinning only ever sees the deforming ones.
        Assert.Equal(["upper"], pose.SkinningMatrices().Keys);
    }

    // two-bone inverse kinematics

    [Fact]
    public void ReachesATargetItCanReach()
    {
        var solution = Ik.SolveTwoBoneIk(
            root: Vector3.Zero(),
            pole: new Vector3(0, 0, 5),
            target: new Vector3(3, 1, 0),
            upperLength: 2,
            lowerLength: 3);

        Assert.True(solution.Reachable);
        ExpectVector(solution.End, new Vector3(3, 1, 0));
    }

    [Fact]
    public void KeepsBothBonesTheirOwnLength()
    {
        const double Upper = 2.0, Lower = 3.0;
        var solution = Ik.SolveTwoBoneIk(
            root: Vector3.Zero(),
            pole: new Vector3(0, 0, 5),
            target: new Vector3(3.5, 1, 0),
            upperLength: Upper,
            lowerLength: Lower);

        Assert.Equal(Upper, solution.Joint.Length, 1e-5);
        Assert.Equal(Lower, (solution.End - solution.Joint).Length, 1e-5);
    }

    [Fact]
    public void ThePoleDecidesWhichWayTheJointBends()
    {
        static Vector3 JointWithPole(Vector3 pole) => Ik.SolveTwoBoneIk(
            root: Vector3.Zero(),
            pole: pole,
            target: new Vector3(4, 0, 0),
            upperLength: 3,
            lowerLength: 3).Joint;

        var front = JointWithPole(new Vector3(2, 0, 5));
        var back = JointWithPole(new Vector3(2, 0, -5));

        // Mirrored about the line to the target: the difference between a knee and a backwards knee.
        Assert.True(front.Z > 0);
        Assert.True(back.Z < 0);
        Assert.Equal(Math.Abs(back.Z), Math.Abs(front.Z), 1e-5);
    }

    [Fact]
    public void StraightensTowardsATargetItCannotReach()
    {
        var solution = Ik.SolveTwoBoneIk(
            root: Vector3.Zero(),
            pole: new Vector3(0, 0, 5),
            target: new Vector3(100, 0, 0),
            upperLength: 2,
            lowerLength: 3);

        Assert.False(solution.Reachable);
        // Extended along the line to the target, not quite perfectly straight: an exactly straight limb has no
        // defined bend plane, so the solver stops a hair short.
        Assert.Equal(5, solution.End.Length, 1e-3);
        ExpectVector(solution.End.Normalized(), new Vector3(1, 0, 0), tolerance: 5e-3);
    }

    [Fact]
    public void FoldsAsFarAsItGoesTowardsSomethingTooClose()
    {
        var solution = Ik.SolveTwoBoneIk(
            root: Vector3.Zero(),
            pole: new Vector3(0, 0, 5),
            target: new Vector3(0.01, 0, 0),
            upperLength: 3,
            lowerLength: 1);

        Assert.False(solution.Reachable);
        // Cannot get closer than the difference between the two bones.
        Assert.Equal(2, solution.End.Length, 0.01);
    }

    [Fact]
    public void APoleInLineWithTheTargetStillGivesAUsableBend()
    {
        // Degenerate: the pole names no plane. It picks one rather than produce a limb full of NaNs.
        var solution = Ik.SolveTwoBoneIk(
            root: Vector3.Zero(),
            pole: new Vector3(2, 0, 0),
            target: new Vector3(4, 0, 0),
            upperLength: 3,
            lowerLength: 3);

        Assert.True(double.IsFinite(solution.Joint.X));
        Assert.Equal(3, solution.Joint.Length, 1e-5);
    }

    // constraints

    [Fact]
    public void CopyRotationTakesAnotherBonesOrientation()
    {
        var pose = new Pose(SourceAndTarget());
        pose["source"].Rotation = QuarterTurnAboutZ();
        pose.Constrain("target", new CopyRotation("source"));
        pose.Evaluate();

        // Turned like the source, but still where it was.
        ExpectVector(pose.HeadOf("target"), new Vector3(5, 0, 0));
        ExpectVector(pose.TailOf("target"), new Vector3(4, 0, 0));
    }

    [Fact]
    public void InfluenceBlendsRatherThanSwitching()
    {
        var pose = new Pose(SourceAndTarget());
        pose["source"].Rotation = QuarterTurnAboutZ();
        pose.Constrain("target", new CopyRotation("source", influence: 0.5));
        pose.Evaluate();

        // Half of a quarter turn: the tail is up and across, not fully across.
        var tail = pose.TailOf("target");
        Assert.True(tail.X < 5);
        Assert.True(tail.Y > 0);
    }

    [Fact]
    public void DampedTrackAimsTheChosenAxisAtATarget()
    {
        var pose = new Pose(new Armature(
        [
            new Bone("eye", Vector3.Zero(), new Vector3(0, 1, 0)),
            new Bone("focus", new Vector3(0, 0, -4), new Vector3(0, 1, -4)),
        ]));
        pose.Constrain("eye", new DampedTrack(target: "focus"));
        pose.Evaluate();

        // The bone's own Y now points at the focus.
        var direction = (pose.TailOf("eye") - pose.HeadOf("eye")).Normalized();
        ExpectVector(direction, new Vector3(0, 0, -1));
    }

    [Fact]
    public void LimitRotationClampsHowFarAJointBends()
    {
        var pose = new Pose(new Armature([new Bone("finger", Vector3.Zero(), new Vector3(0, 1, 0))]));
        // Asked for a half turn, allowed a quarter.
        pose["finger"].Rotation = Quaternion.AxisAngle(new Vector3(0, 0, 1), Math.PI);
        pose.Constrain("finger", new LimitRotation(maximumAngle: Math.PI / 2));
        pose.Evaluate();

        // A quarter turn about Z puts +Y at -X.
        ExpectVector(pose.TailOf("finger"), new Vector3(-1, 0, 0));
    }

    [Fact]
    public void AConstraintCarriesTheBonesBelowIt()
    {
        var armature = Arm();
        armature.Add(new Bone("source", new Vector3(9, 0, 0), new Vector3(9, 1, 0)));

        var pose = new Pose(armature);
        pose["source"].Rotation = QuarterTurnAboutZ();
        pose.Constrain("upper", new CopyRotation("source"));
        pose.Evaluate();

        // Constraining the upper arm swung the forearm too, which makes a rig a mechanism rather than a list of parts.
        ExpectVector(pose.TailOf("lower"), new Vector3(-5, 0, 0));
    }

    // naming

    [Fact]
    public void MakesAFilesJointNamesFitToBeBoneNames()
    {
        // A name the file gives is never taken by a renamed joint.
        Assert.Equal(
            ["hip", "joint 1", "hip.002", "hip.001"],
            BoneNaming.Unique(["hip", "", "hip", "hip.001"]));
    }

    [Fact]
    public void APrefixSaysWhatABoneIsFor()
    {
        Assert.Equal(BoneRole.Deform, BoneNaming.RoleOf("DEF-upper_arm.L"));
        Assert.Equal(BoneRole.Original, BoneNaming.RoleOf("ORG-spine"));
        Assert.Equal(BoneRole.Mechanism, BoneNaming.RoleOf("MCH-ik_target"));
        Assert.Equal(BoneRole.Control, BoneNaming.RoleOf("hand_ik.L"));
    }

    [Fact]
    public void ASideSurvivesADuplicateSuffix()
    {
        Assert.Equal(Side.Left, BoneNaming.SideOf("hand.L"));
        Assert.Equal(Side.Right, BoneNaming.SideOf("hand.R"));
        // The number goes after the side, so anything that only looked at the end of the name would call this sideless.
        Assert.Equal(Side.Left, BoneNaming.SideOf("hand.L.001"));
        Assert.Null(BoneNaming.SideOf("spine"));
    }

    [Fact]
    public void TheBaseIsWhatIsLeftAfterTheRoleAndTheSide()
    {
        Assert.Equal("upper_arm", BoneNaming.BaseOf("DEF-upper_arm.L"));
        Assert.Equal("foot_roll", BoneNaming.BaseOf("MCH-foot_roll.R.001"));
        Assert.Equal("spine", BoneNaming.BaseOf("spine"));
    }

    [Fact]
    public void NamesAreBuiltFromTheirParts()
    {
        Assert.Equal("DEF-upper_arm.L", BoneNaming.Compose("upper_arm", role: BoneRole.Deform, side: Side.Left));
        Assert.Equal("spine", BoneNaming.Compose("spine"));
    }

    [Fact]
    public void ABoneCanBeAskedForInAnotherRole()
    {
        Assert.Equal("upper_arm.L", BoneNaming.AsRole("DEF-upper_arm.L", BoneRole.Control));
        Assert.Equal("MCH-upper_arm.L", BoneNaming.AsRole("upper_arm.L", BoneRole.Mechanism));
    }

    [Fact]
    public void MirroringSwapsTheSideAndKeepsEverythingElse()
    {
        Assert.Equal("DEF-hand.R", BoneNaming.Mirror("DEF-hand.L"));
        Assert.Equal("hand.L", BoneNaming.Mirror("hand.R"));
        // A bone on the midline has no opposite.
        Assert.Null(BoneNaming.Mirror("spine"));
    }

    [Fact]
    public void AConstraintThatClosesALoopIsRefused()
    {
        var pose = new Pose(new Armature(
        [
            new Bone("a", Vector3.Zero(), new Vector3(0, 1, 0)),
            new Bone("b", new Vector3(1, 0, 0), new Vector3(1, 1, 0)),
        ]));
        pose.Constrain("a", new CopyRotation("b"));
        pose.Constrain("b", new CopyRotation("a"));

        // No order satisfies both, and saying so beats evaluating one of them against last frame's answer.
        Assert.Throws<ArmatureException>(() => pose.Evaluate());
    }

    [Fact]
    public void AConstraintOnACousinIsEvaluatedInTheRightOrder()
    {
        // Two roots, where the one added first reads the one added second.
        var pose = new Pose(new Armature(
        [
            new Bone("reader", Vector3.Zero(), new Vector3(0, 1, 0)),
            new Bone("source", new Vector3(5, 0, 0), new Vector3(5, 1, 0)),
        ]));
        pose["source"].Rotation = QuarterTurnAboutZ();
        pose.Constrain("reader", new CopyRotation("source"));
        pose.Evaluate();

        ExpectVector(pose.TailOf("reader"), new Vector3(-1, 0, 0));
    }

    // more naming

    [Fact]
    public void MirrorsAreRecognisedAsAPair()
    {
        Assert.True(BoneNaming.AreMirrors("hand.L", "hand.R"));
        Assert.False(BoneNaming.AreMirrors("hand.L", "foot.R"));
        Assert.False(BoneNaming.AreMirrors("spine", "spine"));
    }

    // a constraint driven by a property

    private static Pose TwoBones() => new(new Armature(
    [
        new Bone("driver", new Vector3(0, 0, 0), new Vector3(0, 1, 0)),
        new Bone("follower", new Vector3(1, 0, 0), new Vector3(1, 1, 0)),
    ]));

    [Fact]
    public void CopyRotationReadsThePropertyNotTheFixedInfluence()
    {
        // Declaring influenceProperty and then ignoring it is the worst kind of failure: the slider moves and
        // nothing happens.
        var pose = TwoBones();
        pose.SetProperty("blend", 0);
        pose.Constrain("follower", new CopyRotation("driver", influenceProperty: "blend"));

        pose["driver"].Rotation = QuarterTurnAboutZ();
        pose.Evaluate();
        Assert.Equal(1, pose.TailOf("follower").Y, 1e-6);

        pose.SetProperty("blend", 1);
        pose.Evaluate();
        Assert.Equal(0, pose.TailOf("follower").X, 1e-6);
    }

    [Fact]
    public void DampedTrackReadsThePropertyToo()
    {
        var pose = TwoBones();
        pose.SetProperty("look", 0);
        pose.Constrain("follower", new DampedTrack(target: "driver", influenceProperty: "look"));

        pose.Evaluate();
        Assert.Equal(1, pose.TailOf("follower").Y, 1e-6);

        pose.SetProperty("look", 1);
        pose.Evaluate();
        // Now aimed at the driver, which is a metre to its left.
        Assert.Equal(0, pose.TailOf("follower").X, 1e-6);
    }

    [Fact]
    public void AnInvertedInfluenceIsTheOtherHalfOfASwitch()
    {
        var pose = TwoBones();
        pose.SetProperty("blend", 1);
        pose.Constrain("follower", new CopyRotation("driver", influenceProperty: "blend", invertInfluence: true));

        pose["driver"].Rotation = QuarterTurnAboutZ();
        pose.Evaluate();
        // One minus one is nothing, so the follower stays put.
        Assert.Equal(1, pose.TailOf("follower").Y, 1e-6);
    }
}
