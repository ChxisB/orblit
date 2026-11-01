using Orblit.VectorMath;

namespace Orblit.Rig;

/// <summary>
/// Reads where a socket on a bone is, for something that is not a bone. A sword in a hand, a hat on a
/// head, a light on a shoulder: none of them is part of the skeleton, and each has to follow a place
/// on it. The place is a bone and an offset in that bone's own space. This writes nothing to the pose.
/// It samples the bone where it stands at this step of the stack, so it goes after the modifiers whose
/// work it should follow.
/// While the slot is off or at zero influence the attachment keeps the last place it was given, and
/// does not snap anywhere.
/// </summary>
public sealed class BoneAttachment(string bone, Matrix4? offset = null) : Modifier
{
    private Matrix4? _model;
    private Matrix4? _world;

    public string Bone { get; } = bone;

    /// <summary>
    /// Where the socket sits in the bone's own space. The bone's Y axis runs along its length, so
    /// <c>translation(0, length, 0)</c> is its tail.
    /// </summary>
    public Matrix4 Offset { get; } = offset?.Clone() ?? Matrix4.Identity();

    /// <summary>The socket in the model's space, or null before the first frame.</summary>
    public Matrix4? Model => _model?.Clone();

    /// <summary>The socket in the world, or null before the first frame.</summary>
    public Matrix4? World => _world?.Clone();

    public override void Check(Armature armature) =>
        BoneEdit.RequireBone(armature, Bone, "so there is nothing to attach to");

    public override void Solve(Pose pose, ModifierFrame frame)
    {
        var inModel = pose.WorldOf(Bone).Multiplied(Offset);
        _model = inModel;
        _world = frame.ModelToWorld.Multiplied(inModel);
    }
}
