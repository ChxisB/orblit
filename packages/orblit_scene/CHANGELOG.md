# Changelog

## 0.9.0

- `JointComponent` holds a body to the body it hangs from: fixed, point,
  hinge, slider, distance, cone or six-axis, with limits per axis in metres
  and degrees, a motor, a breaking force and torque, and whether the two
  still collide. Which two bodies is where the entity sits rather than ids it
  names: the nearest body at or above it, held to the nearest body above
  that, or to the world. `JointComponent.endsOf` is that rule, for anything
  with a tree to ask. The entity's own origin and axes are the joint's.
  Written after `body`. What a joint does is `orblit_physics`'s.

## 0.8.0

- `TerrainComponent` puts ground in a scene: an `.oterrain` file by its
  project path, and whether it casts and receives shadows. The file is the
  terrain; the scene only says it is here. Laid where its own texels say,
  whatever the entity's transform. Written after `splats`. What a terrain is
  and how it is edited is `orblit_terrain`'s.

## 0.7.0

- `MotionComponent` says which clips an entity plays: `.oclip` files by
  their project path, and which one, if any, starts on its own. Written after
  `body`. What a clip is and how it plays is `orblit_motion`'s.
- The glTF importer reads its accessors through `orblit_mesh`'s
  `GltfAccessors` and `gltfParts`, the same reader the clip importer uses,
  instead of a private copy. It now also reads geometry stored as whole
  numbers.
- `TransformComponent.rotationOf` and `anglesOf` turn a transform's degrees
  into a rotation and back, composed Z, then Y, then X. One place for the
  scene's order, for anything that writes a rotation into a transform.

## 0.6.0

- **An instance is a link, not a copy.** A prefab placed in a scene is saved
  as one entity carrying a `PrefabComponent`: the asset it is an instance of,
  and `overrides`, a `SceneDiff` of what is different about this one. Change
  the prefab and every instance changes; change one instance and only it does.
  The file holds the difference, never the expansion, so a street of a
  hundred lamps is a hundred short entries rather than a hundred lamps.
- **The id is the path.** Opening an instance puts its parts in the document
  under ids made of the id each has in every document it is inside, joined by
  `/`: `street1/lamp3/bulb` is the bulb of the lamp `lamp3` in the street
  prefab placed as `street1`. The root of an instance keeps the instance's own
  id. One id per document crossed and nothing for the entities in between, so
  moving something inside a prefab never changes the path anything else uses
  to name it. `EntityPath` holds the rules, and `/` is reserved in ids.
- `expandInstances` opens a document's instances and `foldInstances` closes
  them again for saving, working out each one's overrides from what its parts
  now are. A `PrefabSource` hands them prefabs by path, so reading files is
  the caller's business. A prefab inside a prefab opens too, and one that
  contains itself is left closed and said so.
- An instance whose prefab cannot be read stays exactly as it was saved, link
  and overrides both, and anything hung off one of its parts keeps pointing
  there. An override of a part the prefab no longer has is let go, with a
  note.
- Something that is not the prefab's, hung off a part of an instance — a flag
  on a lamp's bulb — belongs to the scene and stays in it, parented by path.
- `PrefabDocument` is the `.oprefab` file, written with the same entity
  encoding as a scene. `makePrefab` turns a subtree into one and the subtree
  into its first instance; `applyInstance` writes an instance's overrides into
  its prefab, and every other instance keeps its own and picks up the rest;
  `revertInstance` drops an instance's overrides and keeps where it stands;
  `unpackInstance` turns one back into plain entities; `refreshInstances`
  reopens every instance of a prefab that has changed. Each returns what was
  renamed, so a selection can follow.
- A prefab whose root is not in it any more is treated as one that cannot be
  read: an instance of it stays a link, and unpacking one still unpacks. A
  part given a parent outside its own instance cannot be said as a change to
  that instance, and folds back under the instance's root; moving parts out
  is what unpacking is for.
- **The format is at 5.** A version-four scene's prefab instances were
  stamped copies, every part carrying the link. They are read as
  `PrefabState.stamped` and relinked the first time their prefab can be read,
  with whatever had been changed about each kept as its overrides.
- A glTF export states the format version on the scene's extras, and a
  re-import reads each node's components at the version they were written in.
  One from before this states none and is read as the version four it was.

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
- `BodyComponent.copyWith`, because a component is replaced rather than
  edited: an inspector changing one field of a body makes a new body.
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
