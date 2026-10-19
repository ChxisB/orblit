import 'dart:async';

/// The clock a game's loop passes its frame delta through, so that a debugger
/// can hold time still and walk it forward a frame at a time.
///
/// The stage and the world take no clock of their own. The game's ticker calls
/// `World.tick`, so pausing means asking the loop to hand over a smaller delta.
/// A game opts in with `world.tick(gate.advance(delta))`.
final class TimeGate {
  /// [stepDelta] is how much time one stepped frame passes, which defaults to
  /// a sixtieth of a second because the solver is tuned for that rate.
  ///
  /// Throws [ArgumentError] unless it is a positive, finite number, because a
  /// stepped frame that passes no time or NaN would look like a frozen game.
  TimeGate({this.stepDelta = 1 / 60}) {
    if (!(stepDelta > 0 && stepDelta.isFinite)) {
      throw ArgumentError.value(stepDelta, 'stepDelta', 'is not above 0');
    }
  }

  final double stepDelta;

  bool _paused = false;
  int _pending = 0;
  int _passed = 0;
  Completer<void>? _stepped;

  bool get paused => _paused;

  /// Stepped frames the loop has passed since the gate was made. A step reads
  /// it before and after to say how many frames it got.
  int get passed => _passed;

  /// Frames a step has asked for that the loop has not passed yet.
  int get pending => _pending;

  /// The delta to simulate for a frame that took [real] seconds.
  ///
  /// Not a query: a step is spent by being passed, and answering it without
  /// spending it would let the loop run the same frame twice. F5 is broken
  /// here on purpose.
  double advance(double real) {
    if (_pending > 0) {
      _pending--;
      _passed++;
      if (_pending == 0) _release();
      return stepDelta;
    }
    return _paused ? 0 : real;
  }

  /// Holds time still. Ends a step that has frames left.
  void pause() {
    _paused = true;
    _pending = 0;
    _release();
  }

  /// Lets time run. Ends a step that has frames left.
  void resume() {
    _paused = false;
    _pending = 0;
    _release();
  }

  /// Pauses, then passes [frames] frames of [stepDelta] each. Completes when
  /// the last of them has been passed, or when a pause or a resume cancels the
  /// rest. Throws [ArgumentError] for fewer than one frame.
  Future<void> step(int frames) {
    if (frames < 1) throw ArgumentError.value(frames, 'frames', 'is below 1');
    _paused = true;
    _pending += frames;
    return (_stepped ??= Completer<void>()).future;
  }

  void _release() {
    _stepped?.complete();
    _stepped = null;
  }
}
