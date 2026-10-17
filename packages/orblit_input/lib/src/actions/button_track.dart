import 'input_action.dart';

/// The edges and gestures of one button action across frames.
///
/// Time comes in with each call, so a replay or a test sets the clock.
final class ButtonTrack {
  bool _down = false;
  bool _pressed = false;
  bool _released = false;
  bool _held = false;
  bool _tapped = false;
  bool _doubleTapped = false;

  // False from the moment the action is hidden until its control is seen
  // up. A key still held when a menu closes would otherwise press the
  // action the player never meant to press.
  bool _armed = true;
  bool _holdSent = false;
  bool _doublePress = false;
  Duration _downAt = Duration.zero;
  Duration? _tapEnded;

  bool get isDown => _down;

  /// Went down in the last advance.
  bool get wasPressed => _pressed;

  /// Came up in the last advance, or was hidden while down.
  bool get wasReleased => _released;

  /// Passed the hold time in the last advance. True once per press.
  bool get wasHeld => _held;

  /// Came up in the last advance after a short press.
  bool get wasTapped => _tapped;

  /// Went down in the last advance soon after a tap came up.
  bool get wasDoubleTapped => _doubleTapped;

  /// Moves to [now]. [down] is null when no active context shows the action.
  void advance({
    required bool? down,
    required Duration now,
    required Gestures gestures,
  }) {
    _pressed = _released = _held = _tapped = _doubleTapped = false;
    final next = _accept(down);
    if (next && !_down) {
      _begin(now, gestures);
    } else if (!next && _down) {
      _end(now, gestures, hidden: down == null);
    } else if (next) {
      _checkHold(now, gestures);
    }
    _down = next;
  }

  bool _accept(bool? down) {
    if (down == null) {
      _armed = false;
      return false;
    }
    if (!down) _armed = true;
    return down && _armed;
  }

  void _begin(Duration now, Gestures gestures) {
    _pressed = true;
    _downAt = now;
    _holdSent = false;
    final ended = _tapEnded;
    _doublePress = ended != null && now - ended <= gestures.doubleTap;
    _doubleTapped = _doublePress;
  }

  void _end(Duration now, Gestures gestures, {required bool hidden}) {
    _released = true;
    // A press cut off by a menu opening was not a tap.
    _tapped = !hidden && now - _downAt <= gestures.tap;
    // The press that made a double tap does not start another.
    _tapEnded = _tapped && !_doublePress ? now : null;
  }

  void _checkHold(Duration now, Gestures gestures) {
    if (_holdSent || now - _downAt < gestures.hold) return;
    _holdSent = true;
    _held = true;
  }
}
