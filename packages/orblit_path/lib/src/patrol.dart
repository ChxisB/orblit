import 'package:vector_math/vector_math_64.dart';

import 'follower.dart';

/// A place to go and how long to wait there.
final class PatrolStop {
  PatrolStop(Vector3 at, {this.wait = 0}) : _at = at.clone() {
    if (wait < 0 || !wait.isFinite) {
      throw ArgumentError.value(wait, 'wait', 'Must be zero or more.');
    }
  }

  final Vector3 _at;

  Vector3 get at => _at.clone();

  /// Seconds to stay once there.
  final double wait;
}

/// Which stop an agent is heading for, and how long it has waited at the
/// last one. The agent does the walking; this says where to.
final class Patrol {
  Patrol(List<PatrolStop> stops, {this.end = PathEnd.loop, this.reached = 0.5})
    : stops = List.unmodifiable(stops) {
    if (this.stops.isEmpty) {
      throw ArgumentError.value(stops, 'stops', 'A patrol needs a stop.');
    }
  }

  final List<PatrolStop> stops;

  /// What happens after the last stop: [PathEnd.stop] ends the patrol, loop
  /// goes back to the first, and pingPong goes back through them.
  final PathEnd end;

  /// How close counts as being there, in metres.
  final double reached;

  int _index = 0;
  int _heading = 1;
  double _waited = 0;
  bool _finished = false;

  /// The stop it is heading for, or null once a patrol that ends is over.
  PatrolStop? get stop => _finished ? null : stops[_index];

  Vector3? get target => stop?.at;

  /// Whether it has arrived and is standing out the stop's wait.
  bool get isWaiting => _waited > 0;

  bool get isFinished => _finished;

  /// Tells the patrol where the agent is, after [seconds] of walking. At a
  /// stop it counts its wait down, and moves to the next when that is over.
  void update(Vector3 position, double seconds) {
    final current = stop;
    if (current == null) return;
    if (position.distanceTo(current.at) > reached) {
      _waited = 0;
      return;
    }
    _waited += seconds;
    if (_waited >= current.wait) _advance();
  }

  void _advance() {
    _waited = 0;
    final last = stops.length - 1;
    final next = _index + _heading;
    if (next >= 0 && next <= last) {
      _index = next;
      return;
    }
    switch (end) {
      case PathEnd.stop:
        _finished = true;
      case PathEnd.loop:
        _index = next < 0 ? last : 0;
      case PathEnd.pingPong:
        _heading = -_heading;
        _index = (_index + _heading).clamp(0, last);
    }
  }
}
