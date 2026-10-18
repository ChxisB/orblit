import 'package:orblit_audio/orblit_audio.dart';
import 'package:orblit_input/orblit_input.dart';

import 'controls.dart';

/// One step of a [TestStage].
final class Frame {
  const Frame({required this.dt, required this.now});

  /// Seconds since the last frame. The same every time.
  final double dt;

  /// Sampled time at the end of this frame.
  final Duration now;
}

/// A game's frame loop with no window, no renderer and no wall clock.
///
/// Time only moves when a test calls [step]. Each frame reads [controls] into
/// [actions] if there are any, runs the game's own frame, then moves [audio]
/// on. The sound goes to [recording], which plays nothing.
final class TestStage {
  TestStage(this._onFrame, {this.actions, this.fps = 60}) {
    if (fps <= 0) throw ArgumentError.value(fps, 'fps', 'must be above zero');
  }

  final void Function(Frame frame) _onFrame;

  /// Updated from [controls] before every frame, when the game has one.
  final ActionMap? actions;

  final int fps;
  final ScriptedControls controls = ScriptedControls();
  final RecordedAudio recording = RecordedAudio();
  late final Audio audio = Audio(recording);

  int _frames = 0;

  /// Frames run so far.
  int get frames => _frames;

  /// Sampled time so far. Worked out from [frames] so it does not drift.
  Duration get now => Duration(microseconds: (_frames * 1000000 / fps).round());

  /// Runs the frames that fit in [duration], rounded to the nearest whole one.
  void step(Duration duration) {
    final count = (duration.inMicroseconds * fps / 1000000).round();
    for (var i = 0; i < count; i++) {
      _frame();
    }
  }

  /// Steps one frame at a time until [done] is true. Fails the test when it
  /// is not true after [within], so a game that never gets there does not
  /// hang the run.
  void stepUntil(bool Function() done, {required Duration within}) {
    final limit = _frames + (within.inMicroseconds * fps / 1000000).round();
    while (!done()) {
      if (_frames >= limit) {
        throw StateError('Not done after $within, at frame $_frames.');
      }
      _frame();
    }
  }

  void _frame() {
    _frames++;
    actions?.update(controls, now: now);
    _onFrame(Frame(dt: 1 / fps, now: now));
    audio.advance(1 / fps);
  }
}
