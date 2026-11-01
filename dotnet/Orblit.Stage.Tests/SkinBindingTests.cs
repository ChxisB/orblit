using Orblit.Filament;
using Orblit.Motion;
using Orblit.Rig;
using Orblit.VectorMath;

namespace Orblit.Stage.Tests;

/// <summary>
/// A small skeleton, turned every which way at rest, built node by node the way the renderer's tree has
/// it. Built from the nodes up rather than from the answers down, so a test knows where every joint is
/// without asking the code under test. The hips hang from a plain node that is offset and turned. The
/// neck hangs from the spine through a second plain node, which is the case where a joint's parent joint
/// is not its parent node. Every joint's frame is rotated away from the direction its bone will point.
/// The skin lists its joints children first, which is allowed.
/// </summary>
internal static class Figure
{
    public const int Neck = 0;
    public const int Hips = 1;
    public const int Tail = 2;
    public const int Spine = 3;

    public static readonly Matrix4 Holder = Placed(new Vector3(0.4, 0.9, -0.2), new Vector3(0, 0, 1), 0.35);
    public static readonly Matrix4 HipsLocal = Placed(new Vector3(0.05, 0.1, 0), new Vector3(1, 1, 0), 0.6);
    public static readonly Matrix4 SpineLocal = Placed(new Vector3(0, 0.3, 0.02), new Vector3(1, 0, 0.2), -0.5);
    public static readonly Matrix4 Socket = Placed(new Vector3(0.01, 0.25, 0), new Vector3(0, 1, 0), 0.8);
    public static readonly Matrix4 NeckLocal = Placed(new Vector3(0, 0.12, 0.03), new Vector3(0.3, 0, 1), 1.1);
    public static readonly Matrix4 TailLocal = Placed(new Vector3(0, -0.05, -0.25), new Vector3(1, 0, 0), 2.0);

    public static readonly Matrix4[] Locals = [NeckLocal, HipsLocal, TailLocal, SpineLocal];

    public static readonly SkinInfo Skin = new()
    {
        Name = "figure",
        Joints = ["neck", "hips", "tail", "spine"],
        Parents = [Spine, -1, Hips, Hips],
        Rest = WorldsFrom(Locals),
        Local = Locals,
    };

    public static Matrix4 Placed(Vector3 at, Vector3 axis, double angle) =>
        Matrix4.Compose(at, Quaternion.AxisAngle(axis.Normalized(), angle), Vector3.All(1));

    /// <summary>Every joint's world, from each joint's local, the way the renderer composes its nodes.</summary>
    public static Matrix4[] WorldsFrom(IReadOnlyList<Matrix4> locals)
    {
        var hipsWorld = Holder.Multiplied(locals[Hips]);
        var spineWorld = hipsWorld.Multiplied(locals[Spine]);
        return
        [
            spineWorld.Multiplied(Socket).Multiplied(locals[Neck]),
            hipsWorld,
            hipsWorld.Multiplied(locals[Tail]),
            spineWorld,
        ];
    }

    public static Vector3 HeadOf(int joint) => Skin.Rest[joint].GetTranslation();

    /// <summary>Where each joint ends up, as the renderer would put it, given what a binding returned.</summary>
    public static Matrix4[] WorldsOf(IReadOnlyList<JointPose> joints)
    {
        Assert.Equal([0, 1, 2, 3], joints.Select(joint => joint.Joint));
        return WorldsFrom([.. joints.Select(joint => joint.Transform)]);
    }

    /// <summary>
    /// The same four bones as the made armature has, with the same heads, pointed and twisted however an
    /// artist happened to leave them.
    /// </summary>
    public static Armature HandMade() => new(
    [
        new Bone("hips", HeadOf(Hips), HeadOf(Hips) + new Vector3(0, 0.2, 0), roll: 0.9),
        new Bone("spine", HeadOf(Spine), HeadOf(Spine) + new Vector3(0.2, 0.1, 0), roll: -1.3, parent: "hips"),
        new Bone("neck", HeadOf(Neck), HeadOf(Neck) + new Vector3(0, 0, 0.1), roll: 2.2, parent: "spine"),
        new Bone("tail", HeadOf(Tail), HeadOf(Tail) + new Vector3(0, -0.1, -0.1), roll: 0.4, parent: "hips"),
    ]);

    /// <summary>The made armature and the hand made one, which every rig test runs against.</summary>
    public static Armature[] Both() => [SkinRig.ArmatureOf(Skin), HandMade()];
}

public class SkinArmatureTests
{
    [Fact]
    public void HasABoneForEveryJointHungFromTheSameParents()
    {
        var armature = SkinRig.ArmatureOf(Figure.Skin);

        Assert.Equal(4, armature.Length);
        Assert.Null(armature["hips"]!.Parent);
        Assert.Equal("hips", armature["spine"]!.Parent);
        Assert.Equal("spine", armature["neck"]!.Parent);
        Assert.Equal("hips", armature["tail"]!.Parent);
        Assert.Equal(4, armature.DeformingBones.Count());
    }

    [Fact]
    public void PutsEachBonesHeadWhereItsJointRests()
    {
        var armature = SkinRig.ArmatureOf(Figure.Skin);

        for (var joint = 0; joint < 4; joint++)
        {
            var name = Figure.Skin.Joints[joint];
            Near.Vector(armature[name]!.Head, Figure.HeadOf(joint), name);
        }
    }

    [Fact]
    public void PointsABoneAtItsChildrenAndGivesEveryBoneALength()
    {
        var armature = SkinRig.ArmatureOf(Figure.Skin);

        Near.Vector(armature["spine"]!.Tail, Figure.HeadOf(Figure.Neck));
        Near.Vector(armature["hips"]!.Tail, (Figure.HeadOf(Figure.Spine) + Figure.HeadOf(Figure.Tail)) / 2);

        // The end of a chain carries on the way it came, by as far again.
        var neckHead = Figure.HeadOf(Figure.Neck);
        Near.Vector(armature["neck"]!.Tail, neckHead + (neckHead - Figure.HeadOf(Figure.Spine)));
        foreach (var bone in armature.Bones)
        {
            Assert.True(bone.Length > 1e-3, bone.Name);
        }
    }

    [Fact]
    public void RollsEachBoneSoItsXAxisLiesNearestItsJoints()
    {
        var armature = SkinRig.ArmatureOf(Figure.Skin);

        for (var joint = 0; joint < 4; joint++)
        {
            var bone = armature[Figure.Skin.Joints[joint]]!;
            var along = bone.Direction;
            var column = Figure.Skin.Rest[joint].GetColumn(0);
            var jointX = new Vector3(column.X, column.Y, column.Z);

            // The nearest a bone's X can come is the joint's X with the part along the bone taken out.
            var nearest = (jointX - (along * jointX.Dot(along))).Normalized();
            var boneX = bone.RestMatrix.GetColumn(0);

            Near.Vector(new Vector3(boneX.X, boneX.Y, boneX.Z), nearest, bone.Name);
        }
    }

    [Fact]
    public void WhereAJointsXRunsAlongItsBoneMatchesTheZAxesInstead()
    {
        var turned = Figure.Placed(Vector3.Zero(), new Vector3(1, 0, 0), 0.7);
        var skin = new SkinInfo
        {
            Name = "along",
            Joints = ["root", "end"],
            Parents = [-1, 0],
            Rest = [turned, Matrix4.TranslationValues(1, 0, 0)],
        };

        var bone = SkinRig.ArmatureOf(skin)["root"]!;
        var boneZ = bone.RestMatrix.GetColumn(2);
        var jointZ = turned.GetColumn(2);

        Near.Vector(bone.Direction, new Vector3(1, 0, 0));
        Near.Vector(new Vector3(boneZ.X, boneZ.Y, boneZ.Z), new Vector3(jointZ.X, jointZ.Y, jointZ.Z));
    }

    [Fact]
    public void AJointWithNoNameOrANameAlreadyTakenStillMakesABone()
    {
        var skin = new SkinInfo
        {
            Name = "careless",
            Joints = ["", "arm", "arm", "arm.001"],
            Parents = [-1, 0, 1, 2],
            Rest = [.. Enumerable.Range(0, 4).Select(joint => Matrix4.TranslationValues(0, joint * 0.3, 0))],
        };

        Assert.Equal(["joint 0", "arm", "arm.002", "arm.001"], SkinRig.BoneNamesOf(skin));
        var armature = SkinRig.ArmatureOf(skin);
        Assert.Equal(4, armature.Length);
        Assert.Equal("arm", armature["arm.002"]!.Parent);
        Assert.All(new SkinBinding(armature, skin, 0).Bones, Assert.NotNull);
    }

    [Fact]
    public void JointsThatAllStandInOnePlaceStillMakeBonesWithLengths()
    {
        var skin = new SkinInfo
        {
            Name = "heap",
            Joints = ["a", "b", "c"],
            Parents = [-1, 0, 0],
            Rest = [Matrix4.Identity(), Matrix4.Identity(), Matrix4.Identity()],
        };

        foreach (var bone in SkinRig.ArmatureOf(skin).Bones)
        {
            Assert.True(bone.Length > 0, bone.Name);
        }
    }
}

public class SkinBindingTests
{
    // Bound to the made armature without its tail bone, so one joint has no bone to follow.
    private static (Pose Pose, SkinBinding Binding) Bound()
    {
        var armature = SkinRig.ArmatureOf(Figure.Skin);
        armature.Remove("tail");
        return (new Pose(armature), new SkinBinding(armature, Figure.Skin, 2));
    }

    // Turns a bone by `turn` as seen from outside the rig, about its own head. A pose's rotation is in the
    // bone's own frame, so one turn in the world is a different pose rotation for two bones that rest
    // differently. This is what lets two armatures be given the same pose.
    private static void TurnInWorld(Pose pose, string bone, Quaternion turn)
    {
        pose.Evaluate();
        var basis = Quaternion.Identity();
        pose.BaseOf(bone).Decompose(Vector3.Zero(), basis, Vector3.Zero());
        pose[bone].Rotation = basis.Conjugated() * turn * basis;
    }

    [Fact]
    public void AtRestGivesEveryJointBackAsTheFileHasIt()
    {
        foreach (var armature in Figure.Both())
        {
            var pose = new Pose(armature);
            pose.Evaluate();
            var joints = new SkinBinding(armature, Figure.Skin, 2).JointsFor(pose);

            Assert.Equal(4, joints.Count);
            foreach (var joint in joints)
            {
                Assert.Equal(2, joint.Skin);
                Near.Matrix(joint.Transform, Figure.Skin.Local[joint.Joint], Figure.Skin.Joints[joint.Joint]);
            }
        }
    }

    [Fact]
    public void MovesEachPosedJointToWhereTheRigPutsItsBonesHead()
    {
        var (pose, binding) = Bound();
        pose["hips"].Location = new Vector3(0.1, -0.05, 0.2);
        pose["hips"].Rotation = Quaternion.AxisAngle(new Vector3(0, 0, 1), 0.7);
        pose["spine"].Rotation = Quaternion.AxisAngle(new Vector3(1, 0, 0.5).Normalized(), -0.9);
        pose.Evaluate();

        var worlds = Figure.WorldsOf(binding.JointsFor(pose));

        foreach (var (joint, bone) in new[] { (Figure.Hips, "hips"), (Figure.Spine, "spine"), (Figure.Neck, "neck") })
        {
            Near.Vector(worlds[joint].GetTranslation(), pose.HeadOf(bone), bone);

            // Carried as if welded: the joint's frame keeps its rest offset from its bone's.
            Near.Matrix(
                Matrix4.Inverted(pose.WorldOf(bone)).Multiplied(worlds[joint]),
                Matrix4.Inverted(pose.Armature.RestOf(bone)).Multiplied(Figure.Skin.Rest[joint]),
                bone);
        }

        // Far enough that the test cannot pass by nothing having moved.
        Assert.True((worlds[Figure.Neck].GetTranslation() - Figure.HeadOf(Figure.Neck)).Length > 0.1);
    }

    [Fact]
    public void CarriesAJointNoBoneIsNamedAfterRigidlyWithItsParent()
    {
        var (pose, binding) = Bound();
        Assert.Equal(["neck", "hips", null, "spine"], binding.Bones);

        pose["hips"].Rotation = Quaternion.AxisAngle(new Vector3(0, 1, 1), 1.2);
        pose.Evaluate();
        var joints = binding.JointsFor(pose);
        var worlds = Figure.WorldsOf(joints);

        Near.Matrix(joints[Figure.Tail].Transform, Figure.TailLocal);
        Near.Matrix(
            Matrix4.Inverted(worlds[Figure.Hips]).Multiplied(worlds[Figure.Tail]),
            Matrix4.Inverted(Figure.Skin.Rest[Figure.Hips]).Multiplied(Figure.Skin.Rest[Figure.Tail]));
        Assert.True((worlds[Figure.Tail].GetTranslation() - Figure.HeadOf(Figure.Tail)).Length > 0.05);
    }

    [Fact]
    public void AnArmatureMadeByHandTwistedDifferentlyGivesTheSameJointsForTheSameTurn()
    {
        var made = SkinRig.ArmatureOf(Figure.Skin);
        var byHand = Figure.HandMade();

        // Not the same armature under another name: every bone rests in a different frame.
        foreach (var bone in made.Bones)
        {
            var difference = Matrix4.Zero();
            for (var at = 0; at < 16; at++)
            {
                difference = SetAt(difference, at, made.RestOf(bone.Name)[at] - byHand.RestOf(bone.Name)[at]);
            }

            Assert.True(Near.Largest(difference) > 0.1, bone.Name);
        }

        var fromMade = Posed(made);
        var fromHand = Posed(byHand);
        var madeWorlds = Figure.WorldsOf(fromMade);
        var handWorlds = Figure.WorldsOf(fromHand);

        for (var joint = 0; joint < 4; joint++)
        {
            var name = Figure.Skin.Joints[joint];
            Near.Vector(handWorlds[joint].GetTranslation(), madeWorlds[joint].GetTranslation(), name);
            Near.Matrix(fromHand[joint].Transform, fromMade[joint].Transform, name);
        }

        Assert.True((madeWorlds[Figure.Neck].GetTranslation() - Figure.HeadOf(Figure.Neck)).Length > 0.1);
    }

    private static Matrix4 SetAt(Matrix4 matrix, int at, double value)
    {
        matrix[at] = value;
        return matrix;
    }

    private static List<JointPose> Posed(Armature armature)
    {
        var pose = new Pose(armature);
        TurnInWorld(pose, "hips", Quaternion.AxisAngle(new Vector3(0.2, 1, 0).Normalized(), 0.8));
        TurnInWorld(pose, "spine", Quaternion.AxisAngle(new Vector3(1, 0, 0.3).Normalized(), -0.6));
        pose.Evaluate();
        return [.. new SkinBinding(armature, Figure.Skin, 0).JointsFor(pose)];
    }

    [Fact]
    public void RefusesAPoseOfSomeOtherArmature()
    {
        var (pose, binding) = Bound();
        var other = new Pose(pose.Armature.Copy());
        other.Evaluate();

        Assert.Throws<ArgumentException>(() => binding.JointsFor(other));
    }

    [Fact]
    public void SaysAPoseHasNotBeenEvaluatedRatherThanThatABoneIsMissing()
    {
        var (pose, binding) = Bound();

        var error = Assert.Throws<ArmatureException>(() => binding.JointsFor(pose));

        Assert.Contains("Pose.Evaluate", error.Message);
    }
}

public class ClipOnASkinTests
{
    private static readonly Vector3 HipsAt = new(0.1, 0.3, -0.2);
    private static readonly Quaternion HipsTurn = Quaternion.AxisAngle(new Vector3(0, 1, 0), 0.9);
    private static readonly Quaternion SpineTurn = Quaternion.AxisAngle(new Vector3(1, 0, 0.4).Normalized(), -0.7);
    private static readonly Vector3 NeckScale = new(1.5, 0.5, 1);

    // A frame of a clip that moves the hips, turns the spine, grows the neck, by `neck` when it is given,
    // and leaves the tail alone.
    private static Dictionary<string, BoneLocal> Frame(Vector3? neck = null) => new()
    {
        ["hips"] = new BoneLocal(position: HipsAt, rotation: HipsTurn),
        ["spine"] = new BoneLocal(rotation: SpineTurn),
        ["neck"] = new BoneLocal(scale: neck ?? Vector3.All(1.3)),
    };

    private static SkinBinding BindingOf(Armature armature) => new(armature, Figure.Skin, 1);

    private static void ExpectRest(PoseTransform transform, string bone)
    {
        Near.Vector(transform.Location, Vector3.Zero(), bone);
        Assert.Equal(1, Math.Abs(transform.Rotation.W), 1e-6);
        Near.Vector(transform.Scale, Vector3.All(1), bone);
    }

    [Fact]
    public void KeysWhatItKeysAndTheRestIsWhereTheFileHasIt()
    {
        var joints = BindingOf(SkinRig.ArmatureOf(Figure.Skin)).JointsFrom(Frame(NeckScale));

        Assert.Equal(4, joints.Count);
        Assert.Equal([1], joints.Select(joint => joint.Skin).Distinct());
        Near.Matrix(
            joints[Figure.Hips].Transform,
            Matrix4.Compose(HipsAt, HipsTurn, Vector3.All(1)),
            "hips");

        // A turn alone keeps the spine where it hangs from the hips.
        Near.Matrix(
            joints[Figure.Spine].Transform,
            Matrix4.Compose(new Vector3(0, 0.3, 0.02), SpineTurn, Vector3.All(1)),
            "spine");
        Near.Matrix(
            joints[Figure.Neck].Transform,
            Matrix4.Compose(
                new Vector3(0, 0.12, 0.03),
                Quaternion.AxisAngle(new Vector3(0.3, 0, 1).Normalized(), 1.1),
                NeckScale),
            "neck");
        Near.Matrix(joints[Figure.Tail].Transform, Figure.TailLocal);
    }

    [Fact]
    public void ThroughARigPutsEveryJointWhereTheClipAloneWould()
    {
        foreach (var armature in Figure.Both())
        {
            var binding = BindingOf(armature);
            var pose = new Pose(armature);
            binding.PoseFrom(Frame(), pose);
            pose.Evaluate();

            var through = Figure.WorldsOf(binding.JointsFor(pose));
            var alone = Figure.WorldsFrom(binding.LocalsFrom(Frame()));
            for (var joint = 0; joint < 4; joint++)
            {
                Near.Matrix(through[joint], alone[joint], Figure.Skin.Joints[joint]);
            }

            // Far enough that the test cannot pass by nothing having moved.
            Assert.True((through[Figure.Neck].GetTranslation() - Figure.HeadOf(Figure.Neck)).Length > 0.1);
        }
    }

    [Fact]
    public void ThroughARigAStretchAlongOneAxisStillPutsEveryJointWhereTheClipDoes()
    {
        var stretched = Frame();
        stretched["spine"] = new BoneLocal(rotation: SpineTurn, scale: NeckScale);

        foreach (var armature in Figure.Both())
        {
            var binding = BindingOf(armature);
            var pose = new Pose(armature);
            binding.PoseFrom(stretched, pose);
            pose.Evaluate();

            var through = Figure.WorldsOf(binding.JointsFor(pose));
            var alone = Figure.WorldsFrom(binding.LocalsFrom(stretched));
            for (var joint = 0; joint < 4; joint++)
            {
                Near.Vector(through[joint].GetTranslation(), alone[joint].GetTranslation(), Figure.Skin.Joints[joint]);
            }
        }
    }

    [Fact]
    public void WithNothingKeyedLeavesEveryBoneAtRest()
    {
        foreach (var armature in Figure.Both())
        {
            var pose = new Pose(armature);
            pose["spine"].Rotation = SpineTurn;
            BindingOf(armature).PoseFrom(new Dictionary<string, BoneLocal>(), pose);

            foreach (var bone in armature.Bones)
            {
                ExpectRest(pose[bone.Name], bone.Name);
            }
        }
    }

    [Fact]
    public void PutsBackABoneItDoesNotKeyAndLeavesABoneThatDrivesNoJointToWhoeverMovesIt()
    {
        var armature = Figure.HandMade();
        armature.Add(new Bone(
            "look",
            Figure.HeadOf(Figure.Neck) + new Vector3(0, 0, 0.5),
            Figure.HeadOf(Figure.Neck) + new Vector3(0, 0, 0.6),
            parent: "spine",
            deform: false));
        var pose = new Pose(armature);
        pose["tail"].Rotation = Quaternion.AxisAngle(new Vector3(1, 0, 0), 0.5);
        pose["look"].Location = new Vector3(0, 0.2, 0);

        BindingOf(armature).PoseFrom(Frame(), pose);

        ExpectRest(pose["tail"], "tail");
        Near.Vector(pose["look"].Location, new Vector3(0, 0.2, 0));
        Assert.False(pose["hips"].IsRest);
    }

    [Fact]
    public void AJointScaledToNothingTakesWhatHangsFromItWithIt()
    {
        var shrunk = Frame();
        shrunk["spine"] = new BoneLocal(scale: Vector3.Zero());

        var alone = Figure.WorldsOf(BindingOf(SkinRig.ArmatureOf(Figure.Skin)).JointsFrom(shrunk));
        Near.Vector(alone[Figure.Neck].GetTranslation(), alone[Figure.Spine].GetTranslation());

        foreach (var armature in Figure.Both())
        {
            var binding = BindingOf(armature);
            var pose = new Pose(armature);
            binding.PoseFrom(shrunk, pose);
            pose.Evaluate();
            var joints = binding.JointsFor(pose);

            foreach (var joint in joints)
            {
                Assert.True(
                    Enumerable.Range(0, 16).All(at => double.IsFinite(joint.Transform[at])),
                    Figure.Skin.Joints[joint.Joint]);
            }

            var through = Figure.WorldsOf(joints);
            Near.Vector(through[Figure.Neck].GetTranslation(), through[Figure.Spine].GetTranslation());
            Near.Vector(through[Figure.Hips].GetTranslation(), alone[Figure.Hips].GetTranslation());
        }
    }

    [Fact]
    public void RefusesAPoseOfSomeOtherArmature()
    {
        var armature = SkinRig.ArmatureOf(Figure.Skin);

        Assert.Throws<ArgumentException>(() => BindingOf(armature).PoseFrom(Frame(), new Pose(armature.Copy())));
    }
}
