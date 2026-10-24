# Changelog

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
