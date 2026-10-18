import 'package:orblit_input/orblit_input.dart';
import 'package:vector_math/vector_math_64.dart';

/// The devices of a test. Nothing is held until the test says so.
///
/// A [ControlSource], so an `ActionMap` reads it the way it reads a keyboard.
/// A stick is its two axes, as on a pad, so a binding to the stick and a
/// binding to one half of an axis both see a push.
final class ScriptedControls implements ControlSource {
  final Map<Control, double> _held = {};
  final Map<PadAxis, double> _axes = {};

  /// Holds a key, button or touch region at [value] until [release]. 1 is
  /// fully down. An axis or a stick is pushed with [pushAxis] or [pushStick].
  void press(Control control, [double value = 1]) {
    if (control is PadAxisControl || control is PadStickControl) {
      throw ArgumentError.value(
        control,
        'control',
        'is pushed with pushAxis or pushStick',
      );
    }
    _held[control] = _finite(value, 'value');
  }

  void release(Control control) => _held.remove(control);

  /// Puts [axis] at [value], from -1 to 1. A trigger runs from 0 to 1. An axis
  /// at zero is let go.
  void pushAxis(PadAxis axis, double value) {
    if (_finite(value, 'value').abs() > 1) {
      throw ArgumentError.value(value, 'value', 'is beyond -1 to 1');
    }
    if (value == 0) {
      _axes.remove(axis);
    } else {
      _axes[axis] = value;
    }
  }

  /// Puts [which] at [to], positive up and to the right, no longer than 1. A
  /// stick at zero is let go.
  void pushStick(PadStick which, Vector2 to) {
    if (!to.length.isFinite || to.length > 1 + 1e-9) {
      throw ArgumentError.value(to, 'to', 'is longer than 1');
    }
    final (x, y) = switch (which) {
      PadStick.left => (PadAxis.leftX, PadAxis.leftY),
      PadStick.right => (PadAxis.rightX, PadAxis.rightY),
    };
    pushAxis(x, to.x);
    pushAxis(y, to.y);
  }

  @override
  double read(Control control) => switch (control) {
    PadAxisControl(:final axis, :final side) => side.read(_axes[axis] ?? 0),
    PadStickControl(:final stick) => this.stick(stick).length,
    _ => _held[control] ?? 0,
  };

  @override
  Vector2 stick(PadStick stick) {
    final (x, y) = switch (stick) {
      PadStick.left => (PadAxis.leftX, PadAxis.leftY),
      PadStick.right => (PadAxis.rightX, PadAxis.rightY),
    };
    return Vector2(_axes[x] ?? 0, _axes[y] ?? 0);
  }

  static double _finite(double value, String name) {
    if (!value.isFinite) {
      throw ArgumentError.value(value, name, 'is not finite');
    }
    return value;
  }
}
