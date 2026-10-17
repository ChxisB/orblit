import 'control.dart';

/// What shape of answer an action gives.
enum ActionKind {
  /// Down or up, with the edges and gestures of a button.
  button,

  /// One number from -1 to 1.
  axis,

  /// Two numbers, as a vector no longer than 1.
  axis2;

  /// Whether [control] can feed an action of this kind. Only a two-axis
  /// action takes a whole stick.
  bool accepts(Control control) => control is! PadStickControl || this == axis2;
}

/// How long a press must last, or how soon the next must come, for the
/// gestures of a button.
final class Gestures {
  const Gestures({
    this.tap = const Duration(milliseconds: 250),
    this.doubleTap = const Duration(milliseconds: 300),
    this.hold = const Duration(milliseconds: 400),
  });

  /// A press released within this long is a tap.
  final Duration tap;

  /// A press that starts within this long of a tap's release is a double tap.
  final Duration doubleTap;

  /// A press still down after this long is a hold.
  final Duration hold;
}

/// Something a player can do, named by the game.
final class InputAction {
  const InputAction.button(this.name, {this.gestures = const Gestures()})
    : kind = ActionKind.button;

  const InputAction.axis(this.name)
    : kind = ActionKind.axis,
      gestures = const Gestures();

  const InputAction.axis2(this.name)
    : kind = ActionKind.axis2,
      gestures = const Gestures();

  final String name;
  final ActionKind kind;

  /// Only a button reads these.
  final Gestures gestures;
}
