import '../armature.dart';
import '../constraints.dart';
import 'bone_edit.dart';
import 'modifier.dart';

/// Applies a [BoneConstraint] to a bone at this place in the stack.
///
/// A constraint registered with [Pose.constrain] runs inside the pose, in the
/// middle of evaluating, before anything in the stack. This runs it after the
/// modifiers above it instead, so a head can track a target that a foot
/// placement has just moved.
///
/// The constraint is applied at full strength. Its own influence would be
/// blended a second time by the slot, so a constraint that is not at one is
/// refused. Set the slot's influence instead.
final class Constrain extends Modifier {
  Constrain({required this.bone, required this.constraint}) {
    if (constraint.influence != 1 ||
        constraint.influenceProperty != null ||
        constraint.invertInfluence) {
      throw ArgumentError.value(
        constraint,
        'constraint',
        'Leave the constraint at full influence. The slot in the stack '
            'blends it.',
      );
    }
  }

  final String bone;
  final BoneConstraint constraint;

  @override
  void check(Armature armature) =>
      requireBone(armature, bone, 'so there is nothing to constrain');

  @override
  void solve(Pose pose, ModifierFrame frame) {
    final constrained = constraint.apply(pose.worldOf(bone), pose, bone);
    placeBone(pose, bone, constrained);
  }
}
