# Scene API: constructors, fields and defaults

Taken from `packages/orblit_filament/lib/src/` at engine commit `94985ff`
(September 2026). Orblit is pre-alpha, so treat this as a map of where to
look, not a contract: open the file named in each heading before relying on a
field, especially one not listed here. Everything below is exported from
`package:orblit_filament/orblit_filament.dart`. Vectors and matrices are
`vector_math_64` types.

## `OrblitView` (`orblit_view.dart`)

`const OrblitView({key, scene, onSceneNotes, onAssetInfo, onViewport, seconds})`

| Field | Type | Meaning |
| --- | --- | --- |
| `scene` | `OrblitScene?` | What to draw. Null draws the renderer's placeholder scene |
| `onSceneNotes` | `ValueChanged<Map<String, String>>?` | What couldn't be done. Only called with a non-empty map, on every send that has notes |
| `onAssetInfo` | `ValueChanged<OrblitAssetInfo>?` | A description of each model file once it loads |
| `onViewport` | `ValueChanged<int>?` | The texture id of the view's surface, once created |
| `seconds` | `double?` | Pins the renderer's own clock (weather, rain, cloud). Null uses the frame's timestamp |

Statics that take the texture id from `onViewport`:
`Future<double> gpuMilliseconds(int textureId)`,
`Future<OrblitDeviceProfile?> profileOf(int textureId)` (its `tier` is an
`OrblitDeviceTier`: `low`, `medium` or `high`), and `capture(...)`.

The scene is resent only when `scene` is not `identical` to the last one sent.

## `OrblitScene` (`scene.dart`)

`OrblitScene({required objects, required camera, ...})`. Not `const`.
Everything else is optional:

| Field | Default when omitted |
| --- | --- |
| `lights` | none |
| `sky` | `OrblitSky()` |
| `fog` | `OrblitFog.none` |
| `precipitation` | `OrblitPrecipitation.none` |
| `populations`, `splats`, `sprites`, `materials`, `videos` | none |
| `post` | `OrblitPostProcess()` |
| `pipeline` | `OrblitPipeline()` |
| `graph` | `OrblitRenderGraph.standard()` |
| `environment` | `OrblitEnvironment.none` |
| `probes`, `volumes`, `decals`, `distortions` | none |
| `field` | `OrblitField.none` |
| `outline` | `OrblitOutline.none` |
| `godRays` | `OrblitGodRays.off` |
| `batching` | `true` |
| `depthPrepass` | `false` |

`copyWith(...)` returns the same scene with fields changed.

## `OrblitObject` (`scene.dart`)

`const OrblitObject({required key, required transform, required colour, ...})`

| Field | Type | Default | Notes |
| --- | --- | --- | --- |
| `key` | `int` | required | Shares one key space with lights, probes and decals |
| `transform` | `Matrix4` | required | World transform |
| `colour` | `Vector3` | required | Linear RGB. Colours the placeholder cube only |
| `mesh` | `String?` | null | Absolute path or `OrblitResources` name of a glTF or glb (FBX and OBJ are converted). Null is the cube, 2 m across |
| `material` | `int?` | null | Key of an `OrblitMaterial` in `scene.materials`. Overrides a model's own materials. A key the scene doesn't list falls back to `colour` |
| `castShadows` | `bool` | true | |
| `receiveShadows` | `bool` | true | |
| `visible` | `bool` | true | |
| `layer` | `int` | 0 | 0 to `OrblitScene.maxLayer` (6) |
| `animation` | `OrblitAnimation?` | null | One of the model's clips |
| `morphWeights` | `List<double>?` | null | Nought to one per morph target. Stops the object batching |
| `variant` | `int?` | null | A glTF material variant |
| `joints` | `List<OrblitJointPose>?` | null | Local joint transforms, applied after any clip |

`OrblitAnimation` (`models.dart`):
`const OrblitAnimation({required clip, seconds = 0, speed = 1, loop = true, from, fade = 1})`.
`clip` is an index into the model's clips (find it with
`OrblitAssetInfo.clipNamed('Walk')`), `seconds` is where the clip is when the
scene is sent, `speed` 0 holds it, and `from` is the clip being faded out of.

## `OrblitLight` (`scene.dart`)

`OrblitLight({required key, required kind, required intensity, ...})`. Not `const`.

| Field | Default | Notes |
| --- | --- | --- |
| `kind` | required | `OrblitLightKind.directional`, `point`, `spot` or `area` |
| `intensity` | required | Lux for `directional`, lumens for the rest |
| `colour` | `(1, 1, 1)` | Linear RGB |
| `position` | `(0, 0, 0)` | Ignored by `directional` |
| `direction` | `(0, -1, 0)` | The way the light travels |
| `falloffRadius` | 10 | Metres. Also a culling distance |
| `innerConeAngle` | 0.5 | Radians, spot only |
| `outerConeAngle` | 0.6 | Radians, spot only |
| `sunAngularRadius` | 0.263 | Degrees, directional only |
| `sourceRadius` | 0.1 | Shadow softness, read by `area` and `soft` shadows only |
| `haloSize`, `haloFalloff` | 10, 80 | The sun's halo |
| `castShadows` | true | |
| `width`, `height` | 1, 1 | Metres, area only |
| `tangent` | `(1, 0, 0)` | The edge `width` runs along, area only |

Budgets per view: one directional (a second is ignored, note `directional`),
256 point and spot (note `punctual`), 16 area (note `area`). Only the first
area light asking to cast gets a shadow (note `areaShadows`).

## `OrblitCamera` (`scene.dart`)

`const OrblitCamera({required position, required target, fieldOfView = 50, orthographic = false, viewHeight = 10, aperture = 16, shutterSpeed = 1 / 125, sensitivity = 100})`

`fieldOfView` is vertical, in degrees. `viewHeight` is the orthographic
view's height in metres. The exposure defaults are "sunny 16", right for
100,000 lux. Has `copyWith`.

## `OrblitSky` (`scene.dart`)

`OrblitSky({colour, zenith, horizon, ambient = 28000, showBody = true, drawn = true, quality = SkyQuality.full, clouds, ...})`

`ambient` is the sky's light in lux. `drawn: false` skips the dome (for
interiors). `quality` is `SkyQuality.lean`, `fair` or `full`. `clouds`
defaults to `OrblitClouds.none`. An `OrblitEnvironment` skybox replaces the
drawn sky; see `showSkybox` below.

`OrblitFog({density = 0.05, distance = 0, height = 0, heightFalloff = 1, ...})`,
and `OrblitFog.none` for none.

## `OrblitMaterial` (`material.dart`)

`const OrblitMaterial({required key, ...})`, listed in `scene.materials` and
named from an object's `material`.

| Field | Default |
| --- | --- |
| `shading` | `OrblitShading.lit` (`unlit` ignores light, for markers and screens) |
| `blend` | `OrblitBlend.opaque` (`transparent`, `fade`, `masked`, `add`) |
| `baseColour` | `Vector4(0.8, 0.8, 0.8, 1)` |
| `metallic` | 0 |
| `roughness` | 0.5 |
| `reflectance` | 0.5 |
| `emissive`, `emissiveIntensity` | zero, 0 |
| `doubleSided` | false |
| `baseColourMap`, `normalMap`, `metallicRoughnessMap`, `occlusionMap`, `emissiveMap` | null, each an `OrblitTexture` |

`const OrblitTexture(path, {srgb = true})` reads PNG, JPEG and KTX2.

## `OrblitEnvironment` (`environment.dart`)

- `const OrblitEnvironment({radiance, skybox, intensity = 30000, rotation = 0, showSkybox = true, size = 0})`
  takes `cmgen`-baked `.ktx` files.
- `const OrblitEnvironment.fromImage(path, {intensity = 30000, ...})` filters
  an `.hdr` or `.exr` while the scene runs.
- `OrblitEnvironment.none` is the default.

With a skybox showing, the procedural sky is switched off. `showSkybox: false`
keeps the environment's lighting and lets `OrblitSky` draw the background.

`const OrblitProbe({required key, required position, radius = 12, resolution = 256, ...})`
shares the object key space.

## Pipeline and post (`pipeline.dart`, `post.dart`)

- `OrblitPipeline({shadows, resolution, lighting, samples = 1, precise = false, culling = true, refraction = true, textures})`,
  or `OrblitPipeline.at(OrblitDetail.low)` (`low`, `medium`, `high`, `ultra`).
- `OrblitShadows({kind = OrblitShadowKind.sharp, mapSize = 1024, cascades = 2, ...})`;
  kinds are `sharp`, `soft`, `area` and `variance`.
- `OrblitPostProcess({enabled = true, antiAliasing = AntiAliasing.fxaa, bloom, depthOfField, vignette, occlusion, reflections, grading, dithering = true})`.

## `OrblitPopulation` (`population.dart`)

`OrblitPopulation({required key, required transforms, required colours, required minimum, required maximum, mesh, range = 0, fade = OrblitFade.sink, revision = 0, castShadows = false, receiveShadows = true, layer = 0})`

- `transforms`: `Float32List`, 16 floats per member, column-major, world space.
- `colours`: `Float32List`, 3 floats per member, linear RGB. An assert checks
  the two lengths agree.
- `minimum`, `maximum`: one world box around every member. Too small and the
  whole population blinks out as the camera turns.
- `range`: metres beyond which members fade out (`fade` is `sink`, `shrink`
  or `none`); 0 draws them all.
- `revision`: bump after writing into either buffer. The buffers are sent only
  when it changes.

```dart
import 'dart:math' as math;
import 'dart:typed_data';

import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart';

/// Ten thousand small cubes on a grid, drawn as one population.
class Field {
  Field() {
    final random = math.Random(1);
    for (var i = 0; i < count; i++) {
      final x = (i % side) - side / 2 + 0.5;
      final z = (i ~/ side) - side / 2 + 0.5;
      final member = Matrix4.translationValues(x, 0.2, z)
        ..scaleByDouble(0.2, 0.2, 0.2, 1); // the cube is 2 m, so 0.4 m
      transforms.setRange(i * 16, i * 16 + 16, member.storage);
      colours.setAll(i * 3, [0.3 + 0.4 * random.nextDouble(), 0.5, 0.3]);
    }
  }

  static const side = 100;
  static const count = side * side;
  final transforms = Float32List(count * 16);
  final colours = Float32List(count * 3);
  int revision = 0;

  /// Moves every member in place, then bumps [revision] so the view sends
  /// the buffers again. A population that doesn't move never bumps it, and
  /// costs nothing per frame.
  void wave(double seconds) {
    for (var i = 0; i < count; i++) {
      // Column-major: the translation is at 12, 13 and 14.
      transforms[i * 16 + 13] = 0.2 + 0.2 * math.sin(seconds * 2 + i * 0.1);
    }
    revision++;
  }

  OrblitPopulation get population => OrblitPopulation(
        key: 500,
        transforms: transforms,
        colours: colours,
        minimum: Vector3(-side / 2, -0.2, -side / 2),
        maximum: Vector3(side / 2, 0.6, side / 2),
        revision: revision,
      );
}
```

Pass `populations: [field.population]` in the scene.

## Sprites (`sprites.dart`)

`const OrblitSprite({required x, required y, width = 1, height = 1, depth = 0, rotation = 0, pivotX = 0.5, pivotY = 0.5, u0 = 0, v0 = 0, u1 = 1, v1 = 1, red = 1, green = 1, blue = 1, alpha = 1})`.
A negative `width` flips it.

`OrblitSprites({required key, required sprites, image, transform, tint, order = 0, filter = OrblitFilter.sharp, snap, blend = OrblitSpriteBlend.alpha, revision = 0})`.
One layer is one image and one draw. `sprites` is a `Float32List` from
`OrblitSprites.pack(list)`. Changing `transform` or `tint` is free; changing
the sprites needs a new `revision`. Layers draw by `order`, lowest first.

```dart
import 'package:orblit_filament/orblit_filament.dart';
import 'package:vector_math/vector_math_64.dart';

OrblitScene spriteScene(int revision) {
  final layer = OrblitSprites(
    key: 1,
    sprites: OrblitSprites.pack(const [
      OrblitSprite(x: 0, y: 0, width: 2, height: 2),
      OrblitSprite(x: 3, y: 0, width: -2, height: 2),
    ]),
    // No image draws flat rectangles, useful before the art exists.
    image: const OrblitTexture('/path/to/sheet.png'),
    revision: revision,
  );
  return OrblitScene(
    objects: const [],
    sprites: [layer],
    camera: OrblitCamera(
      position: Vector3(0, 0, 20),
      target: Vector3.zero(),
      orthographic: true,
      viewHeight: 18,
    ),
    // Without these the renderer grades the art like a photograph.
    post: OrblitPostProcess(
      antiAliasing: AntiAliasing.off,
      dithering: false,
      grading: OrblitGrading(toneMapping: ToneMapping.linear),
    ),
  );
}
```

`orblit_sprite` turns atlases and animations into the `u0`, `v0`, `u1`, `v1`
numbers; see [packages.md](packages.md).

## `OrblitResources` (`resources.dart`)

- `OrblitResources.nameFor(String path)` returns `'orblit:resource/$path'`.
- `Future<void> OrblitResources.provide(String name, Uint8List bytes)` hands
  the bytes over.
- `Future<bool> OrblitResources.release(String name)` lets them go.

Use the name wherever a path would go: an object's `mesh`, a material's
textures, an environment, a decal, a splat capture. The renderer looks there
before the disk, including for the files a `.gltf` names beside itself. It
doesn't load a name again once it has it, so changed bytes need a new name (a
content hash in it works). A scene may name something before its bytes
arrive; it draws without them until they do.
