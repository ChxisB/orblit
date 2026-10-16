import 'package:vector_math/vector_math_64.dart';

import '../armature.dart';
import '../ik.dart';

/// Writes [bone]'s pose so the bone stands at [world], then brings it and the
/// bones below it up to date.
///
/// A pose is a departure from rest, measured from where the parent puts the
/// bone, so this works the departure out from the parent's current world. A
/// parent scaled to nothing has no frame to measure in and leaves the pose
/// as it was.
void placeBone(Pose pose, String bone, Matrix4 world) {
  final inverse = Matrix4.zero();
  final determinant = inverse.copyInverse(pose.baseOf(bone));
  if (determinant == 0 || !determinant.isFinite) return;

  final transform = pose[bone];
  inverse
    ..multiply(world)
    ..decompose(transform.location, transform.rotation, transform.scale);
  final turn = transform.rotation;
  final finite =
      turn.x.isFinite && turn.y.isFinite && turn.z.isFinite && turn.w.isFinite;
  // A scale of nothing on an axis leaves no way to tell which way that axis
  // was turned. Anything scaled flat looks the same either way.
  if (!finite) turn.setValues(0, 0, 0, 1);
  pose.refresh(from: bone);
}

/// Turns [bone] about its own head by [turn], a rotation in model space.
void turnBone(Pose pose, String bone, Quaternion turn) {
  final world = pose.worldOf(bone);
  final head = Vector3.zero();
  final rotation = Quaternion.identity();
  final scale = Vector3.zero();
  world.decompose(head, rotation, scale);
  placeBone(
    pose,
    bone,
    Matrix4.compose(head, (turn * rotation.normalized()).normalized(), scale),
  );
}

/// Points [bone]'s Y axis along [direction], keeping its roll, then brings it
/// and the bones below it up to date.
void aimBone(Pose pose, String bone, Vector3 direction) {
  final rotation = aimedRotation(
    world: pose.worldOf(bone),
    base: pose.baseOf(bone),
    direction: direction,
  );
  if (rotation == null) return;
  pose[bone].rotation = rotation;
  pose.refresh(from: bone);
}

/// The bones from [root] down to [tip], parents first, or null when [tip] is
/// not below [root].
List<String>? bonePath(Armature armature, String root, String tip) {
  final path = <String>[];
  String? name = tip;
  while (name != null) {
    path.add(name);
    if (name == root) return path.reversed.toList();
    name = armature[name]?.parent;
  }
  return null;
}

/// Throws if [armature] has no bone called [name].
void requireBone(Armature armature, String name, String why) {
  if (!armature.contains(name)) {
    throw ArmatureError('No bone called "$name" in this armature, $why.');
  }
}
