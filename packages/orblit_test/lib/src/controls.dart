import 'package:orblit_input/orblit_input.dart';
import 'package:vector_math/vector_math_64.dart';

/// The devices of a test. Nothing is held until the test says so.
///
/// A [ControlSource], so an `ActionMap` reads it the way it reads a keyboard.
final class ScriptedControls implements ControlSource {
  final Map<Control, double> _held = {};
  final Map<PadStick, Vector2> _sticks = {};

  /// Holds [control] at [value] until [release]. A key or a button is 1. An
  /// axis is whatever the test wants it to read.
  void press(Control control, [double value = 1]) => _held[control] = value;

  void release(Control control) => _held.remove(control);

  /// Puts [which] at [to], positive up and to the right, no longer than 1. A
  /// stick at zero is let go.
  void pushStick(PadStick which, Vector2 to) {
    if (to.length2 == 0) {
      _sticks.remove(which);
    } else {
      _sticks[which] = to.clone();
    }
  }

  @override
  double read(Control control) => switch (control) {
    PadStickControl(:final stick) => this.stick(stick).length,
    _ => _held[control] ?? 0,
  };

  @override
  Vector2 stick(PadStick stick) => _sticks[stick]?.clone() ?? Vector2.zero();
}
