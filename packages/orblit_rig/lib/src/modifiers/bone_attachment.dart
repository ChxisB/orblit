import 'package:vector_math/vector_math_64.dart';

import '../armature.dart';
import 'bone_edit.dart';
import 'modifier.dart';

/// Reads where a socket on a bone is, for something that is not a bone.
///
/// A sword in a hand, a hat on a head, a light on a shoulder: none of them is
/// part of the skeleton, and each has to follow a place on it. The place is a
/// bone and an offset in that bone's own space. This writes nothing to the
/// pose. It samples the bone where it stands at this step of the stack, so it
/// goes after the modifiers whose work it should follow.
///
/// While the slot is off or at zero influence the attachment keeps the last
/// place it was given, and does not snap anywhere.
final class BoneAttachment extends Modifier {
  BoneAttachment({required this.bone, Matrix4? offset})
    : offset = offset?.clone() ?? Matrix4.identity();

  final String bone;

  /// Where the socket sits in the bone's own space. The bone's Y axis runs
  /// along its length, so `translation(0, length, 0)` is its tail.
  final Matrix4 offset;

  Matrix4? _model;
  Matrix4? _world;

  /// The socket in the model's space, or null before the first frame.
  Matrix4? get model => _model?.clone();

  /// The socket in the world, or null before the first frame.
  Matrix4? get world => _world?.clone();

  @override
  void check(Armature armature) =>
      requireBone(armature, bone, 'so there is nothing to attach to');

  @override
  void solve(Pose pose, ModifierFrame frame) {
    final inModel = pose.worldOf(bone).multiplied(offset);
    _model = inModel;
    _world = frame.modelToWorld.multiplied(inModel);
  }
}
