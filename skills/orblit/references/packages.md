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
| `orblit_motion` | Animation clips as `.oclip` files, played on a scene's entities and a model's bones, with marks and root motion, and imported from glTF | `ClipDocument`, `ClipPlayer`, `sceneOpsFor`, `clipsFromGltf` |
| `orblit_terrain` | Ground as data: regions of heights, cover and colour, kept as `.oterrain` and `.oregion` files, with the height and slope anywhere and no physics | `Terrain`, `TerrainSet`, `Cover`, `AutoCover`, `TerrainStroke`, `Brush`, `heightAt`, `normalAt`, `raycast` |
| `orblit_sprite` | 2D data: atlases and a packer, sprite animation, parallax, tile maps. Draws nothing; `OrblitSprites` in the scene draws | `Atlas`, `Region`, `SpriteAnimation`, `Parallax`, `TileMap` |
| `orblit_ui` | A game interface as a tree of nodes with utility classes or CSS, built into real Flutter widgets | `UiSurface`, `UiNode` |
| `orblit_scene` | A scene as a document: entities with stable ids, components, migrations, diffs | `SceneDocument.decode`, `SceneDiff`, `TransformComponent` |
| `orblit_stage` | Stages a scene document for the renderer, and a terrain | `OrblitDocumentView`, `terrainFrom` |
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
`orblit-editor` (the editor app), `orblit-examples` (the gallery app) and
`orblit-physics` (rigid bodies, and simulating a scene document's bodies; see
[physics.md](physics.md)).
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
| `Key`, `Easing` | `orblit_sequence`, `orblit_motion` and Flutter | `hide Key, Easing` on one, or a prefix |
| `Blend` | `orblit_agent` and `orblit_camera` | Prefix one: `import '...orblit_camera.dart' as cam;` |
| `Wait` | `orblit_agent` and `orblit_effect` | Prefix or `hide` |
| `Sequence` | `orblit_agent` and `orblit_sequence` | Prefix or `hide` |
| `Shape` | `orblit_collide`, `orblit_mesh` and `orblit_physics` | Prefix or `hide` |
| `Layers` | `orblit_collide` and `orblit_physics` | Prefix or `hide` |

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
https://orblitengine.com/docs/examples/a-camera-that-follows/ and the guide at
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
https://orblitengine.com/docs/examples/something-that-chases-you/ and
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
https://orblitengine.com/docs/examples/a-menu-over-the-game/ and
`/guides/interfaces/`.

## orblit_scene and orblit_stage

Scene files as documents, for games built in the editor.
`SceneDocument.decode(text)` returns a `SceneLoad` with `document` and
`problems`. `OrblitDocumentView(document, projectRoot: ..., materials: ...,
look: ...)` stages it; draw `OrblitView(scene: view.scene)` and change it with
`view.apply(SceneDiff.between(before, after))`. A `TransformComponent` built
with only `position` resets rotation and scale, so carry them over. Guide:
`/guides/scene-files/`.

`orblit_scene` also holds the material document (`.omat`, `MaterialLibrary`,
`MaterialComponent` and its looks) and the scene writers and readers —
`document.writeAs(SceneFormat.glb)` and `readSceneFrom(bytes)` for glTF, GLB
and OBJ. Both report what they could not carry rather than throwing. Details
and the traps: [assets.md](assets.md).

## orblit_asset

Assets as bytes, wherever they come from. Cooking source files for a device
(`GltfImporter`, `AtlasImporter`, `RuntimeImport`, a content-hashed cache),
and fetching them over a network (`AssetOrigin`, `FetchPolicy`,
`AssetFetcher`, `NetworkAssetSource`, `HttpTransport`, and `MapTransport` for
tests). `fetchInStages` gives a stand-in, then a texture's own coarse mip
levels while the rest arrives, then the whole file. Details:
[assets.md](assets.md).

## orblit_sequence

A `Sequence` is tracks of clips over a playhead; `sampleAt(seconds)` gives a
`SequenceFrame` of every value at that moment, with nothing remembered
between samples. Marks (events on an interval) come only from advancing a
`Director`, so scrubbing never fires them.

## orblit_motion

Clips you own, as `.oclip` files the editor's timeline also writes. A
`ClipDocument` (`name`, `duration`, `whenDone`, `rate`, `channels`, `marks`,
`rootMotion`) holds `ClipChannel`s, each naming a `target` entity id (empty
for whatever plays the clip), a `bone` or null, and a `property`
(`transform.position` or `light.power` on an entity; `position`, `rotation`
or `scale` on a bone), with a `ChannelKind` and `Key(at, value, hold: ...)`s.
It re-exports `Key`, `Hold`, `Easing`, `Mark` and `WhenDone` from
`orblit_sequence`. `clip.encode()` writes the file and
`ClipDocument.decode(text)` gives a `ClipLoad` (`clip`, `problems`).

`clip.sampleAt(seconds)` is a `ClipFrame`, with no state. `ClipPlayer(clip)`
starts **paused**: call `play()`, then `advance(dt)` once a frame for a
`ClipStep` with the `frame`, the `marks` passed (every lap included) and the
root motion `moved`. `seek` fires no marks and moves nothing.

```dart
import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_scene/orblit_scene.dart';
import 'package:orblit_stage/orblit_stage.dart';

class LampAnimator {
  LampAnimator(this.view, ClipDocument clip, String lamp)
    : player = ClipPlayer(clip)..play(),
      scope = ClipScope.inScene(view.document, lamp);

  final OrblitDocumentView view;
  final ClipPlayer player;
  final ClipScope scope;

  void tick(double seconds) {
    final step = player.advance(seconds);
    final ops = sceneOpsFor(step.frame, view.document, scope);
    if (ops.isNotEmpty) view.apply(SceneDiff(ops));
  }
}
```

`ClipScope.inScene` maps a prefab's ids onto the placed instance's, so one
clip plays on every copy. For a model's skeleton, hand the frame's bones to
the skin binding and leave the object's `animation` null:
`OrblitSkinBinding(armatureOfSkin(skin), skin, index: i).jointsFrom(frame.bones[''] ?? const {})`
into `OrblitObject.joints`, one binding per skin, built once. `poseFrom`
writes into an `orblit_rig` `Pose` instead, for a rig to adjust.

Root motion: `clip.copyWith(rootMotion: RootMotion(bone: 'hips', turns: true))`,
then each frame `position += heading.asRotationMatrix().transformed(moved.position)`
and `heading = (heading * moved.rotation)..normalize()`. Never
`Quaternion.rotated`, which turns the other way. `clipsFromGltf(bytes)` gives
`ClipsImported` (`clips`, `problems`), naming bones the way the binding does.
A scene file's `motion` component (`MotionComponent`: `clips`, `autoplay`) is
data only; nothing plays it. Changing clip cuts, with no fade yet. Guide:
`/guides/animation/`.

## orblit_terrain

Ground as data, in plain Dart. `Terrain({regionSize = 256, spacing = 1, sets, autoCover, blendSharpness = 0.87})`
is a grid of heights, one every `spacing` metres, in square regions of
`regionSize` texels (a power of two), made only where there is ground.
`addRegion(RegionKey(x, z))` makes one, `fillHeights(key, (x, z) => height)`
fills one from world positions, `regionAt(key)` finds one and `putRegion`
adds a decoded one. Texel `(i, j)` of region `(x, z)` is at
`((x * regionSize + i) * spacing, (z * regionSize + j) * spacing)`.

A `TerrainRegion` holds three maps: `heights` (`Float32List`), `cover`
(`Uint32List`) and `colour` (`Uint8List`, RGBA), written through
`setHeight`, `setCover` and `setColour(i, j, value)`. Each setter moves the
region's `revision`. Writing the lists directly does not, so call `touch()`
after that.

- `Cover.of({base, overlay, blend, angle, scale, hole, navigation, automatic})`
  is one 32-bit word: two sets (0 to 31), how much of the overlay shows (0 to
  1), a turn in sixteenths, a scale step (`Cover.scales`: 0, 20, 40, 60, 80,
  −60, −40, −20 percent), a hole, a navigation mark the renderer ignores, and
  `automatic`, which ignores the sets named and chooses by slope and height.
  `withHole`, `withAngle` and the like change one part. A new region is all
  `Cover.auto`.
- `GroundColour.of({red, green, blue, roughness})` tints what the sets draw
  and nudges roughness (−1 to 1). `GroundColour.none` changes nothing.
- `TerrainSet({name, albedo, normal, tileSize = 4, triplanar = false})` names
  its pictures by path. The albedo's alpha is a height, and the taller of two
  blending sets shows through (`blendSharpness`, 0 a fade, 1 a hard edge).
  The normal map's alpha is roughness. `tileSize` is metres per copy.
  `triplanar` stops cliffs stretching, at three reads instead of one.
- `AutoCover({steep = 0, flat = 1, slope = 1, heightFalloff = 0.1})`: at
  `slope` 1, ground tilted 60° is all `steep`, and at 2, 41°.
  `heightFalloff` is per hundred metres.

`heightAt(x, z)` and `normalAt(x, z)` split each square of four texels into
the same two triangles as the mesh, so what they say is what is drawn. Both
are null off the regions and on a triangle touching a hole.

Files: `terrain.encode()` is the `.oterrain` text and `region.encode()` a
region's `.oregion` bytes, named `region.key.fileName` (`x0_z-1.oregion`).
`Terrain.decode(text)` gives a `TerrainLoad` (`terrain`, `regions` to fetch,
`problems`), and `TerrainRegion.decode(bytes)` a `RegionLoad` (`region`,
`problems`). No paths, so it works in a browser.

Brushes: `TerrainStroke(terrain, {required tool, brush = const Brush(), invert = false, set = 0, colour = GroundColour.none, roughness = 0, height, seed = 0})`
is one press. `moveTo(x, z)` lays a dab every `brush.spacing * brush.size`
metres along the way, writes the maps, touches the regions and returns a
`TerrainPatch`. `BrushTool` is `raise`, `lower`, `smooth`, `flatten` (to
`height`, or where the stroke began), `slope` (a ramp from the start to the
farthest point reached), `cover` (lays set `set`), `colour`, `roughness`
(−1 to 1) and `hole`. `invert` runs raise, lower, cover, colour, roughness
and hole backwards. `Brush({size = 16, strength = 0.5, falloff = 0.5, jitter = 0, spacing = 0.25})`
is shared by every tool. Strength is per pass, not per dab. A patch keeps
the 32-texel tiles it touched, before and after, for the one map its tool
writes: `followedBy` folds a stroke's patches into one, and `apply` and
`revert` write it back. `TerrainRecorder` makes one for any other edit.
Ground with no region is left alone. `terrain.raycast(origin, direction, maxDistance: ...)`
is where a ray first meets the drawn surface, for putting a brush under a
pointer.

In a scene: `TerrainComponent({file, castShadows = true, receiveShadows = true})`
in `orblit_scene` (`SceneComponents.terrain`, JSON key `terrain`) names the
`.oterrain` by project path. The ground sits where its texels say, whatever
the entity's transform. `OrblitDocumentView` does not draw it yet.

Drawing: `terrainFrom(terrain, key: 1, pixels: (path) => rgba[path], picturesRevision: 0, meshSize: 64, levels: 6)`
in `orblit_stage` gives the `OrblitTerrain` for `OrblitScene(terrain: [...])`.
`pixels` returns decoded RGBA rows, square and every picture the same size.
A missing one draws plain. The renderer takes regions of 16 to 2048 texels,
256 regions all within 128 regions of each other, 32 sets, pictures up to
4096 across, a grid of 16 to 256 squares and up to 12 levels. Beyond those it
throws `ArgumentError` saying why.

Traps:

- **Pictures that change in place.** They cross again only when the sets or
  their size change. Move `picturesRevision` when only the pixels do.
- **A const list of sets.** `Terrain` copies the list it is given, so
  `terrain.sets[0] = ...` works even if the list was `const`. A `sets` list
  kept elsewhere is not the terrain's.
- **Settings cost nothing.** `autoCover`, `blendSharpness`, `meshSize` and
  `levels` travel every frame, and no region is resent for them.

Guide: `/guides/terrain/`. The gallery's Terrain example drives a box over
hills with `heightAt` and `normalAt`.

## More worked examples

All compiled in CI against the engine:

- https://orblitengine.com/docs/examples/a-model-on-screen/
- https://orblitengine.com/docs/examples/a-thousand-things/
- https://orblitengine.com/docs/examples/a-camera-that-follows/
- https://orblitengine.com/docs/examples/something-that-chases-you/
- https://orblitengine.com/docs/examples/a-menu-over-the-game/

Append `.md` for Markdown; if that 404s, use the page as is.
