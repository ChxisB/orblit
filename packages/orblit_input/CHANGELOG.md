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
  keeps its scale, half and shaping unless `scale` or `part` say otherwise,
  and throws `BindingClash` when another action in the context has the
  control. `swap` trades the two, and needs exactly one binding to trade
  with. `clash` asks first. An action a context does not bind yet can be
  bound there. `NextInput` waits for the next control a rebinding screen
  should take.

- Half axes. `PadAxisControl` has a `side`, so a stick pushed left binds as
  the left half of its axis. `FrameControls.active` lists the half that was
  pushed, and a trigger once, as its axis.

- A saved profile. `BindingTable.toJson` writes only the slots that differ
  from the defaults, with null for a slot the player left alone, so a default
  the game changes later still reaches a player who touched a neighbour. `load`
  replaces the table in place, and a view from `bindingsIn` follows it. It
  skips names the game no longer has and controls this version cannot read,
  and refuses a profile that is not made of objects and lists, changing
  nothing. Writing the file is the game's job.

- `PressedControls` is what `NextInput` reads. `ControlSource` is what
  `ActionMap` reads. `FrameControls` is both.

- A button whose control was held when its binding appeared, or was cut off
  by a menu or a rebinding, has to be seen up before it presses.

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
