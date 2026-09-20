part of 'runner.dart';

// The world laid in chunks as the runner reaches them, and taken
// up again behind.

extension _Chunks on RunnerExample {
  void _reset() {
    _runs++;
    // A different run every time, and the same runs in the same order every
    // time the game is opened — which is what lets a test say anything.
    _chance = math.Random(_runs * 7919 + 1);
    _along = _prevAlong = _ran = 0;
    _origin = 0;
    _runTime = 0;
    _speed = _startSpeed;
    _slow = 1;
    _lane = 1;
    _x = _prevX = 0;
    _y = _vy = 0;
    _slideLeft = 0;
    _slideQueued = false;
    _jumpBuffer = 0;
    _stumbledAt = -100;
    _spin = 0;
    _coins = 0;
    _bonus = 0;
    _streak = 0;
    _popup = 0;
    _lastCoinAt = -100;
    _newBest = false;
    _hazards.clear();
    _road.clear();
    _nextRow = 60;
    _readySince = _clock;
    for (final spark in _sparks) {
      spark.life = 0;
    }
    _plan();
  }

  // ---- the world ----

  double _z(double along) => -(along - _origin);

  /// Makes the files and hands them to the renderer, once.
  void _prepare() {
    if (_art != null) return;
    final art = _art = RunnerArt.build();
    void failed(Object error) {
      note = "The runner's models could not be handed to the renderer: $error";
    }

    try {
      Future.wait([
        for (final file in art.files.entries)
          OrblitResources.provide(file.key, file.value),
      ]).then<void>((_) => _artReady = true, onError: failed);
    } on Object catch (error) {
      failed(error);
    }
  }

  void _laySceneryAround() {
    final first = (_along / _chunk).floor() - _behind;
    for (var chunk = first; chunk < first + _chunks; chunk++) {
      final slot = chunk % _chunks;
      if (_slotChunk[slot] == chunk && _slotOrigin[slot] == _origin) continue;
      _lay(slot, chunk);
    }
  }

  /// Fills [slot] with the scenery of [chunk]: the same trees and houses in
  /// the same places every time that stretch of road comes round.
  void _lay(int slot, int chunk) {
    _slotChunk[slot] = chunk;
    _slotOrigin[slot] = _origin;
    final chance = math.Random(chunk * 92821 + 7);
    final start = chunk * _chunk;
    final props = _props[slot]..clear();
    final counts = List.filled(_caps.length, 0);
    final taken = <(double, double, double)>[];

    void put(int type, Matrix4 transform) {
      if (counts[type] >= _caps[type]) return;
      final key = 1000 + type * 100 + slot * _caps[type] + counts[type]++;
      props.add(
        OrblitObject(
          key: key,
          mesh: _sceneryMeshes[type],
          transform: transform,
          colour: _white,
          // Hills are too big and too far to shadow anything worth seeing,
          // and the shadow map has better things to spend its texels on.
          castShadows: type != 8,
        ),
      );
    }

    bool clear(double x, double along, double room) {
      for (final (tx, ta, r) in taken) {
        final dx = x - tx, da = along - ta;
        if (dx * dx + da * da < (r + room) * (r + room)) return false;
      }
      return true;
    }

    double side() => chance.nextBool() ? 1.0 : -1.0;

    // A house now and then, facing the road, and sometimes a dark one
    // across from it.
    final houseSide = side();
    if (chance.nextDouble() < 0.6) {
      final x = houseSide * (13 + chance.nextDouble() * 6);
      final along = start + 8 + chance.nextDouble() * 24;
      put(2, place(x, 0, _z(along), yaw: houseSide * math.pi / 2));
      taken.add((x, along, 5));
    }
    if (chance.nextDouble() < 0.3) {
      final x = -houseSide * (13 + chance.nextDouble() * 6);
      final along = start + 8 + chance.nextDouble() * 24;
      put(3, place(x, 0, _z(along), yaw: -houseSide * math.pi / 2));
      taken.add((x, along, 5));
    }

    // A billboard, turned a little towards whoever is coming.
    if (chance.nextDouble() < 0.3) {
      final s = side();
      final x = s * 9.5;
      final along = start + chance.nextDouble() * _chunk;
      if (clear(x, along, 3)) {
        put(7, place(x, 0, _z(along), yaw: -s * 0.35));
        taken.add((x, along, 3));
      }
    }

    // Trees, thicker near the road where they are seen going past.
    for (var i = 0; i < 16; i++) {
      final pine = chance.nextDouble() < 0.55;
      final out = chance.nextDouble();
      final x = side() * (7.5 + out * out * 37.5);
      final along = start + chance.nextDouble() * _chunk;
      final size = 0.8 + chance.nextDouble() * 0.6;
      final yaw = chance.nextDouble() * math.pi * 2;
      if (!clear(x, along, 2)) continue;
      put(
        pine ? 0 : 1,
        place(x, 0, _z(along), sx: size, sy: size, sz: size, yaw: yaw),
      );
      taken.add((x, along, 1.5));
    }

    // Bushes and rocks on the verge.
    for (var i = 0; i < 7; i++) {
      final kind = i < 3
          ? 4
          : i < 4
          ? 5
          : 6;
      final x = side() * (5.4 + chance.nextDouble() * 2.1);
      final along = start + chance.nextDouble() * _chunk;
      final size = kind == 6
          ? 0.35 + chance.nextDouble() * 0.5
          : 0.7 + chance.nextDouble() * 0.5;
      final yaw = chance.nextDouble() * math.pi * 2;
      if (chance.nextDouble() < 0.35 || !clear(x, along, 0.8)) continue;
      put(
        kind,
        place(x, -0.03, _z(along), sx: size, sy: size, sz: size, yaw: yaw),
      );
      taken.add((x, along, size));
    }

    // A hill in the distance.
    if (chance.nextDouble() < 0.7) {
      final x = side() * (90 + chance.nextDouble() * 80);
      final along = start + chance.nextDouble() * _chunk;
      final size = 35 + chance.nextDouble() * 35;
      put(8, place(x, -0.05, _z(along), sx: size, sy: size, sz: size));
    }

    _layGrass(slot, chance, start);
  }

  /// Tufts of grass along both sides of the road: blades in clumps, each a
  /// thin box leaning its own way.
  void _layGrass(int slot, math.Random chance, double start) {
    final transforms = _grass[slot];
    final colours = _grassColours[slot];
    final blade = Matrix4.identity();
    var at = 0;
    while (at < _bladesPerChunk) {
      final s = chance.nextBool() ? 1.0 : -1.0;
      final out = chance.nextDouble();
      final x = s * (4.8 + out * out * 24);
      final along = start + chance.nextDouble() * _chunk;
      final shade = chance.nextDouble();
      final count = math.min(4 + chance.nextInt(4), _bladesPerChunk - at);
      for (var i = 0; i < count; i++, at++) {
        final height = 0.2 + chance.nextDouble() * 0.28;
        final bx = x + (chance.nextDouble() - 0.5) * 0.4;
        blade
          ..setIdentity()
          ..translateByDouble(
            s * math.max(4.6, bx.abs()),
            -0.03,
            _z(along + (chance.nextDouble() - 0.5) * 0.4),
            1,
          )
          ..rotateY(chance.nextDouble() * math.pi)
          ..rotateZ((chance.nextDouble() - 0.5) * 0.7)
          ..translateByDouble(0, height / 2, 0, 1)
          ..scaleByDouble(0.045, height, 0.045, 1);
        transforms.setRange(at * 16, at * 16 + 16, blade.storage);
        // From a deep green to a sunlit yellow-green, by clump rather than
        // by blade, so a tuft reads as one plant.
        final tone = shade * 0.8 + chance.nextDouble() * 0.2;
        colours
          ..[at * 3] = 0.16 + tone * 0.2
          ..[at * 3 + 1] = 0.42 + tone * 0.2
          ..[at * 3 + 2] = 0.02 + tone * 0.03;
      }
    }
    _grassLow[slot].setValues(-30, -0.1, _z(start + _chunk) - 1);
    _grassHigh[slot].setValues(30, 0.6, _z(start) + 1);
    _grassRevision[slot]++;
  }
}
