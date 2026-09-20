part of 'runner.dart';

// What the road ahead is made of. Rows of hazards and lines of
// coins are planned a chunk at a time, ahead of where the runner is.

extension _Hazards on RunnerExample {
  /// Lays rows of obstacles and coins until the road is full to the edge of
  /// the world.
  ///
  /// A row is made as it comes into view rather than all at the start,
  /// because how far apart rows should be depends on how fast the runner is
  /// going when it gets to them — and because a run that never ends cannot
  /// be laid out in advance.
  void _plan() {
    while (_nextRow < _along + _ahead) {
      _row();
    }
  }

  void _row() {
    final at = _nextRow;
    final difficulty = ((at - 60) / 1500).clamp(0.0, 1.0);
    final speed = math.max(_speed, _startSpeed);
    final roll = _chance.nextDouble();
    var length = 0.0;

    if (roll < 0.12) {
      // A breather: nothing but coins.
      length = _coinLine(_chance.nextInt(3), at, 5 + _chance.nextInt(4));
    } else if (roll < 0.45) {
      // One thing in one lane.
      final lane = _chance.nextInt(3);
      final kind = _anyKind();
      length = _put(kind, lane, at);
      if (kind == _Kind.hurdle && _chance.nextBool()) {
        _coinArc(lane, at, speed);
      } else {
        _coinLine((lane + 1 + _chance.nextInt(2)) % 3, at - 4, 6);
      }
    } else if (roll < 0.72) {
      // Two taken, one free, and the free one paid for.
      final free = _chance.nextInt(3);
      for (var lane = 0; lane < 3; lane++) {
        if (lane != free) length = math.max(length, _put(_anyKind(), lane, at));
      }
      _coinLine(free, at - 5, 6);
    } else if (roll < 0.72 + (difficulty > 0.3 ? 0.1 + difficulty * 0.15 : 0)) {
      // All three, and at least one of them something you can get over or
      // under rather than round.
      final open = _chance.nextInt(3);
      for (var lane = 0; lane < 3; lane++) {
        final kind = lane == open
            ? (_chance.nextBool() ? _Kind.hurdle : _Kind.bar)
            : _anyKind();
        length = math.max(length, _put(kind, lane, at));
      }
    } else {
      // A train of containers, in one lane or two.
      final lane = _chance.nextInt(3);
      final cars = difficulty > 0.2 && _chance.nextBool() ? 2 : 1;
      length = _train(lane, at, cars);
      var free = (lane + 1 + _chance.nextInt(2)) % 3;
      if (_chance.nextDouble() < 0.3 + difficulty * 0.3) {
        final other = free;
        free = 3 - lane - other;
        final offset = _chance.nextDouble() * 8;
        length = math.max(length, offset + _train(other, at + offset, 1));
      }
      _coinLine(free, at, 1 + (length / 2.5).floor());
    }

    final gap = speed * (0.9 + _chance.nextDouble() * 0.6 - 0.25 * difficulty);
    _nextRow = at + length + math.max(14.0, gap);
  }

  _Kind _anyKind() {
    final pick = _chance.nextDouble();
    return pick < 0.4
        ? _Kind.hurdle
        : pick < 0.7
        ? _Kind.bar
        : _Kind.container;
  }

  /// Puts one [kind] in [lane] at [at], and says how far along it reaches.
  double _put(_Kind kind, int lane, double at) {
    if (kind == _Kind.container) return _train(lane, at, 1);
    _hazards.add(_Hazard(kind, lane, at - 0.1, at + 0.1, _hazardKey(kind)));
    return 0.1;
  }

  double _train(int lane, double at, int cars) {
    const gap = 0.6;
    for (var car = 0; car < cars; car++) {
      final near = at + car * (RunnerArt.containerLength + gap);
      _hazards.add(
        _Hazard(
          _Kind.container,
          lane,
          near,
          near + RunnerArt.containerLength,
          _hazardKey(_Kind.container),
        ),
      );
    }
    return cars * RunnerArt.containerLength + (cars - 1) * gap;
  }

  int _hazardKey(_Kind kind) => 2000 + kind.index * 200 + _hazardCount++ % 200;

  double _coinLine(int lane, double from, int count) {
    for (var i = 0; i < count; i++) {
      _road.add(_Coin(lane, from + i * 2.5, 0.9, 3000 + _coinCount++ % 600));
    }
    return (count - 1) * 2.5;
  }

  /// Five coins along the path of a jump that clears a hurdle at [at] — so
  /// the coins are also the advice.
  void _coinArc(int lane, double at, double speed) {
    const time = 2 * _jumpSpeed / _gravity;
    for (var i = 0; i < 5; i++) {
      final t = i / 4 * time;
      final along = at + (i / 4 - 0.5) * time * speed;
      final height = 0.9 + _jumpSpeed * t - 0.5 * _gravity * t * t;
      _road.add(_Coin(lane, along, height, 3000 + _coinCount++ % 600));
    }
  }

  /// Lets go of what is well behind.
  void _forget() {
    _hazards.removeWhere((hazard) => hazard.far < _along - 30);
    _road.removeWhere((coin) => coin.along < _along - 30);
  }
}

enum _Kind { hurdle, bar, container }

class _Hazard {
  _Hazard(this.kind, this.lane, this.near, this.far, this.key);

  final _Kind kind;
  final int lane;

  /// Where along the road it starts and ends.
  final double near;
  final double far;
  final int key;

  /// Run through, with crashing turned off, and no longer in the way.
  bool struck = false;
  double grazedAt = -100;
}

class _Coin {
  _Coin(this.lane, this.along, this.height, this.key);

  final int lane;
  final double along;
  final double height;
  final int key;
  double? takenAt;
}
