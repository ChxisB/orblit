part of 'runner.dart';

// The runner played by nobody. It is the fairness test: a game
// the autopilot cannot survive is one a person cannot either.

extension _Autopilot on RunnerExample {
  /// Plays: picks the best lane it can get to, and jumps or slides for
  /// whatever is in the one it is in.
  ///
  /// Only what a player could see, and only the three moves a player has —
  /// it presses the same buttons, so what it shows is the game working, not
  /// something that looks like it.
  void _drive() {
    final speed = _speed;
    final look = speed * 1.1 + 6;
    final worth = [0.0, 0.0, 0.0];
    for (final hazard in _hazards) {
      if (hazard.struck || hazard.far < _along - 0.5) continue;
      if (hazard.near - _along > look) continue;
      // A container is a wall: nothing gets past one, and a lane with one in
      // it is worth nothing at any price. A hurdle and a bar are not walls.
      // They are cleared, over and under, at no cost but the timing, so they
      // count for very little here -- enough to prefer an empty lane when
      // there is nothing else to choose between them, and not nearly enough
      // to give up a lane of coins for.
      worth[hazard.lane] -= hazard.kind == _Kind.container ? 100 : 0.12;
    }
    for (final coin in _road) {
      final ahead = coin.along - _along;
      if (coin.takenAt == null && ahead > 0 && ahead < look) {
        worth[coin.lane] += 0.4;
      }
    }
    worth[_lane] += 0.5;
    worth[1] += 0.1;

    var wanted = _lane;
    for (var lane = 0; lane < 3; lane++) {
      if (worth[lane] > worth[wanted]) wanted = lane;
    }
    final settled = (_x - _lanes[_lane]).abs() < 0.3;

    // Whatever is being cleared in this lane goes on being cleared. Leaving
    // halfway through looks like running away from a hurdle rather than over
    // it, and it lands the runner in whatever the next lane was holding.
    final clearing =
        !_grounded ||
        _isSliding ||
        _hazards.any(
          (hazard) =>
              !hazard.struck &&
              hazard.kind != _Kind.container &&
              hazard.lane == _lane &&
              hazard.near - _along > 0 &&
              hazard.near - _along < speed * 1.4 + 2,
        );
    // Unless it is a container that is being left, which is the one thing
    // worth breaking off a jump for, because it cannot be jumped.
    final fleeing = worth[_lane] < -50;

    if (wanted != _lane && settled && (fleeing || !clearing)) {
      final next = _lane + (wanted > _lane ? 1 : -1);
      if (!_walled(next, speed)) steer(next - _lane);
    }

    for (final hazard in _hazards) {
      if (hazard.struck) continue;
      if (hazard.lane != _lane && (_x - _lanes[hazard.lane]).abs() >= _reach) {
        continue;
      }
      final ahead = hazard.near - _along;
      if (ahead < 0) continue;
      switch (hazard.kind) {
        case _Kind.hurdle when ahead <= speed * 0.28 + 0.5 && _grounded:
          jump();
        case _Kind.bar when ahead <= speed * 0.35 + 0.5 && !_isSliding:
          slide();
        default:
          break;
      }
    }
  }

  /// Whether moving into [lane] now would be moving into trouble: a
  /// container alongside it, or a hurdle or bar too close to get over or
  /// under once there.
  bool _walled(int lane, double speed) {
    for (final hazard in _hazards) {
      if (hazard.lane != lane || hazard.struck) continue;
      // Room enough to be in the lane before it, and for a hurdle, to be
      // high enough by then: a jump takes a tenth of a second to clear one.
      // A slide starts the moment it is asked for, so a bar needs little.
      final clearance = switch (hazard.kind) {
        _Kind.container => speed * 0.3 + 2,
        _Kind.hurdle => speed * 0.25 + 1,
        _Kind.bar => speed * 0.1 + 1,
      };
      if (hazard.near - 1 < _along + clearance && hazard.far > _along - 1) {
        return true;
      }
    }
    return false;
  }
}
