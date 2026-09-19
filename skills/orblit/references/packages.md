# The packages

Every package is in `https://github.com/ChxisB/orblit.git` under
`packages/<name>`, added with a git dependency and that `path`. Only
`orblit_filament`, `orblit_ui` and `orblit_stage` need Flutter; the rest run
anywhere Dart does. Names below were checked against engine commit `94985ff`.
Each package's `lib/<name>.dart` lists its exports, and its doc comments are
the fullest reference.

| Package | What it is | Start with |
| --- | --- | --- |
| `orblit_filament` | The renderer, composited by Flutter | `OrblitView`, `OrblitScene` (see [scene-api.md](scene-api.md)) |
| `orblit_camera` | Cameras as shots: say what to frame, and a brain works out where to be and blends between shots | `CameraBrain`, `VirtualCamera`, `FollowBody`, `HardLookAt`, `Lens`, `damp` |
| `orblit_agent` | Steering behaviours (where to go) and behaviour trees (what to want) | `Steerable`, `Seek`, `Arrive`, `Separate`, `Blend`, `Brain`, `Selector`, `Sequence` |
| `orblit_collide` | Shapes, raycasts and overlap tests, 3D and 2D. Queries, not a physics simulation | `Sphere`, `Box`, `Capsule`, `contact`, `overlaps`, `Ray`, `raycastFirst`, `Broadphase` |
| `orblit_effect` | Move, turn, grow, tint and fade as functions of time, sequenced and combined | `MoveBy`, `TurnBy`, `Then`, `Both`, `Shake`, `applied`, `Playing` |
| `orblit_sequence` | Cutscenes: tracks of clips over a playhead | `Sequence`, `sampleAt`, `Director` |
| `orblit_sprite` | 2D data: atlases and a packer, sprite animation, parallax, tile maps. Draws nothing; `OrblitSprites` in the scene draws | `Atlas`, `Region`, `SpriteAnimation`, `Parallax`, `TileMap` |
| `orblit_ui` | A game interface as a tree of nodes with utility classes or CSS, built into real Flutter widgets | `UiSurface`, `UiNode` |
| `orblit_scene` | A scene as a document: entities with stable ids, components, migrations, diffs | `SceneDocument.decode`, `SceneDiff`, `TransformComponent` |
| `orblit_stage` | Stages a scene document for the renderer | `OrblitDocumentView` |
| `orblit_light` | Lights stated in watts, metres and degrees, converted to photometric units | `Light`, `Photometry` |
| `orblit_mesh` | Building and editing geometry, and writing it out (OBJ, glb, STL, PLY) | `Mesh`, `MeshExport` |
| `orblit_rig` | Armatures, poses, bone constraints, IK | `Armature`, `Pose`, `solveTwoBoneIk` |
| `orblit_noise` | Deterministic value, gradient, cell and fractal noise | `GradientNoise`, `FractalNoise` |
| `orblit_weather` | Weather conditions, day cycle, and the exposure they need | `WeatherState`, `DayCycle`, `CameraExposure` |
| `orblit_input` | Gamepads as per-frame state. **Linux only so far**; elsewhere it reports no pads | `Pads`, `PadState` |
| `orblit_asset` | Asset ids, content hashes and sources. Its directory classes throw in a browser | `AssetId`, `AssetSource` |
| `orblit_core` | An entity-component store in C++ over a C ABI | `World`, `Query` |
| `orblit_native` | Compiling and loading C++ scripts | `NativeScript`, `ScriptRunner` |

Other repositories: `orblit-net` (multiplayer: replication, ownership,
interpolation), `orblit-script` (TypeScript scripting on QuickJS),
`orblit-editor` (the editor app) and `orblit-examples` (the gallery app).
Keyboard and touch input come from Flutter itself (`Focus`,
`GestureDetector`); there is no Orblit keyboard package.

## Name clashes

A clash is an error only where the name is used. These were found by
importing everything together and analysing:

| Name | Clashes between | Fix |
| --- | --- | --- |
| `Sphere`, `Ray` | `orblit_collide` and `vector_math` | `import 'package:vector_math/vector_math_64.dart' hide Ray, Sphere;` |
| `Colors` | Flutter and `vector_math` | `hide Colors` on `vector_math` |
| `Align` | `orblit_agent` and Flutter | `hide Align` on one, or a prefix |
| `Key`, `Easing` | `orblit_sequence` and Flutter | `hide Key, Easing` on one, or a prefix |
| `Blend` | `orblit_agent` and `orblit_camera` | Prefix one: `import '...orblit_camera.dart' as cam;` |
| `Wait` | `orblit_agent` and `orblit_effect` | Prefix or `hide` |
| `Sequence` | `orblit_agent` and `orblit_sequence` | Prefix or `hide` |
| `Shape` | `orblit_collide` and `orblit_mesh` | Prefix or `hide` |

The behaviour-tree time limit is `Deadline`, not `Timeout`.

## orblit_camera

A `VirtualCamera` has a `body` (where to stand: `StaticBody`, `FollowBody`,
`OrbitBody`, `FramingBody`, `ScreenFollowBody`), an `aim` (where to look:
`HardLookAt`, `StaticAim`, `ComposerAim` and others), a `lens`, a `priority`
and `enabled`. The `CameraBrain` shows the highest-priority enabled camera
and blends when that changes. What it follows is any `CameraTarget`;
`FixedTarget` is the simplest, with a settable `position` and `rotation`.

```dart
import 'package:orblit_camera/orblit_camera.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart';

/// The game moves this each frame: `player.position = ...`.
final player = FixedTarget(Vector3.zero());

final brain = CameraBrain()
  ..add(
    VirtualCamera(
      name: 'chase',
      priority: 20,
      follow: player,
      lookAt: player,
      // Behind and above, in the player's own frame. Damping is seconds of
      // lag per axis, so the camera trails but never bobs.
      body: FollowBody(
        offset: Vector3(0, 2.4, 7),
        damping: Vector3(0.35, 0.18, 0.5),
      ),
      aim: const HardLookAt(),
      lens: const Lens(fieldOfView: 55),
    ),
  )
  ..snap();

/// Once a frame, with a clamped delta. The result goes in the scene.
OrblitCamera shot(double delta, double width, double height) {
  if (width > 0 && height > 0) brain.aspect = width / height;
  brain.update(delta);
  final state = brain.state;
  return OrblitCamera(
    position: state.position,
    target: state.position + state.forward,
    fieldOfView: state.lens.fieldOfView,
  );
}
```

`lookRotation(direction, null)` turns a heading into a `Quaternion` for a
target's `rotation`. `damp(current, target, seconds, dt)` smooths one number
the same way at any frame rate. Worked example:
https://orblitengine.com/examples/a-camera-that-follows/ and the guide at
`/guides/cameras/`.

## orblit_agent

Steering: a `Steerable` (`position`, `velocity`, `maxSpeed` 4, `maxForce` 8,
`radius` 0.5) is moved by `agent.integrate(force, dt)`, where the force comes
from a behaviour's `force(agent)`: `Seek(target)`, `Flee`,
`Arrive(target, slowingRadius: ...)`, `Pursue`, `Evade`,
`Wander(seed: ..., at: seconds)`, `Separate(others, radius: ...)`, `Align`,
`Cohere`, `FollowPath`. Combine with
`Blend([(behaviour: ..., weight: ...), ...])`.

Behaviour trees: build a `Node` from `Selector([...])`, `Sequence([...])`,
`Check(name, (tick) => bool)` and `Do(name, (tick) => Status.running)`, plus
`Invert`, `Repeat`, `Cooldown`, `Deadline` and `Parallel`. Give each agent a
`Brain(tree)` and call `brain.tick(now, blackboard: {...})`; inside a leaf, `tick.blackboard` and
`tick.seconds`. One tree can be shared by many brains. Worked example:
https://orblitengine.com/examples/something-that-chases-you/ and
`/guides/agents/`.

## orblit_collide

Shapes are positioned by their centre: `Sphere(position, radius)`,
`Box(position, size)`, `Capsule(position, height:, radius:)`.
`contact(a, b)` gives a `Contact` (`point`, `normal`, `depth`) or null;
`overlaps(a, b)` is the cheaper yes or no. `Ray(from, direction, distance:)`
normalises its direction; `raycast(ray, shape)` gives a `Hit` (`distance`,
`point`, `normal`, `inside`), and `raycastFirst(ray, shapes)` the nearest
with its index. `Broadphase` buckets many shapes; `Layers` (what a body is,
and what it cares about) filters pairs. The 2D side is `Circle`, `Rectangle`
and `touching`.

```dart
import 'package:orblit_collide/orblit_collide.dart';
import 'package:vector_math/vector_math_64.dart' hide Ray, Sphere;

/// Which of [targets] a shot from [from] along [direction] hits first.
int? shotHits(Vector3 from, Vector3 direction, List<Shape> targets) =>
    raycastFirst(Ray(from, direction, distance: 50), targets)?.index;

/// Whether a player half a metre wide has reached a 1 m crate.
bool reached(Vector3 player, Vector3 crate) =>
    overlaps(Sphere(player, 0.5), Box(crate, Vector3.all(1)));
```

## orblit_effect

An `Effect` is asked what it looks like at a moment, `effect.at(seconds)`,
and answers a `Change` (`move`, `turn`, `grow`, `tint`, `fade`). Tweens:
`MoveBy(offset, duration)`, `TurnBy(angle, duration: ...)` (radians, about
`axis`, default up), `GrowTo(factor, duration)`, `TintTo(colour, duration)`,
`FadeTo(opacity, duration)`, `Shake(strength, duration)`, each with an
`ease:` from `Eases`. Combine with `Then([...])`, `Both([...])`,
`Again(inner, times: ...)`, `OutAndBack(inner)`, `After(delay, inner)` and
`Wait(duration)`.

Apply a change with `applied(transform, change)`, never with
`Quaternion.rotated`, which turns the wrong way. `applied` handles move, turn
and grow; tint and fade are yours to multiply into a colour.

```dart
import 'dart:math' as math;

import 'package:orblit_effect/orblit_effect.dart';
import 'package:vector_math/vector_math_64.dart';

/// A hop with a half turn, then a small shake. Built once, sampled by time.
final Effect hop = Then([
  Both([
    OutAndBack(MoveBy(Vector3(0, 1, 0), 0.2)),
    TurnBy(math.pi, duration: 0.4),
  ]),
  const Shake(0.05, 0.3),
]);

/// The transform [seconds] after the hop began.
Matrix4 hopping(Matrix4 resting, double seconds) =>
    applied(resting, hop.at(seconds.clamp(0.0, hop.duration)));
```

For effects started by events, a `Playing` holds several: `start(effect)`,
`advance(dt)` (returns what finished), and `change` for the sum. Finished
effects keep their end state until `bake()` hands it over to fold into the
object's resting transform.

## orblit_sprite

Data only; `OrblitSprites` in the scene draws (see
[scene-api.md](scene-api.md)). `Atlas.grid(...)` cuts an evenly spaced sheet
into regions named `<name>_0` upwards in reading order; `Atlas.read(json)`
reads what TexturePacker and Aseprite write (null if it can't).
`SpriteAnimation.from(atlas, prefix, fps: ...)` or
`SpriteAnimation.at(regions, fps: ...)` (default 12, `loop` true,
`pingPong` false) and `animation.at(seconds)` gives the `Region` to show.
`region.uv(imageWidth, imageHeight)` gives the fractions an `OrblitSprite`
takes, with `v0` at the top on both sides.

```dart
import 'package:orblit_filament/orblit_filament.dart';
import 'package:orblit_sprite/orblit_sprite.dart';

/// A 256 by 64 sheet of four 64-pixel frames: run_0 to run_3.
final sheet = Atlas.grid(
  image: 'hero.png',
  imageWidth: 256,
  imageHeight: 64,
  cellWidth: 64,
  cellHeight: 64,
  name: 'run',
);
final run = SpriteAnimation.from(sheet, 'run', fps: 10);

/// The hero standing at ([x], [y]) at [seconds] into the run.
OrblitSprite hero(double x, double y, double seconds) {
  final frame = run.at(seconds);
  if (frame == null) return OrblitSprite(x: x, y: y);
  final uv = frame.uv(sheet.width, sheet.height);
  return OrblitSprite(
    x: x,
    y: y,
    pivotY: 0, // the feet, not the middle
    u0: uv.u0,
    v0: uv.v0,
    u1: uv.u1,
    v1: uv.v1,
  );
}
```

A packed atlas may have trimmed regions: place them with `offsetX`,
`offsetY` and `placedSize`. `AtlasPackOptions` has `allowRotation` off by
default; keep it off, since `OrblitSprite` takes an upright rectangle.
`Parallax(layers).at(cameraX, seconds)` gives each layer's offset;
`TileMap` and `Tileset` hold tile maps. Guide: `/guides/two-dimensions/`.

## orblit_ui

`UiSurface(description: UiNode(...), onEvent: (handler, payload) {...},
width: ...)` builds real widgets from a tree of `UiNode(type: ...,
classes: ..., text: ..., props: {...}, children: [...])`. Types include
`column`, `text` and `button`; classes are utility names such as
`p-6 gap-4 bg-slate-900 rounded-xl`, or pass `css:`. A button names its
handler in `props: {'onPressed': 'resume'}` and `onEvent` receives
`'resume'`, so a description can come from a file. Lay it over the
`OrblitView` in a `Stack`. For a simple menu, plain Flutter widgets are just
as good. Worked example:
https://orblitengine.com/examples/a-menu-over-the-game/ and
`/guides/interfaces/`.

## orblit_scene and orblit_stage

Scene files as documents, for games built in the editor.
`SceneDocument.decode(text)` returns a `SceneLoad` with `document` and
`problems`. `OrblitDocumentView(document, projectRoot: ...)` stages it; draw
`OrblitView(scene: view.scene)` and change it with
`view.apply(SceneDiff.between(before, after))`. A `TransformComponent` built
with only `position` resets rotation and scale, so carry them over. Guide:
`/guides/scene-files/`.

## orblit_sequence

A `Sequence` is tracks of clips over a playhead; `sampleAt(seconds)` gives a
`SequenceFrame` of every value at that moment, with nothing remembered
between samples. Marks (events on an interval) come only from advancing a
`Director`, so scrubbing never fires them.

## More worked examples

All compiled in CI against the engine:

- https://orblitengine.com/examples/a-model-on-screen/
- https://orblitengine.com/examples/a-thousand-things/
- https://orblitengine.com/examples/a-camera-that-follows/
- https://orblitengine.com/examples/something-that-chases-you/
- https://orblitengine.com/examples/a-menu-over-the-game/

Append `.md` for Markdown; if that 404s, use the page as is.
