import 'package:orblit_rig/orblit_rig.dart'
    show rotationBetween, solveTwoBoneIk;
import 'package:vector_math/vector_math_64.dart';

import 'frame.dart';
import 'rest.dart';

/// Where a limb of three joints ends up reaching for a point.
final class LimbReach {
  const LimbReach({
    required this.root,
    required this.mid,
    required this.end,
    required this.reachable,
  });

  /// The turn to give the upper bone in its parent.
  final Quaternion root;

  /// The turn to give the lower bone in the upper one.
  final Quaternion mid;

  /// Where the tip stands, in the model's space. The target when it was
  /// reachable, and the nearest the limb gets when it was not.
  final Vector3 end;

  /// False when the target was further than the limb is long, or so near the
  /// limb would have to fold through itself.
  final bool reachable;
}

/// The turns that put the tip of a limb on [target], as a pose would have
/// them: for [root] and [mid], their rotation in their parents.
///
/// [root] is the upper bone, a thigh or an upper arm. [mid] hangs from it,
/// and [tip] hangs from [mid], so the three heads are a hip, a knee and an
/// ankle. [pose] is where the bones stand now, as a clip frame gives them,
/// and [target] is in the model's space, the space [RestSkeleton.posedOrigins]
/// answers in.
///
/// Without a [pole] the joint bends the way it already does, turning with
/// the limb, so a knee that was forward stays forward as the foot is lifted.
/// A limb that is straight bends the way the rest pose bends it. Neither
/// bone is stretched, since the answer is two rotations that a clip can key,
/// and a rotation cannot lengthen a bone.
///
/// Worked out from world turns, so it assumes the skeleton is scaled the same
/// in every direction, as a rig made in centimetres is.
///
/// Throws [ArgumentError] when a name is not a bone, or the three are not a
/// chain from parent to child.
LimbReach reachLimb(
  RestSkeleton skeleton,
  Map<String, BoneLocal> pose, {
  required String root,
  required String mid,
  required String tip,
  required Vector3 target,
  Vector3? pole,
}) {
  final rootAt = _boneNamed(skeleton, root);
  final midAt = _boneNamed(skeleton, mid);
  final tipAt = _boneNamed(skeleton, tip);
  if (skeleton.parents[midAt] != rootAt || skeleton.parents[tipAt] != midAt) {
    throw ArgumentError(
      '"$root", "$mid" and "$tip" are not a limb: each has to hang from the '
      'one before.',
    );
  }

  final worlds = skeleton.posedWorlds(pose);
  final rootHead = worlds[rootAt].getTranslation();
  final midHead = worlds[midAt].getTranslation();
  final tipHead = worlds[tipAt].getTranslation();
  final upper = midHead - rootHead;
  final lower = tipHead - midHead;

  final solution = solveTwoBoneIk(
    root: rootHead,
    pole:
        pole ??
        _bendPole(skeleton, worlds, (rootAt, midAt, tipAt), target) ??
        midHead,
    target: target,
    upperLength: upper.length,
    lowerLength: lower.length,
  );

  final upperTurn = rotationBetween(upper, solution.joint - rootHead);
  final lowerTurn = rotationBetween(
    // Through the matrix, because `Quaternion.rotated` turns backwards.
    upperTurn.asRotationMatrix().transformed(lower),
    solution.end - solution.joint,
  );

  final parent = skeleton.parents[rootAt];
  final aboveRoot = _rotationOf(
    parent < 0 ? skeleton.parentWorldOf(rootAt) : worlds[parent],
  );
  final rootWorld = upperTurn * _rotationOf(worlds[rootAt]);
  final midWorld = lowerTurn * upperTurn * _rotationOf(worlds[midAt]);

  return LimbReach(
    root: (aboveRoot.conjugated() * rootWorld).normalized(),
    mid: (rootWorld.conjugated() * midWorld).normalized(),
    end: solution.end,
    reachable: solution.reachable,
  );
}

/// A point on the side of the root to tip line that the joint bends to,
/// turned the way the line turns to reach [target].
///
/// The joint itself will not do as a pole: it is on one side of the line
/// from the root to where the tip is, and a target past it puts it on the
/// other side of the line to there, which would swing the knee backwards.
/// Null when the limb is straight and so is the rest, which names no side.
Vector3? _bendPole(
  RestSkeleton skeleton,
  List<Matrix4> worlds,
  (int, int, int) limb,
  Vector3 target,
) {
  final (rootAt, midAt, tipAt) = limb;
  final rootHead = worlds[rootAt].getTranslation();
  final line = worlds[tipAt].getTranslation() - rootHead;
  final bend =
      _bendOf(worlds, limb) ?? _bendOf(skeleton.posedWorlds(const {}), limb);
  if (bend == null) return null;
  final turn = rotationBetween(line, target - rootHead);
  return rootHead + turn.asRotationMatrix().transformed(bend);
}

/// The part of the joint's offset from the root that is not along the line
/// to the tip, or null when the limb is straight to within rounding.
Vector3? _bendOf(List<Matrix4> worlds, (int, int, int) limb) {
  final (rootAt, midAt, tipAt) = limb;
  final rootHead = worlds[rootAt].getTranslation();
  final line = worlds[tipAt].getTranslation() - rootHead;
  final joint = worlds[midAt].getTranslation() - rootHead;
  final length = line.length;
  if (length < 1e-9) return null;
  final along = line * (joint.dot(line) / (length * length));
  final bend = joint - along;
  return bend.length < 1e-6 * length ? null : bend;
}

int _boneNamed(RestSkeleton skeleton, String name) {
  final at = skeleton.indexOf(name);
  if (at == null) {
    throw ArgumentError.value(name, 'name', 'The skeleton has no such bone.');
  }
  return at;
}

Quaternion _rotationOf(Matrix4 matrix) {
  final rotation = Quaternion.identity();
  matrix.decompose(Vector3.zero(), rotation, Vector3.zero());
  return rotation.normalized();
}
