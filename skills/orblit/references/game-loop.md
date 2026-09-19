# A complete small game

A dodging game in one file: arrow keys steer a blue cube, orange cubes come
down the lane faster and faster, and a hit ends the run until Space is
pressed. It shows the shape most Orblit games take:

- **State lives in the `State` object; the scene is built from it** in
  `build`, fresh every frame. Nothing is added to or removed from the
  renderer. A hazard that leaves the list leaves the screen.
- **Keys come from reserved ranges**, because objects and lights share one key
  space: lights 1 to 9, fixed objects 10 to 999, hazards from 1000 up. Each
  hazard keeps its key for its whole life, and no key is reused.
- **One clock**, a `Ticker`, and a `dt` guarded against pauses and hitches.
- **Input through Flutter**: a `Focus` with `onKeyEvent`, holding the set of
  keys that are down. Touch would be a `GestureDetector` in the same `Stack`.
- **The interface is ordinary Flutter** laid over the view in a `Stack`. For
  a styled game interface, see `orblit_ui` in [packages.md](packages.md).
- **Scene notes on screen**, set only when they change.

It compiled against engine commit `94985ff` with `flutter analyze`. Names may
have moved since; check them against the source.

```dart
import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:flutter/services.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

void main() => runApp(const MaterialApp(home: Scaffold(body: Dodge())));

// One key space for objects and lights, so each kind gets its own range.
const _sunKey = 1;
const _floorKey = 10;
const _playerKey = 11;
const _firstHazardKey = 1000;

class _Hazard {
  _Hazard(this.id, this.x, this.z);

  final int id; // never reused, so the renderer never confuses two hazards
  final double x;
  double z;
}

class Dodge extends StatefulWidget {
  const Dodge({super.key});

  @override
  State<Dodge> createState() => _DodgeState();
}

class _DodgeState extends State<Dodge> with SingleTickerProviderStateMixin {
  late final Ticker _ticker;
  final _held = <LogicalKeyboardKey>{};
  final _random = math.Random(7);
  final _hazards = <_Hazard>[];
  double? _last;
  double _clock = 0;
  double _nextSpawn = 0;
  double _x = 0;
  int _spawned = 0;
  bool _over = false;
  String? _notes;

  @override
  void initState() {
    super.initState();
    _ticker = createTicker((elapsed) {
      setState(() => _frame(elapsed.inMicroseconds / 1e6));
    })..start();
  }

  @override
  void dispose() {
    _ticker.dispose();
    super.dispose();
  }

  void _frame(double seconds) {
    var dt = seconds - (_last ?? seconds);
    _last = seconds;
    if (dt < 0 || dt > 0.5) dt = 0; // a pause or a hitch, not game time
    _update(math.min(dt, 0.05));
  }

  void _update(double dt) {
    if (_over) return;
    _clock += dt;

    final right = _held.contains(LogicalKeyboardKey.arrowRight) ? 1 : 0;
    final left = _held.contains(LogicalKeyboardKey.arrowLeft) ? 1 : 0;
    _x = (_x + (right - left) * 8 * dt).clamp(-4.0, 4.0);

    if (_clock >= _nextSpawn) {
      _hazards.add(_Hazard(_spawned++, _random.nextDouble() * 8 - 4, -30));
      _nextSpawn = _clock + 0.6;
    }

    final speed = 10 + _clock * 0.5;
    for (final hazard in _hazards) {
      hazard.z += speed * dt;
      // Both cubes are 1 m across, so they touch within 1 m on each axis.
      if ((hazard.x - _x).abs() < 1 && hazard.z.abs() < 1) _over = true;
    }
    _hazards.removeWhere((hazard) => hazard.z > 8);
  }

  void _restart() {
    _hazards.clear();
    _clock = 0;
    _nextSpawn = 0;
    _x = 0;
    _over = false;
  }

  KeyEventResult _key(KeyEvent event) {
    if (event is KeyDownEvent) {
      _held.add(event.logicalKey);
      if (event.logicalKey == LogicalKeyboardKey.space && _over) _restart();
    } else if (event is KeyUpEvent) {
      _held.remove(event.logicalKey);
    }
    return KeyEventResult.handled;
  }

  @override
  Widget build(BuildContext context) {
    const label = TextStyle(color: Colors.white, fontSize: 18);
    return Focus(
      autofocus: true,
      onKeyEvent: (node, event) => _key(event),
      child: Stack(
        children: [
          Positioned.fill(
            child: OrblitView(
              scene: _scene(),
              onSceneNotes: (notes) {
                final text = notes.entries
                    .map((note) => '${note.key}: ${note.value}')
                    .join('\n');
                if (text != _notes) setState(() => _notes = text);
              },
            ),
          ),
          Positioned(
            left: 16,
            top: 16,
            child: Text(
              _over
                  ? 'Hit after ${_clock.toStringAsFixed(1)} s. Space to go again.'
                  : '${_clock.toStringAsFixed(1)} s',
              style: label,
            ),
          ),
          if (_notes != null)
            Positioned(
              left: 16,
              bottom: 16,
              child: Text(_notes!, style: label),
            ),
        ],
      ),
    );
  }

  /// The placeholder cube is 2 m across, so a scale of 0.5 makes a 1 m cube.
  Matrix4 _cube(double x, double z) =>
      Matrix4.translationValues(x, 0.5, z)..scaleByDouble(0.5, 0.5, 0.5, 1);

  OrblitScene _scene() {
    return OrblitScene(
      camera: OrblitCamera(
        position: Vector3(0, 6, 10),
        target: Vector3(0, 0, -6),
      ),
      lights: [
        OrblitLight(
          key: _sunKey,
          kind: OrblitLightKind.directional,
          direction: Vector3(-0.3, -1, -0.5)..normalize(),
          intensity: 100000,
        ),
      ],
      objects: [
        OrblitObject(
          key: _floorKey,
          // 12 m wide, 40 m long, its top at y = 0.
          transform: Matrix4.translationValues(0, -0.05, -10)
            ..scaleByDouble(6, 0.05, 20, 1),
          colour: Vector3(0.18, 0.19, 0.21),
          castShadows: false,
        ),
        OrblitObject(
          key: _playerKey,
          transform: _cube(_x, 0),
          colour: Vector3(0.2, 0.45, 0.9),
        ),
        // Four or more cubes alike in colour, material and flags are batched
        // into shared draws automatically; each keeps its own key.
        for (final hazard in _hazards)
          OrblitObject(
            key: _firstHazardKey + hazard.id,
            transform: _cube(hazard.x, hazard.z),
            colour: Vector3(0.85, 0.42, 0.16),
          ),
      ],
    );
  }
}
```

## Going further

- **Thousands of things** (debris, crowds, terrain) belong in an
  `OrblitPopulation`; see [scene-api.md](scene-api.md). Keep objects for
  things the game tracks one by one.
- **A following camera** that doesn't jitter: `orblit_camera`'s
  `CameraBrain` and virtual cameras (see [packages.md](packages.md)), or at
  least `damp` on each coordinate of the camera position. Guard the aspect
  ratio against a zero-sized first layout.
- **Enemies that chase**: `orblit_agent`'s steering behaviours for movement
  and behaviour trees for decisions.
- **Hits beyond boxes**: `orblit_collide` for shapes, raycasts and overlaps.
- **Juice** (a flash, a shake, a pop): `orblit_effect`, sampled at the game
  clock.
- **Models**: `mesh:` with a `.glb`, provided as bytes through
  `OrblitResources` on the web, on Android and in a sandboxed macOS app.

The gallery's runner (`packages/orblit_examples/lib/src/examples/runner.dart`)
is a full game in this style, with an autopilot, sparks and a camera, and is
worth reading before building anything larger.
