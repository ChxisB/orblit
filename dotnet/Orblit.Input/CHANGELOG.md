# Changelog

## 0.6.0

- `TouchControls` takes typed values. `Hold(string, bool)`,
  `MoveStick(PadStick, Vector2)` and `Swipe(Vector2)` replace the methods
  that took an `object?` payload. A game that passed a `Vector` to the old
  `MoveStick` compiled and the stick never moved. Now it is a compile error.
  A `Vector2` with a value that is not finite is ignored, as a payload of
  another shape was.

## 0.5.0

- A port of `orblit_input` 0.5.0: the pad model, the evdev decoder and
  `Pads`, the named actions and the Linux backend.
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
- `LinuxPads` reads gamepads through evdev. Sysfs says what is a pad and
  libc `open`, `read`, `ioctl` and `close` read it, so no native library is
  built. `Pads.Open` picks it on Linux and a backend that reports nothing
  elsewhere. It lists pads in event number order and exposes `Problems` for
  a node it could not open.
- The browser Gamepad API and the web backend are not ported.
