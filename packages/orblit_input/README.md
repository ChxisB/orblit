# orblit_input

Gamepads as state a frame can ask about, and as the handful of things that
happened between two frames. On top of that, actions: a game asks about
`jump`, and the player decides which key that is.

Part of [Orblit](https://github.com/ChxisB/orblit), a Dart-first 3D game
engine. The documentation is at [orblitengine.com](https://orblitengine.com).

## Using it

```yaml
dependencies:
  orblit_input:
    git:
      url: https://github.com/ChxisB/orblit.git
      path: packages/orblit_input
```

Needs Dart alone. Runs anywhere Dart runs, including a headless CI runner. On
a platform with no backend it reports no pads rather than failing.

```dart
final pads = Pads.open();

// Once a frame.
final frame = pads.poll();
final pad = frame.first;

move(pad.stick(PadStick.left) * speed * delta);
if (pad.wasPressed(PadButton.south)) jump();
if (pad.trigger(PadSide.right) > 0.5) fire();

// The things a snapshot cannot hold: a tap and release inside one frame,
// and a pad arriving or leaving.
for (final event in frame.events) {
  if (event case PadDisconnected()) pause();
}
```

## What it does

- **State and events, from one poll.** `PadState` answers "is it down now"
  and "did it go down since the last frame". `PadFrame.events` carries what a
  snapshot cannot express.
- **Dead zones and curves**, radial rather than per-axis, with the rescaling
  that keeps the output continuous where the zone ends.
- **Layouts** from the kernel's own gamepad codes, with a small table for the
  pads whose labels or axes differ. No external controller database.
- **Hot-plug.** A pad that vanishes reads as neutral rather than as whatever
  it was holding, and takes its slot back when it returns.
- **Actions.** A game asks about `jump` and `move`, never about a key. See
  below.

Linux only so far, through evdev. The other platforms will each put their own
gamepad interface behind the same front door.

## Actions

```dart
final input = ActionMap(
  actions: const [
    InputAction.button('jump'),
    InputAction.axis2('move'),
  ],
  contexts: [
    InputContext('play', bindings: const {
      'jump': [
        Binding(KeyControl('Space')),
        Binding(PadButtonControl(PadButton.south)),
      ],
      'move': [
        Binding(KeyControl('KeyA'), scale: -1),
        Binding(KeyControl('KeyD')),
        Binding(KeyControl('KeyS'), scale: -1, part: AxisPart.y),
        Binding(KeyControl('KeyW'), part: AxisPart.y),
        Binding(PadStickControl(PadStick.left)),
      ],
    }),
  ],
  active: const ['play'],
);

// Once a frame. The game says which keys are down and what time it is.
input.update(
  FrameControls(keys: heldKeys, pad: pad),
  now: elapsed,
);
if (input.wasPressed('jump')) jump();
move(input.vector('move') * speed * delta);
```

Keys are strings the game supplies. The physical key, such as `KeyW`, keeps a
binding in place when the layout changes. A key on both sides of an axis gives
a diagonal no faster than a straight line, and a stick shares the action with
the keys.

Contexts stack. `input.activate('menu')` puts a context on top. If it was made
with `blocks: true`, every action it does not bind reads as neutral until it
is switched off. A key held while the menu closes does not press anything.

A rebinding screen asks `input.bindings.clash(...)` and calls
`input.bindings.rebind(...)`. `NextInput` finds the control to pass in. A
stick pushed left comes back as the left half of that axis, so "steer left"
gets the push the player made. Only the same control clashes, so a stick and
its axes do not. `input.bindings.toJson()` returns only the slots the player
changed, with their scale and half, and `load` reads it back. The game writes
the file.

`FrameControls` sees what is held at the moment of the poll. A pad button
pressed and released between two polls is in `PadFrame.events` and not in the
action layer, so poll at the frame rate.

On a touch screen, `TouchControls` takes the events of the `stick`,
`touch-button` and `swipe-pad` elements of `orblit_ui`. The two packages do
not import each other. They agree on the shape of a payload.

```dart
final touch = TouchControls();

// In the interface's onEvent.
(handler, payload) => switch (handler) {
  'move' => touch.moveStick(PadStick.left, payload),
  'jump' => touch.hold('jump', payload),
  'look' => touch.swipe(payload),
  _ => null,
};

// Once a frame.
input.update(
  FrameControls(touch: touch.held, sticks: touch.sticks, pad: pad),
  now: elapsed,
);
look(touch.swiped * sensitivity);
touch.endFrame();
```

A touch stick stands in for a pad stick, so bindings to `PadStickControl` and
to its axes work on a phone. A button binds with `TouchControl('jump')`. If a pad and a screen
push the same stick, the longer push is read.

Not here: motion sensors, touchpads, and pointer lock.

## Checking it

The parsing, the mapping, the shaping and the hot-plug run under
`tool/check.sh` on any machine, against recorded byte streams. That is where
the bugs are: a read that lands mid-record, a capability word whose top bit is
set, the kernel admitting it dropped events.

The read path itself needs a gamepad, and there was none, so Linux is asked
to invent one. `/dev/uinput` takes the same description a driver would give
and creates a real event device. The backend then finds it, classifies it from
sysfs, asks the driver for its axis ranges and reads it, exactly as it would
real hardware.

```sh
./tool/check_input_linux.sh
```

The recipe is in that script's header, including the two things about
containers that are not obvious: a `/dev` that no node ever appears in, and why
this needs `--privileged`.

What it does not prove is how any real driver behaves. The records come
through the kernel, but the pad they describe is one this repository made up,
so a pad whose driver reports something unusual is still untested. No physical
controller, and no Steam Deck, has run this.

## Status

Pre-alpha. Nothing here is API-stable, and the version is bumped for every
feature. See [VERSIONING.md](../../VERSIONING.md).

## Licence

MPL-2.0, © 2026 Chris Beckett. See [LICENSE](LICENSE), and the repository root
for the third-party notices that apply to builds linking the renderer.
