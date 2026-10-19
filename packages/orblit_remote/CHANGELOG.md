# Changelog

## 0.2.0

- The `orblit_remote` executable: reads a game's address, serves its control
  surface as seven tools over the tool protocol on stdin and stdout, and logs
  to stderr. Only `set` writes, and it is absent unless the program is
  started with `--allow-write`.
- The address comes from the command line or `ORBLIT_REMOTE_ADDRESS`. The
  host must be loopback, and an unknown flag is refused.
- Exit codes: 64 for a bad address, 69 for a game it cannot reach or that
  goes away. A message with no id is never run. Batches are answered. Bad
  UTF-8 on stdin is a parse error.
- `step` takes 1 to 300 frames and is not marked read only, because it runs
  game code and holds time.
- The tool server and the client live under `lib/src` and are not exported.
  The program is the interface.

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
