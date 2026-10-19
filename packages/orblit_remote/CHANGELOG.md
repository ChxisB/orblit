# Changelog

## 0.1.0

- First cut of `orblit_remote`. Pre-alpha: everything is subject to change.
- `RemoteServer`: a JSON-RPC server on a loopback WebSocket with a one-time
  token, refused unless asserts are on.
- `RemoteVerbs` and `RemoteHost`: entities, components, time, snapshots and
  diffs, input, screenshots, the log and a perf summary.
- `CoreWorldInspector` for an `orblit_core` world, `TimeGate`, `LogTail`,
  `SnapshotStore` and `diffJson`.
