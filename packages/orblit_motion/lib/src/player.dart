import 'dart:math' as math;

import 'package:orblit_sequence/orblit_sequence.dart';

import 'clip.dart';
import 'frame.dart';
import 'root.dart';

/// One step of a [ClipPlayer]: where the clip now stands, and what happened
/// getting there.
class ClipStep {
  ClipStep({required this.frame, this.marks = const [], RootStep? moved})
    : moved = moved ?? RootStep();

  /// The clip where the player now is, with its root motion held in place.
  final ClipFrame frame;

  /// Every mark passed, in the order it was passed.
  final List<Mark> marks;

  /// How far root motion carried the character.
  final RootStep moved;
}

/// A clip being played.
///
/// Like a sequence's director, it holds only where the playhead is and which
/// way it is going, and asks the clip for everything else, so it can be
/// seeked anywhere at any time. It starts paused, the way a director does.
///
/// What it adds is the clip's two kinds of consequence. Marks fire on the
/// way forwards, including one at the very start of each pass, so a
/// footstep on the first frame of a walk lands on every lap. Root motion is
/// measured over every stretch actually played, so a loop that wraps in the
/// middle of a step still moves the character by the whole step.
class ClipPlayer {
  ClipPlayer(this.clip, {WhenDone? whenDone, this.speed = 1})
    : whenDone = whenDone ?? clip.whenDone;

  final ClipDocument clip;

  /// What playing does at the end. The clip's own, unless said otherwise.
  WhenDone whenDone;

  /// How fast clip time runs against whatever is advancing it. Negative
  /// plays it backwards, which fires no marks and walks backwards.
  double speed;

  double _at = 0;
  bool _playing = false;
  bool _finished = false;
  int _direction = 1;

  /// Where the playhead is, in seconds.
  double get at => _at;

  bool get playing => _playing;

  /// Whether it ran to an end and stopped there on its own.
  bool get finished => _finished;

  /// Whether it finished by letting go: nothing it says should be applied
  /// any more, and whatever else drives the model takes it back.
  bool get released => _finished && whenDone == WhenDone.release;

  void play() {
    _playing = true;
    _finished = false;
  }

  void pause() => _playing = false;

  /// Back to the start, and stopped.
  void stop() {
    _playing = false;
    _finished = false;
    _direction = 1;
    _at = 0;
  }

  /// Moves the playhead without playing anything: no marks and no motion,
  /// because a scrub is somebody looking, not the character moving.
  void seek(double to) {
    _at = to.clamp(0.0, clip.duration);
    _finished = false;
  }

  /// Moves on by [seconds] of wall time and hands back everything that
  /// happened on the way, however big the step: a frame dropped under load
  /// must not lose a footstep or shorten a walk.
  ClipStep advance(double seconds) {
    final duration = clip.duration;
    var left = seconds * speed.abs();
    if (!_playing || !(left > 0)) return ClipStep(frame: sample());

    final marks = <Mark>[];
    var moved = RootStep();
    var ends = 0;
    while (true) {
      final forward = (_direction > 0) == (speed > 0);
      final room = forward ? duration - _at : _at;
      final go = math.min(left, room);
      if (go > 0) {
        final from = _at;
        final to = go == room
            ? (forward ? duration : 0.0)
            : (forward ? from + go : from - go);
        if (forward) {
          // A pass that starts at the very start takes the marks there
          // too: nothing else would ever fire them.
          marks.addAll(clip.marksBetween(from <= 0 ? -1e-9 : from, to));
        }
        moved = moved.then(clip.rootStep(from, to));
        _at = to;
        left -= go;
      }

      final atEnd = forward ? _at >= duration : _at <= 0;
      if (!atEnd) break;
      if (whenDone == WhenDone.hold || whenDone == WhenDone.release) {
        _playing = false;
        _finished = true;
        break;
      }
      // A loop or a bounce turns at the end only with time left to spend,
      // so a step that lands exactly on the last frame shows it.
      if (left <= 0 || duration <= 0) break;
      if (whenDone == WhenDone.loop) {
        _at = forward ? 0 : duration;
      } else {
        _direction = -_direction;
      }

      if (++ends >= _maxEnds) {
        // Past this many ends in one step the step is not a frame, it is a
        // stall. Whole laps are skipped, with where they would have taken
        // the character but without their marks.
        final lap = whenDone == WhenDone.loop ? duration : 2 * duration;
        final laps = (left / lap).floor();
        left -= laps * lap;
        if (whenDone == WhenDone.loop && laps > 0) {
          final one = forward
              ? clip.rootStep(0, duration)
              : clip.rootStep(duration, 0);
          moved = moved.then(repeated(one, laps));
        }
        ends = 0;
      }
    }

    return ClipStep(frame: sample(), marks: marks, moved: moved);
  }

  /// What the clip says right now, root held, without moving.
  ClipFrame sample() => clip.sampleAt(_at, inPlace: true);

  static const int _maxEnds = 256;
}
