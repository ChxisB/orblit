using Orblit.Filament;
using Orblit.Motion;
using Orblit.Rig;
using Orblit.VectorMath;

namespace Orblit.Stage;

/// <summary>An armature driving a model's skin.</summary>
/// <remarks>
/// This sits between a rig solved in code and a character drawn from a file. A <see cref="Pose"/> says
/// where bones are, and the renderer wants to know where joints are, each relative to whatever it hangs
/// from. This turns one into the other.
///
/// Bones find joints by name, and nothing else about the armature has to match the file. A bone may point
/// another way from its joint, twist another way, or sit somewhere else. What is kept from each pair is
/// the offset between the two at rest, and the joint is carried by its bone as if welded to it at that
/// offset. So an armature from <see cref="SkinRig.ArmatureOf"/> works, and so does one an artist built by
/// hand. The one thing that has to match is the armature's space, which must be the model's own: every
/// rest is compared in it, and a rig standing a metre to one side would turn the skin about points a
/// metre away from its joints.
///
/// The offsets are measured when the binding is made. An armature whose rest is edited afterwards needs a
/// new binding, in the same way a mesh has to be bound again when its skeleton changes.
/// </remarks>
public sealed class SkinBinding
{
    private readonly IReadOnlyList<string> _names;
    private readonly IReadOnlyList<int> _parents;
    private readonly IReadOnlyList<int> _order;
    private readonly IReadOnlyList<Matrix4> _rest;
    private readonly IReadOnlyList<Matrix4> _local;

    // Where each joint sits in its bone's frame: inverse(bone rest) * joint rest, or null for a joint
    // with no bone.
    private readonly Matrix4?[] _offsets;

    // Where each joint rests in its parent joint's frame, or in the model's for a joint that hangs from no
    // joint. This is what carries a joint no bone drives.
    private readonly Matrix4[] _restFromParent;

    // What takes a world already brought into the parent joint's frame the rest of the way, into the
    // joint's own parent node's frame. That node is not always the parent joint. The renderer names a
    // joint's nearest ancestor that is a joint, and a file may put plain nodes between the two. Those
    // nodes are nobody's to move, so they ride with the joint above them, and this is the constant step
    // from it to them. For a joint that hangs from no joint, the parent node stays where the file put it,
    // and this is the whole way there from the model's root.
    private readonly Matrix4[] _nodeFromParent;

    // The rest of it is worked out only for a binding a clip drives, which most are not yet.
    private readonly Lazy<Matrix4[]> _parentFromNode;
    private readonly Lazy<(Vector3 Position, Quaternion Rotation, Vector3 Scale)[]> _restParts;
    private readonly Lazy<Matrix4?[]> _boneFromJoint;
    private readonly Lazy<Dictionary<string, int>> _jointOfBone;

    /// <param name="armature">The rig whose bones carry the skin's joints.</param>
    /// <param name="skin">The skeleton the model has.</param>
    /// <param name="index">Which skin this is, by its position in <see cref="AssetInfo.Skins"/>.</param>
    public SkinBinding(Armature armature, SkinInfo skin, int index)
        : this(armature, skin, index, new SkinShape(skin))
    {
    }

    private SkinBinding(Armature armature, SkinInfo skin, int index, SkinShape shape)
    {
        Armature = armature;
        Skin = skin;
        Index = index;
        Bones = [.. shape.Names.Select(name => armature.Contains(name) ? name : null)];
        _names = shape.Names;
        _parents = shape.Parents;
        _order = shape.Order;
        _rest = shape.Rest;
        _local = shape.Local;
        _offsets = OffsetsOf(armature, shape);
        _restFromParent = [.. Enumerable.Range(0, shape.Names.Count).Select(joint => RestFromParentOf(shape, joint))];
        _nodeFromParent = [.. Enumerable.Range(0, shape.Names.Count).Select(joint => NodeFromParentOf(shape, joint))];
        _parentFromNode = new Lazy<Matrix4[]>(() => [.. _nodeFromParent.Select(Matrix4.Inverted)]);
        _restParts = new Lazy<(Vector3, Quaternion, Vector3)[]>(() => [.. _local.Select(PartsOf)]);
        _boneFromJoint = new Lazy<Matrix4?[]>(() => [.. _offsets.Select(offset => offset is null ? null : Matrix4.Inverted(offset))]);
        _jointOfBone = new Lazy<Dictionary<string, int>>(JointsByBone);
    }

    public Armature Armature { get; }

    public SkinInfo Skin { get; }

    /// <summary>
    /// Which skin this is, by its position in <see cref="AssetInfo.Skins"/>. This is the number every
    /// <see cref="JointPose"/> from <see cref="JointsFor"/> carries.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// The bone each joint follows, in the skin's order, or null for a joint no bone is named after.
    /// Worth a look when an armature made by hand is first bound. A joint that no bone drives looks
    /// exactly like a joint that is meant to keep still, and a bone renamed on one side and not the other
    /// is otherwise found by watching an arm fail to move.
    /// </summary>
    public IReadOnlyList<string?> Bones { get; }

    /// <summary>
    /// Each joint's transform relative to whatever it hangs from, where <paramref name="locals"/> puts it.
    /// </summary>
    /// <remarks>
    /// <paramref name="locals"/> is a clip's bones at one moment, by name, the way a sampled clip frame
    /// keeps them for the model it plays on. A clip keys a joint the way the file does, relative to the
    /// node above it, so these are the clip's own numbers. Whatever the clip leaves out, a part or a whole
    /// joint, is the rest's: a clip that only turns an arm keeps the arm's length.
    /// </remarks>
    public List<Matrix4> LocalsFrom(IReadOnlyDictionary<string, BoneLocal> locals) =>
    [
        .. Enumerable.Range(0, _names.Count).Select(joint => LocalFrom(joint, locals.GetValueOrDefault(_names[joint]))),
    ];

    /// <summary>
    /// <paramref name="locals"/> straight to the renderer, with no rig in between. For a model that only
    /// plays clips. One whose rig has something to add, a foot put on the ground or a head turned toward a
    /// sound, goes through <see cref="PoseFrom"/> instead. Every joint is returned, for the reason
    /// <see cref="JointsFor"/> gives.
    /// </summary>
    public List<JointPose> JointsFrom(IReadOnlyDictionary<string, BoneLocal> locals) =>
        [.. LocalsFrom(locals).Select((transform, joint) => new JointPose(Index, joint, transform))];

    /// <summary>
    /// Sets <paramref name="pose"/> so that, once evaluated, its bones carry the joints to where
    /// <paramref name="locals"/> puts them.
    /// </summary>
    /// <remarks>
    /// How a clip reaches a rig: this, then <see cref="Pose.Evaluate"/>, then <see cref="JointsFor"/>.
    /// Evaluating is where the rig has its say, so a constraint or a limb reaching for a target starts
    /// from the clip's pose rather than from rest, the way an animator's rig works on the keys underneath
    /// it. With nothing in the rig to say anything, the joints come out as <see cref="JointsFrom"/> gives
    /// them.
    ///
    /// With one exception: a joint stretched further along one of its own axes than another. A bone's
    /// axes are its own, not its joint's, so that stretch is a shear to the bone, and a pose has no shear
    /// to give it. The joint, and every joint that hangs from it, still ends up where the clip puts it,
    /// turned as near as a bone can carry it. A model whose clips squash and stretch that way plays them
    /// through <see cref="JointsFrom"/>.
    ///
    /// Every bone that drives a joint is written, including one the clip does not key, which goes back to
    /// rest: a bone left as the last clip had it would hold that clip's pose under this one. A bone that
    /// drives no joint, a control or a target, is left as it is, for whoever moves it.
    /// </remarks>
    public void PoseFrom(IReadOnlyDictionary<string, BoneLocal> locals, Pose pose)
    {
        CheckArmatureOf(pose);
        var joints = JointWorldsFrom(LocalsFrom(locals));

        // Each bone, parents first, where it has to be to carry its joint, and the pose that puts it
        // there from wherever its parent ended up.
        var worlds = new Dictionary<string, Matrix4>();
        foreach (var name in Armature.EvaluationOrder)
        {
            var parent = Armature[name]?.Parent;
            var rest = Armature.RestLocalOf(name);
            var basis = parent is not null && worlds.TryGetValue(parent, out var above) ? above.Multiplied(rest) : rest;
            var transform = pose[name];

            if (_jointOfBone.Value.TryGetValue(name, out var joint))
            {
                // A joint in the map has a bone, so it has an offset to undo.
                FitBone(transform, basis, joints[joint].Multiplied(_boneFromJoint.Value[joint]!));
            }

            // Where the pose will put the bone, which is not always where the clip wanted it. Its
            // children are measured from here, so they make up the difference rather than inherit it.
            worlds[name] = basis.Multiplied(transform.Matrix);
        }
    }

    /// <summary>
    /// Every joint of the skin, placed as <paramref name="pose"/> has its bones.
    /// </summary>
    /// <remarks>
    /// <paramref name="pose"/> has to be a pose of <see cref="Armature"/>, and it has to have been
    /// evaluated already. This does not evaluate it, because evaluating is not something to do twice. A
    /// pose writes what it solves for an inverse-kinematics chain back into itself, and a chain at part
    /// influence blends again on every evaluation, so a second one gives a different pose. Whoever
    /// evaluated it has already paid for it, too, to draw the bones or read a head.
    ///
    /// Every joint is returned, in the skin's order, including those still at rest. The renderer keeps a
    /// joint set by hand where it was set until the object stops being posed altogether. So a joint left
    /// out because it had gone back to rest would stay wherever it was last put: an elbow that never
    /// straightens. Telling which joints have moved would also cost as much as working them out. A joint
    /// no bone drives comes back exactly as the file has it, which, relative to its parent, keeps it where
    /// it rests.
    /// </remarks>
    public List<JointPose> JointsFor(Pose pose)
    {
        CheckArmatureOf(pose);
        var world = JointWorldsFor(pose);

        // A parent's inverse is shared by all its children, and a pelvis or a hand has several.
        var undone = new Matrix4?[world.Length];
        return [.. Enumerable.Range(0, world.Length).Select(joint => new JointPose(Index, joint, LocalOf(joint, world, undone)))];
    }

    private static Matrix4?[] OffsetsOf(Armature armature, SkinShape shape) =>
    [
        .. shape.Names.Select((name, joint) => armature.Contains(name)
            ? Matrix4.Inverted(armature.RestOf(name)).Multiplied(shape.Rest[joint])
            : null),
    ];

    private static Matrix4 RestFromParentOf(SkinShape shape, int joint) =>
        shape.Parents[joint] is var parent and >= 0
            ? Matrix4.Inverted(shape.Rest[parent]).Multiplied(shape.Rest[joint])
            : shape.Rest[joint];

    private static Matrix4 NodeFromParentOf(SkinShape shape, int joint)
    {
        var undone = shape.Local[joint].Multiplied(Matrix4.Inverted(shape.Rest[joint]));
        return shape.Parents[joint] is var parent and >= 0 ? undone.Multiplied(shape.Rest[parent]) : undone;
    }

    private Dictionary<string, int> JointsByBone()
    {
        var joints = new Dictionary<string, int>();
        for (var joint = 0; joint < Bones.Count; joint++)
        {
            if (Bones[joint] is { } bone)
            {
                joints[bone] = joint;
            }
        }

        return joints;
    }

    // Each joint in the model's frame, the way the renderer would compose it. A joint hanging from no
    // joint starts from the model's root.
    private Matrix4[] JointWorldsFrom(List<Matrix4> transforms)
    {
        var joints = new Matrix4[transforms.Count];
        foreach (var joint in _order)
        {
            var parent = _parents[joint];
            var step = _parentFromNode.Value[joint].Multiplied(transforms[joint]);
            joints[joint] = parent >= 0 ? joints[parent].Multiplied(step) : step;
        }

        return joints;
    }

    private Matrix4[] JointWorldsFor(Pose pose)
    {
        var world = new Matrix4[_order.Count];
        foreach (var joint in _order)
        {
            var parent = _parents[joint];
            if (Bones[joint] is { } bone)
            {
                // A joint with a bone has an offset from it.
                world[joint] = Posed(pose, bone).Multiplied(_offsets[joint]!);
            }
            else
            {
                world[joint] = parent >= 0 ? world[parent].Multiplied(_restFromParent[joint]) : _rest[joint];
            }
        }

        return world;
    }

    private Matrix4 LocalOf(int joint, Matrix4[] world, Matrix4?[] undone)
    {
        if (Bones[joint] is null)
        {
            return _local[joint].Clone();
        }

        var parent = _parents[joint];
        if (parent < 0)
        {
            return _nodeFromParent[joint].Multiplied(world[joint]);
        }

        var intoParent = undone[parent] ??= InverseOf(world[parent]);

        // A parent scaled to nothing, the usual way a clip hides a part, takes everything under it along,
        // and has no frame to place a joint in.
        return intoParent is null
            ? _local[joint].Clone()
            : _nodeFromParent[joint].Multiplied(intoParent).Multiplied(world[joint]);
    }

    private Matrix4 LocalFrom(int joint, BoneLocal? local)
    {
        if (local is null || (local.Position is null && local.Rotation is null && local.Scale is null))
        {
            return _local[joint].Clone();
        }

        var rest = _restParts.Value[joint];
        return Matrix4.Compose(local.Position ?? rest.Position, local.Rotation ?? rest.Rotation, local.Scale ?? rest.Scale);
    }

    private void CheckArmatureOf(Pose pose)
    {
        if (!ReferenceEquals(pose.Armature, Armature))
        {
            throw new ArgumentException(
                "A pose of a different armature. The offsets were measured against the armature this "
                + "binding was made with, and the bones of another one would carry the joints from "
                + "somewhere they never rested.",
                nameof(pose));
        }
    }

    // Where a bone ended up, with a clearer error than the pose's own. A bone that is in the armature and
    // missing from the pose's answers means the pose has never been evaluated. The pose itself would only
    // report that it has no such bone, which sends whoever reads it off checking names that are fine.
    private static Matrix4 Posed(Pose pose, string bone)
    {
        try
        {
            return pose.WorldOf(bone);
        }
        catch (ArmatureException)
        {
            throw new ArmatureException(
                $"Bone \"{bone}\" has not been worked out in this pose. Call Pose.Evaluate before asking "
                + "where the joints are.");
        }
    }

    private static void FitBone(PoseTransform transform, Matrix4 basis, Matrix4 world)
    {
        // Under a bone scaled to nothing, where a bone is makes no difference to anything drawn, and
        // there is no frame to measure it in.
        if (InverseOf(basis) is { } undo)
        {
            SetParts(undo.Multiplied(world), transform);
        }
        else
        {
            transform.Reset();
        }
    }

    // The matrix undone, or null for one that squashes something flat.
    private static Matrix4? InverseOf(Matrix4 matrix)
    {
        var inverse = Matrix4.Zero();
        var determinant = inverse.CopyInverse(matrix);
        return determinant == 0 || !double.IsFinite(determinant) ? null : inverse;
    }

    private static (Vector3 Position, Quaternion Rotation, Vector3 Scale) PartsOf(Matrix4 matrix)
    {
        var transform = new PoseTransform();
        SetParts(matrix, transform);
        return (transform.Location, transform.Rotation, transform.Scale);
    }

    // The matrix written into the transform as a move, a turn and a scale. A scale of nothing on some axis
    // leaves no way to tell which way that axis was turned, and the turn comes out as not a number. It is
    // taken as no turn at all, which is what anything scaled flat looks like anyway.
    private static void SetParts(Matrix4 matrix, PoseTransform transform)
    {
        matrix.Decompose(transform.Location, transform.Rotation, transform.Scale);
        var turn = transform.Rotation;
        if (!(double.IsFinite(turn.X) && double.IsFinite(turn.Y) && double.IsFinite(turn.Z) && double.IsFinite(turn.W)))
        {
            turn.SetValues(0, 0, 0, 1);
        }
    }
}
