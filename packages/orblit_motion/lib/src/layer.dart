import 'dart:math' as math;

import 'package:orblit_sequence/orblit_sequence.dart';
import 'package:vector_math/vector_math_64.dart';

import 'clip.dart';
import 'frame.dart';
import 'rest.dart';

/// How much an overlay may move each bone of one target.
final class BoneMask {
  BoneMask(Map<String, double> weights, {this.target = ''})
    : weights = Map.unmodifiable(weights) {
    for (final weight in weights.values) {
      if (!weight.isFinite || weight < 0 || weight > 1) {
        throw ArgumentError.value(weight, 'weights', 'Between zero and one.');
      }
    }
  }

  /// Includes [bone] and every bone hanging from it.
  factory BoneMask.below(
    RestSkeleton skeleton,
    String bone, {
    String target = '',
    double weight = 1,
  }) {
    final root = skeleton.indexOf(bone);
    if (root == null) throw ArgumentError.value(bone, 'bone', 'Unknown bone.');
    final included = <int>{root};
    for (var i = root + 1; i < skeleton.length; i++) {
      if (included.contains(skeleton.parents[i])) included.add(i);
    }
    return BoneMask({
      for (final i in included) skeleton.names[i]: weight,
    }, target: target);
  }

  final String target;
  final Map<String, double> weights;

  double weightOf(String target, String bone) =>
      target == this.target ? weights[bone] ?? 0 : 0;
}

/// The full local rest pose of each target's skeleton.
ClipFrame restFrame(Map<String, RestSkeleton> skeletons) {
  final out = ClipFrame(0);
  for (final MapEntry(:key, :value) in skeletons.entries) {
    out.bones[key] = {
      for (var i = 0; i < value.length; i++)
        value.names[i]: BoneLocal(
          position: value.positionOf(i),
          rotation: value.rotationOf(i),
          scale: value.scaleOf(i),
        ),
    };
  }
  return out;
}

/// A complete pose, with unauthored channels filled by [rest].
ClipFrame completeFrame(ClipFrame frame, ClipFrame rest) {
  final out = ClipFrame.mix([rest], [1], at: frame.at);
  for (final MapEntry(:key, :value) in frame.values.entries) {
    (out.values[key] ??= {}).addAll({
      for (final MapEntry(:key, :value) in value.entries)
        key: _cloneValue(value),
    });
  }
  for (final MapEntry(:key, :value) in frame.bones.entries) {
    final into = out.bones[key] ??= {};
    for (final MapEntry(key: bone, value: local) in value.entries) {
      final base = into[bone];
      into[bone] = BoneLocal(
        position: (local.position ?? base?.position)?.clone(),
        rotation: (local.rotation ?? base?.rotation)?.clone(),
        scale: (local.scale ?? base?.scale)?.clone(),
      );
    }
  }
  return out;
}

Object _cloneValue(Object value) => switch (value) {
  final Vector3 vector => vector.clone(),
  final Quaternion rotation => rotation.clone(),
  _ => value,
};

/// Replaces authored overlay channels in proportion to [weight] and [mask].
ClipFrame layerFrame(
  ClipFrame base,
  ClipFrame overlay, {
  required ClipFrame rest,
  BoneMask? mask,
  double weight = 1,
}) {
  _checkWeight(weight);
  final out = completeFrame(base, rest);
  if (mask == null) {
    for (final MapEntry(:key, :value) in overlay.values.entries) {
      final into = out.values[key] ??= {};
      for (final MapEntry(key: property, :value) in value.entries) {
        final before = into[property];
        if (before != null) into[property] = _mixValue(before, value, weight);
        if (before == null && weight > 0) into[property] = _cloneValue(value);
      }
    }
  }
  for (final MapEntry(:key, :value) in overlay.bones.entries) {
    final into = out.bones[key] ??= {};
    for (final MapEntry(key: bone, value: local) in value.entries) {
      final say = weight * (mask?.weightOf(key, bone) ?? 1);
      if (say <= 0) continue;
      into[bone] = _layerLocal(into[bone] ?? BoneLocal(), local, say);
    }
  }
  return out;
}

BoneLocal _layerLocal(BoneLocal base, BoneLocal overlay, double weight) =>
    BoneLocal(
      position: _mixPart(base.position, overlay.position, weight) as Vector3?,
      rotation:
          _mixPart(base.rotation, overlay.rotation, weight) as Quaternion?,
      scale: _mixPart(base.scale, overlay.scale, weight) as Vector3?,
    );

Object? _mixPart(Object? base, Object? overlay, double weight) =>
    overlay == null
    ? base
    : base == null
    ? _cloneValue(overlay)
    : _mixValue(base, overlay, weight);

Object _mixValue(Object base, Object overlay, double weight) =>
    switch ((base, overlay)) {
      (final Vector3 a, final Vector3 b) => vector3Mixer.mix(
        [a, b],
        [1 - weight, weight],
      ),
      (final Quaternion a, final Quaternion b) => quaternionMixer.mix(
        [a, b],
        [1 - weight, weight],
      ),
      (final double a, final double b) => a + (b - a) * weight,
      _ => weight >= 0.5 ? overlay : base,
    };

/// Adds the overlay's local bone difference from [reference] to [base].
///
/// [rest] fills missing base channels. The reference defaults to rest.
/// Rotations compose on the right, and scales use a ratio to the reference.
ClipFrame addFrame(
  ClipFrame base,
  ClipFrame overlay, {
  required ClipFrame rest,
  ClipFrame? reference,
  BoneMask? mask,
  double weight = 1,
}) {
  _checkWeight(weight);
  final out = completeFrame(base, rest);
  final against = completeFrame(reference ?? rest, rest);
  if (mask == null) _addValues(out, overlay, against, weight);
  for (final MapEntry(:key, :value) in overlay.bones.entries) {
    final into = out.bones[key] ??= {};
    for (final MapEntry(key: bone, value: local) in value.entries) {
      final say = weight * (mask?.weightOf(key, bone) ?? 1);
      final ref = against.boneOf(key, bone);
      if (say <= 0 || ref == null) continue;
      into[bone] = _addLocal(into[bone] ?? ref, local, ref, say);
    }
  }
  return out;
}

void _addValues(
  ClipFrame out,
  ClipFrame overlay,
  ClipFrame reference,
  double weight,
) {
  for (final MapEntry(:key, :value) in overlay.values.entries) {
    final into = out.values[key];
    if (into == null) continue;
    for (final MapEntry(key: property, :value) in value.entries) {
      final base = into[property];
      final rest = reference.valueOf(key, property);
      if (base != null && rest != null) {
        into[property] = _addValue(base, value, rest, weight);
      }
    }
  }
}

Object _addValue(
  Object base,
  Object overlay,
  Object reference,
  double weight,
) => switch ((base, overlay, reference)) {
  (final double a, final double b, final double r) => a + (b - r) * weight,
  (final Vector3 a, final Vector3 b, final Vector3 r) => a + (b - r) * weight,
  (final Quaternion a, final Quaternion b, final Quaternion r) =>
    (a *
          quaternionMixer.mix(
            [Quaternion.identity(), r.conjugated() * b],
            [1 - weight, weight],
          ))
      ..normalize(),
  _ => base,
};

BoneLocal _addLocal(
  BoneLocal base,
  BoneLocal overlay,
  BoneLocal reference,
  double weight,
) => BoneLocal(
  position: _addPart(
    base.position,
    overlay.position,
    reference.position,
    weight,
  ),
  rotation: _addPart(
    base.rotation,
    overlay.rotation,
    reference.rotation,
    weight,
  ),
  scale: _addScale(base.scale, overlay.scale, reference.scale, weight),
);

T? _addPart<T extends Object>(
  T? base,
  T? overlay,
  T? reference,
  double weight,
) {
  if (base == null || overlay == null || reference == null) return base;
  return _addValue(base, overlay, reference, weight) as T;
}

Vector3? _addScale(
  Vector3? base,
  Vector3? overlay,
  Vector3? reference,
  double weight,
) {
  final scale = base?.clone();
  if (scale != null && overlay != null && reference != null) {
    for (var i = 0; i < 3; i++) {
      final divisor = reference[i];
      if (divisor != 0) {
        scale[i] *= 1 + (overlay[i] / divisor - 1) * weight;
      } else {
        scale[i] += overlay[i] * weight;
      }
    }
  }
  return scale;
}

void _checkWeight(double weight) {
  if (!weight.isFinite || weight < 0 || weight > 1) {
    throw ArgumentError.value(weight, 'weight', 'Between zero and one.');
  }
}

/// A one-shot over a moving pose. Its clock is an explicit [LayerPlace].
final class ClipLayer {
  ClipLayer(
    this.clip, {
    this.mask,
    this.fadeIn = 0.1,
    this.fadeOut = 0.1,
    this.influence = 1,
    this.additive = false,
    ClipFrame? reference,
  }) : _reference = reference == null
           ? null
           : completeFrame(reference, ClipFrame(0)) {
    _checkWeight(influence);
    for (final fade in [fadeIn, fadeOut]) {
      if (!fade.isFinite || fade < 0) {
        throw ArgumentError.value(fade, 'fade', 'A finite time at least zero.');
      }
    }
  }

  final ClipDocument clip;
  final BoneMask? mask;
  final double fadeIn;
  final double fadeOut;
  final double influence;
  final bool additive;
  final ClipFrame? _reference;

  double weightAt(LayerPlace place) {
    if (place.at >= clip.duration || clip.duration <= 0) return 0;
    final into = fadeIn > 0 ? place.at / fadeIn : 1.0;
    final out = fadeOut > 0 ? (clip.duration - place.at) / fadeOut : 1.0;
    return math.min(into, out).clamp(0.0, 1.0) * influence;
  }

  ClipFrame sampleAt(
    ClipFrame base,
    LayerPlace place, {
    required ClipFrame rest,
  }) {
    final frame = clip.sampleAt(
      place.at.clamp(0.0, clip.duration),
      inPlace: true,
    );
    final weight = weightAt(place);
    return additive
        ? addFrame(
            base,
            frame,
            rest: rest,
            reference: _reference,
            mask: mask,
            weight: weight,
          )
        : layerFrame(base, frame, rest: rest, mask: mask, weight: weight);
  }

  LayerStep advance(LayerPlace place, double seconds) {
    final step = seconds.isFinite && seconds > 0 ? seconds : 0.0;
    final at = math.min(place.at + step, clip.duration);
    return LayerStep(
      place: LayerPlace(at),
      marks: at > place.at
          ? clip.marksBetween(place.at == 0 ? -1e-9 : place.at, at).toList()
          : const [],
      finished: at >= clip.duration,
    );
  }
}

/// Seconds through a one-shot. Save beside the graph's place.
final class LayerPlace {
  LayerPlace([this.at = 0]) {
    if (!at.isFinite || at < 0) throw ArgumentError.value(at, 'at');
  }

  final double at;

  Map<String, Object?> toJson() => {'at': at};

  static LayerPlace? fromJson(Object? raw) {
    if (raw is! Map<String, Object?>) return null;
    final at = raw['at'];
    return at is num && at.isFinite && at >= 0
        ? LayerPlace(at.toDouble())
        : null;
  }
}

final class LayerStep {
  LayerStep({
    required this.place,
    required List<Mark> marks,
    required this.finished,
  }) : marks = List.unmodifiable(marks);

  final LayerPlace place;
  final List<Mark> marks;
  final bool finished;
}
