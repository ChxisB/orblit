# orblit_remote

A control surface for a running debug game. A script or an assistant connects
over a loopback WebSocket, lists entities and their components, pauses and
steps time, changes a value, takes a snapshot and compares two.

Part of [Orblit](https://github.com/ChxisB/orblit), a Dart-first 3D game
engine. The documentation is at [orblitengine.com](https://orblitengine.com).

## Using it

```yaml
dependencies:
  orblit_remote:
    git:
      url: https://github.com/ChxisB/orblit.git
      path: packages/orblit_remote
```

Needs Dart alone. Runs anywhere Dart runs, including a headless CI runner.
The server starts only when asserts are on, so a release build never opens the
port.

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature. See [VERSIONING.md](../../VERSIONING.md).

## Licence

MPL-2.0, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
