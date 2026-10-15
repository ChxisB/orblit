import '../armature.dart';
import '../ik.dart';
import 'bone_edit.dart';
import 'modifier.dart';

/// Holds bones to a top speed, so a snap in the target is a turn.
///
/// A head that follows a target whips round the moment the target jumps. This
/// remembers where each bone was left last frame and lets it travel no more
/// than [turnSpeed] radians a second, or [moveSpeed] units a second, towards
/// where it is asked to be.
///
/// The first frame passes as it is. A frame of no time passes nothing on: a
/// bone stays where it was left, because no time went by for it to move in.
final class SpeedLimit extends Modifier {
  SpeedLimit({required this.bones, this.turnSpeed, this.moveSpeed}) {
    if (bones.isEmpty) {
      throw ArgumentError.value(bones, 'bones', 'Give at least one bone.');
    }
    for (final speed in [turnSpeed, moveSpeed]) {
      if (speed != null && (speed < 0 || !speed.isFinite)) {
        throw ArgumentError.value(speed, 'speed', 'A speed is 0 or more.');
      }
    }
  }

  final List<String> bones;

  /// The most a bone may turn, in radians a second. Null leaves turns alone.
  final double? turnSpeed;

  /// The most a bone's pose may move, in units a second. Null leaves it.
  final double? moveSpeed;

  final Map<String, PoseTransform> _left = {};

  @override
  void check(Armature armature) {
    for (final bone in bones) {
      requireBone(armature, bone, 'so there is nothing to slow');
    }
  }

  @override
  void solve(Pose pose, ModifierFrame frame) {
    for (final bone in bones) {
      final now = pose[bone];
      final left = _left[bone];
      if (left != null) _hold(now, left, frame.dt);
      _left[bone] = now.copy();
    }
  }

  @override
  void reset() => _left.clear();

  void _hold(PoseTransform now, PoseTransform left, double dt) {
    final turn = turnSpeed;
    if (turn != null) {
      final gap = turnAngle(left.rotation.conjugated() * now.rotation);
      final allowed = turn * dt;
      if (gap > allowed) {
        now.rotation = slerpShortest(
          left.rotation,
          now.rotation,
          allowed / gap,
        );
      }
    }

    final move = moveSpeed;
    if (move != null) {
      final step = now.location - left.location;
      final allowed = move * dt;
      if (step.length > allowed) {
        now.location = left.location + step.normalized() * allowed;
      }
    }
  }
}
