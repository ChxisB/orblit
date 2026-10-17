import 'control.dart';
import 'input_action.dart';

/// What [NextInput] needs to see of the devices.
abstract interface class PressedControls {
  /// Every control pressed or pushed halfway or more now.
  ///
  /// Keys come first, then pad buttons, sticks, pad axes and touch regions,
  /// so that pushing a stick is read as the stick before it is read as one of
  /// its axes. An axis is listed as the half that was pushed.
  Iterable<Control> get active;
}

/// Waits for the player to press something, for a rebinding screen.
///
/// Whatever is already down when it starts is ignored until it comes up, so
/// the key that opened the screen is not taken as the answer. A stick axis is
/// taken as the half the player pushed, so "steer left" gets the left push.
final class NextInput {
  NextInput(this.kind);

  /// What the answer has to suit.
  final ActionKind kind;

  final Set<Control> _ignored = {};
  bool _started = false;
  Control? _captured;

  /// The first suitable control pressed since it started. Null until then.
  Control? get captured => _captured;

  /// Looks at one more frame. Does nothing once something is captured.
  void update(PressedControls source) {
    if (_captured != null) return;
    final active = source.active.toList();
    if (!_started) {
      _started = true;
      _ignored.addAll(active);
      return;
    }
    _ignored.retainAll(active);
    for (final control in active) {
      if (_ignored.contains(control) || !kind.accepts(control)) continue;
      _captured = control;
      return;
    }
  }
}
