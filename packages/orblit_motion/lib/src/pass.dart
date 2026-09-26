import 'dart:math' as math;

import 'package:orblit_sequence/orblit_sequence.dart';

import 'clip.dart';
import 'root.dart';

/// Seconds into [clip] at [lap] times through, ending the [whenDone] way.
///
/// A loop at a whole lap is at its end rather than its start, and so is a
/// bounce at an odd one, so a step that lands exactly on the last frame
/// shows it, as a player's does. Letting go is holding here: a blend always
/// has a say, and handing a character back is a change to another state.
double timeAt(ClipDocument clip, WhenDone whenDone, double lap) {
  final length = clip.duration;
  if (!(length > 0) || !(lap > 0)) return 0;
  switch (whenDone) {
    case WhenDone.hold || WhenDone.release:
      return math.min(lap, 1.0) * length;
    case WhenDone.loop:
      final into = lap - lap.floorToDouble();
      return (into == 0 ? 1.0 : into) * length;
    case WhenDone.bounce:
      final pass = lap.floor();
      final into = lap - pass;
      if (into == 0) return pass.isOdd ? length : 0;
      return (pass.isEven ? into : 1 - into) * length;
  }
}

/// Where [clip] carries the character, and the marks it passes, playing
/// from [from] times through to [to] the [whenDone] way.
///
/// The same as a player playing the stretch: marks fire on the way forwards,
/// the first frame of each pass included, and root motion is measured over
/// every stretch actually played. [marks] false skips looking for them.
({RootStep moved, List<Mark> marks}) passOver(
  ClipDocument clip,
  WhenDone whenDone,
  double from,
  double to, {
  bool marks = true,
}) {
  final passed = <Mark>[];
  var moved = RootStep();
  final length = clip.duration;
  if (!(to > from) || !(length > 0)) return (moved: moved, marks: passed);

  void forwards(double start, double end) {
    if (marks) {
      passed.addAll(clip.marksBetween(start <= 0 ? -1e-9 : start, end));
    }
    moved = moved.then(clip.rootStep(start, end));
  }

  if (whenDone == WhenDone.hold || whenDone == WhenDone.release) {
    final start = from.clamp(0.0, 1.0);
    final end = to.clamp(0.0, 1.0);
    if (end > start) forwards(start * length, end * length);
    return (moved: moved, marks: passed);
  }

  var at = math.max(from, 0.0);
  var pass = at.floor();
  while (at < to) {
    if (to - at > _mostPasses && at == pass) {
      // Past this many passes in one step the step is not a frame, it is a
      // stall. Whole passes are skipped, with where they would have taken
      // the character but without their marks.
      final whole = (to - at).floor() - 1;
      if (whenDone == WhenDone.loop) {
        moved = moved.then(repeated(clip.rootStep(0, length), whole));
      } else if (whole.isOdd) {
        // A bounce there and back goes nowhere, so only the odd one out
        // counts, and it goes the way the first of them did.
        moved = moved.then(
          pass.isEven ? clip.rootStep(0, length) : clip.rootStep(length, 0),
        );
      }
      at += whole;
      pass += whole;
      continue;
    }
    final end = math.min(to, pass + 1.0);
    final start = (at - pass) * length;
    final stop = (end - pass) * length;
    if (whenDone == WhenDone.loop || pass.isEven) {
      forwards(start, stop);
    } else {
      // The way back of a bounce, which fires nothing.
      moved = moved.then(clip.rootStep(length - start, length - stop));
    }
    at = end;
    pass++;
  }
  return (moved: moved, marks: passed);
}

const int _mostPasses = 256;
