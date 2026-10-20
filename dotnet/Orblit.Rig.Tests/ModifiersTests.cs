namespace Orblit.Rig.Tests;

public class ModifiersTests
{
    /// <summary>A count of bones a metre long along +Y, each hung from the one before.</summary>
    private static Armature Chain(int count, string prefix = "b")
    {
        var bones = new List<Bone>();
        for (var i = 0; i < count; i++)
        {
            bones.Add(new Bone(
                $"{prefix}{i}",
                new Vector3(0, i, 0),
                new Vector3(0, i + 1.0, 0),
                parent: i == 0 ? null : $"{prefix}{i - 1}",
                connected: i > 0));
        }

        return new Armature(bones);
    }

    private static Pose Posed(Armature armature)
    {
        var pose = new Pose(armature);
        pose.Evaluate();
        return pose;
    }

    private static ModelPoint At(double x, double y, double z) => (_, _) => new Vector3(x, y, z);

    private static double AngleBetween(Vector3 a, Vector3 b) =>
        Math.Acos(Math.Clamp(a.Normalized().Dot(b.Normalized()), -1.0, 1.0));

    /// <summary>How far a point is from the nearest part of a line of straight pieces.</summary>
    private static double DistanceToPolyline(Vector3 point, List<Vector3> line)
    {
        var nearest = double.PositiveInfinity;
        for (var i = 0; i + 1 < line.Count; i++)
        {
            var along = line[i + 1] - line[i];
            var t = Math.Clamp((point - line[i]).Dot(along) / along.Length2, 0.0, 1.0);
            nearest = Math.Min(nearest, point.DistanceTo(line[i] + along.Scaled(t)));
        }

        return nearest;
    }

    /// <summary>A pose axis the way the rest of a bone sees it.</summary>
    private static Vector3 YAxisOf(Pose pose, string bone)
    {
        var column = pose.WorldOf(bone).GetColumn(1);
        return new Vector3(column.X, column.Y, column.Z).Normalized();
    }

    private static void Apply(Pose pose, Modifier modifier, double dt = 0, Matrix4? toWorld = null)
    {
        var stack = new ModifierStack();
        stack.Add("m", modifier);
        stack.Apply(pose, new ModifierFrame(dt: dt, modelToWorld: toWorld));
    }

    /// <summary>How far a rotation turns, in radians.</summary>
    private static double TurnAngleOf(Quaternion rotation) => 2 * Math.Acos(Math.Clamp(Math.Abs(rotation.W), 0.0, 1.0));

    // two-bone IK

    [Fact]
    public void PutsTheEndOfTheLimbOnATargetItCanReach()
    {
        var pose = Posed(Chain(2));
        Apply(pose, new TwoBoneIk("b0", "b1", At(1, 1, 0)));
        Assert.True(pose.TailOf("b1").DistanceTo(new Vector3(1, 1, 0)) < 1e-6);
    }

    [Fact]
    public void KeepsTheLengthOfBothBones()
    {
        var pose = Posed(Chain(2));
        Apply(pose, new TwoBoneIk("b0", "b1", At(1, 1, 0)));
        Assert.Equal(1, pose.HeadOf("b1").Length, 1e-6);
        Assert.Equal(1, pose.TailOf("b1").DistanceTo(pose.HeadOf("b1")), 1e-6);
    }

    [Fact]
    public void ReachesTheSamePlaceThePoseDoesWithAnIkChain()
    {
        var armature = new Armature(
        [
            .. Chain(2).Bones.Select(b => b.Copy()),
            new Bone("goal", new Vector3(1, 1, 0), new Vector3(1, 1.2, 0)),
            new Bone("pole", new Vector3(0, 1, 2), new Vector3(0, 1.2, 2)),
        ]);
        var inside = new Pose(armature);
        inside.AddIkChain(new IkChain("b0", "b1", "goal", pole: "pole"));
        inside.Evaluate();

        var outside = Posed(armature);
        Apply(outside, new TwoBoneIk("b0", "b1", At(1, 1, 0), pole: At(0, 1, 2)));

        Assert.True(outside.HeadOf("b1").DistanceTo(inside.HeadOf("b1")) < 1e-6);
        Assert.True(outside.TailOf("b1").DistanceTo(inside.TailOf("b1")) < 1e-6);
    }

    [Fact]
    public void IsTheSameWhenItRunsTwiceOnTheSamePose()
    {
        var pose = Posed(Chain(2));
        var modifier = new TwoBoneIk("b0", "b1", At(1, 1, 0));
        Apply(pose, modifier);
        var once = pose.TailOf("b1").Clone();
        Apply(pose, modifier);
        Assert.True(pose.TailOf("b1").DistanceTo(once) < 1e-9);
    }

    // The Dart suite names this test twice, once for the two-bone solver and once for chain IK.
    [Fact]
    public void TwoBoneStraightensTowardsATargetOutOfReach()
    {
        var pose = Posed(Chain(2));
        Apply(pose, new TwoBoneIk("b0", "b1", At(0, 0, 5)));
        var end = pose.TailOf("b1");
        Assert.Equal(2, end.Z, 1e-2);
        Assert.True(Math.Abs(end.Y) < 1e-2);
    }

    [Fact]
    public void StretchesPastFullReachWhenAllowed()
    {
        var pose = Posed(Chain(2));
        Apply(pose, new TwoBoneIk("b0", "b1", At(0, 0, 3), stretch: 1));
        Assert.True(pose.TailOf("b1").DistanceTo(new Vector3(0, 0, 3)) < 1e-6);
    }

    [Fact]
    public void DropsAStretchOnceTheTargetIsBackInReach()
    {
        var pose = Posed(Chain(2));
        var goal = new Vector3(0, 0, 3);
        var modifier = new TwoBoneIk("b0", "b1", (_, _) => goal, stretch: 1);
        Apply(pose, modifier);
        Assert.True(pose["b0"].Scale.Y > 1.2);

        goal = new Vector3(0, 0, 1);
        Apply(pose, modifier);
        Assert.Equal(1, pose["b0"].Scale.Y, 1e-9);
    }

    [Fact]
    public void ReachesForAPointGivenInTheWorld()
    {
        var pose = Posed(Chain(2));
        var lifted = Matrix4.TranslationValues(10, 0, 0);
        Apply(
            pose,
            new TwoBoneIk("b0", "b1", (_, frame) => frame.ToModel(new Vector3(11, 1, 0))),
            toWorld: lifted);
        Assert.True(pose.TailOf("b1").DistanceTo(new Vector3(1, 1, 0)) < 1e-6);
    }

    [Fact]
    public void LeavesTheLimbAloneWithNoTarget()
    {
        var pose = Posed(Chain(2));
        Apply(pose, new TwoBoneIk("b0", "b1", (_, _) => null));
        Assert.True(pose["b0"].IsRest);
    }

    [Fact]
    public void RefusesTwoBonesThatAreNotOneLimb()
    {
        var pose = Posed(Chain(3));
        Assert.Throws<ArmatureException>(() => Apply(pose, new TwoBoneIk("b0", "b2", At(1, 1, 0))));
    }

    // look at

    [Fact]
    public void TurnsABoneToFaceATargetInsideTheLimit()
    {
        var pose = Posed(Chain(1));
        Apply(pose, new LookAt("b0", At(1, 0, 0), limit: Math.PI));
        var facing = YAxisOf(pose, "b0");
        Assert.True(AngleBetween(facing, new Vector3(1, 0, 0)) < 1e-6);
    }

    [Fact]
    public void StopsAtTheLimitAndNoFurther()
    {
        var pose = Posed(Chain(1));
        Apply(pose, new LookAt("b0", At(1, 0, 0), limit: 0.5));
        var facing = YAxisOf(pose, "b0");
        Assert.Equal(0.5, AngleBetween(facing, new Vector3(0, 1, 0)), 1e-6);
        // It turned the right way: towards the target and not away from it.
        Assert.True(facing.X > 0);
    }

    [Fact]
    public void LooksAsFarRoundAsTheLimitGoesForATargetBehind()
    {
        var pose = Posed(Chain(1));
        Apply(pose, new LookAt("b0", At(0, -5, 0.001), limit: 1));
        Assert.Equal(1, AngleBetween(YAxisOf(pose, "b0"), new Vector3(0, 1, 0)), 1e-6);
    }

    [Fact]
    public void AddsToATurnTheAnimationAlreadyMade()
    {
        var pose = Posed(Chain(1));
        pose["b0"].Rotation = Quaternion.AxisAngle(new Vector3(0, 0, 1), -0.3);
        pose.Evaluate();
        Apply(pose, new LookAt("b0", At(0, 5, 0), limit: Math.PI));
        Assert.True(AngleBetween(YAxisOf(pose, "b0"), new Vector3(0, 1, 0)) < 1e-6);
    }

    [Fact]
    public void EasesTowardsTheTargetByTheTimeThatPassed()
    {
        var pose = Posed(Chain(1));
        var look = new LookAt("b0", At(1, 0, 0), limit: Math.PI, ease: 0.1);
        var stack = new ModifierStack();
        stack.Add("look", look);
        stack.Apply(pose, new ModifierFrame(dt: 0.1));
        var turned = AngleBetween(YAxisOf(pose, "b0"), new Vector3(0, 1, 0));
        // After one time constant it has closed 1 - 1/e of the quarter turn.
        Assert.Equal(Math.PI / 2 * (1 - Math.Exp(-1)), turned, 1e-6);
    }

    [Fact]
    public void DoesNotMoveInAFrameOfNoTimeWhileEasing()
    {
        var pose = Posed(Chain(1));
        Apply(pose, new LookAt("b0", At(1, 0, 0), limit: Math.PI, ease: 0.2));
        Assert.True(pose["b0"].IsRest);
    }

    [Fact]
    public void LetsGoOfATargetThatGoesAway()
    {
        var pose = Posed(Chain(1));
        Vector3? goal = new Vector3(1, 0, 0);
        var stack = new ModifierStack();
        stack.Add("look", new LookAt("b0", (_, _) => goal, limit: Math.PI));
        stack.Apply(pose, new ModifierFrame(dt: 0.1));
        goal = null;
        pose.Reset();
        pose.Evaluate();
        stack.Apply(pose, new ModifierFrame(dt: 0.1));
        Assert.True(pose["b0"].IsRest);
    }

    [Fact]
    public void DoesNotSpinTheBoneAboutItsOwnLength()
    {
        var pose = Posed(Chain(1));
        Apply(pose, new LookAt("b0", At(1, 1, 0), limit: Math.PI));
        var column = pose.WorldOf("b0").GetColumn(0);
        var side = new Vector3(column.X, column.Y, column.Z).Normalized();
        // A rest bone's X axis is along +X. Turning about Z keeps it in the XY plane, so it has no component along Z.
        Assert.True(Math.Abs(side.Z) < 1e-9);
    }

    [Fact]
    public void RefusesALimitThatIsNotAnAngle()
    {
        Assert.ThrowsAny<ArgumentException>(() => new LookAt("b0", At(0, 0, 0), limit: -1));
    }

    // constrain

    [Fact]
    public void RunsAConstraintAfterTheModifiersAboveIt()
    {
        var armature = new Armature(
        [
            .. Chain(1).Bones.Select(b => b.Copy()),
            new Bone("goal", new Vector3(3, 0, 0), new Vector3(3, 1, 0)),
        ]);
        var pose = Posed(armature);
        Apply(pose, new Constrain("b0", new DampedTrack(target: "goal")));
        var toGoal = pose.HeadOf("goal") - pose.HeadOf("b0");
        Assert.True(AngleBetween(YAxisOf(pose, "b0"), toGoal) < 1e-6);
    }

    [Fact]
    public void RefusesAConstraintThatHasItsOwnInfluence()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Constrain("b0", new DampedTrack(target: "goal", influence: 0.5)));
    }

    // chain IK

    [Fact]
    public void LandsTheTipOnATargetItCanReach()
    {
        var pose = Posed(Chain(4));
        Apply(pose, new ChainIk("b0", "b3", At(1.5, 2, 1)));
        Assert.True(pose.TailOf("b3").DistanceTo(new Vector3(1.5, 2, 1)) < 1e-2);
    }

    [Fact]
    public void KeepsTheRootWhereItWas()
    {
        var pose = Posed(Chain(4));
        Apply(pose, new ChainIk("b0", "b3", At(1.5, 2, 1)));
        Assert.True(pose.HeadOf("b0").Length < 1e-9);
    }

    [Fact]
    public void KeepsTheLengthOfEveryBone()
    {
        var pose = Posed(Chain(4));
        Apply(pose, new ChainIk("b0", "b3", At(1.5, 2, 1)));
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(1, pose.TailOf($"b{i}").DistanceTo(pose.HeadOf($"b{i}")), 1e-6);
        }
    }

    [Fact]
    public void KeepsTheChainJoinedEndToEnd()
    {
        var pose = Posed(Chain(4));
        Apply(pose, new ChainIk("b0", "b3", At(1.5, 2, 1)));
        for (var i = 0; i < 3; i++)
        {
            Assert.True(pose.TailOf($"b{i}").DistanceTo(pose.HeadOf($"b{i + 1}")) < 1e-6);
        }
    }

    [Fact]
    public void ChainStraightensTowardsATargetOutOfReach()
    {
        var pose = Posed(Chain(4));
        Apply(pose, new ChainIk("b0", "b3", At(10, 0, 0)));
        var tip = pose.TailOf("b3");
        Assert.Equal(4, tip.X, 1e-6);
        Assert.True(Math.Abs(tip.Y) < 1e-6);
    }

    [Fact]
    public void RefusesATipThatIsNotBelowTheRoot()
    {
        var armature = new Armature(
        [
            .. Chain(2, prefix: "a").Bones.Select(b => b.Copy()),
            .. Chain(2, prefix: "c").Bones.Select(b => b.Copy()),
        ]);
        var pose = Posed(armature);
        Assert.Throws<ArmatureException>(() => Apply(pose, new ChainIk("a0", "c1", At(0, 0, 0))));
    }

    // fabrik

    [Fact]
    public void KeepsEveryGapAndTheFirstPoint()
    {
        var points = Enumerable.Range(0, 5).Select(i => new Vector3(0, i * 0.5, 0)).ToList();
        var solved = Ik.SolveFabrik(points: points, target: new Vector3(1, 1, 0));
        Assert.Equal(Vector3.Zero(), solved[0]);
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(0.5, solved[i].DistanceTo(solved[i + 1]), 1e-9);
        }

        Assert.True(solved[^1].DistanceTo(new Vector3(1, 1, 0)) < 1e-3);
    }

    [Fact]
    public void LeavesTheInputAlone()
    {
        var points = new List<Vector3> { Vector3.Zero(), new(0, 1, 0) };
        Ik.SolveFabrik(points: points, target: new Vector3(1, 0, 0));
        Assert.Equal(new Vector3(0, 1, 0), points[^1]);
    }

    [Fact]
    public void HasNothingToDoWithOnePoint()
    {
        var solved = Ik.SolveFabrik(points: [new Vector3(1, 2, 3)], target: Vector3.Zero());
        Assert.Equal(new Vector3(1, 2, 3), Assert.Single(solved));
    }

    // spline IK

    private static List<Vector3> Controls() =>
    [
        new Vector3(0, 0, 0),
        new Vector3(2, 1, 0),
        new Vector3(4, 0, 0),
        new Vector3(6, 1, 0),
    ];

    [Fact]
    public void PutsTheRootOnTheStartOfTheCurve()
    {
        var controls = Controls();
        var pose = Posed(Chain(3));
        Apply(pose, new SplineIk("b0", "b2", (_, _) => controls));
        Assert.True(pose.HeadOf("b0").DistanceTo(controls[0]) < 1e-9);
    }

    [Fact]
    public void KeepsEveryBoneItsOwnLength()
    {
        var controls = Controls();
        var pose = Posed(Chain(3));
        Apply(pose, new SplineIk("b0", "b2", (_, _) => controls));
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(1, pose.TailOf($"b{i}").DistanceTo(pose.HeadOf($"b{i}")), 1e-6);
        }
    }

    [Fact]
    public void LaysTheJointsOnTheCurve()
    {
        var controls = Controls();
        var pose = Posed(Chain(3));
        Apply(pose, new SplineIk("b0", "b2", (_, _) => controls));
        var curve = SplineIk.SampleCurve(controls, 12);
        var joints = new[] { pose.HeadOf("b1"), pose.HeadOf("b2"), pose.TailOf("b2") };
        foreach (var joint in joints)
        {
            Assert.True(DistanceToPolyline(joint, curve) < 1e-6);
        }
    }

    [Fact]
    public void GoesOnStraightWhereTheCurveRunsOut()
    {
        var pose = Posed(Chain(8));
        var shortCurve = new List<Vector3> { new(0, 0, 0), new(1, 0, 0), new(2, 0, 0) };
        Apply(pose, new SplineIk("b0", "b7", (_, _) => shortCurve));
        Assert.Equal(8, pose.TailOf("b7").X, 1e-3);
        Assert.True(Math.Abs(pose.TailOf("b7").Y) < 1e-3);
    }

    [Fact]
    public void PassesThroughItsControlPoints()
    {
        var controls = Controls();
        var curve = SplineIk.SampleCurve(controls, 12);
        foreach (var point in controls)
        {
            Assert.True(curve.Min(p => p.DistanceTo(point)) < 1e-9);
        }
    }

    [Fact]
    public void LeavesTheChainAloneWithFewerThanTwoPoints()
    {
        var pose = Posed(Chain(3));
        Apply(pose, new SplineIk("b0", "b2", (_, _) => new[] { Vector3.Zero() }));
        Assert.True(pose["b0"].IsRest);
    }

    // cone limit

    [Fact]
    public void LeavesABoneInsideTheConeAlone()
    {
        var pose = Posed(Chain(2));
        pose["b0"].Rotation = Quaternion.AxisAngle(new Vector3(1, 0, 0), 0.2);
        Apply(pose, new ConeLimit("b0", swing: 0.5));
        Assert.Equal(0.2, TurnAngleOf(pose["b0"].Rotation), 1e-9);
    }

    [Fact]
    public void HoldsASwingToTheLimit()
    {
        var pose = Posed(Chain(2));
        pose["b0"].Rotation = Quaternion.AxisAngle(new Vector3(1, 0, 0), 1.2);
        Apply(pose, new ConeLimit("b0", swing: 0.5));
        Assert.Equal(0.5, TurnAngleOf(pose["b0"].Rotation), 1e-6);
    }

    [Fact]
    public void HoldsTheSwingInTheDirectionItWasGoing()
    {
        var pose = Posed(Chain(2));
        pose["b0"].Rotation = Quaternion.AxisAngle(new Vector3(1, 0, 0), 1.2);
        Apply(pose, new ConeLimit("b0", swing: 0.5));
        var facing = YAxisOf(pose, "b0");
        // A turn about +X by a positive angle swings +Y towards +Z.
        Assert.Equal(Math.Sin(0.5), facing.Z, 1e-6);
    }

    [Fact]
    public void HoldsARollToTheLimit()
    {
        var pose = Posed(Chain(2));
        pose["b0"].Rotation = Quaternion.AxisAngle(new Vector3(0, 1, 0), 1.5);
        Apply(pose, new ConeLimit("b0", swing: 1, twist: 0.4));
        Assert.Equal(0.4, TurnAngleOf(pose["b0"].Rotation), 1e-6);
    }

    [Fact]
    public void MovesWhatHangsBelowIt()
    {
        var pose = Posed(Chain(2));
        pose["b0"].Rotation = Quaternion.AxisAngle(new Vector3(1, 0, 0), 1.2);
        pose.Evaluate();
        Apply(pose, new ConeLimit("b0", swing: 0.5));
        Assert.Equal(Math.Sin(0.5), pose.HeadOf("b1").Z, 1e-6);
    }

    [Fact]
    public void DoesNotClampARestPoseThatIsNotAxisAligned()
    {
        var armature = new Armature([new Bone("a", Vector3.Zero(), new Vector3(1, 1, 0))]);
        var pose = Posed(armature);
        Apply(pose, new ConeLimit("a", swing: 0.1));
        Assert.True(pose["a"].IsRest);
    }

    [Fact]
    public void RefusesASwingPastAHalfTurn()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ConeLimit("a", swing: 4));
    }

    // hinge limit

    [Fact]
    public void KeepsABendAboutTheAxis()
    {
        var pose = Posed(Chain(2));
        pose["b1"].Rotation = Quaternion.AxisAngle(new Vector3(1, 0, 0), 0.8);
        Apply(pose, new HingeLimit("b1", min: -0.1, max: 1.5));
        Assert.Equal(0.8, TurnAngleOf(pose["b1"].Rotation), 1e-9);
    }

    [Fact]
    public void HoldsABendToTheEndsOfItsRange()
    {
        var pose = Posed(Chain(2));
        pose["b1"].Rotation = Quaternion.AxisAngle(new Vector3(1, 0, 0), 2.5);
        Apply(pose, new HingeLimit("b1", min: 0, max: 1.5));
        Assert.Equal(1.5, TurnAngleOf(pose["b1"].Rotation), 1e-9);
    }

    [Fact]
    public void WillNotBendTheWrongWay()
    {
        var pose = Posed(Chain(2));
        pose["b1"].Rotation = Quaternion.AxisAngle(new Vector3(1, 0, 0), -0.7);
        Apply(pose, new HingeLimit("b1", min: 0, max: 1.5));
        Assert.True(pose["b1"].IsRest);
    }

    [Fact]
    public void DropsATurnOffTheAxis()
    {
        var pose = Posed(Chain(2));
        pose["b1"].Rotation = Quaternion.AxisAngle(new Vector3(0, 0, 1), 0.9);
        Apply(pose, new HingeLimit("b1", min: -1, max: 1));
        Assert.Equal(0, TurnAngleOf(pose["b1"].Rotation), 1e-6);
    }

    [Fact]
    public void RefusesARangeThatRunsBackwards()
    {
        Assert.ThrowsAny<ArgumentException>(() => new HingeLimit("a", min: 1, max: 0));
    }

    // twist spread

    private static Armature Forearm() => new(
    [
        new Bone("fore", Vector3.Zero(), new Vector3(0, 3, 0)),
        new Bone("twist0", new Vector3(0, 1, 0), new Vector3(0, 2, 0), parent: "fore"),
        new Bone("twist1", new Vector3(0, 2, 0), new Vector3(0, 3, 0), parent: "fore"),
        new Bone("hand", new Vector3(0, 3, 0), new Vector3(0, 4, 0), parent: "fore"),
    ]);

    [Fact]
    public void SharesARollOutEvenlyByDefault()
    {
        var pose = Posed(Forearm());
        pose["hand"].Rotation = Quaternion.AxisAngle(new Vector3(0, 1, 0), 1.2);
        Apply(pose, new TwistSpread("hand", ["twist0", "twist1"]));
        Assert.Equal(0.6, TurnAngleOf(pose["twist0"].Rotation), 1e-9);
        Assert.Equal(1.2, TurnAngleOf(pose["twist1"].Rotation), 1e-9);
    }

    [Fact]
    public void TakesTheSharesItIsGiven()
    {
        var pose = Posed(Forearm());
        pose["hand"].Rotation = Quaternion.AxisAngle(new Vector3(0, 1, 0), 1.0);
        Apply(pose, new TwistSpread("hand", ["twist0", "twist1"], shares: [0.25, 0.75]));
        Assert.Equal(0.25, TurnAngleOf(pose["twist0"].Rotation), 1e-9);
        Assert.Equal(0.75, TurnAngleOf(pose["twist1"].Rotation), 1e-9);
    }

    [Fact]
    public void IgnoresABendThatIsNotARoll()
    {
        var pose = Posed(Forearm());
        pose["hand"].Rotation = Quaternion.AxisAngle(new Vector3(1, 0, 0), 1.0);
        Apply(pose, new TwistSpread("hand", ["twist0"]));
        Assert.Equal(0, TurnAngleOf(pose["twist0"].Rotation), 1e-9);
    }

    [Fact]
    public void FollowsARollTheOtherWay()
    {
        var pose = Posed(Forearm());
        pose["hand"].Rotation = Quaternion.AxisAngle(new Vector3(0, 1, 0), -0.8);
        Apply(pose, new TwistSpread("hand", ["twist0"]));
        Assert.True(pose["twist0"].Rotation.Y < 0);
    }

    [Fact]
    public void RefusesAShareForEachBoneThatIsNotThere()
    {
        Assert.ThrowsAny<ArgumentException>(() => new TwistSpread("hand", ["twist0", "twist1"], shares: [1]));
    }

    // speed limit

    [Fact]
    public void LetsTheFirstFrameThrough()
    {
        var pose = Posed(Chain(1));
        pose["b0"].Rotation = Quaternion.AxisAngle(new Vector3(0, 0, 1), 1.5);
        Apply(pose, new SpeedLimit(["b0"], turnSpeed: 1), dt: 0.1);
        Assert.Equal(1.5, TurnAngleOf(pose["b0"].Rotation), 1e-9);
    }

    [Fact]
    public void LimitsATurnToTheSpeedTimesTheTime()
    {
        var pose = Posed(Chain(1));
        var stack = new ModifierStack();
        stack.Add("slow", new SpeedLimit(["b0"], turnSpeed: 1));
        stack.Apply(pose, new ModifierFrame(dt: 0.1));

        pose["b0"].Rotation = Quaternion.AxisAngle(new Vector3(0, 0, 1), 1.5);
        stack.Apply(pose, new ModifierFrame(dt: 0.1));
        Assert.Equal(0.1, TurnAngleOf(pose["b0"].Rotation), 1e-9);
    }

    [Fact]
    public void GetsThereInTime()
    {
        var pose = Posed(Chain(1));
        var stack = new ModifierStack();
        stack.Add("slow", new SpeedLimit(["b0"], turnSpeed: 1));
        stack.Apply(pose, new ModifierFrame(dt: 0.1));
        for (var i = 0; i < 20; i++)
        {
            pose["b0"].Rotation = Quaternion.AxisAngle(new Vector3(0, 0, 1), 1.0);
            stack.Apply(pose, new ModifierFrame(dt: 0.1));
        }

        Assert.Equal(1.0, TurnAngleOf(pose["b0"].Rotation), 1e-9);
    }

    [Fact]
    public void LimitsMovementAsWellAsTurning()
    {
        var pose = Posed(Chain(1));
        var stack = new ModifierStack();
        stack.Add("slow", new SpeedLimit(["b0"], moveSpeed: 2));
        stack.Apply(pose, new ModifierFrame(dt: 0.5));

        pose["b0"].Location.SetValues(10, 0, 0);
        stack.Apply(pose, new ModifierFrame(dt: 0.5));
        Assert.Equal(1, pose["b0"].Location.X, 1e-9);
    }

    [Fact]
    public void HoldsABoneStillInAFrameOfNoTime()
    {
        var pose = Posed(Chain(1));
        var stack = new ModifierStack();
        stack.Add("slow", new SpeedLimit(["b0"], turnSpeed: 1));
        stack.Apply(pose, new ModifierFrame(dt: 0.1));
        pose["b0"].Rotation = Quaternion.AxisAngle(new Vector3(0, 0, 1), 1.5);
        stack.Apply(pose);
        Assert.True(pose["b0"].IsRest);
    }

    [Fact]
    public void StartsAgainAfterAReset()
    {
        var pose = Posed(Chain(1));
        var stack = new ModifierStack();
        stack.Add("slow", new SpeedLimit(["b0"], turnSpeed: 1));
        stack.Apply(pose, new ModifierFrame(dt: 0.1));
        stack.Reset();
        pose["b0"].Rotation = Quaternion.AxisAngle(new Vector3(0, 0, 1), 1.5);
        stack.Apply(pose, new ModifierFrame(dt: 0.1));
        Assert.Equal(1.5, TurnAngleOf(pose["b0"].Rotation), 1e-9);
    }

    // bone attachment

    [Fact]
    public void ReportsNothingBeforeTheFirstFrame()
    {
        Assert.Null(new BoneAttachment("b0").World);
    }

    [Fact]
    public void FollowsTheBoneInTheModelAndInTheWorld()
    {
        var pose = Posed(Chain(2));
        pose["b0"].Location.SetValues(0, 0, 2);
        pose.Evaluate();
        var socket = new BoneAttachment("b1", Matrix4.TranslationValues(0, 1, 0));
        Apply(pose, socket, toWorld: Matrix4.TranslationValues(10, 0, 0));
        Assert.Equal(new Vector3(0, 2, 2), socket.Model!.GetTranslation());
        Assert.Equal(new Vector3(10, 2, 2), socket.World!.GetTranslation());
    }

    [Fact]
    public void DoesNotChangeThePose()
    {
        var pose = Posed(Chain(2));
        Apply(pose, new BoneAttachment("b1"));
        Assert.True(pose["b1"].IsRest);
    }

    [Fact]
    public void HandsOutACopy()
    {
        var pose = Posed(Chain(2));
        var socket = new BoneAttachment("b1");
        Apply(pose, socket);
        socket.World!.SetTranslation(new Vector3(9, 9, 9));
        Assert.Equal(new Vector3(0, 1, 0), socket.World!.GetTranslation());
    }
}
