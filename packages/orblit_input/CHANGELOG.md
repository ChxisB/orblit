# Changelog

## 0.2.0

- Actions. A game names what a player can do, as a button, an axis or a
  two-axis action, and asks `ActionMap` about those names instead of about
  keys. `ActionMap.update` reads one frame of a keyboard, touch regions and a
  pad through `ControlSource`. `FrameControls` is the ready-made source.

- Contexts. `InputContext` groups default bindings. They stack, the one
  switched on last reads first, and one that `blocks` hides everything below
  it. A key still held when a menu closes does not press the action behind
  it.

- Gestures on a button: `wasTapped`, `wasDoubleTapped` and `wasHeld`, with
  times set per action. The clock is passed to `update`.

- Rebinding. `BindingTable.rebind` points one binding at another control,
  keeps its scale, half and shaping, and throws `BindingClash` when another
  action in the context has the control. `swap` trades the two. `clash` asks
  first. `NextInput` waits for the next control a rebinding screen should
  take.

- A saved profile. `BindingTable.toJson` writes only what differs from the
  defaults, and `load` reads it back all at once or not at all, so a default
  the game changes later still reaches a player who never touched it. It
  skips names the game no longer has. Writing the file is the game's job.

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
