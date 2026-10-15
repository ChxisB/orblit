import 'dart:math' as math;

import 'package:orblit_sequence/orblit_sequence.dart';
import 'package:vector_math/vector_math_64.dart';

import 'clip.dart';

/// [clip] with the keys that its straight lines do not need taken out.
///
/// A baked clip, such as a retarget that could not keep the source's keys,
/// has a key at every moment any bone had one. Most of them sit on the line
/// between their neighbours. A key is dropped when the channel, joined
/// straight between the keys kept, is within tolerance of it. The first and
/// last key of a channel stay.
///
/// [angle] is the most a rotation may be turned, in radians. [distance] is
/// the most a vector or a number may be off, in the channel's own unit,
/// which is metres for a bone's position. Both default to a twentieth of a
/// degree and half a millimetre.
///
/// Only channels whose keys are all straight lines are reduced. Eased and
/// curved channels are kept as they are, because taking a key out of one
/// changes the curve across the keys next to it, not only at the key. A
/// channel with a cut, two keys at one time, is kept as it is too.
ClipDocument reduceKeys(
  ClipDocument clip, {
  double angle = 5e-4,
  double distance = 5e-4,
}) => clip.copyWith(
  channels: [
    for (final channel in clip.channels) _thin(channel, angle, distance),
  ],
);

ClipChannel<Object> _thin(
  ClipChannel<Object> channel,
  double angle,
  double distance,
) {
  if (channel is ClipChannel<Quaternion>) {
    return _thinned<Quaternion>(channel, _turnedBetween, angle);
  }
  if (channel is ClipChannel<Vector3>) {
    return _thinned<Vector3>(channel, (a, b) => a.distanceTo(b), distance);
  }
  if (channel is ClipChannel<double>) {
    return _thinned<double>(channel, (a, b) => (a - b).abs(), distance);
  }
  return channel;
}

double _turnedBetween(Quaternion a, Quaternion b) {
  final along = (a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w).abs();
  return 2 * math.acos(along.clamp(0.0, 1.0));
}

bool _straight<T extends Object>(List<Key<T>> keys) {
  for (var i = 0; i < keys.length - 1; i++) {
    final key = keys[i];
    final plain = key.slopeIn == null && key.slopeOut == null;
    if (key.hold != Hold.linear || !plain || key.at >= keys[i + 1].at) {
      return false;
    }
  }
  return true;
}

ClipChannel<T> _thinned<T extends Object>(
  ClipChannel<T> channel,
  double Function(T, T) apart,
  double tolerance,
) {
  final keys = channel.keys;
  if (keys.length < 3 || !_straight(keys)) return channel;
  final keep = List.filled(keys.length, false);
  keep.first = true;
  keep.last = true;

  // The key farthest from the line between two kept keys is kept, and the
  // spans either side are looked at again. Nothing farther than [tolerance]
  // is left out, and between two kept keys the channel is a straight line,
  // so the keys are where it could be off.
  void span(int from, int to) {
    var worst = -1;
    var most = tolerance;
    final seconds = keys[to].at - keys[from].at;
    for (var at = from + 1; at < to; at++) {
      final along = (keys[at].at - keys[from].at) / seconds;
      final line = channel.kind.mixer.lerp(
        keys[from].value,
        keys[to].value,
        along,
      );
      final off = apart(line, keys[at].value);
      if (off > most) {
        most = off;
        worst = at;
      }
    }
    if (worst < 0) return;
    keep[worst] = true;
    span(from, worst);
    span(worst, to);
  }

  span(0, keys.length - 1);
  return channel.withKeys([
    for (var i = 0; i < keys.length; i++)
      if (keep[i]) keys[i],
  ]);
}
