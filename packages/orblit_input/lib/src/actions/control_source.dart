import 'package:vector_math/vector_math_64.dart';

import '../pad.dart';
import 'control.dart';
import 'next_input.dart';

/// How far a control must travel to count as pressed.
///
/// A key is 0 or 1. A trigger or a stick axis is pressed past halfway.
const pressPoint = 0.5;

/// What the action layer reads from the devices, once a frame.
///
/// The game fills one per frame. [FrameControls] does it for a keyboard, a
/// touch screen and one pad. A test or a replay can implement it directly.
abstract interface class ControlSource {
  /// How far [control] is pressed or pushed. 0 to 1 for a key, a button, a
  /// touch region, a trigger or half of a stick axis. -1 to 1 for a whole
  /// axis. For a whole stick, how far it is from the middle.
  double read(Control control);

  /// A stick as a vector, positive up and to the right.
  Vector2 stick(PadStick stick);
}

/// One frame of a keyboard, a touch screen and the pad one player holds.
///
/// It sees what is held at the moment of the poll. A pad button pressed and
/// released between two polls is in `PadFrame.events` and not here, so poll
/// at the frame rate.
final class FrameControls implements ControlSource, PressedControls {
  FrameControls({
    this.keys = const {},
    this.touch = const {},
    this.sticks = const {},
    PadState? pad,
  }) : pad = pad ?? PadState.absent;

  /// The keys held now, by name.
  final Set<String> keys;

  /// The touch regions being touched now, by name.
  final Set<String> touch;

  /// The sticks on a touch screen, by the pad stick each stands in for. When a
  /// pad and a screen both push the same stick, the longer push is read.
  final Map<PadStick, Vector2> sticks;

  final PadState pad;

  @override
  double read(Control control) => switch (control) {
    KeyControl(:final key) => keys.contains(key) ? 1 : 0,
    PadButtonControl(:final button) => pad.isDown(button) ? 1 : 0,
    PadAxisControl(:final axis, :final side) => side.read(pad.axis(axis)),
    PadStickControl(stick: final which) => stick(which).length,
    TouchControl(:final region) => touch.contains(region) ? 1 : 0,
  };

  @override
  Vector2 stick(PadStick stick) {
    final padded = pad.stick(stick);
    final touched = sticks[stick];
    return touched != null && touched.length2 > padded.length2
        ? touched
        : padded;
  }

  @override
  Iterable<Control> get active sync* {
    for (final key in keys) {
      yield KeyControl(key);
    }
    for (final button in pad.held) {
      // A trigger that travels is listed as its axis, which keeps the
      // analogue value for an action that wants one.
      if (!_listedAsAxis(button)) yield PadButtonControl(button);
    }
    for (final stick in PadStick.values) {
      if (this.stick(stick).length >= pressPoint) yield PadStickControl(stick);
    }
    for (final axis in PadAxis.values) {
      final value = pad.axis(axis);
      if (value.abs() < pressPoint) continue;
      yield PadAxisControl(
        axis,
        side: value > 0 ? AxisSide.positive : AxisSide.negative,
      );
    }
    for (final region in touch) {
      yield TouchControl(region);
    }
  }

  bool _listedAsAxis(PadButton button) => switch (button) {
    PadButton.leftTrigger => pad.axis(PadAxis.leftTrigger) >= pressPoint,
    PadButton.rightTrigger => pad.axis(PadAxis.rightTrigger) >= pressPoint,
    _ => false,
  };
}
