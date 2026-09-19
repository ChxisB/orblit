# orblit_scene

A scene as a document: entities with stable ids, a parent and a place in the
order; components that say what each entity is; an ordered list of migrations
from every older format; and diffs between two documents that can be applied
and inverted.

Part of [Orblit](https://github.com/ChxisB/orblit), a Dart-first 3D game
engine. The documentation is at [orblitengine.com](https://orblitengine.com).

## Using it

```yaml
dependencies:
  orblit_scene:
    git:
      url: https://github.com/ChxisB/orblit.git
      path: packages/orblit_scene
```

Needs Dart alone — no Flutter, and nothing in it draws anything. That is the
point of it being its own package: the editor, the runtime, an importer and a
command-line cook step all have to agree on what a scene *is*, and three of
those four have no widget toolkit in them.

## What an entity is

There is no `kind` field. An entity is an id, a name, where it sits in the
tree, and the set of components it has — so the question "is this a light" is
answered by asking whether it has a light component. A lamp can be a mesh and a
light at once, which the shape this replaced could not say at all.

```dart
final lamp = SceneEntity(
  id: 'lamp',
  name: 'Lamp',
  components: {
    SceneComponents.mesh: const MeshComponent(asset: 'models/lamp.glb'),
    SceneComponents.light: const LightComponent(power: 40),
  },
);
```

A component this version of Orblit has never heard of is kept exactly as it
arrived and written back exactly as it arrived, so the newer half of a team
does not silently delete the older half's work.

## Reading and writing

```dart
final load = SceneDocument.decode(await file.readAsString());
for (final problem in load.problems) {
  print(problem);  // what was dropped, and why
}
await file.writeAsString(load.document.encode());
```

A file that is not a scene is refused, and so is one written by a newer Orblit.
Everything else is read as far as it can be: one broken object is reported and
left out rather than costing somebody the other ninety-nine.

Saving the same document twice gives identical bytes — fixed key order, fixed
component order — because a scene file lives in somebody's repository and a
format that reorders itself turns every commit into a diff nobody can review.

## Migrations

An ordered list, oldest first, each working on decoded JSON rather than on
types this version still has. A version-one file runs every step in turn and
arrives where a version-three file does after running the last one. Each step
can leave a note, which comes back in `SceneLoad.problems`: a migration that
silently changes what a scene looks like is worse than one that refuses.

| Version | What changed |
|---|---|
| 2 | A light's power stopped being watts for everything. A sun is watts per square metre. |
| 3 | The air moved off the scene and into an object, so a change to it has somewhere to live. |
| 4 | An object's kind became the set of components it has. |

## Diffs

```dart
final diff = SceneDiff.between(before, after);
final undo = diff.inverse;

diff.applyTo(before);        // == after
undo.applyTo(diff.applyTo(before));  // == before
```

Operations are addressed by id, never by position — an undo stack full of "the
fourth object" corrupts a scene the moment somebody deletes the third. They
serialise, so an editor can keep them on an undo stack, and they are what lets
a renderer bring a scene with four thousand things in it up to date by touching
the one that moved.

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature — see [VERSIONING.md](../../VERSIONING.md).

## Licence

FSL-1.1-MIT, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
