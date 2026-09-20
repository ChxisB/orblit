part of 'runner.dart';

// A run in progress: moving the runner, and what happens when it
// meets something.

extension _Play on RunnerExample {
  void _run(double dt) {
    _runTime += dt;
    _intro = math.min(1, _intro + dt / 0.9);
    _slow = math.min(1, _slow + dt * 0.25);
    _speed =
        (topSpeed - (topSpeed - _startSpeed) * math.exp(-_runTime / 110)) *
        _slow;

    if (autopilot) _drive();

    _prevAlong = _along;
    _prevX = _x;
    _along += _speed * dt;
    _ran = _along;
    _x += (_lanes[_lane] - _x) * (1 - math.exp(-dt * 14));

    if (!_grounded) {
      _vy -= _gravity * dt;
      _y += _vy * dt;
      if (_y <= 0) {
        _y = 0;
        _impact = math.min(1, -_vy / 18);
        _vy = 0;
        _landedAt = _clock;
        if (_slideQueued) {
          _slideQueued = false;
          _slideLeft = _slideTime;
        } else if (_jumpBuffer > 0) {
          _vy = _jumpSpeed;
        }
      }
    }
    _jumpBuffer = math.max(0, _jumpBuffer - dt);
    _slideLeft = math.max(0, _slideLeft - dt);
    _step += dt * (8 + _speed * 0.5);

    _collide();
    if (_phase != RunnerPhase.running) return;
    _collect();
    _plan();
    _forget();

    if (_along - _origin > _rebase) {
      _origin += _rebase;
      for (final spark in _sparks) {
        spark.at.z += _rebase;
      }
    }
  }

  /// After a crash: thrown up, turned over, and brought to a stop on the
  /// ground.
  void _tumble(double dt) {
    _speed *= math.exp(-dt * 6);
    _along += _speed * dt;
    _prevAlong = _along;
    if (!_grounded) {
      _vy -= _gravity * dt;
      _y = math.max(0, _y + _vy * dt);
      _spin += dt * 9;
      if (_y <= 0) _vy = _vy.abs() > 3 ? -_vy * 0.3 : 0;
    } else {
      // Settled onto whichever side it came down nearest.
      const quarter = math.pi / 2;
      final rest = (_spin / quarter).round() * quarter;
      _spin += (rest - _spin) * (1 - math.exp(-dt * 10));
    }
  }

  // ---- hitting things ----

  void _collide() {
    final back = _prevAlong - _halfDepth;
    final front = _along + _halfDepth;
    for (final hazard in _hazards) {
      if (hazard.struck) continue;
      if (hazard.near > front || hazard.far < back) continue;
      final laneX = _lanes[hazard.lane];
      if ((_x - laneX).abs() >= _reach) continue;
      if (!_blockedBy(hazard)) continue;

      // Come at from the side — it was already alongside, and the runner
      // was not in its lane — is a stumble and a bounce back, which is the
      // genre's forgiveness for a lane change a moment too early. Anything
      // else is running into it.
      final alongside = hazard.near < _prevAlong + _halfDepth;
      final beside = (_prevX - laneX).abs() >= _reach;
      if (alongside && beside) {
        if (_clock - hazard.grazedAt < 0.6) continue;
        hazard.grazedAt = _clock;
        _lane = (_x < laneX ? hazard.lane - 1 : hazard.lane + 1).clamp(0, 2);
        _stumble(hazard);
      } else if (invincible) {
        hazard.struck = true;
        _stumble(hazard);
      } else {
        _crash(hazard);
      }
      if (_phase != RunnerPhase.running) return;
    }
  }

  bool _blockedBy(_Hazard hazard) => switch (hazard.kind) {
    _Kind.hurdle => _y < _hurdleClear,
    _Kind.bar => _y + (_isSliding ? _sliding : _standing) > _barBottom,
    _Kind.container => true,
  };

  void _stumble(_Hazard hazard) {
    // Twice in quick succession is once too often.
    if (!invincible && _clock - _stumbledAt < 4) {
      _crash(hazard);
      return;
    }
    _stumbledAt = _clock;
    _slow = 0.7;
    _shake = 0.45;
    _streak = 0;
  }

  void _crash(_Hazard hazard) {
    _phase = RunnerPhase.over;
    _crashedAt = _clock;
    _newBest = score > _best;
    _best = math.max(_best, score);
    _shake = 0.8;
    _vy = 6.5;
    _y = math.max(_y, 0.01);
    _spin = 0;
    _slideLeft = 0;
    // A hurdle trips you over itself; a bar or the end of a container is a
    // wall, and you come off it backwards rather than through it.
    if (hazard.kind != _Kind.hurdle) {
      _speed = -3;
      _along = math.min(_along, hazard.near - _halfDepth);
    }
  }

  void _collect() {
    final from = _prevAlong - 0.9;
    final to = _along + 0.9;
    for (final coin in _road) {
      if (coin.takenAt != null) continue;
      if (coin.along < from || coin.along > to) continue;
      if ((_lanes[coin.lane] - _x).abs() > 0.9) continue;
      if ((coin.height - (_y + 0.7)).abs() > 1.0) continue;

      coin.takenAt = _clock;
      _coins++;
      // One after another is worth more each time, which is what makes a
      // line of them worth following rather than merely worth having.
      if (_clock - _lastCoinAt < 0.8) {
        _streak++;
      } else {
        _streak = 0;
        _popup = 0;
      }
      final worth = 1 + _streak;
      _bonus += worth;
      _popup += worth;
      _lastCoinAt = _clock;
      for (var i = 0; i < 4; i++) {
        _spark(Vector3(_lanes[coin.lane], coin.height, _z(coin.along)));
      }
    }
  }
}
