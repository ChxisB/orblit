# Changelog

## 0.3.0

- Pads in a browser. `Pads.open()` on the web reads `navigator.getGamepads()`
  and hands the pad to the same layouts, shaping and edges as a Linux one.
  The package compiles for the web: `LinuxPads` is a stub there that
  throws as it does on any platform that is not Linux.

- `SnapshotPad` turns a platform that can only be asked where a pad stands
  into the bytes evdev would have written. A tap that begins and ends between
  two polls leaves no trace in a snapshot, which a browser cannot avoid.

- `GamepadApiPads` over a `GamepadSource`, with `PadMapping` as the data for
  a pad the browser does not put in the standard order. The table is empty
  and is passed to the constructor, keyed by `PadMapping.keyOf`. A pad in
  neither is read in the standard order, which is a guess.

- Rumble. `Pads.canRumble` and `Pads.rumble` take two motors from nought to
  one and a duration. A browser pad with a dual-rumble actuator has it. A
  Linux pad says it cannot, because force feedback is not built there.
  `PadDevice` gains `canRumble` and `rumble`, so a backend written outside
  this package must add them.

- A pad that goes away while a button is held sends `PadReleased` for it
  before `PadDisconnected`. The state already read as neutral.

- Not built: macOS, Windows, Android and iOS pads, and no physical pad has
  been tried through the browser backend. The tests use made-up readings and
  a stand-in for `navigator.getGamepads`.

## 0.1.0

- First cut of `orblit_input`. Pre-alpha: everything is subject to change.

- Gamepads as both polled state and events from one `Pads.poll()` — `PadState`
  for what is held now and what changed since the last frame, `PadFrame.events`
  for the tap that began and ended inside one frame and for pads arriving and
  leaving.

- Dead zones and response curves owned by the engine: radial on a stick,
  scalar on a trigger, rescaled so deflection is continuous where the zone
  ends.

- Layouts built on the kernel's own gamepad button and axis codes, with a
  small table for the pads whose labels or axes differ from them, and a
  predictable fallback for pads in neither. No external controller database.

- A Linux backend over evdev: sysfs to find and classify a pad, `dart:ffi` to
  libc to read it. No compiled code of our own.
