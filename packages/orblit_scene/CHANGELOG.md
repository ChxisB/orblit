# Changelog

## 0.5.0

- **A body.** `BodyComponent` says what the physics does with an entity: a
  `BodyShape` (a box, a ball, a capsule or endless ground), a `BodyMotion`
  (fixed, driven or free), its mass, grip, bounce and damping, which layers it
  is in and which it cares about, and whether it starts asleep. Sizes are in
  the entity's own units, so a body scales with its entity, and a box's size is
  edge to edge and a capsule's height tip to tip, because a one metre crate
  should say one. It is written after what an entity draws and before what it
  lights. Nothing here simulates anything — that is `orblit_physics_scene`'s
  job — and a body is not a mesh's boundary: one is for picking, the other for
  falling.
- `Values.bits` reads a layer mask, kept to thirty-two bits.
- A new component type needs no format change: a file with a body in it opened
  by 0.4.0 keeps the body as a component it has not heard of and writes it
  back untouched, so the format stays at 4.

## 0.4.0

- **A scene can be read back in.** `readSceneFrom` takes the bytes of a `.glb`
  or a `.gltf` and gives back a `SceneDocument`. A scene this package wrote
  comes back as itself — the same ids, the same order, the same parents, the
  same components, down to `encode()` matching — because the exporter already
  writes a full record of the scene into `extras.orblit`, so reading one is a
  read rather than a re-derivation. That is the half of the round trip that
  could not be proved before.
- A glTF from anywhere else is interpreted instead: nodes become entities, a
  matrix or a TRS becomes a transform, punctual lights come back at the power
  they left at, a camera keeps its field of view, and triangles become
  geometry somebody can still edit. What could not be carried across is
  listed in `SceneImported.problems` rather than dropped quietly.
- A model grafted into an export — the copy of `models/tree.glb` sitting under
  the node that draws it — stays geometry on the way back in. Only nodes
  carrying `extras.orblit` become entities, because turning the rest into
  entities would double the outliner on every round trip.
- Buffers are read at their `byteStride`, so an interleaved file from another
  tool comes in with the positions it actually has rather than ones that are
  subtly wrong.
- `SceneFormatException` is thrown only when there is nothing to read at all —
  bytes that are neither a GLB nor JSON, or JSON that is not a document. A
  scene with one unreadable node is still a scene worth opening.

## 0.3.0

- **A scene can be written out.** `SceneDocument.writeAs` exports glTF, GLB or
  OBJ, returning the files and a list of what would not fit rather than
  throwing on the first thing it cannot carry. An export that silently drops
  half a scene is worse than one that says so.
- glTF and GLB carry the hierarchy, transforms, meshes, materials, lights and
  cameras. Orblit's own components ride along in `extras` under an `orblit`
  key, verbatim, so nothing an exporter has no word for is lost on the way
  out.
- Materials are resolved through `MaterialLibrary` before they are written, so
  an exported material carries the values an `.omat` states rather than the
  mesh's colour, and reaches for an extension where glTF has one:
  `KHR_materials_unlit`, `_clearcoat`, `_sheen`, `_specular`, `_anisotropy`,
  `_emissive_strength` and `KHR_texture_transform`. Named looks become
  `KHR_materials_variants`.
- Lights are converted rather than copied. A sun is stated in lux and a lamp
  in candela, both from watts through the same photometry the engine lights
  with, which is the conversion Blender's own exporter makes — so a scene
  round-trips at the brightness it was authored at instead of arriving a
  factor of 4π out.
- An imported model is grafted into the export whole, its indices rebased.
  Placed twice, it is copied once and placed twice: the second placement costs
  a node rather than a second copy of the mesh. A skinned or animated model is
  copied per placement, because a skin names its joints by node and an
  animation names the nodes it moves, and sharing those would make the two
  copies bend as one.
- A surface wearing a normal map exports tangents, so it lights the way it was
  meant to in a loader that does not generate its own.
- OBJ flattens to world space with a group per entity and a material library
  beside it, and reports what the format has no word for — lights, cameras,
  imported models, the hierarchy itself.
- `tool/dump_scene.dart` writes a scene of each kind, and `tool/check_gltf.sh`
  hands them to the Khronos glTF-Validator in CI alongside the mesh exports.

## 0.2.0

- Material files. `MaterialDocument` reads an `.omat`: a shading model, a
  blend mode, the parameters in `MaterialFields`, the maps a surface wears,
  and a parent to take the rest from. A file with one unreadable parameter
  keeps the others and says what it dropped.
- `MaterialLibrary` resolves a parent chain and a group's overrides once, in
  Dart, and caches the answer. A loop or a missing parent is reported rather
  than thrown or hung on.
- `MaterialComponent` carries named looks, the shape `KHR_materials_variants`
  uses: an object states only what it wears differently under each name.

## 0.1.0

- First cut of `orblit_scene`. Pre-alpha: everything is subject to change.
