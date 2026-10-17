import 'control.dart';
import 'control_source.dart';
import 'input_action.dart';

/// Waits for the player to press something, for a rebinding screen.
///
/// Whatever is already down when it starts is ignored until it comes up, so
/// the key that opened the screen is not taken as the answer.
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
  void update(ControlSource source) {
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
