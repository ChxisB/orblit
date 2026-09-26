import 'package:orblit_sequence/orblit_sequence.dart';
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

  /// [frames] blended, each with as much say as its weight in [weights].
  ///
  /// A value mixes the way its channel's kind does: numbers and vectors by
  /// weighted average, rotations the short way round, and flags by whichever
  /// frame has the most say. Only the frames that say something about a
  /// value share it, so a clip that leaves an arm alone leaves the arm to the
  /// clips that move it, the way [BoneLocal] leaves a part to whatever else
  /// has a say. A frame with no weight has none, and a value one frame has
  /// as a rotation and another as angles goes by whichever the first frame
  /// to have it says.
  static ClipFrame mix(
    List<ClipFrame> frames,
    List<double> weights, {
    double at = 0,
  }) {
    if (frames.length != weights.length) {
      throw ArgumentError(
        'A weight for every frame, and a frame for every weight.',
      );
    }
    final out = ClipFrame(at);
    final values = <String, Map<String, _Say>>{};
    final bones = <String, Map<String, _BoneSay>>{};
    for (var i = 0; i < frames.length; i++) {
      final weight = weights[i];
      if (!(weight > 0)) continue;
      final frame = frames[i];
      for (final MapEntry(key: target, value: properties)
          in frame.values.entries) {
        final into = values[target] ??= {};
        for (final MapEntry(key: property, value: value)
            in properties.entries) {
          (into[property] ??= _Say()).add(value, weight);
        }
      }
      for (final MapEntry(key: target, value: locals) in frame.bones.entries) {
        final into = bones[target] ??= {};
        for (final MapEntry(key: bone, value: local) in locals.entries) {
          final say = into[bone] ??= _BoneSay();
          if (local.position case final position?) {
            say.position.add(position, weight);
          }
          if (local.rotation case final rotation?) {
            say.rotation.add(rotation, weight);
          }
          if (local.scale case final scale?) say.scale.add(scale, weight);
        }
      }
    }

    for (final MapEntry(key: target, value: properties) in values.entries) {
      final into = out.values[target] = {};
      for (final MapEntry(key: property, value: say) in properties.entries) {
        if (say.mixed() case final value?) into[property] = value;
      }
    }
    for (final MapEntry(key: target, value: locals) in bones.entries) {
      final into = out.bones[target] = {};
      for (final MapEntry(key: bone, value: say) in locals.entries) {
        into[bone] = BoneLocal(
          position: say.position.mixed() as Vector3?,
          rotation: say.rotation.mixed() as Quaternion?,
          scale: say.scale.mixed() as Vector3?,
        );
      }
    }
    return out;
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

/// Everything the frames being mixed say about one value, and how loudly.
class _Say {
  final List<Object> values = [];
  final List<double> weights = [];

  void add(Object value, double weight) {
    values.add(value);
    weights.add(weight);
  }

  /// The mix, or null when nothing said anything.
  Object? mixed() {
    if (values.isEmpty) return null;
    // The kind is named each time: left to itself, the switch would take
    // every value as an Object and hand the mixer the wrong sort of list.
    return switch (values.first) {
      double() => _all<double>(doubleMixer),
      Vector3() => _all<Vector3>(vector3Mixer),
      Quaternion() => _all<Quaternion>(quaternionMixer),
      bool() => _all<bool>(boolMixer),
      final other => other,
    };
  }

  /// The values that are [T], mixed.
  T _all<T extends Object>(Mixer<T> mixer) {
    final kept = <T>[];
    final said = <double>[];
    for (var i = 0; i < values.length; i++) {
      if (values[i] case final T value) {
        kept.add(value);
        said.add(weights[i]);
      }
    }
    // One voice needs no mixing, and a copy keeps the mix from sharing a
    // vector with the frame it came from.
    if (kept.length == 1) {
      return switch (kept.first) {
        final Vector3 vector => vector.clone() as T,
        final Quaternion rotation => rotation.clone() as T,
        final other => other,
      };
    }
    return mixer.mix(kept, said);
  }
}

class _BoneSay {
  final _Say position = _Say();
  final _Say rotation = _Say();
  final _Say scale = _Say();
}
