import 'dart:math' as math;

import 'package:vector_math/vector_math_64.dart';

import '../armature.dart';
import '../ik.dart';
import 'bone_edit.dart';
import 'modifier.dart';

final _alongBone = Vector3(0, 1, 0);

/// Keeps a joint inside a cone of its rest direction.
///
/// A shoulder or a hip is a ball: the bone swings off its rest direction in
/// any way, and rolls about its own length. The swing is held to [swing]
/// radians and the roll to [twist] either side of rest. Both are measured
/// from the bone's rest pose, so a bone that is not axis-aligned is not
/// treated as already bent.
///
/// Put it after whatever moves the bone. A limit that runs first is undone by
/// the next modifier that turns the bone.
final class ConeLimit extends Modifier {
  ConeLimit({required this.bone, required this.swing, this.twist = math.pi}) {
    if (!swing.isFinite || swing < 0 || swing > math.pi) {
      throw ArgumentError.value(swing, 'swing', 'Swing is 0 to pi radians.');
    }
    if (!twist.isFinite || twist < 0 || twist > math.pi) {
      throw ArgumentError.value(twist, 'twist', 'Twist is 0 to pi radians.');
    }
  }

  final String bone;

  /// The most the bone may swing off its rest direction, in radians.
  final double swing;

  /// The most it may roll about its own length either way, in radians.
  final double twist;

  @override
  void check(Armature armature) =>
      requireBone(armature, bone, 'so there is no joint to limit');

  @override
  void solve(Pose pose, ModifierFrame frame) {
    final parts = swingTwist(pose[bone].rotation, _alongBone);

    final swung = turnAngle(parts.swing);
    final kept = swung <= swing
        ? parts.swing
        : slerpShortest(Quaternion.identity(), parts.swing, swing / swung);

    final rolled = twistAngle(parts.twist, _alongBone).clamp(-twist, twist);
    pose[bone].rotation = (kept * Quaternion.axisAngle(_alongBone, rolled))
      ..normalize();
    pose.refresh(from: bone);
  }
}

/// Lets a joint turn about one axis only, between two angles.
///
/// A knee or an elbow is a hinge. Whatever the clip or a modifier above asked
/// of it off the axis is dropped, and the turn about the axis is held to
/// [min] and [max]. The axis is in the bone's own space, which makes X the
/// default: a limb that bends forwards in the rest pose bends about X.
final class HingeLimit extends Modifier {
  HingeLimit({
    required this.bone,
    required this.min,
    required this.max,
    Vector3? axis,
  }) : axis = (axis ?? Vector3(1, 0, 0)).normalized() {
    if (!min.isFinite || !max.isFinite || min > max) {
      throw ArgumentError.value(
        [min, max],
        'min, max',
        'A hinge needs a lowest angle that is not past its highest.',
      );
    }
    if (this.axis.length2 < 0.5) {
      throw ArgumentError.value(axis, 'axis', 'A hinge needs an axis.');
    }
  }

  final String bone;

  /// The hinge's axis, in the bone's own space. Always a unit vector.
  final Vector3 axis;

  /// Radians. Zero is the rest pose, and a bend is one side or the other.
  final double min;
  final double max;

  @override
  void check(Armature armature) =>
      requireBone(armature, bone, 'so there is no joint to limit');

  @override
  void solve(Pose pose, ModifierFrame frame) {
    final parts = swingTwist(pose[bone].rotation, axis);
    final angle = twistAngle(parts.twist, axis).clamp(min, max);
    pose[bone].rotation = Quaternion.axisAngle(axis, angle);
    pose.refresh(from: bone);
  }
}
