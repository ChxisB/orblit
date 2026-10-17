import 'package:vector_math/vector_math_64.dart';

import '../pad.dart';
import 'control.dart';

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
  /// touch region or a trigger. -1 to 1 for a stick axis. For a whole stick,
  /// how far it is from the middle.
  double read(Control control);

  /// A stick as a vector, positive up and to the right.
  Vector2 stick(PadStick stick);

  /// Every control pressed or pushed to [pressPoint] or more now.
  ///
  /// Keys come first, then pad buttons, sticks, pad axes and touch regions,
  /// so that pushing a stick is read as the stick before it is read as one of
  /// its axes.
  Iterable<Control> get active;
}

/// One frame of a keyboard, a touch screen and the pad one player holds.
final class FrameControls implements ControlSource {
  FrameControls({this.keys = const {}, this.touch = const {}, PadState? pad})
    : pad = pad ?? PadState.absent;

  /// The keys held now, by name.
  final Set<String> keys;

  /// The touch regions being touched now, by name.
  final Set<String> touch;

  final PadState pad;

  @override
  double read(Control control) => switch (control) {
    KeyControl(:final key) => keys.contains(key) ? 1 : 0,
    PadButtonControl(:final button) => pad.isDown(button) ? 1 : 0,
    PadAxisControl(:final axis) => pad.axis(axis),
    PadStickControl(:final stick) => pad.stick(stick).length,
    TouchControl(:final region) => touch.contains(region) ? 1 : 0,
  };

  @override
  Vector2 stick(PadStick stick) => pad.stick(stick);

  @override
  Iterable<Control> get active sync* {
    for (final key in keys) {
      yield KeyControl(key);
    }
    for (final button in pad.held) {
      yield PadButtonControl(button);
    }
    for (final stick in PadStick.values) {
      if (pad.stick(stick).length >= pressPoint) yield PadStickControl(stick);
    }
    for (final axis in PadAxis.values) {
      if (pad.axis(axis).abs() >= pressPoint) yield PadAxisControl(axis);
    }
    for (final region in touch) {
      yield TouchControl(region);
    }
  }
}
