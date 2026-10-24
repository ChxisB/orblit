# Changelog

## 0.5.0

- A port of `orblit_input` 0.5.0: the pad model, the evdev decoder and
  `Pads`, and the named actions. The Linux backend comes next.
- A backend is `IPadBackend` and a device is `IPadDevice`. `RecordedPad` and
  `RecordedPads` drive a session from a test, and `SnapshotPad` reads a
  platform that answers with a snapshot.
- The actions are `ActionMap`, `InputAction`, `InputContext` and `Binding`,
  with press, release, hold, tap and double tap, and a value or a vector
  for an axis. `BindingTable` rebinds a control, refuses a clash or swaps
  it, and saves and loads a profile. `NextInput` captures the next press
  and `TouchControls` reads touch payloads.
- `PadLayout.Of` folds a name with the capital dotted I and the Kelvin sign
  to plain `i` and `k`, as Dart does.
- A pad axis that reads NaN counts as a push on the negative side in
  `FrameControls.Active`, as it does in Dart.
- The browser Gamepad API and the web backend are not ported.
