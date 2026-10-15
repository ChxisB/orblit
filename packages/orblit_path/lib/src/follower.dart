import 'dart:math' as math;

import 'package:vector_math/vector_math_64.dart';

import 'spline.dart';

/// What something does on reaching the end of a path.
enum PathEnd {
  /// Stays there.
  stop,

  /// Goes back to the start and runs it again.
  loop,

  /// Turns round and runs it the other way, and again.
  pingPong,
}

/// Something riding a [Spline] at a speed, a distance at a time.
final class PathFollower {
  PathFollower(
    this.spline, {
    this.speed = 4,
    this.end = PathEnd.stop,
    double distance = 0,
  }) : _distance = distance.clamp(0.0, spline.length);

  final Spline spline;

  /// Metres a second. Negative runs the path backwards.
  double speed;

  final PathEnd end;

  double _distance;
  double _heading = 1;

  /// How far along the spline it is, in metres.
  double get distance => _distance;

  /// Whether a follower that stops has reached the end it is heading for.
  bool get isFinished {
    if (end != PathEnd.stop) return false;
    final goal = speed * _heading >= 0 ? spline.length : 0.0;
    return (_distance - goal).abs() < 1e-9;
  }

  PathFrame get frame => spline.frameAt(_distance);

  /// Rides on for [seconds].
  void advance(double seconds) {
    final length = spline.length;
    if (length <= 0 || seconds <= 0) return;
    var next = _distance + speed * _heading * seconds;
    switch (end) {
      case PathEnd.stop:
        next = next.clamp(0.0, length);
      case PathEnd.loop:
        next = _wrapped(next, length);
      case PathEnd.pingPong:
        next = _bounced(next, length);
    }
    _distance = next;
  }

  static double _wrapped(double value, double length) {
    final wrapped = value % length;
    return wrapped < 0 ? wrapped + length : wrapped;
  }

  /// Folds [value] back off each end, flipping the heading for every fold.
  double _bounced(double value, double length) {
    var at = value;
    while (at < 0 || at > length) {
      _heading = -_heading;
      at = at < 0 ? -at : 2 * length - at;
    }
    return at;
  }
}

/// A slider on a [Spline] that follows a target by sliding to the nearest
/// point of the curve: a camera on a dolly track, a cart on a line.
final class Rail {
  Rail(this.spline, {this.speed = 6, double distance = 0})
    : _distance = distance.clamp(0.0, spline.length);

  final Spline spline;

  /// How fast it can slide, in metres a second.
  double speed;

  double _distance;

  double get distance => _distance;

  Vector3 get position => spline.positionAt(_distance);

  /// Slides towards the point of the rail nearest [target], for [seconds].
  /// A closed rail goes whichever way round is shorter.
  void track(Vector3 target, double seconds) {
    if (seconds <= 0) return;
    final goal = spline.closest(target).distance;
    var gap = goal - _distance;
    final length = spline.length;
    if (spline.closed && length > 0) {
      gap = (gap + length / 2) % length - length / 2;
    }
    final most = speed * seconds;
    final move = gap.clamp(-most, most);
    _distance = spline.closed && length > 0
        ? _distance + move
        : math.max(0.0, math.min(length, _distance + move));
  }
}
