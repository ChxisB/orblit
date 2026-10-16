import 'package:vector_math/vector_math_64.dart';

import '../armature.dart';
import '../ik.dart';
import 'bone_edit.dart';
import 'modifier.dart';

final _alongBone = Vector3(0, 1, 0);

/// Shares the roll of one bone out along the twist bones above it.
///
/// A wrist that rolls a quarter turn pinches the skin of the forearm into a
/// sweet wrapper if one bone takes all of it. A twist bone halfway up the
/// forearm that takes half of the roll, and another that takes a quarter,
/// spread it over the length of the arm.
///
/// The roll is read from [source]'s own turn about its length. Each of [bones]
/// is given that angle times its share, and its other rotation is replaced.
/// A twist bone is one the clip has no business animating, so nothing is lost.
final class TwistSpread extends Modifier {
  TwistSpread({required this.source, required this.bones, List<double>? shares})
    : shares = shares ?? _evenShares(bones.length) {
    if (bones.isEmpty) {
      throw ArgumentError.value(bones, 'bones', 'Give at least one bone.');
    }
    if (this.shares.length != bones.length) {
      throw ArgumentError.value(
        shares,
        'shares',
        'Give one share for each of the ${bones.length} bones.',
      );
    }
    if (this.shares.any((share) => !share.isFinite)) {
      throw ArgumentError.value(shares, 'shares', 'A share is a number.');
    }
  }

  /// The bone whose roll is spread, a hand or a foot.
  final String source;

  /// The twist bones, from the one furthest from [source] to the nearest.
  final List<String> bones;

  /// What part of the roll each of [bones] takes. One for the bone that should
  /// turn as far as the source does, and zero for one that stays put.
  final List<double> shares;

  static List<double> _evenShares(int count) => [
    for (var i = 1; i <= count; i++) i / count,
  ];

  @override
  void check(Armature armature) {
    requireBone(armature, source, 'so there is no roll to spread');
    for (final bone in bones) {
      requireBone(armature, bone, 'so there is nowhere to spread it');
    }
  }

  @override
  void solve(Pose pose, ModifierFrame frame) {
    final roll = twistAngle(
      swingTwist(pose[source].rotation, _alongBone).twist,
      _alongBone,
    );
    for (var i = 0; i < bones.length; i++) {
      pose[bones[i]].rotation = Quaternion.axisAngle(
        _alongBone,
        roll * shares[i],
      );
      pose.refresh(from: bones[i]);
    }
  }
}
