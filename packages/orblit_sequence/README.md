# orblit_sequence

Cutscenes as a function of time. A sequence is tracks of clips over a
playhead; sampling it at any moment gives the whole world's worth of values,
so scrubbing, replaying and stepping backwards all come out the same.

Part of [Orblit](https://github.com/ChxisB/orblit), a Dart-first 3D game
engine. The documentation is at [orblitengine.com](https://orblitengine.com).

## Using it

```yaml
dependencies:
  orblit_sequence:
    git:
      url: https://github.com/ChxisB/orblit.git
      path: packages/orblit_sequence
```

Needs Dart alone. Runs anywhere Dart runs, including a headless CI runner.

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature — see [VERSIONING.md](../../VERSIONING.md).

## Licence

FSL-1.1-MIT, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
