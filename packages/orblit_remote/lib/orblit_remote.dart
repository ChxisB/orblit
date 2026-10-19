/// A control surface for a running debug game.
///
/// A script or an assistant connects over a loopback WebSocket, lists the
/// entities and their components, pauses and steps time, changes a value, and
/// compares one snapshot with another.
library;

export 'src/core_inspector.dart' show CoreWorldInspector;
export 'src/inspector.dart' show EntityRow, WorldInspector;
export 'src/log_tail.dart' show LogTail;
export 'src/server.dart' show RemoteServer;
export 'src/snapshot.dart' show Difference, SnapshotStore, diffJson;
export 'src/time_gate.dart' show TimeGate;
export 'src/verbs.dart' show RemoteError, RemoteHost, RemoteVerbs;
