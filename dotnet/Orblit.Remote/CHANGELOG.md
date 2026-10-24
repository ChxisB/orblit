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
