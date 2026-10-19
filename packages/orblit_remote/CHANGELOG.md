# Changelog

## 0.2.0

- `ToolServer`: the control surface as seven named tools over the tool
  protocol, for an assistant. Only `set` writes, and it is absent unless the
  server is started with `--allow-write`.
- `RemoteClient` joins a game's surface and pairs calls with answers.
- `bin/orblit_remote.dart`, the `orblit_remote` executable: reads a game's
  address, serves the tools on stdin and stdout, and logs to stderr. It exits
  with 64 for a bad address and 69 for a game it cannot reach. The host must
  be loopback.

## 0.1.0

- First cut of `orblit_remote`. Pre-alpha: everything is subject to change.
- `RemoteServer`: a JSON-RPC server on a loopback WebSocket with a one-time
  token, refused unless asserts are on.
- `RemoteVerbs` and `RemoteHost`: entities, components, time, snapshots and
  diffs, input, screenshots, the log and a perf summary.
- `CoreWorldInspector` for an `orblit_core` world, `TimeGate`, `LogTail`,
  `SnapshotStore` and `diffJson`.
- A connection needs the token, a loopback `Host` and no `Origin`. It is
  answered one request at a time, at most eight stay open, and a frame over
  one mebibyte closes it. Time runs again when the last one leaves.
- `RemoteHost.timeout` bounds `input.send` and `view.screenshot`, and
  restarts for each frame a `time.step` passes. `stepped` is the frames
  passed. A snapshot is a JSON copy, and a float JSON cannot write is kept as
  its name.
