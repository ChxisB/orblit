import 'dart:math' as math;

import 'package:vector_math/vector_math_64.dart';

import '../armature.dart';
import '../constraints.dart';
import '../ik.dart';
import 'bone_edit.dart';
import 'modifier.dart';

/// Turns a bone to face a point, no further than a limit, and easing there.
///
/// The turn is measured from where the clip had the bone pointing. A head
/// that nods in the animation keeps nodding, and the look-at is added to it.
/// The limit is on that added turn, so a head can never be asked to face
/// backwards, and a target behind the character is looked at as far round as
/// the limit goes.
///
/// It takes the shortest turn to the target and leaves the bone's roll
/// alone, so a bone does not spin as the target passes overhead.
///
/// While [ease] is above zero the turn approaches its goal and does not jump
/// to it, and a target that goes away is let go of the same way. The easing
/// is exponential: the gap closes by about two thirds in [ease] seconds.
final class LookAt extends Modifier {
  LookAt({
    required this.bone,
    required this.target,
    this.axis = TrackAxis.y,
    this.limit = math.pi / 2,
    this.ease = 0,
  }) {
    if (limit < 0 || !limit.isFinite) {
      throw ArgumentError.value(limit, 'limit', 'A limit is an angle of 0+.');
    }
    if (ease < 0 || !ease.isFinite) {
      throw ArgumentError.value(ease, 'ease', 'Easing takes 0 or more seconds');
    }
  }

  /// The bone that turns.
  final String bone;

  /// What it faces, in the model's space. Null lets the turn go.
  final ModelPoint target;

  /// Which of the bone's own axes points at the target.
  final TrackAxis axis;

  /// The most the bone may turn from where the clip had it, in radians.
  final double limit;

  /// Seconds for the turn to close by about two thirds. Zero snaps.
  final double ease;

  Quaternion? _turn;

  @override
  void check(Armature armature) =>
      requireBone(armature, bone, 'so there is nothing to turn');

  @override
  void solve(Pose pose, ModifierFrame frame) {
    final goal = _goalTurn(pose, frame);
    final held = _turn ?? Quaternion.identity();

    // Time that has not passed moves nothing, so a pose solved once with
    // easing on stays where it was. Snapping is what zero asks for.
    final share = ease <= 0 ? 1.0 : 1 - math.exp(-frame.dt / ease);
    final turn = slerpShortest(held, goal, share);
    _turn = turn;

    if (turnAngle(turn) < 1e-9) return;
    turnBone(pose, bone, turn);
  }

  @override
  void reset() => _turn = null;

  Quaternion _goalTurn(Pose pose, ModifierFrame frame) {
    final point = target(pose, frame);
    if (point == null) return Quaternion.identity();

    final toward = point - pose.headOf(bone);
    if (toward.length2 < 1e-12) return Quaternion.identity();

    final goal = rotationBetween(axis.of(pose.worldOf(bone)), toward);
    final angle = turnAngle(goal);
    if (angle <= limit) return goal;
    return slerpShortest(Quaternion.identity(), goal, limit / angle);
  }
}
