---
name: orblit
description: Use when building a game, app or scene with Orblit, the 3D and 2D engine for Flutter. Covers writing or fixing Dart that uses OrblitScene, OrblitView, OrblitObject, OrblitLight, OrblitCamera, OrblitPopulation, OrblitSprites or any orblit_* package; adding Orblit to a Flutter project and setting up macOS, iOS, Android, Linux, Windows or the web; materials and looks, scene documents and their glTF, GLB and OBJ export and import, Gaussian splats, cooked and networked assets; animation clips and blends with orblit_motion (.oclip files on entities and bones, root motion, clips from glTF; .oblend graphs of states that mix clips along a line or across a plane and fade between them, with a place that saves and replicates); terrain with orblit_terrain (heights, texture sets, brushes, heightAt, terrainFrom); rigid-body physics, walking characters and terrain collision with orblit_physics, and bodies in scene documents; and questions about Orblit's API, lighting units, platforms, or why a scene is black or missing something. Orblit is pre-alpha and its names change between commits, so this skill says how to check the real API first. It is for building with Orblit, not for working on the engine itself.
---

# Building with Orblit

Orblit is a set of Dart and Flutter packages over Google's Filament renderer.
A game states its whole scene as a value every frame, as an `OrblitScene`, and
an `OrblitView` widget draws it. Everything that isn't drawing (cameras,
agents, collision, effects, cutscenes, 2D atlases, scene files) is plain Dart.

## It is pre-alpha: check before you write

Nothing is API-stable. Names, fields and defaults change between commits, the
packages aren't on pub.dev, and anything you remember about Orblit is likely
stale or wrong. Before you use a class, field or named argument you haven't
seen in this session, confirm it in one of these, in this order:

1. **The source the project actually resolves.** For a git dependency the
   package is at
   `${PUB_CACHE:-$HOME/.pub-cache}/git/orblit-<commit>/packages/<package>/lib/`,
   and `<commit>` is the `resolved-ref` in the project's `pubspec.lock`. If the
   project uses path dependencies on a sibling checkout (the engine's own
   editor and examples do), read that checkout instead. `lib/<package>.dart`
   lists what's exported. The doc comments are long and are the real
   reference.
2. **The gallery examples** in the same checkout,
   `packages/orblit_examples/lib/src/examples/*.dart`. Each is a working scene
   against that exact commit, so copy patterns from them rather than from
   memory.
3. **The docs site.** https://orblitengine.com/llms.txt is an index written for
   models, https://orblitengine.com/llms-full.txt is every page in one long
   file, and any page is also Markdown with `.md` on the end, such as
   https://orblitengine.com/docs/guides/lighting.md. The gallery is at
   `https://orblitengine.com/docs/gallery/<section>/`, where `<section>` is one of
   `basics`, `lighting`, `materials`, `atmosphere`, `effects`, `content`,
   `scripting`, `performance` or `showcases`. If an `.md` or `llms` URL
   returns 404, fetch the HTML page (`https://orblitengine.com/docs/guides/lighting/`)
   or go back to the source. Where the docs and the source disagree, the
   source wins.

Then run `flutter analyze` on what you wrote. Say so when you couldn't verify a
name, rather than guessing.

## Adding Orblit to a Flutter app

Flutter 3.47.0 or newer on stable, Dart 3.10 or newer. The packages resolve
from git:

```yaml
dependencies:
  flutter:
    sdk: flutter
  vector_math: ^2.1.4   # Orblit takes and returns vector_math types
  orblit_filament:
    git:
      url: https://github.com/ChxisB/orblit.git
      path: packages/orblit_filament
```

Every package lives in the one repository, so each dependency names its
`path`. Without a `ref`, pub takes the default branch, `main`, and pins the
commit in `pubspec.lock`; `flutter pub upgrade` moves it.

On a Mac, for macOS, iOS or Android, run this after every `flutter pub get`
that brings in a new engine commit (it skips copies already set up):

```sh
for setup in "${PUB_CACHE:-$HOME/.pub-cache}"/git/orblit-*/packages/orblit_filament/darwin/setup.sh; do
  bash "$setup"
done
```

A build error naming `Filament.xcframework/macos-arm64/macos.a` means that
loop hasn't run for the current commit. Linux, Windows and Android on Linux run
their setup from the build. The web is a manual build. Details for every
platform, including what each needs installed, are in
[references/platforms.md](references/platforms.md).

To prove the stack works, `MaterialApp(home: Scaffold(body: OrblitView()))`
with no scene draws the renderer's default placeholder scene.

### Which platforms draw

| Platform | State |
| --- | --- |
| macOS | The reference. CI draws a frame on every change. Apple silicon only |
| iOS | Draws on the simulator |
| Android | Draws, including on a handset. `arm64-v8a` only |
| Linux | Has drawn only on arm64 Debian 13 with Mesa's software rasteriser. No real GPU yet |
| Windows | Builds in CI. **No frame has ever been drawn on Windows** |
| Web | Chrome only, after a manual Emscripten build |

The plain-Dart packages run anywhere Dart does. When a user is on Windows or
Linux, tell them plainly how little has been seen to work there.

## The model: state the whole scene, every frame

- **Build a new `OrblitScene` each time something changes**, usually in
  `build`. It lists every object, light and population that exists. There is
  no `add`, `remove` or `addChild`; an object missing from this frame's list
  is gone. The renderer diffs by key and only sends what changed, so this is
  cheap.
- **Never mutate a scene you've handed over.** `OrblitView` resends only when
  `scene` is a different instance (`identical`), so editing a list inside a
  kept scene does nothing. Build a new one, or use `scene.copyWith(...)`.
- **Keys are `int`s you choose.** Keep them stable across frames (a changed
  key destroys the old thing and creates a new one) and unique in the scene
  (two objects on one key draw only one, with a `keys` note). **Objects and
  lights share one key space**, so a light keyed 1 and an object keyed 1 are a
  clash. Probes and decals use the same space. Reserve ranges, for example
  lights 1 to 9, fixed props 10 to 999, spawned things from 1000 up, and don't
  reuse a key for a new thing while the old one is still in the scene.
- **Each `transform` is a world transform** (`Matrix4`). There is no
  hierarchy in `OrblitScene`; compose parent and child yourself.
- **`colour` is linear RGB as a `Vector3`**, not a Flutter `Color` and not
  sRGB. It colours the placeholder cube only. A model keeps its own
  materials unless you give the object a `material` key from
  `OrblitScene.materials`, which overrides them.
- **`mesh: null` is the placeholder cube, 2 m across** (-1 to 1 on each axis).
  Scale by 0.5 for a 1 m cube.
- **The view needs bounded constraints.** It sizes its surface from
  `constraints.maxWidth` and `maxHeight`, so put it in `Positioned.fill`, an
  `Expanded` or a `SizedBox`, not an unbounded `Column` or `ListView`.

## A minimal scene

This compiles against the engine at the time of writing. Check the names
against the source if it doesn't.

```dart
import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart' hide Colors;

void main() => runApp(const MaterialApp(home: Scaffold(body: Spinner())));

class Spinner extends StatefulWidget {
  const Spinner({super.key});

  @override
  State<Spinner> createState() => _SpinnerState();
}

class _SpinnerState extends State<Spinner> with SingleTickerProviderStateMixin {
  late final Ticker _ticker;
  double _seconds = 0;
  String? _notes;

  @override
  void initState() {
    super.initState();
    _ticker = createTicker((elapsed) {
      setState(() => _seconds = elapsed.inMicroseconds / 1e6);
    })..start();
  }

  @override
  void dispose() {
    _ticker.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Stack(
      children: [
        Positioned.fill(
          child: OrblitView(
            scene: _scene(_seconds),
            onSceneNotes: (notes) {
              final text = notes.entries
                  .map((note) => '${note.key}: ${note.value}')
                  .join('\n');
              // Only on a change: notes repeat on every send.
              if (text != _notes) setState(() => _notes = text);
            },
          ),
        ),
        if (_notes != null)
          Positioned(
            left: 16,
            bottom: 16,
            child: Text(_notes!, style: const TextStyle(color: Colors.white)),
          ),
      ],
    );
  }

  // A new scene every frame, built from state. Keys: 1 and 2 are objects,
  // 10 is the sun, because objects and lights share one key space.
  OrblitScene _scene(double seconds) {
    return OrblitScene(
      camera: OrblitCamera(
        position: Vector3(5, 4, 7),
        target: Vector3(0, 1, 0),
      ),
      objects: [
        OrblitObject(
          key: 1,
          transform: Matrix4.rotationY(seconds)
            ..setTranslation(Vector3(0, 1, 0)),
          colour: Vector3(0.85, 0.42, 0.16),
        ),
        OrblitObject(
          key: 2,
          transform: Matrix4.translationValues(0, -0.05, 0)
            ..scaleByDouble(10, 0.05, 10, 1),
          colour: Vector3(0.18, 0.19, 0.21),
          castShadows: false,
        ),
      ],
      lights: [
        OrblitLight(
          key: 10,
          kind: OrblitLightKind.directional,
          direction: Vector3(-0.4, -1, -0.6)..normalize(),
          intensity: 100000, // lux: full daylight
        ),
      ],
    );
  }
}
```

The cube sits on the floor: it spans 2 m, so centred at y = 1 its base is at
0, and the floor slab's top is at 0. The default sky adds 28,000 lux of
ambient light, and the default camera exposure is set for daylight.

A small complete game (input, a clamped game loop, spawning and despawning
with stable keys) is in [references/game-loop.md](references/game-loop.md).

## Lighting and exposure

Lights use real photometric units, and the camera has a real exposure.

| `OrblitLightKind` | Unit of `intensity` | Realistic values |
| --- | --- | --- |
| `directional` | lux | 100,000 full daylight, 400 heavy overcast |
| `point` | lumens | 1,600 a bright domestic bulb, 450 a dim one |
| `spot` | lumens | As point, in a cone (`innerConeAngle`, `outerConeAngle`, radians) |
| `area` | lumens | As point, off one face of a `width` by `height` rectangle |

- **One directional light per scene.** A second is ignored and reported with
  a `directional` note. For a second sun-like source, use a spot or area
  light.
- `direction` is the way the light travels, so a sun overhead points down,
  `(0, -1, 0)`, which is the default.
- A view shades at most 256 point and spot lights (the furthest from the
  camera drop out, `punctual` note) and 16 area lights (`area` note). Only the
  first area light that asks to cast gets a shadow.
- `falloffRadius` (default 10 m) is a culling distance as much as a physical
  one. Making it far larger than the light reaches costs time and changes
  nothing.
- **Exposure.** `OrblitCamera` defaults to `aperture: 16`,
  `shutterSpeed: 1 / 125`, `sensitivity: 100`, the "sunny 16" rule for a scene
  at 100,000 lux. A black or murky interior usually means daylight exposure on
  a room: for about 300 lux use ISO 800 and f/2.8, rather than turning a sun
  down to 300.
- `OrblitSky` (default `ambient: 28000` lux) lights the scene as well as
  drawing the background. `OrblitSky(drawn: false)` skips the dome for
  interiors.
- **Sky versus environment.** Set both an `OrblitEnvironment` with a skybox and
  a procedural `OrblitSky`, and the HDRI wins, so the sky and its clouds
  vanish. Use `OrblitEnvironment(..., showSkybox: false)` to keep the
  environment's lighting behind a procedural sky.
- Objects cast and receive shadows by default; populations don't cast by
  default. Shadow quality lives on `OrblitScene.pipeline`
  (`OrblitPipeline(shadows: OrblitShadows(...))`), not on the light.
- For lights an artist states in watts, metres and degrees, `orblit_light`
  converts them.

## Time is sampled, not stepped

Keep one clock, usually a `Ticker`'s elapsed time. Anything with a playhead is
asked for a moment rather than advanced: an effect is `effect.at(seconds)`, a
sprite animation `animation.at(seconds)`, a cutscene
`sequence.sampleAt(seconds)`, and a model's clip is
`OrblitAnimation(clip: index, seconds: whereInTheClip)`. That makes scrubbing,
replays and late joiners give the same answer. Physics, behaviour trees and
blends do step, because they have history. A blend keeps all of its history in
a `BlendPlace`, a plain value that can be saved, sent or built by hand.

For the parts that step, work out `dt` from the clock and guard it the way the
gallery's runner does:

```dart
import 'dart:math' as math;

double? _last;

void frame(double seconds, void Function(double dt) update) {
  var dt = seconds - (_last ?? seconds);
  _last = seconds;
  if (dt < 0 || dt > 0.5) dt = 0; // a pause or a hitch, not game time
  update(math.min(dt, 0.05));
}
```

Smooth towards a target with `1 - math.exp(-dt * rate)` rather than a fixed
fraction per frame, or use `damp` from `orblit_camera`. `OrblitView(seconds:)`
pins the renderer's own clock (weather, rain, cloud), which you only need for
reproducible frames such as screenshots and tests.

## Many things

- **Batching is on by default.** Four or more placeholder cubes with the same
  material, colour and flags are merged into draws of up to 64 while keeping
  their own keys. Named meshes aren't merged yet. `batching: false` turns it
  off.
- **Past a few thousand, use an `OrblitPopulation`.** One mesh, one flat
  `Float32List` of transforms (16 floats each, column-major, world space) and
  one of colours (3 floats each, linear RGB), with one world-space box
  (`minimum`, `maximum`) that must cover every member or the lot blinks out.
  Members have no keys and can't be picked or moved individually by the
  renderer.
- **Bump `revision` after writing into a population's buffers.** The view
  resends the buffers only when `revision` differs from what it last sent, so
  a changed buffer with the same revision never reaches the screen, and a
  static one costs nothing per frame. `OrblitSprites` layers follow the same
  rule.

A population example is in [references/scene-api.md](references/scene-api.md).

## Models and files

- `mesh` is an absolute path to a `.glb` or `.gltf` (`.fbx` and `.obj` are
  converted on first use), or a name registered with `OrblitResources`.
- A file that can't be read is drawn as the placeholder cube, with a scene
  note keyed by its path.
- A browser has no disk and an Android app's assets are inside its archive,
  so hand bytes over by name:

  ```dart
  import 'package:flutter/services.dart';
  import 'package:orblit_filament/orblit_filament.dart';

  Future<String> provideCrate() async {
    final data = await rootBundle.load('assets/crate.glb');
    final name = OrblitResources.nameFor('crate.glb');
    await OrblitResources.provide(name, data.buffer.asUint8List());
    return name; // use this wherever a path would go
  }
  ```

- A sandboxed macOS app (Flutter's default) can't read arbitrary absolute
  paths, so models silently become cubes with a note. Provide bytes as above,
  or turn the App Sandbox off in `macos/Runner/DebugProfile.entitlements` for
  development, as the gallery does.
- `onAssetInfo` hands back an `OrblitAssetInfo` per model: its clips
  (`clipNamed`), joints (`jointNamed`), variants, materials, lights and bounds.

## Materials, scene files and assets

Two packages hold everything a project keeps on disk, and neither has Flutter,
a filesystem or a renderer in it.

`orblit_scene` has the scene document (`.oscene`), the material document
(`.omat`) and the glTF, GLB and OBJ writers and readers:

```dart
final library = MaterialLibrary(materials: {...});
final view = OrblitDocumentView(document, materials: library, look: 'winter');

final written = document.writeAs(SceneFormat.glb, materials: library);
final read = readSceneFrom(written.first.bytes);
```

A scene Orblit wrote comes back as itself — the same ids, order, parents and
components, including ones this build has never heard of. A glTF from anywhere
else is interpreted instead and says so. Neither `writeAs` nor `readSceneFrom`
throws over one thing it could not carry: both return a list of problems, and
code that does not show them makes a lossy export look successful.

`orblit_asset` cooks assets for a device and fetches them over a network. The
policy is checked before any connection is made, a 404 is distinguished from
an unreachable server, and `fetchInStages` draws a texture's own coarse mip
levels while the rest of it is still arriving.

Full API, parameter lists and the traps: [references/assets.md](references/assets.md).

## Physics

Rigid bodies are in a separate repository, `ChxisB/orblit-physics`, as three
git dependencies: `orblit_physics` (the solver, native platforms only, not the
web), `orblit_physics_scene` (`ScenePhysics`, which simulates a scene
document) and `orblit_physics_terrain` (`TerrainPhysics`, which lays a terrain
as ground). An entity gets a body from a `body` component (`BodyComponent` in
`orblit_scene`), and each `advance` answers with a `SceneDiff` for the view:

```dart
final scene = ScenePhysics(document);
final moved = scene.advance(seconds); // already in scene.document
if (!moved.isEmpty) setState(() => view.apply(moved));
```

Edits go to both `scene.apply` and `view.apply`, and are teleports. Never hand
`advance`'s own diff back to `scene.apply`. Read collisions from
`scene.events`, not `scene.physics.events`, which holds only the last step.
`Shape.box` takes half sizes where the component's `size` is edge to edge.

Anything that walks is a character, not a free body:
`physics.addCharacter(id, at: ...)`, then before every step
`drive(id, velocity: ...)` with the velocity it wants, **gravity included**,
built from `footingOf(id)!.velocity`. The world decides what it gets. It
slides along walls, climbs steps, rides platforms and pushes crates, and
crates cannot push it. Root motion goes in the same way: the clip's step,
turned to world space and divided by the tick, is the velocity to ask for.

Ground is `physics.layGround(id, heights: ..., columns: ..., rows: ...)`, and
a terrain is laid for you, the regions near the camera, by
`TerrainPhysics(physics, terrain).sync(x: ..., z: ..., radius: ...)` every
frame. Pass `refresh: false` while a brush stroke is still being drawn.

Install, API and the traps: [references/physics.md](references/physics.md).

## Terrain

Ground is data in `orblit_terrain` (plain Dart): a `Terrain` of square
`TerrainRegion`s, each a map of heights, cover words and colours, made only
where there is ground. `orblit_filament` draws it as an `OrblitTerrain`, and
`terrainFrom` in `orblit_stage` builds one from a `Terrain`:

```dart
final terrain = Terrain(regionSize: 64, spacing: 2, sets: const [
  TerrainSet(name: 'rock', albedo: 'rock.png', normal: 'rock_n.png',
      tileSize: 12, triplanar: true),
  TerrainSet(name: 'grass', albedo: 'grass.png', normal: 'grass_n.png'),
]);
terrain.fillHeights(const RegionKey(0, 0), (x, z) => math.sin(x / 20) * 4);

OrblitScene(objects: things, camera: camera, terrain: [
  terrainFrom(terrain, key: 1, pixels: (path) => decodedRgba[path]),
]);

final y = terrain.heightAt(x, z); // null off the ground or over a hole
final up = terrain.normalAt(x, z);

// A brush: one press is one stroke, and its patches fold into one undo step.
final stroke = TerrainStroke(terrain, tool: BrushTool.raise,
    brush: const Brush(size: 12, strength: 0.8));
var patch = stroke.moveTo(10, 10);
patch = patch.followedBy(stroke.moveTo(30, 10));
patch.revert(terrain); // undo
```

Build it every frame. It is cheap, because a region crosses only when its
`revision` moves, which every edit does. `heightAt` interpolates exactly as
the mesh does, so a thing placed with it sits on the drawn surface, and it
needs no physics. For things that fall and roll on it, `TerrainPhysics` in
`orblit_physics_terrain` lays it in a physics world, triangle for triangle the
same ground. A scene file names a terrain with a `terrain` component
(`TerrainComponent(file: 'terrain/hills/hills.oterrain')`), which the editor
draws and shapes (Add › Terrain, then the Terrain mode) but
`OrblitDocumentView` does not load yet: load the files and call
`terrainFrom` yourself. Only macOS has been seen to draw it. API and the
traps:
[references/packages.md](references/packages.md#orblit_terrain).

## Scene notes, not silence

The renderer reports what it couldn't do through `onSceneNotes`, a
`Map<String, String>` keyed by what was asked for (`directional`, `punctual`,
`area`, `keys`, a file's path, `environment`, `skybox` and others) with a
sentence saying what went wrong.

- Wire it up from the first scene and put the text on screen during
  development. When something is black, missing or a cube, read the notes
  before changing code.
- It fires only when there are notes, and never with an empty map, so it
  can't tell you a problem has cleared. Clear your copy yourself when you
  change the scene.
- It fires again on every send. Call `setState` only when the text changed,
  or you schedule a rebuild every frame.

## Traps

- **NaN aspect.** A viewport not yet laid out is 0 by 0. Guard
  `size.width > 0 && size.height > 0 && size.isFinite` before dividing.
  `CameraBrain`'s `aspect` setter ignores bad values, but its constructor
  doesn't.
- **Camera roll.** A camera that only pitches and yaws still needs its roll
  pinned, or a sequence of aims accumulates one.
- **Quaternions.** `vector_math`'s `Quaternion.rotate` turns the opposite way
  to `Matrix4.compose`. Use `rotateVector` from `orblit_camera`, and test
  rotation through the matrix.
- **Name clashes.** `orblit_collide` exports `Sphere` and `Ray`, as does
  `vector_math` (`hide Ray, Sphere` on one). Flutter's `Colors` clashes with
  `vector_math`'s, hence `hide Colors`. `orblit_agent`'s `Align` and
  the `Key` and `Easing` of `orblit_sequence` and `orblit_motion` clash with
  Flutter, and `Blend`,
  `Wait`, `Sequence` and `Shape` each exist in two Orblit packages. The full
  table is in [references/packages.md](references/packages.md). The
  behaviour-tree time limit is `Deadline`, not `Timeout`.
- **Steering.** `Separate` must not be normalised (it pushes harder the closer
  things are). `Arrive` does overshoot; plan for it.
- **Spatial hashes.** `x*a ^ y*b ^ z*c` collides on symmetric coordinates.
  Mix the coordinates sequentially.
- **Scene files.** `TransformComponent(position: ...)` on its own resets
  rotation and scale. Carry the fields you aren't changing.
- **Sprite atlases.** Turn rotation off in the packer (rotated regions can't
  be drawn yet) and respect `offsetX`, `offsetY` and `placedSize` for trimmed
  frames.
- **2D colours.** Sprites want an orthographic camera and
  `ToneMapping.linear`, or the renderer grades the art like a photograph.
- **Web.** The web build must use the Filament fork's `matc`. The release's
  compiles materials in which every directional light contributes nothing,
  without an error.

## The packages

`orblit_filament` draws. The rest are mostly plain Dart: `orblit_camera`
(cameras as shots, blends, damping), `orblit_agent` (steering and behaviour
trees), `orblit_collide` (shapes, raycasts, overlaps), `orblit_effect`
(change as a function of time), `orblit_sequence` (cutscenes),
`orblit_motion` (animation clips, on entities and bones alike, and blends
that mix and fade them),
`orblit_terrain` (ground as regions of heights, and standing on it),
`orblit_sprite` (atlases, sprite animation, parallax, tile maps),
`orblit_ui` (game interfaces from a tree of elements with utility classes or
CSS), `orblit_scene` and `orblit_stage` (scene documents and staging them),
`orblit_light`, `orblit_mesh`, `orblit_rig`, `orblit_noise`,
`orblit_weather`, `orblit_input`, `orblit_asset`, `orblit_core`. What each
does and the entry points worth knowing are in
[references/packages.md](references/packages.md). Add each the same way as
`orblit_filament`, with its own `path`. Physics is the exception: it is in
its own repository, as the section above says.

## Reference files

Load these when the task needs them:

- [references/scene-api.md](references/scene-api.md): constructors, fields
  and defaults for the scene types, with a population and a sprite layer.
- [references/game-loop.md](references/game-loop.md): a complete small game.
- [references/platforms.md](references/platforms.md): per-platform setup and
  what has actually been seen to work.
- [references/assets.md](references/assets.md): materials and looks, scene
  files and their export and import, splats, cooked and networked assets.
- [references/physics.md](references/physics.md): the physics world,
  characters and root motion on them, ground and terrain, bodies in scene
  documents, and simulating a document.
- [references/packages.md](references/packages.md): the other packages,
  terrain among them.

## Licence

Orblit is under MPL-2.0, the Mozilla Public License — open source, and a
file-level copyleft (not plain MIT). Games made with it, commercial ones
included, are fine and stay the author's own. Changes to Orblit's own files
stay under the same licence. For anything beyond that, point the user at
`LICENSE` in the repository rather than interpreting it.
