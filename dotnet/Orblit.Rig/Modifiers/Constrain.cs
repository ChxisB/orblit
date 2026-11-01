namespace Orblit.Rig;

/// <summary>
/// Applies a <see cref="BoneConstraint"/> to a bone at this place in the stack. A constraint registered
/// with <see cref="Pose.Constrain"/> runs inside the pose, in the middle of evaluating, before anything
/// in the stack. This runs it after the modifiers above it instead, so a head can track a target that
/// a foot placement has just moved.
/// The constraint is applied at full strength. Its own influence would be blended a second time by the
/// slot, so a constraint that is not at one is refused. Set the slot's influence instead.
/// </summary>
public sealed class Constrain : Modifier
{
    public Constrain(string bone, BoneConstraint constraint)
    {
        if (constraint.Influence != 1 || constraint.InfluenceProperty is not null || constraint.InvertInfluence)
        {
            throw new ArgumentException("Leave the constraint at full influence. The slot in the stack blends it.", nameof(constraint));
        }

        Bone = bone;
        Constraint = constraint;
    }

    public string Bone { get; }

    public BoneConstraint Constraint { get; }

    public override void Check(Armature armature) =>
        BoneEdit.RequireBone(armature, Bone, "so there is nothing to constrain");

    public override void Solve(Pose pose, ModifierFrame frame)
    {
        var constrained = Constraint.Apply(pose.WorldOf(Bone), pose, Bone);
        BoneEdit.PlaceBone(pose, Bone, constrained);
    }
}
