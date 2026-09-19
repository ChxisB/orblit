# orblit_stage

A scene document, staged for a renderer: entities and components become
objects, lights, clouds and sprite layers, and a diff moves only what it
touched.

Part of [Orblit](https://github.com/ChxisB/orblit), a Dart-first 3D game
engine. The documentation is at [orblitengine.com](https://orblitengine.com).

## Using it

```yaml
dependencies:
  orblit_stage:
    git:
      url: https://github.com/ChxisB/orblit.git
      path: packages/orblit_stage
```

```dart
final view = OrblitDocumentView(
  SceneDocument.decode(text).document,
  projectRoot: '/where/the/assets/are',
);

// Every frame. Cheap: the lists are assembled, not rebuilt.
renderer.publish(view.scene);

// An edit. This is the expensive call, and it happens far less often.
view.apply(diff);
```

## Driving a model's skin with a rig

An [`orblit_rig`](../orblit_rig) armature can move the skeleton of a model
loaded from a file. Bones find joints by name, and the renderer is told where
each joint is relative to whatever it hangs from:

```dart
final skin = info.skins.first;              // from OrblitView.onAssetInfo
final armature = armatureOfSkin(skin);      // or one built by hand
final binding = OrblitSkinBinding(armature, skin, index: 0);
final pose = Pose(armature);

// Every frame, after posing.
pose.evaluate();
final character = OrblitObject(
  key: 7,
  transform: placement,
  colour: Vector3.all(1),
  mesh: 'models/character.glb',
  joints: binding.jointsFor(pose),
);
```

- **`armatureOfSkin`** makes one bone per joint, with its head where the joint
  rests and its tail at the middle of the joint's children. A joint at the end
  of a chain carries on the way it came. Each bone's roll lays its X axis as
  near its joint's X axis as the bone allows. No bone is left with no length.
  An unnamed joint is called `joint 3`, and a second joint with a name already
  taken gets `.001`; `boneNamesOfSkin` gives the names.
- **An armature made by hand works too.** Only the names and the space have to
  match: bones may point and twist however they like, because each joint keeps
  its rest offset from its bone. The armature's space has to be the model's
  own. `binding.bones` shows which joints found a bone.
- **A joint no bone is named after** keeps its rest place relative to its
  parent, and moves with it.
- **Every joint is sent, every frame**, including those at rest. The renderer
  leaves a hand-set joint where it was put until the object stops being posed,
  so a joint left out would stay bent.
- **The pose has to be evaluated first.** `jointsFor` does not evaluate it,
  because a pose writes its inverse-kinematics solutions back into itself and
  a second evaluation can give a different pose.

## Why it is its own package

It is the one piece that has to know both halves, and neither half should have
to know the other.

[`orblit_scene`](../orblit_scene) has no renderer in it, on purpose: a runtime,
an importer and a command-line cook step all read scenes and none of them draw.
[`orblit_filament`](../orblit_filament) has no artist units in it, on purpose:
it takes light in the units a renderer works in, and an app using it directly
should not have to carry a scene format, a set of migrations and a weather
model to do so.

## What it does

- **Transforms** are resolved against the whole tree, so a child is placed by
  its parent as well as by itself. Rotation composes Z, then Y, then X — the
  order the editor has always used, and therefore the only order that draws a
  saved scene the way it was saved.
- **Visibility inherits.** Hiding a group hides what is in it. A hidden light
  is left out of the scene rather than sent dark, because Filament shades one
  directional light and a budget of punctual ones, and a light nobody can see
  should not be the one that fills the budget.
- **Keys are stable**, one per entity and per role, handed out once and never
  reused — so the renderer keeps what it has built across an edit. A lamp that
  is both a mesh and a light gets two, since the two lists may or may not share
  a namespace and this costs nothing and does not care.
- **The air** comes from whichever entity carries the weather: the sky is
  greyed and its ambient scattered by the cloud, fog is built from the mist,
  and rain or snow becomes precipitation blowing the way the wind does.
- **A diff rebuilds only what it touched**, meaning the entities its operations
  name and everything under them — because where an entity is in the world and
  whether it is shown are both inherited.

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature — see [VERSIONING.md](../../VERSIONING.md).

## Licence

MIT, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
