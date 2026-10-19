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
that address. A connection gets a 403 unless it has the token, names a loopback
host and sends no `Origin` header, which keeps a web page in the user's browser
out. The server binds the loopback address only, and throws `StateError` unless
asserts are on, so a profile or release build never opens the port.

A connection is answered one request at a time, so a client that wants to pause
during a long `time.step` opens a second connection. At most eight stay open,
and a frame over one mebibyte closes its connection. When the last connection
goes, or the server closes, time runs again.

## Verbs

Each request is a JSON-RPC 2.0 object with named params.

| Method | Params | Answers |
| --- | --- | --- |
| `world.entities` | `filter` (component names) | ids with component names |
| `entity.get` | `id` | the components of one entity, by field |
| `entity.set` | `id`, `component`, `field`, `value` | nothing |
| `time.pause`, `time.resume` | none | `paused` |
| `time.step` | `n` | `stepped`, the frames the loop passed, which is `n` unless a pause or resume cut it short |
| `world.snapshot` | none | an `id`, the entities and the `extras` |
| `world.diff` | `a`, `b` (snapshot ids) | each path that differs |
| `input.send` | `event` | nothing |
| `view.screenshot` | none | `png` as base64 |
| `view.passes` | none | a row for each pass of the last frame: `name`, `milliseconds`, `draws` |
| `view.draws` | `limit`, 100 unless asked | `count`, the first `limit` rows and `truncated` |
| `view.draw` | `id` | the row of one draw |
| `view.material` | `key` | the values of one material |
| `log.tail` | `n` (default 50) | the last lines |
| `perf.summary` | none | what the host reports |

The render verbs read functions the host gives: `RemoteHost.passes`, `draws`
and `material`. A row of `draws` has a text `id` and says what the scene
submitted, which is not what the renderer drew, because culling and batching
come after. `view.passes` is what the renderer did, so set one against the
other. A float JSON cannot write is text in these rows. There is no verb for
the pixel of a pass or for a scan, because nothing returns pixels or depth to
Dart yet.

A snapshot holds the entities and the `extras` the host gave, as JSON. The
physics world's own `PhysicsSnapshot` is native memory with no JSON form, so a
host puts what it wants compared, such as contacts or stats, in `extras`.

The surface keeps the last eight snapshots. A component the inspector has no
field names for is listed by name and cannot be set. `readOnly: true` refuses
`entity.set` and `input.send`. A verb whose part the game did not give answers
error `-32004`.

`RemoteHost.timeout` (ten seconds) is how long a verb waits on the game. For
`time.step` it restarts each time a frame passes. For `input.send`, the
screenshot and the render verbs it is the whole wait. A callback that outlasts it answers
`-32003`. An argument or state error thrown by a host callback answers
`-32603`, because it is the game's fault and not a bad request.

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature. See [VERSIONING.md](../../VERSIONING.md).

## Licence

MPL-2.0, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
