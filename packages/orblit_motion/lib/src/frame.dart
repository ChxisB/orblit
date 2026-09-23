import 'package:vector_math/vector_math_64.dart';

import 'clip.dart';

/// Where a bone is relative to its parent, as far as a clip says.
///
/// Each part is null where the clip does not move it, which is not the same
/// as moving it nowhere: a clip that only turns an arm leaves the arm's
/// length to whatever else has a say, the rest pose or another clip.
class BoneLocal {
  BoneLocal({this.position, this.rotation, this.scale});

  Vector3? position;
  Quaternion? rotation;
  Vector3? scale;
}

/// What a clip says at one moment.
///
/// Values rather than effects, like a sequence's frame: nothing here has
/// touched a scene or a skeleton, so a frame can be shown for a scrub,
/// compared with another, or thrown away without anything to undo.
class ClipFrame {
  ClipFrame(this.at);

  /// Seconds into the clip.
  final double at;

  /// Target, then property, then the value, for everything on an entity.
  final Map<String, Map<String, Object>> values = {};

  /// Target, then bone, then where the clip has the bone.
  final Map<String, Map<String, BoneLocal>> bones = {};

  Object? valueOf(String target, String property) => values[target]?[property];

  BoneLocal? boneOf(String target, String bone) => bones[target]?[bone];

  /// Every channel of [clip], sampled at [at].
  static ClipFrame sample(ClipDocument clip, double at) {
    final frame = ClipFrame(at);
    for (final channel in clip.channels) {
      frame.put(channel, channel.valueAt(at));
    }
    return frame;
  }

  /// Puts [value] where [channel] says it goes.
  void put(ClipChannel<Object> channel, Object value) {
    final bone = channel.bone;
    if (bone == null) {
      (values[channel.target] ??= {})[channel.property] = value;
      return;
    }
    final local = (bones[channel.target] ??= {})[bone] ??= BoneLocal();
    switch ((channel.property, value)) {
      case ('position', final Vector3 position):
        local.position = position;
      case ('rotation', final Quaternion rotation):
        local.rotation = rotation;
      case ('scale', final Vector3 scale):
        local.scale = scale;
    }
  }
}
