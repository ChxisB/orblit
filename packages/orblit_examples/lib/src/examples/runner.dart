import 'dart:math' as math;
import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

import '../example.dart';
import '../platform/io.dart';
import 'runner_art.dart';
import 'surface.dart' show linearOf;

part 'runner_rules.dart';
part 'runner_play.dart';
part 'runner_hazards.dart';
part 'runner_autopilot.dart';
part 'runner_chunks.dart';
part 'runner_draw.dart';
part 'runner_hud.dart';

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
  ///
  /// Off by default, because somebody who opens this wants to play it. On
  /// from the environment, because something that is recording the gallery
  /// has no hands: ORBLIT_AUTOPILOT=1 starts a run and keeps it going, and
  /// the panel's own switch still works over the top of it.
  bool autopilot = Platform.environment['ORBLIT_AUTOPILOT'] == '1';

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
