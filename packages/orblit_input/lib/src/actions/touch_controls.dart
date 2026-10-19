import 'dart:collection';

import 'package:vector_math/vector_math_64.dart';

import '../pad.dart';

/// What an on-screen interface says its player is doing.
///
/// A game fills one from the events of the `stick`, `touch-button` and
/// `swipe-pad` elements of `orblit_ui`, and gives what it holds to
/// [FrameControls]. The two packages never import each other. They agree on
/// the shape of a payload, and a payload of any other shape is ignored,
/// because it came from a script that was being edited.
///
/// Name each element once. Two buttons called `jump` share one slot, so
/// lifting one finger releases the other.
///
/// ```dart
/// onEvent: (handler, payload) => switch (handler) {
///   'move' => touch.moveStick(PadStick.left, payload),
///   'jump' => touch.hold('jump', payload),
///   'look' => touch.swipe(payload),
///   _ => null,
/// },
/// ```
final class TouchControls {
  final Set<String> _held = {};
  final Map<PadStick, Vector2> _sticks = {};
  final Vector2 _swiped = Vector2.zero();

  /// The regions being touched now. A live view, so one object does for every
  /// frame.
  late final Set<String> held = UnmodifiableSetView(_held);

  /// The sticks being pushed now, by the pad stick each stands in for. A stick
  /// at rest has no entry. Positive is up and to the right.
  late final Map<PadStick, Vector2> sticks = UnmodifiableMapView(_sticks);

  /// How far fingers have moved across a swipe pad since [endFrame]. Positive
  /// is up and to the right, in logical pixels.
  Vector2 get swiped => _swiped.clone();

  /// Presses or releases [region] for a payload of `true` or `false`.
  void hold(String region, Object? payload) {
    if (payload is! bool) return;
    payload ? _held.add(region) : _held.remove(region);
  }

  /// Puts [which] where a payload of `[x, y]` says, each from -1 to 1. A push
  /// past the edge of the circle is brought back to it.
  void moveStick(PadStick which, Object? payload) {
    final point = _pair(payload);
    if (point == null) return;
    if (point.length2 > 1) point.normalize();
    if (point.length2 == 0) {
      _sticks.remove(which);
    } else {
      _sticks[which] = point;
    }
  }

  /// Adds a payload of `[dx, dy]` to [swiped].
  void swipe(Object? payload) {
    final point = _pair(payload);
    if (point != null) _swiped.add(point);
  }

  /// Forgets the swipes, once the frame has used them.
  void endFrame() => _swiped.setZero();
}

/// The two numbers of a payload, or null for anything else.
Vector2? _pair(Object? payload) {
  if (payload is! List || payload.length != 2) return null;
  final [x, y] = payload;
  if (x is! num || y is! num || !x.isFinite || !y.isFinite) return null;
  return Vector2(x.toDouble(), y.toDouble());
}
