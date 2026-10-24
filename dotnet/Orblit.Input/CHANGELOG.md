# Changelog

## 0.5.0

- A port of the pad part of `orblit_input` 0.5.0: the pad model, the evdev
  decoder and `Pads`. The named actions come next, then the Linux backend.
- A backend is `IPadBackend` and a device is `IPadDevice`. `RecordedPad` and
  `RecordedPads` drive a session from a test, and `SnapshotPad` reads a
  platform that answers with a snapshot.
- `PadLayout.Of` folds a name with the capital dotted I and the Kelvin sign
  to plain `i` and `k`, as Dart does.
- The browser Gamepad API and the web backend are not ported.
