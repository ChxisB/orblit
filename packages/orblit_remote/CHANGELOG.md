# Changelog

## 0.2.0

- Four verbs that ask the renderer: `view.passes`, `view.draws`, `view.draw`
  and `view.material`. They read three new optional `RemoteHost` functions,
  `passes`, `draws` and `material`, and answer `-32004` without them.
- `view.draws` answers the count and the first `limit` rows, 100 unless asked,
  and says `truncated`. A draw row says what the scene submitted. Culling and
  batching come after it, so the pass rows are the check on it.
- A float JSON cannot write is text in these rows, so a NaN from the renderer
  can be read.
- Not answerable yet: the pixel of a pass and a scan for NaN or depth fighting.
  The plugin returns no pixels or depth to Dart. The task plan lists the
  native work.

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
