/// A control surface for a running debug game.
///
/// A script or an assistant connects over a loopback WebSocket, lists the
/// entities and their components, pauses and steps time, changes a value, and
/// compares one snapshot with another.
library;

export 'src/core_inspector.dart' show CoreWorldInspector;
export 'src/inspector.dart' show EntityRow, WorldInspector;
