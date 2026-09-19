# orblit_native

Compiling and loading C++ scripts. A script is a file the engine loads, not a
file the engine has to have been linked into: it is handed a table of what it
may call, and answers start, step and stop.

Part of [Orblit](https://github.com/ChxisB/orblit), a Dart-first 3D game
engine. The documentation is at [orblitengine.com](https://orblitengine.com).

## Using it

```yaml
dependencies:
  orblit_native:
    git:
      url: https://github.com/ChxisB/orblit.git
      path: packages/orblit_native
```

Needs Dart alone. Runs anywhere Dart runs, including a headless CI runner.

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature — see [VERSIONING.md](../../VERSIONING.md).

## Licence

FSL-1.1-MIT, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
