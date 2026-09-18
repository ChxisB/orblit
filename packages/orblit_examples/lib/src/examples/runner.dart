import 'dart:math' as math;
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

import '../example.dart';
import 'runner_art.dart';
import 'surface.dart' show linearOf;

/// Where a run is.
enum RunnerPhase {
  /// Standing at the start, waiting to be told to go.
  ready,
  running,
  paused,

  /// Crashed, and waiting to be told to go again.
  over,
}

/// A three-lane runner, played.
///
/// The genre is the one everybody knows: the world comes at you, and you
/// change lane, jump and slide to stay in it, picking up coins on the way.
/// What makes it a game rather than a picture of one is that every one of
/// those is real — a hurdle is cleared by being higher than it at the moment
/// you reach it, a bar by being lower, a container by not being in its lane —
/// and getting it wrong ends the run.
///
/// Everything drawn is a model, made in code by [RunnerArt] and handed to the
/// renderer the way a downloaded one would be. The world is made as it is
/// reached and forgotten once it is behind: the scenery in forty-metre
/// chunks whose keys are reused as the chunks come round, and the grass as
/// one population per chunk, sent once when the chunk is laid.
///
/// The runner really does run. Rather than hold it still and move the world
/// past — which means moving every blade of grass every frame — the world
/// stays where it was put and the runner and camera go through it; every
/// kilometre, the lot is moved back by a kilometre so the numbers never get
/// large enough for a float to notice.
class RunnerExample extends Example {
  RunnerExample() {
    _reset();
  }

  @override
  String get name => 'Runner';

  @override
  ExampleSection get section => ExampleSection.showcases;

  @override
  String get blurb =>
      'A runner you can play: dodge, jump, slide and pick up coins.';

  @override
  ViewPoint get viewpoint =>
      // Only a starting hint for whatever is showing this: the game supplies
      // its own camera every frame, because a runner is followed rather than
      // orbited.
      const ViewPoint(distance: 13, pitch: 0.24, height: 1.6, yaw: 0);

  /// Where the run is.
  RunnerPhase get phase => _phase;
  RunnerPhase _phase = RunnerPhase.ready;

  /// Whether the game plays itself.
  bool autopilot = false;

  /// Whether hitting something only slows you down.
  bool invincible = false;

  /// How fast a run gets, in metres a second. It starts at [_startSpeed] and
  /// closes on this over the first few minutes.
  double topSpeed = 30;

  /// How far this run went, in metres, up to the moment it ended.
  double get travelled => _ran;

  /// Coins picked up this run.
  int get coins => _coins;

  /// Metres run, plus what the coins were worth.
  int get score => _ran.floor() + _bonus;

  /// The best score since the game was opened.
  int get best => _best;

  // ---- the rules ----

  /// Three lanes, which is the number this kind of game has: two gives no
  /// middle to go back to, four gives no obvious one.
  static const _lanes = [-2.6, 0.0, 2.6];
  static const _startSpeed = 14.0;
  static const _gravity = 30.0;

  /// Up at nine and a half metres a second, which under this gravity is a
  /// hop a metre and a half high and six-tenths of a second long: well over
  /// a hurdle, and short enough that a jump is a decision rather than a way
  /// of life.
  static const _jumpSpeed = 9.4;
  static const _slideTime = 0.7;

  /// How far the runner reaches along the road either side of its middle,
  /// and across it from its middle to an obstacle's.
  static const _halfDepth = 0.35;
  static const _reach = 1.55;

  /// What clears what. The hurdle's top is at a metre; the bar's underside
  /// is at 1.05, which a runner standing at 1.3 does not fit under and one
  /// sliding at 0.8 does.
  static const _hurdleClear = 0.92;
  static const _barBottom = 1.05;
  static const _standing = 1.3;
  static const _sliding = 0.8;

  // ---- the world ----

  static const _chunk = 40.0;
  static const _chunks = 11;
  static const _behind = 3;

  /// How far is laid ahead: to the end of the chunks, where the haze has
  /// all but finished.
  static const _ahead = (_chunks - _behind) * _chunk;
  static const _rebase = 1024.0;
  static const _bladesPerChunk = 1100;
  static const _grassRange = 60.0;
  static const _unlaid = -1 << 30;

  /// How many of each kind of scenery one chunk may hold, which is what
  /// keeps a chunk's keys apart from the next one's.
  static const _caps = [6, 5, 1, 1, 3, 1, 3, 1, 1];
  static final _sceneryMeshes = [
    RunnerArt.pine,
    RunnerArt.oak,
    RunnerArt.house,
    RunnerArt.darkHouse,
    RunnerArt.bush,
    RunnerArt.redBush,
    RunnerArt.rock,
    RunnerArt.billboard,
    RunnerArt.hill,
  ];

  /// The clouds, where they are relative to the runner: across, up, how far
  /// ahead, and how big.
  static const _clouds = [
    (x: -170.0, y: 58.0, ahead: 280.0, size: 17.0),
    (x: -60.0, y: 74.0, ahead: 320.0, size: 13.0),
    (x: 30.0, y: 46.0, ahead: 250.0, size: 11.0),
    (x: 120.0, y: 66.0, ahead: 300.0, size: 16.0),
    (x: 230.0, y: 50.0, ahead: 270.0, size: 14.0),
    (x: -280.0, y: 40.0, ahead: 260.0, size: 12.0),
    (x: 300.0, y: 80.0, ahead: 330.0, size: 15.0),
  ];

  static final _white = Vector3(1, 1, 1);

  // ---- state ----

  double? _last;
  double _clock = 0;
  double _readySince = 0;
  double _runTime = 0;
  double _along = 0;
  double _prevAlong = 0;
  double _ran = 0;
  double _origin = 0;
  double _speed = _startSpeed;
  double _slow = 1;
  int _lane = 1;
  double _x = 0;
  double _prevX = 0;
  double _y = 0;
  double _vy = 0;
  double _slideLeft = 0;
  bool _slideQueued = false;
  double _jumpBuffer = 0;
  double _landedAt = -10;
  double _impact = 0;
  double _stumbledAt = -100;
  double _crashedAt = 0;
  double _spin = 0;
  double _intro = 1;
  double _shake = 0;
  double _step = 0;
  int _coins = 0;
  int _bonus = 0;
  int _best = 0;
  bool _newBest = false;
  int _streak = 0;
  double _lastCoinAt = -100;
  int _popup = 0;
  int _runs = 0;
  math.Random _chance = math.Random(1);
  double _nextRow = 0;
  int _hazardCount = 0;
  int _coinCount = 0;

  final _hazards = <_Hazard>[];
  final _road = <_Coin>[];
  final _sparks = List.generate(48, (_) => _Spark());
  int _nextSpark = 0;

  final _slotChunk = List.filled(_chunks, _unlaid);
  final _slotOrigin = List.filled(_chunks, double.nan);
  final _props = List.generate(_chunks, (_) => <OrblitObject>[]);
  final _grass = List.generate(
    _chunks,
    (_) => Float32List(_bladesPerChunk * 16),
  );
  final _grassColours = List.generate(
    _chunks,
    (_) => Float32List(_bladesPerChunk * 3),
  );
  final _grassRevision = List.filled(_chunks, 0);
  final _grassLow = List.generate(_chunks, (_) => Vector3.zero());
  final _grassHigh = List.generate(_chunks, (_) => Vector3.zero());

  RunnerArt? _art;
  bool _artReady = false;

  // What the last frame was seen from, for putting things over it.
  Vector3 _eye = Vector3(0, 3, 7);
  Vector3 _look = Vector3.zero();
  double _fieldOfView = 56;
  double _aspect = 16 / 9;

  final _focus = FocusNode(debugLabel: 'Runner');
  Offset _swipe = Offset.zero;
  bool _swiped = false;

  // ---- what somebody does ----

  /// Starts running, from the start.
  void start() {
    if (_phase != RunnerPhase.ready) return;
    _phase = RunnerPhase.running;
    _intro = 0;
  }

  /// Starts a new run, straight into running.
  void restart() {
    _reset();
    _phase = RunnerPhase.running;
    _intro = 1;
  }

  void pause() {
    if (_phase == RunnerPhase.running) _phase = RunnerPhase.paused;
  }

  void resume() {
    if (_phase == RunnerPhase.paused) _phase = RunnerPhase.running;
  }

  /// One lane to the left for -1 and to the right for 1.
  void steer(int direction) {
    if (_phase != RunnerPhase.running) return;
    final to = _lane + direction;
    if (to < 0 || to >= _lanes.length) return;
    _lane = to;
  }

  void jump() {
    if (_phase != RunnerPhase.running) return;
    if (_grounded) {
      _vy = _jumpSpeed;
      _slideLeft = 0;
    } else {
      // Pressed a moment before landing, which is when people press it: kept
      // and acted on at the landing rather than thrown away.
      _jumpBuffer = 0.15;
    }
  }

  void slide() {
    if (_phase != RunnerPhase.running) return;
    if (_y <= 0) {
      // Still on the ground, even if a jump was asked for a moment ago: the
      // later of the two wins, as it would if they were a frame apart.
      _vy = 0;
      _slideLeft = _slideTime;
    } else {
      // In the air, down means down now: dropped to the ground and sliding
      // when it gets there, which is how a jump that was a mistake is undone.
      _vy = math.min(_vy, -18);
      _slideQueued = true;
    }
  }

  bool get _grounded => _y <= 0 && _vy <= 0;
  bool get _isSliding => _slideLeft > 0 && _grounded;

  // ---- the frame ----

  @override
  OrblitScene scene(OrblitCamera camera, double seconds) {
    _prepare();

    var dt = seconds - (_last ?? seconds);
    _last = seconds;
    if (dt < 0 || dt > 0.5) {
      // The example was put away and brought back, which starts its clock
      // again, or the application stalled. Either way the time that passed
      // is not time anybody played through.
      if (!autopilot) pause();
      dt = 0;
    }
    update(math.min(dt, 0.05));

    final objects = <OrblitObject>[];

    // The road and the fields, each one long piece moved along in whole
    // repeats of its texture, so the move cannot be seen.
    final snap = (_along / RunnerArt.roadRepeat).floor() * RunnerArt.roadRepeat;
    objects
      ..add(
        OrblitObject(
          key: 10,
          mesh: RunnerArt.road,
          material: 1,
          transform: place(0, 0, _z(snap)),
          colour: _white,
          castShadows: false,
        ),
      )
      ..add(
        OrblitObject(
          key: 11,
          mesh: RunnerArt.ground,
          material: 2,
          transform: place(0, -0.03, _z(snap)),
          colour: _white,
          castShadows: false,
        ),
      );

    _drawRunner(objects);
    _drawClouds(objects);
    for (final props in _props) {
      objects.addAll(props);
    }
    _drawRoad(objects);
    _drawSparks(objects);

    // Until the renderer has the files, a model it is asked for is drawn as a
    // placeholder cube — so the models are left out until then, and the world
    // arrives whole rather than as a field of boxes.
    if (!_artReady) objects.removeWhere((object) => object.mesh != null);

    final sun = Vector3(-0.5, -0.75, -0.43)..normalize();
    final horizon = linearOf(const Color(0xFFBFE3FA));

    return OrblitScene(
      camera: _camera(),
      objects: objects,
      populations: [
        for (var slot = 0; slot < _chunks; slot++)
          if (_slotChunk[slot] != _unlaid)
            OrblitPopulation(
              key: 1 + slot,
              transforms: _grass[slot],
              colours: _grassColours[slot],
              minimum: _grassLow[slot],
              maximum: _grassHigh[slot],
              revision: _grassRevision[slot],
              range: _grassRange,
              fade: OrblitFade.sink,
            ),
      ],
      materials: [
        // The road and the fields: white, so the textures are the colour.
        OrblitMaterial(
          key: 1,
          baseColourMap: OrblitTexture(RunnerArt.asphalt),
          roughness: 0.9,
        ),
        OrblitMaterial(
          key: 2,
          baseColourMap: OrblitTexture(RunnerArt.grass),
          roughness: 0.95,
        ),
        // The coins glow a little. Not metal, though a coin is: a metal shows
        // its surroundings and nothing else, and the surroundings here are a
        // painted sky — so a metal coin comes out a dull brown.
        OrblitMaterial(
          key: 3,
          baseColour: Vector4(1.0, 0.54, 0.01, 1),
          roughness: 0.3,
          emissive: Vector3(1.0, 0.62, 0.08),
          emissiveIntensity: 0.9,
        ),
        OrblitMaterial(
          key: 4,
          baseColour: Vector4(1.0, 0.62, 0.05, 1),
          emissive: Vector3(1.0, 0.7, 0.12),
          emissiveIntensity: 4,
        ),
      ],
      lights: [
        OrblitLight(
          key: 1,
          kind: OrblitLightKind.directional,
          direction: sun,
          colour: linearOf(const Color(0xFFFFF4E0)),
          intensity: 95000,
          castShadows: true,
        ),
      ],
      sky: OrblitSky(
        zenith: linearOf(const Color(0xFF2B78D6)),
        horizon: horizon,
        ambient: 34000,
        bodyDirection: -sun,
        // Clear: the clouds are models, drawn with everything else.
        clouds: OrblitClouds.none,
      ),
      // Finished by the end of the world: the scenery stops a little over
      // three hundred metres ahead, and a haze that is still thin there shows
      // the edge of it.
      fog: OrblitFog(
        colour: horizon,
        density: 0.012,
        distance: 45,
        heightFalloff: 0.12,
      ),
      pipeline: OrblitPipeline(
        shadows: OrblitShadows(
          kind: OrblitShadowKind.soft,
          cascades: 2,
          mapSize: 2048,
          distance: 70,
        ),
        // Grass is a thousand edges a pixel wide, and those shimmer under
        // anything less.
        samples: 4,
        resolution: OrblitResolution(adaptive: true, minScale: 0.7),
      ),
      post: OrblitPostProcess(
        antiAliasing: AntiAliasing.off,
        bloom: OrblitBloom(enabled: true, strength: 0.08),
        grading: OrblitGrading(enabled: true, saturation: 1.12, vibrance: 1.1),
      ),
    );
  }

  /// Moves the game on by [dt] seconds.
  ///
  /// What [scene] does each frame before drawing; public so a test can run a
  /// minute of the game without drawing any of it.
  void update(double dt) {
    _laySceneryAround();
    if (_phase == RunnerPhase.paused) return;
    _clock += dt;
    _shake = math.max(0, _shake - dt * 1.6);
    _updateSparks(dt);

    switch (_phase) {
      case RunnerPhase.ready:
        if (autopilot && _clock - _readySince > 1.2) start();
      case RunnerPhase.running:
        _run(dt);
      case RunnerPhase.over:
        _tumble(dt);
        if (autopilot && _clock - _crashedAt > 2.5) restart();
      case RunnerPhase.paused:
        break;
    }
  }

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

  // ---- what is on the road ----

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

  // ---- the autopilot ----

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
      worth[hazard.lane] -= hazard.kind == _Kind.container ? 100 : 1;
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
    if (wanted != _lane && settled) {
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

  // ---- starting again ----

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

  // ---- drawing ----

  void _drawRunner(List<OrblitObject> objects) {
    final z = _z(_along);
    final crashed = _phase == RunnerPhase.over;
    final ready = _phase == RunnerPhase.ready;
    final stride = _step;

    // Squash and stretch: a little with every step, flat when sliding,
    // tall on the way up and squashed on landing.
    var sy = 1 - 0.05 * math.cos(stride * 2);
    if (ready) sy = 1 + 0.03 * math.sin(_clock * 3);
    if (_isSliding) sy = 0.6;
    if (!_grounded && !crashed) sy = _vy > 0 ? 1.1 : 1.02;
    final sinceLanding = _clock - _landedAt;
    if (sinceLanding < 0.16 && !_isSliding && !crashed) {
      sy -= _impact * 0.28 * (1 - sinceLanding / 0.16);
    }
    final sx = _isSliding ? 1.25 : 1 / math.sqrt(sy);
    final bob = ready || crashed || !_grounded || _isSliding
        ? 0.0
        : math.sin(stride).abs() * 0.08;
    final bodyY = _y + 0.2 + 0.55 * sy + bob;

    // Leaning into a lane change, and forward into the run.
    final drift = _lanes[_lane] - _x;
    final roll = crashed ? 0.0 : (-drift * 0.12).clamp(-0.3, 0.3);
    final yaw = crashed ? 0.0 : (-drift * 0.1).clamp(-0.25, 0.25);
    final pitch = crashed
        ? -_spin
        : ready
        ? 0.0
        : _isSliding
        ? -0.25
        : -0.12;

    final frame = Matrix4.identity()
      ..translateByDouble(_x, bodyY, z, 1)
      ..rotateY(yaw)
      ..rotateZ(roll)
      ..rotateX(pitch);

    objects.add(
      OrblitObject(
        key: 1,
        mesh: RunnerArt.body,
        transform: frame.clone()..scaleByDouble(sx, sy, sx, 1),
        colour: _white,
      ),
    );

    // Wings: a lazy beat when running, a flurry in the air, swept back
    // flat when sliding.
    final flap = _isSliding
        ? -0.2
        : !_grounded || crashed
        ? 0.5 + 0.6 * math.sin(_clock * 30)
        : ready
        ? 0.25 + 0.2 * math.sin(_clock * 4)
        : 0.3 + 0.25 * math.sin(stride * 2);
    for (final side in const [1.0, -1.0]) {
      final wing = frame.clone()
        ..translateByDouble(side * 0.5 * sx, 0.05 * sy, 0.05, 1);
      if (side < 0) wing.rotateY(math.pi);
      wing.rotateZ(flap);
      objects.add(
        OrblitObject(
          key: side > 0 ? 2 : 3,
          mesh: RunnerArt.wing,
          transform: wing,
          colour: _white,
        ),
      );
    }

    // Feet: striding on the ground, tucked in the air, out in front on a
    // slide, and wherever the body goes in a crash.
    for (final side in const [1.0, -1.0]) {
      final phase = stride + (side > 0 ? 0 : math.pi);
      final Matrix4 foot;
      if (crashed) {
        foot = frame.clone()..translateByDouble(side * 0.2, -0.5, 0, 1);
      } else {
        var lift = 0.0, reach = 0.0;
        if (ready) {
          lift = 0;
        } else if (!_grounded) {
          lift = 0.18;
          reach = -0.05;
        } else if (_isSliding) {
          reach = -0.3;
        } else {
          reach = math.sin(phase) * 0.26;
          lift = math.max(0, math.cos(phase)) * 0.16;
        }
        foot = Matrix4.identity()
          ..translateByDouble(_x, _y, z, 1)
          ..rotateY(yaw)
          ..translateByDouble(side * 0.2, 0.08 + lift, reach, 1);
      }
      objects.add(
        OrblitObject(
          key: side > 0 ? 4 : 5,
          mesh: RunnerArt.foot,
          transform: foot,
          colour: _white,
        ),
      );
    }
  }

  void _drawRoad(List<OrblitObject> objects) {
    for (final hazard in _hazards) {
      if (hazard.near > _along + _ahead || hazard.far < _along - 20) continue;
      final mesh = switch (hazard.kind) {
        _Kind.hurdle => RunnerArt.hurdle,
        _Kind.bar => RunnerArt.bar,
        _Kind.container => RunnerArt.container,
      };
      final along = hazard.kind == _Kind.container
          ? hazard.near
          : (hazard.near + hazard.far) / 2;
      final transform = place(_lanes[hazard.lane], 0, _z(along));
      // Knocked flat, if it was run through with crashing turned off.
      if (hazard.struck && hazard.kind != _Kind.container) {
        transform.rotateX(-math.pi / 2 * 0.85);
      }
      objects.add(
        OrblitObject(
          key: hazard.key,
          mesh: mesh,
          transform: transform,
          colour: _white,
        ),
      );
    }

    for (final coin in _road) {
      if (coin.along > _along + _ahead || coin.along < _along - 20) continue;
      var x = _lanes[coin.lane], y = coin.height, z = _z(coin.along);
      var size = 1.0;
      final taken = coin.takenAt;
      if (taken != null) {
        // Up and into the runner, and gone.
        final t = (_clock - taken) / 0.25;
        if (t >= 1) continue;
        x += (_x - x) * t;
        y += (_y + 1.6 - y) * t;
        z += (_z(_along) - z) * t;
        size = 1 - t * 0.7;
      }
      objects.add(
        OrblitObject(
          key: coin.key,
          mesh: RunnerArt.coin,
          material: 3,
          transform: Matrix4.identity()
            ..translateByDouble(x, y, z, 1)
            ..rotateY(_clock * 4 + coin.along * 0.3)
            ..scaleByDouble(size, size, size, 1),
          colour: _white,
          // A coin floats, and the shadow of a thin disc with nothing plainly
          // above it reads as a stain on the road.
          castShadows: false,
        ),
      );
    }
  }

  void _spark(Vector3 at) {
    final spark = _sparks[_nextSpark];
    _nextSpark = (_nextSpark + 1) % _sparks.length;
    final angle = _chance.nextDouble() * math.pi * 2;
    spark
      ..at.setFrom(at)
      ..velocity.setValues(
        math.cos(angle) * 2.2,
        2.5 + _chance.nextDouble() * 2,
        math.sin(angle) * 2.2 - _speed * 0.6,
      )
      ..life = 0.5;
  }

  void _updateSparks(double dt) {
    for (final spark in _sparks) {
      if (spark.life <= 0) continue;
      spark.life -= dt;
      spark.velocity.y -= 9 * dt;
      spark.at.addScaled(spark.velocity, dt);
    }
  }

  /// The clouds stay the same distance ahead however far the runner goes, as
  /// real ones a kilometre off seem to, and only the wind moves them.
  ///
  /// The sky's own clouds are marched a pixel at a time and left grainy for
  /// temporal anti-aliasing to smooth — which this scene cannot have, because
  /// everything in it moves fast enough to smear.
  void _drawClouds(List<OrblitObject> objects) {
    const across = 640.0;
    for (var i = 0; i < _clouds.length; i++) {
      final cloud = _clouds[i];
      final drift = cloud.x + _clock * 1.2 + across / 2;
      final x = drift - (drift / across).floor() * across - across / 2;
      objects.add(
        OrblitObject(
          key: 70 + i,
          mesh: RunnerArt.cloud,
          transform: place(
            x,
            cloud.y,
            _z(_along + cloud.ahead),
            sx: cloud.size,
            sy: cloud.size * 0.8,
            sz: cloud.size * 0.6,
          ),
          colour: _white,
          castShadows: false,
        ),
      );
    }
  }

  void _drawSparks(List<OrblitObject> objects) {
    for (var i = 0; i < _sparks.length; i++) {
      final spark = _sparks[i];
      if (spark.life <= 0) continue;
      // A glint rather than a crumb: long and thin, and turning.
      final size = 0.07 * spark.life / 0.5;
      objects.add(
        OrblitObject(
          key: 20 + i,
          transform: Matrix4.identity()
            ..translateByDouble(spark.at.x, spark.at.y, spark.at.z, 1)
            ..rotateY(spark.life * 12)
            ..rotateZ(math.pi / 4)
            ..scaleByDouble(size * 0.45, size * 1.8, size * 0.45, 1),
          colour: _white,
          material: 4,
          castShadows: false,
        ),
      );
    }
  }

  /// A little behind and above, looking down the road — and at the start,
  /// round in front, looking at the runner, swinging behind when the run
  /// begins.
  OrblitCamera _camera() {
    final z = _z(_along);
    final chaseEye = Vector3(_x * 0.55, 3.2 + _y * 0.35, z + 6.8);
    final chaseLook = Vector3(_x * 0.85, 1.3 + _y * 0.5, z - 9);
    // Wider as it gets faster, which is most of what makes fast feel fast.
    var field = 56 + (math.max(_speed, 0) - 12) * 0.4;
    // And wider still on a screen taller than it is wide, so all three lanes
    // are in it.
    if (_aspect < 1.2) {
      const across = 64 * math.pi / 180;
      final needed = 2 * math.atan(math.tan(across / 2) / _aspect);
      field = math.max(field, math.min(95, needed * 180 / math.pi));
    }

    final eye = Vector3.copy(chaseEye);
    final look = Vector3.copy(chaseLook);
    if (_phase == RunnerPhase.ready || _intro < 1) {
      final t = _phase == RunnerPhase.ready ? 0.0 : _intro;
      final blend = t * t * (3 - 2 * t);
      final frontEye = Vector3(_x + 3.4, 1.35, z - 3.6);
      final frontLook = Vector3(_x - 0.3, 0.95, z + 0.6);
      eye.setFrom(frontEye + (chaseEye - frontEye) * blend);
      look.setFrom(frontLook + (chaseLook - frontLook) * blend);
      field = 44 + (field - 44) * blend;
    }

    if (_shake > 0) {
      final amount = _shake * _shake * 0.25;
      eye.x += math.sin(_clock * 53) * amount;
      eye.y += math.sin(_clock * 41 + 1) * amount;
    }

    _eye = eye;
    _look = look;
    _fieldOfView = field;
    return OrblitCamera(position: eye, target: look, fieldOfView: field);
  }

  // ---- what is drawn over it ----

  @override
  Widget? overlay(BuildContext context, VoidCallback changed) {
    return DefaultTextStyle(
      style: const TextStyle(
        color: Colors.white,
        fontSize: 14,
        letterSpacing: 0,
        decoration: TextDecoration.none,
        fontWeight: FontWeight.w500,
      ),
      child: Focus(
        focusNode: _focus,
        autofocus: true,
        onKeyEvent: (node, event) => _key(event, changed),
        onFocusChange: (focused) {
          // Somebody who clicked somewhere else is not playing.
          if (!focused && _phase == RunnerPhase.running && !autopilot) {
            pause();
            changed();
          }
        },
        child: GestureDetector(
          behavior: HitTestBehavior.opaque,
          onTapDown: (_) => _focus.requestFocus(),
          onTap: () {
            _confirm();
            changed();
          },
          onPanStart: (_) {
            _focus.requestFocus();
            _swipe = Offset.zero;
            _swiped = false;
          },
          onPanUpdate: (details) {
            if (_swiped) return;
            _swipe += details.delta;
            if (_swipe.distance < 28) return;
            // Once per swipe, as soon as it is clearly one, rather than when
            // the finger lifts: a lane change that waits for the end of the
            // gesture is a lane change a tenth of a second late.
            _swiped = true;
            if (_phase != RunnerPhase.running) {
              _confirm();
            } else if (_swipe.dx.abs() > _swipe.dy.abs()) {
              steer(_swipe.dx > 0 ? 1 : -1);
            } else if (_swipe.dy < 0) {
              jump();
            } else {
              slide();
            }
            changed();
          },
          child: LayoutBuilder(
            builder: (context, constraints) {
              final size = constraints.biggest;
              if (size.width > 0 && size.height > 0 && size.isFinite) {
                _aspect = size.width / size.height;
              }
              final popup = _popupAt(size);
              return Stack(
                fit: StackFit.expand,
                children: [
                  if (_phase == RunnerPhase.over && _clock - _crashedAt < 0.45)
                    ColoredBox(
                      color: const Color(0xFFE0302A).withValues(
                        alpha: 0.35 * (1 - (_clock - _crashedAt) / 0.45),
                      ),
                    ),
                  if (_phase != RunnerPhase.ready)
                    Positioned(
                      top: 14,
                      left: 0,
                      right: 0,
                      child: Center(child: _hud()),
                    ),
                  if (popup != null) popup,
                  if (_phase == RunnerPhase.running &&
                      _clock - _stumbledAt < 1.2)
                    const Positioned(
                      top: 70,
                      left: 0,
                      right: 0,
                      child: Center(
                        child: Text(
                          'Careful!',
                          style: TextStyle(
                            color: Color(0xFFFFB13B),
                            fontSize: 20,
                            fontWeight: FontWeight.w800,
                            shadows: [
                              Shadow(color: Color(0xAA000000), blurRadius: 6),
                            ],
                          ),
                        ),
                      ),
                    ),
                  if (_phase == RunnerPhase.ready)
                    Positioned(
                      left: 16,
                      right: 16,
                      bottom: 20,
                      child: Center(child: _readyCard()),
                    ),
                  if (_phase == RunnerPhase.paused)
                    Center(
                      child: _card([
                        const Text(
                          'Paused',
                          style: TextStyle(
                            fontSize: 24,
                            fontWeight: FontWeight.w800,
                          ),
                        ),
                        const SizedBox(height: 6),
                        const Text(
                          'Tap, or press Esc or Space, to carry on',
                          style: TextStyle(color: Color(0xFFB4BCCB)),
                        ),
                      ]),
                    ),
                  if (_phase == RunnerPhase.over && _clock - _crashedAt > 0.7)
                    Center(child: _overCard()),
                ],
              );
            },
          ),
        ),
      ),
    );
  }

  static const _figures = [FontFeature.tabularFigures()];

  Widget _hud() {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: const Color(0xD91A1D26),
        borderRadius: BorderRadius.circular(18),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 7),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Text(
              '$score',
              style: const TextStyle(
                fontSize: 22,
                fontWeight: FontWeight.w800,
                fontFeatures: _figures,
              ),
            ),
            const SizedBox(width: 16),
            const _CoinIcon(),
            const SizedBox(width: 6),
            Text(
              '$_coins',
              style: const TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w700,
                color: Color(0xFFFFD34D),
                fontFeatures: _figures,
              ),
            ),
            const SizedBox(width: 16),
            Text(
              '${math.max(_speed, 0).round()} m/s',
              style: const TextStyle(
                fontSize: 14,
                color: Color(0xFFB4BCCB),
                fontFeatures: _figures,
              ),
            ),
            if (autopilot) ...[
              const SizedBox(width: 12),
              const Text(
                'AUTO',
                style: TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w800,
                  color: Color(0xFF7ED9B7),
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  /// The "+N" for the coins just picked up, over the runner's head.
  Widget? _popupAt(Size size) {
    final since = _clock - _lastCoinAt;
    if (_popup == 0 || since > 1.1 || _phase == RunnerPhase.ready) return null;
    if (size.width <= 0 || size.height <= 0 || !size.isFinite) return null;

    final view = makeViewMatrix(_eye, _look, Vector3(0, 1, 0));
    final projection = makePerspectiveMatrix(
      _fieldOfView * math.pi / 180,
      size.width / size.height,
      0.1,
      1000,
    );
    final clip = projection
        .multiplied(view)
        .transform(Vector4(_x, _y + 1.75 + since * 0.4, _z(_along), 1));
    if (clip.w <= 0) return null;
    final sx = (clip.x / clip.w + 1) / 2 * size.width;
    final sy = (1 - clip.y / clip.w) / 2 * size.height;
    final fade = since < 0.8 ? 1.0 : 1 - (since - 0.8) / 0.3;

    return Positioned(
      left: sx - 60,
      top: sy - 20,
      width: 120,
      child: Opacity(
        opacity: fade.clamp(0.0, 1.0),
        child: Text(
          '+$_popup',
          textAlign: TextAlign.center,
          style: const TextStyle(
            fontSize: 26,
            fontWeight: FontWeight.w900,
            color: Color(0xFFFFD34D),
            shadows: [Shadow(color: Color(0xCC3A2500), blurRadius: 5)],
          ),
        ),
      ),
    );
  }

  Widget _card(List<Widget> children) {
    return DecoratedBox(
      decoration: BoxDecoration(
        color: const Color(0xE61A1D26),
        borderRadius: BorderRadius.circular(16),
      ),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 22, vertical: 16),
        child: Column(mainAxisSize: MainAxisSize.min, children: children),
      ),
    );
  }

  Widget _readyCard() {
    Widget line(String keys, String does) => Padding(
      padding: const EdgeInsets.symmetric(vertical: 1.5),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          SizedBox(
            width: 118,
            child: Text(
              keys,
              textAlign: TextAlign.right,
              style: const TextStyle(
                color: Color(0xFFFFD34D),
                fontWeight: FontWeight.w700,
                fontSize: 13,
              ),
            ),
          ),
          const SizedBox(width: 10),
          SizedBox(
            width: 110,
            child: Text(
              does,
              style: const TextStyle(color: Color(0xFFD5DAE3), fontSize: 13),
            ),
          ),
        ],
      ),
    );

    return _card([
      const Text(
        'Tap or press Space to run',
        style: TextStyle(fontSize: 20, fontWeight: FontWeight.w800),
      ),
      const SizedBox(height: 10),
      line('← →  or  A D', 'change lane'),
      line('↑  W  or  Space', 'jump'),
      line('↓  or  S', 'slide'),
      line('Esc', 'pause'),
      const SizedBox(height: 6),
      const Text(
        'On a touch screen, swipe.',
        style: TextStyle(color: Color(0xFF8E97A8), fontSize: 12),
      ),
      if (_best > 0) ...[
        const SizedBox(height: 6),
        Text(
          'Best $_best',
          style: const TextStyle(color: Color(0xFF8E97A8), fontSize: 12),
        ),
      ],
    ]);
  }

  Widget _overCard() {
    Widget figure(String label, String value, {Color? colour}) => Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          SizedBox(
            width: 76,
            child: Text(
              label,
              style: const TextStyle(color: Color(0xFFB4BCCB)),
            ),
          ),
          SizedBox(
            width: 76,
            child: Text(
              value,
              textAlign: TextAlign.right,
              style: TextStyle(
                fontSize: 17,
                fontWeight: FontWeight.w800,
                color: colour,
                fontFeatures: _figures,
              ),
            ),
          ),
        ],
      ),
    );

    return _card([
      const Text(
        'Crashed!',
        style: TextStyle(fontSize: 26, fontWeight: FontWeight.w900),
      ),
      if (_newBest)
        const Text(
          'A new best',
          style: TextStyle(
            color: Color(0xFFFFD34D),
            fontWeight: FontWeight.w700,
          ),
        ),
      const SizedBox(height: 10),
      figure('Score', '$score'),
      figure('Coins', '$_coins', colour: const Color(0xFFFFD34D)),
      figure('Distance', '${_ran.floor()} m'),
      figure('Best', '$_best'),
      const SizedBox(height: 12),
      const Text(
        'Tap or press Space to run again',
        style: TextStyle(color: Color(0xFFD5DAE3)),
      ),
    ]);
  }

  /// Tap, Space or Enter: whatever "go" means just now.
  void _confirm() {
    switch (_phase) {
      case RunnerPhase.ready:
        start();
      case RunnerPhase.paused:
        resume();
      case RunnerPhase.over:
        // Not in the moment of the crash, when a tap was meant for a jump.
        if (_clock - _crashedAt > 0.7) restart();
      case RunnerPhase.running:
        break;
    }
  }

  static final _left = {LogicalKeyboardKey.arrowLeft, LogicalKeyboardKey.keyA};
  static final _right = {
    LogicalKeyboardKey.arrowRight,
    LogicalKeyboardKey.keyD,
  };
  static final _up = {
    LogicalKeyboardKey.arrowUp,
    LogicalKeyboardKey.keyW,
    LogicalKeyboardKey.space,
  };
  static final _down = {LogicalKeyboardKey.arrowDown, LogicalKeyboardKey.keyS};
  static final _stop = {LogicalKeyboardKey.escape, LogicalKeyboardKey.keyP};
  static final _go = {LogicalKeyboardKey.enter, LogicalKeyboardKey.numpadEnter};

  KeyEventResult _key(KeyEvent event, VoidCallback changed) {
    final key = event.logicalKey;
    final ours =
        _left.contains(key) ||
        _right.contains(key) ||
        _up.contains(key) ||
        _down.contains(key) ||
        _stop.contains(key) ||
        _go.contains(key);
    if (!ours) return KeyEventResult.ignored;
    // Held keys repeat, and a held arrow is not three lane changes.
    if (event is! KeyDownEvent) return KeyEventResult.handled;

    if (_stop.contains(key)) {
      if (_phase == RunnerPhase.running) {
        pause();
      } else if (_phase == RunnerPhase.paused) {
        resume();
      }
    } else if (_phase != RunnerPhase.running) {
      _confirm();
    } else if (_left.contains(key)) {
      steer(-1);
    } else if (_right.contains(key)) {
      steer(1);
    } else if (_up.contains(key)) {
      jump();
    } else if (_down.contains(key)) {
      slide();
    }
    changed();
    return KeyEventResult.handled;
  }

  // ---- the panel ----

  @override
  Widget settings(BuildContext context, VoidCallback changed) {
    final small = Theme.of(context).textTheme.bodySmall;
    var scenery = 0;
    for (final props in _props) {
      scenery += props.length;
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'Click the game, then use the arrow keys or WASD: left and right '
          'change lane, up jumps a hurdle, down slides under a bar. Go round '
          'the containers. Space starts, Esc pauses.',
          style: small,
        ),
        const SizedBox(height: 8),
        Toggle(
          label: 'Autopilot',
          value: autopilot,
          note: 'Plays itself, with the same three moves you have',
          onChanged: (value) {
            autopilot = value;
            changed();
          },
        ),
        Toggle(
          label: "Can't crash",
          value: invincible,
          note: 'Hitting something slows you down instead of ending the run',
          onChanged: (value) {
            invincible = value;
            changed();
          },
        ),
        Setting(
          label: 'Top speed',
          value: topSpeed,
          min: 20,
          max: 40,
          decimals: 0,
          unit: ' m/s',
          onChanged: (value) {
            topSpeed = value;
            changed();
          },
        ),
        Choice(
          label: 'Run',
          options: const ['Start again'],
          selected: '',
          onSelect: (_) {
            restart();
            changed();
          },
        ),
        const SizedBox(height: 8),
        Text(
          '$scenery pieces of scenery and '
          '${_bladesPerChunk * _chunks} blades of grass in $_chunks chunks; '
          '${_hazards.length} obstacles and ${_road.length} coins on the road '
          'ahead. Every model is made in code and sent to the renderer once.',
          style: small,
        ),
      ],
    );
  }

  @override
  String get code => '''
// Every model is made in code, written out as a glb, and handed to the
// renderer by name — the same path a downloaded file takes.
final art = RunnerArt.build();
for (final file in art.files.entries) {
  await OrblitResources.provide(file.key, file.value);
}

// The world is laid in chunks as the runner reaches them. A chunk that
// falls behind hands its keys to the one being laid ahead, so the
// renderer reuses what it made rather than making more.
final key = 1000 + type * 100 + slot * cap + index;

// Grass is one population per chunk, sent when the chunk is laid and never
// again: the revision only changes when the blades do.
OrblitPopulation(
  key: 1 + slot,
  transforms: blades,
  colours: shades,
  minimum: low,
  maximum: high,
  revision: revisions[slot],
  range: 60,
);

// A hurdle is cleared by being above it when you reach it, a bar by being
// below it, a container by not being in its lane.
final blocked = switch (hazard.kind) {
  Kind.hurdle => y < 0.92,
  Kind.bar => y + (sliding ? 0.8 : 1.3) > 1.05,
  Kind.container => true,
};
''';
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

class _Spark {
  final at = Vector3.zero();
  final velocity = Vector3.zero();
  double life = 0;
}

/// A coin, drawn: a gold disc with a paler rim.
class _CoinIcon extends StatelessWidget {
  const _CoinIcon();

  @override
  Widget build(BuildContext context) {
    return const SizedBox(
      width: 18,
      height: 18,
      child: DecoratedBox(
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          gradient: RadialGradient(
            center: Alignment(-0.3, -0.35),
            colors: [Color(0xFFFFF0A0), Color(0xFFFFC21A), Color(0xFFC98A00)],
            stops: [0, 0.55, 1],
          ),
          border: Border.fromBorderSide(
            BorderSide(color: Color(0xFFFFE27A), width: 1.2),
          ),
        ),
      ),
    );
  }
}
