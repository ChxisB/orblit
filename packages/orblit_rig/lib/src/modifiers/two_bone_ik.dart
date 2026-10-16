import 'package:vector_math/vector_math_64.dart';

import '../armature.dart';
import '../ik.dart';
import 'bone_edit.dart';
import 'modifier.dart';

/// Reaches a limb of two bones for a point.
///
/// This is the solver [IkChain] runs, as a step in the stack. It reaches for
/// a point in the model's space and not a bone, and the stack's influence
/// does the blending that [IkChain.influence] does inside the pose.
///
/// Without a [pole] the joint bends the way the clip already bent it, which is
/// the right default after playback: the knee that was forward stays forward.
final class TwoBoneIk extends Modifier {
  const TwoBoneIk({
    required this.root,
    required this.mid,
    required this.target,
    this.pole,
    this.stretch = 0,
  });

  /// The upper bone, a thigh or an upper arm.
  final String root;

  /// The lower bone, a child of [root]. The chain's end is its tail.
  final String mid;

  /// Where the end of the chain reaches. Null leaves the limb as it is.
  final ModelPoint target;

  /// Where the joint points. Null keeps the bend the animation had.
  final ModelPoint? pole;

  /// How far the limb may stretch past full extension, from zero to one.
  final double stretch;

  @override
  void check(Armature armature) {
    requireBone(armature, root, 'so there is no limb to reach with');
    requireBone(armature, mid, 'so there is no limb to reach with');
    if (armature[mid]!.parent != root) {
      throw ArmatureError(
        'Bone "$mid" is not a child of "$root", so the two are not one limb.',
      );
    }
  }

  @override
  void solve(Pose pose, ModifierFrame frame) {
    final point = target(pose, frame);
    if (point == null) return;
    reachTwoBones(
      pose,
      root: root,
      mid: mid,
      target: point,
      pole: pole?.call(pose, frame),
      stretch: stretch,
    );
  }
}

/// Aims [root] and [mid] so the tail of [mid] lands on [target], or as near
/// as the limb reaches.
void reachTwoBones(
  Pose pose, {
  required String root,
  required String mid,
  required Vector3 target,
  Vector3? pole,
  double stretch = 0,
}) {
  final rootBone = pose.armature[root]!;
  final midBone = pose.armature[mid]!;
  final rootHead = pose.headOf(root);

  final solution = solveTwoBoneIk(
    root: rootHead,
    pole: pole ?? pose.headOf(mid),
    target: target,
    upperLength: rootBone.length,
    lowerLength: midBone.length,
    stretch: stretch.clamp(0.0, 1.0),
  );

  // Only the root is scaled. Scale is inherited, so stretching the upper bone
  // carries the lower one out to the right place and lengthens it by the same
  // factor. It is written every solve, back to one included, so a stretch
  // from a frame that reached further does not outlive the reach.
  pose[root].scale.setFrom(stretchScale(solution.stretch));
  aimBone(pose, root, solution.joint - rootHead);

  pose[mid].scale.setFrom(stretchScale(1));
  aimBone(pose, mid, solution.end - pose.headOf(mid));
}
