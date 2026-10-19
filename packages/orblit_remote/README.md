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

## Starting it

```dart
final gate = TimeGate();
final log = LogTail();
final server = await RemoteServer.start(
  RemoteHost(
    world: CoreWorldInspector(world, [position, health]),
    gate: gate,
    log: log,
  ),
);
// In the game's loop, pass each delta through the gate so time can be held.
world.tick(gate.advance(delta));
```

The server prints `orblit-remote ws://127.0.0.1:PORT/?token=TOKEN`. Connect to
that address. A connection without the token gets a 403. The server binds the
loopback address only, and throws `StateError` unless asserts are on, so a
profile or release build never opens the port.

## Verbs

Each request is a JSON-RPC 2.0 object with named params.

| Method | Params | Answers |
| --- | --- | --- |
| `world.entities` | `filter` (component names) | ids with component names |
| `entity.get` | `id` | the components of one entity, by field |
| `entity.set` | `id`, `component`, `field`, `value` | nothing |
| `time.pause`, `time.resume` | none | `paused` |
| `time.step` | `n` | `stepped`, after the loop passed `n` frames |
| `world.snapshot` | none | an `id`, the entities and the `extras` |
| `world.diff` | `a`, `b` (snapshot ids) | each path that differs |
| `input.send` | `event` | nothing |
| `view.screenshot` | none | `png` as base64 |
| `log.tail` | `n` (default 50) | the last lines |
| `perf.summary` | none | what the host reports |

The surface keeps the last eight snapshots. A component the inspector has no
field names for is listed by name and cannot be set. `readOnly: true` refuses
`entity.set` and `input.send`. A verb whose part the game did not give answers
error `-32004`.

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature. See [VERSIONING.md](../../VERSIONING.md).

## Licence

MPL-2.0, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
