import 'package:vector_math/vector_math_64.dart';

import '../armature.dart';
import '../ik.dart';
import 'bone_edit.dart';
import 'modifier.dart';

/// Reaches a chain of any length for a point, from [root] down to [tip].
///
/// A spine, a tail or a tentacle has more bones than a limb has, and a closed
/// form for two does not stretch to them. This is FABRIK: it keeps the length
/// of every bone and the place of the root, and bends the chain until its tip
/// lands on the target or runs out of reach.
///
/// With no pole there is nothing to say which way a joint folds, so the chain
/// keeps the bend the animation gave it as far as the reaching allows.
final class ChainIk extends Modifier {
  const ChainIk({
    required this.root,
    required this.tip,
    required this.target,
    this.iterations = 12,
  });

  /// The first bone of the chain. Its head does not move.
  final String root;

  /// The last bone. Its tail is what reaches.
  final String tip;

  /// Where the tip reaches, in the model's space. Null leaves the chain as it
  /// is.
  final ModelPoint target;

  /// How many passes the solver may make. More passes close more of the gap
  /// on a long chain and cost a pass each.
  final int iterations;

  @override
  void check(Armature armature) {
    requireBone(armature, root, 'so the chain has no start');
    requireBone(armature, tip, 'so the chain has no end');
    if (bonePath(armature, root, tip) == null) {
      throw ArmatureError(
        'Bone "$tip" is not below "$root", so they are not '
        'one chain.',
      );
    }
  }

  @override
  void solve(Pose pose, ModifierFrame frame) {
    final goal = target(pose, frame);
    if (goal == null) return;

    final path = bonePath(pose.armature, root, tip)!;
    final points = [
      for (final bone in path) pose.headOf(bone),
      pose.tailOf(tip),
    ];
    final wanted = solveFabrik(
      points: points,
      target: goal,
      iterations: iterations,
    );
    pointAlong(pose, path, wanted);
  }
}

/// Turns each bone of [path] so the line from its head to the next bone's
/// head, or to the tip's tail, runs along [points].
///
/// Parents go first. Turning a bone carries the bones below it with it, so by
/// the time a bone is reached its head is already where the one above put it.
void pointAlong(Pose pose, List<String> path, List<Vector3> points) {
  for (var i = 0; i < path.length; i++) {
    final bone = path[i];
    final here = pose.headOf(bone);
    final next = i + 1 < path.length
        ? pose.headOf(path[i + 1])
        : pose.tailOf(bone);
    final now = next - here;
    final wanted = points[i + 1] - points[i];
    if (now.length2 < 1e-12 || wanted.length2 < 1e-12) continue;
    turnBone(pose, bone, rotationBetween(now, wanted));
  }
}
