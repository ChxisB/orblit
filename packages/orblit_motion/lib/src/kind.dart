import 'dart:math' as math;

import 'package:orblit_sequence/orblit_sequence.dart';
import 'package:vector_math/vector_math_64.dart';

/// What a channel carries: how its values mix, and how they are written.
///
/// Four of them, and the channel says which rather than the value, because a
/// file's `[0, 0, 0, 1]` is a rotation or a colour only by what it was
/// written as. A rotation is kept as a rotation, four numbers in glTF's order,
/// so it turns the short way between keys instead of spinning about three
/// axes one after another. A vector on a transform's `rotation` is also
/// allowed, and is three angles in degrees, because that is the only way to
/// say "spin twice": a rotation has no idea how many times it has been round.
sealed class ChannelKind<T extends Object> {
  const ChannelKind._(this.name);

  /// A single number: an intensity, a field of view, a weight.
  static const ChannelKind<double> number = _Number();

  /// Three numbers: a position, a scale, or angles in degrees.
  static const ChannelKind<Vector3> vector = _Vector();

  /// A rotation, which turns the short way between keys.
  static const ChannelKind<Quaternion> rotation = _Rotation();

  /// On or off, and nothing in between.
  static const ChannelKind<bool> flag = _Flag();

  static const List<ChannelKind<Object>> values = [
    number,
    vector,
    rotation,
    flag,
  ];

  /// The kind called [name] in a file, or null when it is not one.
  static ChannelKind<Object>? named(Object? name) {
    for (final kind in values) {
      if (kind.name == name) return kind;
    }
    return null;
  }

  /// What a file calls it.
  final String name;

  Mixer<T> get mixer;

  /// A value out of decoded JSON, or null when what is there is not one.
  T? read(Object? raw);

  /// A slope out of decoded JSON: how fast a value is changing, which is
  /// read like a value but is not one. The same as [read] for all but a
  /// rotation, whose slope is four numbers of any length.
  T? readSlope(Object? raw) => read(raw);

  /// A value as decoded JSON, the way [read] reads it back.
  Object write(T value);
}

class _Number extends ChannelKind<double> {
  const _Number() : super._('number');

  @override
  Mixer<double> get mixer => doubleMixer;

  @override
  double? read(Object? raw) =>
      raw is num && raw.isFinite ? raw.toDouble() : null;

  @override
  Object write(double value) => value;
}

class _Vector extends ChannelKind<Vector3> {
  const _Vector() : super._('vector');

  @override
  Mixer<Vector3> get mixer => vector3Mixer;

  @override
  Vector3? read(Object? raw) {
    final numbers = _numbers(raw, 3);
    return numbers == null ? null : Vector3.array(numbers);
  }

  @override
  Object write(Vector3 value) => [value.x, value.y, value.z];
}

class _Rotation extends ChannelKind<Quaternion> {
  const _Rotation() : super._('rotation');

  @override
  Mixer<Quaternion> get mixer => quaternionMixer;

  /// Four numbers, x, y, z and then w, as glTF writes them.
  ///
  /// Made unit length on the way in: a hand-edited file is rarely exactly
  /// one long, and a rotation that is not one also scales. One already as
  /// near as a float gets is left as written, so reading a file and writing
  /// it back changes nothing. Four noughts are no rotation at all rather
  /// than the rotation that does nothing, so they are refused rather than
  /// guessed at.
  @override
  Quaternion? read(Object? raw) {
    final numbers = _numbers(raw, 4);
    if (numbers == null) return null;
    final length = math.sqrt(numbers.fold(0.0, (sum, one) => sum + one * one));
    if (length < 1e-9) return null;
    final scale = (length - 1).abs() < 1e-6 ? 1.0 : 1 / length;
    return Quaternion(
      numbers[0] * scale,
      numbers[1] * scale,
      numbers[2] * scale,
      numbers[3] * scale,
    );
  }

  @override
  Quaternion? readSlope(Object? raw) {
    final numbers = _numbers(raw, 4);
    return numbers == null
        ? null
        : Quaternion(numbers[0], numbers[1], numbers[2], numbers[3]);
  }

  @override
  Object write(Quaternion value) => [value.x, value.y, value.z, value.w];
}

class _Flag extends ChannelKind<bool> {
  const _Flag() : super._('flag');

  @override
  Mixer<bool> get mixer => boolMixer;

  @override
  bool? read(Object? raw) => raw is bool ? raw : null;

  @override
  Object write(bool value) => value;
}

/// Exactly [count] finite numbers, or null.
///
/// All or nothing, unlike a scene's vectors, which fall back per component.
/// A key is one value at one moment, and a key with a made-up component is a
/// value nobody chose. Dropping the key leaves the curve running through the
/// ones either side of it, which is nearer what was meant.
List<double>? _numbers(Object? raw, int count) {
  if (raw is! List || raw.length != count) return null;
  final out = <double>[];
  for (final one in raw) {
    if (one is! num || !one.isFinite) return null;
    out.add(one.toDouble());
  }
  return out;
}
