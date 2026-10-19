# Changelog

## 0.14.0

- Add `CookedSurface` and `OrblitDocumentView.surfaces`. A material asset
  that ends in `.oshader` is drawn with the package the cook made for it.
  An object that states no values shares one material with every other.
  One that states a value gets a material of its own under a stable key,
  so a value being dragged rewrites it in place. The stage sends the
  defaults from `params.json`, because the package carries none. Until a
  shader is cooked its objects keep what their mesh brought.
- Add `SurfaceParam`, `CookedSurface.params` and `CookedSurface.valueOf`,
  so an inspector can show a colour row for `source_color`, a slider
  for `hint_range`, and the number the renderer is sent.

## 0.13.0

- `OrblitTrailStrip` draws a `TrailStrip` as a ribbon that turns its face to
  the camera. It is a sheet two particles wide, so a trail holds at most 128
  points. The poses are relative to the newest point, `TrailStrip.head`.

## 0.12.0

- `OrblitParticles` draws the `ParticleEmitterComponent`s of a staged scene.
  Each shown emitter has a clock that starts when it is first shown and is
  kept when its component is edited. An emitter is one population of
  camera-facing cubes, sized and turned by the particle. A scene `budget`
  caps the lot, and the emitters that started first keep their share. A
  world-space emitter keeps where its entity has been, so particles stay
  where they were born while the entity moves on. A component that does not
  make an emitter is left out and named in `rejected`.
- `OrblitDocumentView.camera` is the camera the view looks through now.
  `worldOf` and `isShown` are public.

## 0.11.0

- Add `OrblitSound`. It plays each `SoundSourceComponent` from its entity
  while the entity is shown, hears from the first `SoundListenerComponent`
  or the view's camera, and plays a cutscene's sound cues. `surfaceOf`
  names what an entity is made of.
- `OrblitDocumentView` gives out `worldOf`, `isShown` and `camera`.

## 0.10.0

- `scatterFrom` takes `rangeScale`, which multiplies every block layer's
  `range`. A layer with no range stays unlimited. It changes no revision, so
  the renderer keeps the buffers it holds. A game passes a
  `DistancePolicy.fraction` to pull the grass in when frames run long.

## 0.9.0

- `OrblitParticleSheet` draws a grid of particles that something else moves,
  such as a cloth. It writes a glb with one vertex and one joint to a
  particle, and `poses` sets each joint to its particle and turns it to
  follow the cloth, so the sheet is lit as it folds. Up to 256 particles.

## 0.8.0

- A staged material carries `litScreen` to the renderer.

## 0.7.0

- `OrblitCutscenes` plays `orblit_motion` cutscenes over a staged scene, one
  at a time. One is started by name from game code, or by `startFrom` with
  the marks a clip passed, when one of them names it. Each `advance` keys
  the scene and has the view look through the shots, blending two cameras
  where shots overlap. At the end the view looks through the scene's own
  camera again. A cutscene that holds leaves the scene where it put it, and
  one that releases puts back what it moved.
- `blendCameras` averages cameras by weight: where they stand, which way
  they look and their field of view. The rest of the lens is the heaviest
  camera's.
- `OrblitDocumentView.through` is a camera to look through in place of the
  scene's own. `cameraOf` gives the view from a camera entity, shown or not.

## 0.6.0

- `scatterFrom` turns what an `orblit_terrain` `ScatterPlacer` put down into
  what the renderer draws. A block layer is a population a region, its
  buffers shared rather than copied and its revision the group's, so only a
  region placed again is sent again. A model layer is an object each, in the
  material the layer names so that they are drawn together; a population is
  drawn from the cube alone, so a model cannot be one yet. `models: false`
  leaves model layers out, for a view with no way to resolve their files.

## 0.5.0

- `terrainFrom` turns an `orblit_terrain` `Terrain` into the `OrblitTerrain`
  the renderer draws. The regions' maps are shared rather than copied, and a
  region is sent again only when an edit has moved its revision. A set names
  its images by path; the caller decodes them and hands the pixels over.

## 0.4.0

- **Clips drive skins.** `OrblitSkinBinding.jointsFrom` takes an
  `orblit_motion` clip's bones at one moment straight to the joints the
  renderer sets, with no rig in between. `poseFrom` writes them into a rig's
  `Pose` instead, so a constraint or a limb reaching for a target starts from
  the clip rather than from rest. `localsFrom` is the joints' locals on
  their own. Whatever a clip leaves out, a part of a joint or a whole one,
  stays as the file has it.
- Through a rig, a joint stretched further along one of its own axes than
  another is carried as near as a bone can carry it: every joint still ends
  up where the clip puts it. `jointsFrom` carries it exactly.
- A joint scaled to nothing no longer throws: what hangs from it goes with
  it.
- `boneNamesOfSkin` makes names unique with `orblit_rig`'s `BoneNaming`,
  which the clip importer shares, so an imported clip names its bones the way
  the binding does. A joint the renderer could not name is `<unknown>`.

## 0.3.0

- `materialFrom` turns a resolved `.omat` into an `OrblitMaterial`, with a
  contract test that fails if a parameter is added to the table and not
  applied here.
- `OrblitDocumentView` takes a `MaterialLibrary` and an optional look.
  Materials are now keyed by what decided them rather than by entity, so a
  hundred crates wearing one material are one material and one batch.
- Naming an image rather than an `.omat` still means "a plain surface wearing
  this picture", as it did before materials had files.

## 0.2.0

- **`OrblitSkinBinding`** drives a model file's skin from an `orblit_rig`
  armature: bones matched to joints by name, an evaluated `Pose` turned into
  the joints the renderer sets by hand. Bone and joint frames need not agree.
  `armatureOfSkin` makes an armature from a skin.

## 0.1.0

- First cut of `orblit_stage`. Pre-alpha: everything is subject to change.
