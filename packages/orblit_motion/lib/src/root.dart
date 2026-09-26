import 'package:orblit_scene/orblit_scene.dart' show TransformComponent;
import 'package:vector_math/vector_math_64.dart';

import 'clip.dart';
import 'frame.dart';

/// How far root motion carried the character between two moments.
///
/// In the character's own frame as it stood at the first moment, so a step
/// forward is forward whichever way the character faces. Taking one is
/// moving by [position] turned the way the character faces, and then
/// turning by [rotation], which is [then] with the character as the first
/// step.
///
/// The space is the one the root's own channels are in: the model's, for a
/// skeleton's root bone, and the parent's, for an entity. A model scaled
/// down to half size walks half as far, and scaling the step is the job of
/// whoever knows the scale.
class RootStep {
  RootStep({Vector3? position, Quaternion? rotation})
    : position = position ?? Vector3.zero(),
      rotation = rotation ?? Quaternion.identity();

  final Vector3 position;
  final Quaternion rotation;

  /// Whether it goes nowhere and turns nothing.
  bool get isNone => position.length2 < 1e-18 && 1 - rotation.w.abs() < 1e-12;

  /// This step, and then [next] from wherever this one ends.
  RootStep then(RootStep next) => RootStep(
    position: position + turned(rotation, next.position),
    rotation: (rotation * next.rotation)..normalize(),
  );

  @override
  String toString() => 'RootStep($position, $rotation)';
}

/// A clip's root channels, read the way its [RootMotion] asks.
///
/// Splits the root into a body and a pose. The body is the part that goes to
/// the character: across the ground always, up and down when the root
/// [RootMotion.rises], and the turn about up when it [RootMotion.turns]. The
/// pose is what is left, which the model keeps. The body at any moment is
/// the root's extracted part as the clip has it, so the model's root sits
/// exactly over the character rather than wherever the clip started it.
class RootTrack {
  RootTrack._(this.motion, this._position, this._rotation);

  /// The root [clip] names, or null when it names none.
  static RootTrack? of(ClipDocument clip) {
    final motion = clip.rootMotion;
    if (motion == null) return null;
    final onBone = motion.bone != null;
    return RootTrack._(
      motion,
      clip.channelFor(
        motion.target,
        onBone ? 'position' : 'transform.position',
        bone: motion.bone,
      ),
      clip.channelFor(
        motion.target,
        onBone ? 'rotation' : 'transform.rotation',
        bone: motion.bone,
      ),
    );
  }

  final RootMotion motion;
  final ClipChannel<Object>? _position;
  final ClipChannel<Object>? _rotation;

  /// How far the body goes from [from] seconds in to [to].
  ///
  /// Backwards when [to] is before [from], which is what playing a walk in
  /// reverse should do.
  RootStep step(double from, double to) {
    final start = _bodyAt(from);
    final end = _bodyAt(to);
    final back = start.heading.conjugated();
    return RootStep(
      position: turned(back, end.ground - start.ground),
      rotation: back * end.heading,
    );
  }

  /// Takes the body out of [frame], leaving the root where the body is.
  void hold(ClipFrame frame) {
    final body = _bodyAt(frame.at);
    final back = body.heading.conjugated();
    final bone = motion.bone;

    if (bone != null) {
      final local = frame.boneOf(motion.target, bone);
      if (local == null) return;
      final position = local.position;
      if (position != null) local.position = position - body.ground;
      final rotation = local.rotation;
      if (rotation != null && motion.turns) local.rotation = back * rotation;
      return;
    }

    final values = frame.values[motion.target];
    if (values == null) return;
    if (values['transform.position'] case final Vector3 position) {
      values['transform.position'] = position - body.ground;
    }
    if (!motion.turns) return;
    switch (values['transform.rotation']) {
      case final Quaternion rotation:
        values['transform.rotation'] = back * rotation;
      case final Vector3 degrees:
        values['transform.rotation'] = TransformComponent.anglesOf(
          (back * _fromDegrees(degrees)).asRotationMatrix(),
        );
    }
  }

  ({Vector3 ground, Quaternion heading}) _bodyAt(double at) {
    final position = _position?.valueAt(at);
    final rotation = switch (_rotation?.valueAt(at)) {
      final Quaternion rotation => rotation,
      final Vector3 degrees => _fromDegrees(degrees),
      _ => null,
    };
    return (
      ground: position is Vector3 ? _ground(position) : Vector3.zero(),
      heading: rotation == null ? Quaternion.identity() : _heading(rotation),
    );
  }

  /// The part of [position] the body takes.
  Vector3 _ground(Vector3 position) {
    if (motion.rises) return position.clone();
    final up = motion.up;
    return position - up * position.dot(up);
  }

  /// The part of [rotation] the body takes: its turn about up, or none.
  ///
  /// The turn is the rotation with everything but its spin about up taken
  /// out, which is the same whether the lean came before the turn or after.
  /// A root on its back has no turn to speak of, and gives none rather than
  /// a sudden half turn.
  Quaternion _heading(Quaternion rotation) {
    if (!motion.turns) return Quaternion.identity();
    final up = motion.up;
    final along = up.dot(Vector3(rotation.x, rotation.y, rotation.z));
    final turn = Quaternion(
      up.x * along,
      up.y * along,
      up.z * along,
      rotation.w,
    );
    if (turn.length < 1e-6) return Quaternion.identity();
    return turn..normalize();
  }

  static Quaternion _fromDegrees(Vector3 degrees) =>
      Quaternion.fromRotation(TransformComponent.rotationOf(degrees));
}

/// [step] taken [times] times over, in as few compositions as it can.
RootStep repeated(RootStep step, int times) {
  var out = RootStep();
  var power = step;
  for (var left = times; left > 0; left >>= 1) {
    if (left.isOdd) out = out.then(power);
    power = power.then(power);
  }
  return out;
}

/// [vector] turned by [rotation].
///
/// Not `Quaternion.rotated`, which turns by the inverse of the rotation it
/// is called on, where multiplying rotations and their matrices do not.
Vector3 turned(Quaternion rotation, Vector3 vector) =>
    rotation.asRotationMatrix().transformed(vector);
