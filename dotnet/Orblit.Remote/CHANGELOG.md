# Changelog

## 0.4.0

- `world.wait` waits inside the game until one number field of an entity
  holds against a value, or until an entity is gone. It checks every ten
  milliseconds for `timeout_ms`, 5000 unless given and 20000 at most, and
  answers `met`, the value read and `waited_ms`. A wait that runs out answers
  `TimedOut` and names the last value read. The deadline is real time, so a
  game held by the time gate times out and is not resumed. It is not a write,
  so a read only session may wait.
- A component or field that the entity does not hold is `InvalidParams`, as
  for `entity.set`. An entity that is not alive is `NotFound`, unless `gone`
  is what was asked for.
- The `wait` tool passes the same arguments to `world.wait`. The list holds
  eight tools with writes allowed and seven without.
- `world.snapshot` and the `snapshot` tool take an optional `label`, which
  becomes the snapshot's id, so a diff can be asked for by name. A label used
  again replaces the older snapshot and counts as the newest. Labelled and
  automatic snapshots share the cap of eight, and a label does not move the
  automatic count. A blank label, one over 40 characters and one shaped like
  `s3` are refused with `InvalidParams`, and nothing is kept.
- `SnapshotStore.Add` takes the label as a second, optional argument. A
  caller built against 0.3.0 must be rebuilt.

## 0.3.0

- The start of a port of `orblit_remote` 0.3.0, the control surface for a
  debug game. This part holds the pieces that need no world and no socket.
- `RemoteException` carries the nine JSON-RPC codes the verbs answer with.
- `LogTail` keeps the last lines a game wrote. `TimeGate` pauses the game
  clock and steps it a fixed number of frames. A game logs from any thread
  and a session calls the gate from a socket thread, so both lock.
- `SnapshotStore` keeps the last snapshots by id, and `JsonDiff` lists where
  two snapshots differ. A whole number equals the double that holds it, and
  NaN equals nothing, as in Dart.
- `IWorldInspector` is what the server needs from a world, declared beside
  the server. `CoreWorldInspector` answers it for an `Orblit.Core` world, by
  component name. A manifest entry gives a component its field names, and
  one with none is listed and cannot be set.
- An entity handle goes on the wire as a signed 64 bit number, so a handle
  with its top bit set is negative, as in Dart. An integer written to a
  column wraps to the column width and loses bits past 2^53.
- `RemoteHost` holds the parts of a game the surface can reach, each
  optional. A timeout that is not above zero is refused where it is set.
- `RemoteVerbs.HandleAsync` answers one JSON-RPC request with the sixteen
  verbs of the Dart surface. It never throws for a bad request or a failing
  host callback. A notification is run and not answered. A callback that
  blocks is not covered by the timeout.
- A count past 32 bits is read as the nearest 32 bit count. That reads the
  same for a log tail or a draw limit, and a step that large times out.
- `RemoteServer` serves the verbs over a WebSocket on the loopback address
  and announces `orblit-remote ws://127.0.0.1:PORT/?token=TOKEN` once. It
  refuses a request with no token, a wrong one, an `Origin` header or a
  foreign `Host`, and a ninth connection. It starts in a debug build only.
  The port is always a free one, because the Dart `port` argument has no use
  a game needs.
- Each request is posted to the `SynchronizationContext` the game passes, so
  every verb and every `await` inside one runs on the game thread. A game
  with no context passes a `GameLoopContext` and calls `Pump` once a frame.
  A game that never pumps gets no answers, and a client times out.
- Closing the server runs a paused game again and ends each connection as
  going away. A client that never reads is cut off after a second.
- `RemoteClient` pairs calls with answers by id, times a call out after its
  deadline, and fails every waiting call when the connection closes. A
  refused token says so and never repeats the token. A text frame that is not
  valid UTF-8 is read with replacement characters, where Dart closes the
  connection.
- `ToolServer` serves the verbs as seven named tools over the tool protocol,
  one message in and one answer out, with batches. `set` is neither listed
  nor callable unless writes are allowed. It reports the package version
  instead of a copy of it that a test must keep equal.
- `RemoteCli` and the `orblit-remote` tool in `Orblit.Remote.Tool` carry the
  tools over stdin and stdout. The address must name 127.0.0.1 or localhost
  with a port, because the token goes to whatever host is named. Exit 64 is
  a usage fault and 69 is a game that cannot be reached or has closed.
- `RemoteCli.OpenInput` reads stdin as UTF-8 and nothing else. A reader that
  looks for a byte order mark took a stray `0xff 0xfe` for UTF-16.
