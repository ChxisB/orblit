# orblit_motion

Clips: animation as an asset. A clip is channels of keys moving an entity's
properties or a model's bones, named by path so one clip plays on every copy
of a prefab. Clips are written in the editor or imported from glTF, played
with marks and root motion, and saved as `.oclip` files that diff one key at a
time.

Blends decide which clips play. A blend is a graph of states, each playing a
clip or a mix of clips along one input or over two, and changes between them
that fade when a condition on the inputs holds. Where a blend has got to is a
place, a plain value: saved, a character comes back mid-stride; sent as
numbers, it stands the same on another machine; built by hand, a test can ask
what happens next without playing up to it.

Part of [Orblit](https://github.com/ChxisB/orblit), a Dart-first 3D game
engine. The documentation is at [orblitengine.com](https://orblitengine.com).

## Using it

```yaml
dependencies:
  orblit_motion:
    git:
      url: https://github.com/ChxisB/orblit.git
      path: packages/orblit_motion
```

Needs Dart alone. Runs anywhere Dart runs, including a headless CI runner.

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature. See [VERSIONING.md](../../VERSIONING.md).

## Licence

MPL-2.0, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
