import 'package:orblit_motion/orblit_motion.dart';
import 'package:test/test.dart';

/// A second of a light coming up, with a mark at each end and the middle.
ClipDocument marked({WhenDone whenDone = WhenDone.hold, double duration = 1}) =>
    ClipDocument(
      name: 'Steps',
      duration: duration,
      whenDone: whenDone,
      channels: [
        ClipChannel<double>(
          target: '',
          property: 'light.power',
          kind: ChannelKind.number,
          keys: const [
            Key(0, 0, hold: Hold.linear),
            Key(1, 1),
          ],
        ),
      ],
      marks: const [Mark(0, 'start'), Mark(0.5, 'middle'), Mark(1, 'end')],
    );

List<String> names(ClipStep step) => [for (final mark in step.marks) mark.name];

ClipPlayer playing(ClipDocument clip, {double speed = 1}) =>
    ClipPlayer(clip, speed: speed)..play();

void main() {
  test('starts paused, and a paused player goes nowhere', () {
    final player = ClipPlayer(marked());
    final step = player.advance(0.5);
    expect(player.playing, isFalse);
    expect(player.at, 0);
    expect(step.marks, isEmpty);
    expect(step.moved.isNone, isTrue);
  });

  test("plays to the clip's own ending unless told another", () {
    expect(ClipPlayer(marked(whenDone: WhenDone.loop)).whenDone, WhenDone.loop);
    expect(
      ClipPlayer(marked(), whenDone: WhenDone.bounce).whenDone,
      WhenDone.bounce,
    );
  });

  test('samples the clip where the playhead is', () {
    final player = playing(marked());
    final step = player.advance(0.25);
    expect(step.frame.at, 0.25);
    expect(step.frame.valueOf('', 'light.power'), closeTo(0.25, 1e-12));
    expect(player.sample().at, 0.25);
  });

  group('holding', () {
    test('fires each mark once, the first included, and stops at the end', () {
      final player = playing(marked());
      expect(names(player.advance(0.25)), ['start']);
      expect(names(player.advance(0.5)), ['middle']);
      final last = player.advance(0.5);
      expect(names(last), ['end']);
      expect(player.at, 1);
      expect(last.frame.at, 1);
      expect(player.playing, isFalse);
      expect(player.finished, isTrue);
      expect(player.released, isFalse);

      expect(player.advance(1).marks, isEmpty);
      expect(player.at, 1);
    });

    test('playing again after the end clears finished', () {
      final player = playing(marked())..advance(2);
      player.play();
      expect(player.finished, isFalse);
      expect(player.playing, isTrue);
    });
  });

  test('releasing lets go at the end', () {
    final player = ClipPlayer(marked(), whenDone: WhenDone.release)..play();
    player.advance(2);
    expect(player.finished, isTrue);
    expect(player.released, isTrue);
  });

  group('looping', () {
    test('fires the marks of every pass, the start of each included', () {
      final player = playing(marked(whenDone: WhenDone.loop));
      expect(names(player.advance(0.25)), ['start']);
      expect(names(player.advance(1)), ['middle', 'end', 'start']);
      expect(player.at, closeTo(0.25, 1e-12));
      expect(player.playing, isTrue);
      expect(player.finished, isFalse);
    });

    test('shows the last frame when a step lands exactly on it', () {
      final player = playing(marked(whenDone: WhenDone.loop));
      expect(names(player.advance(1)), ['start', 'middle', 'end']);
      expect(player.at, 1);
      expect(names(player.advance(0.25)), ['start']);
      expect(player.at, closeTo(0.25, 1e-12));
    });

    test('loses nothing to a step many laps long', () {
      final player = playing(marked(whenDone: WhenDone.loop));
      final all = names(player.advance(3.5));
      expect(all.where((name) => name == 'start'), hasLength(4));
      expect(all.where((name) => name == 'middle'), hasLength(4));
      expect(all.where((name) => name == 'end'), hasLength(3));
      expect(player.at, closeTo(0.5, 1e-9));
    });

    test('stops counting marks past a few hundred ends in one step', () {
      final player = playing(marked(whenDone: WhenDone.loop, duration: 0.01));
      final step = player.advance(1000);
      // A hundred thousand laps: a few hundred are played, the rest skipped.
      expect(step.marks.length, inInclusiveRange(256, 300));
      expect(player.at, inInclusiveRange(0, 0.01));
    });

    test('a loop of no length does not hang', () {
      final player = playing(marked(whenDone: WhenDone.loop, duration: 0));
      player.advance(1);
      expect(player.at, 0);
    });
  });

  test('bouncing turns at each end, with marks only on the way forwards', () {
    final player = playing(marked(whenDone: WhenDone.bounce));
    expect(names(player.advance(1.25)), ['start', 'middle', 'end']);
    expect(player.at, closeTo(0.75, 1e-12));
    expect(names(player.advance(1)), ['start']);
    expect(player.at, closeTo(0.25, 1e-12));
    expect(player.playing, isTrue);
  });

  test('playing backwards fires no marks, and finishes at the start', () {
    final player = ClipPlayer(marked(), speed: -1)
      ..seek(1)
      ..play();
    expect(player.advance(0.75).marks, isEmpty);
    expect(player.at, closeTo(0.25, 1e-12));
    expect(player.advance(1).marks, isEmpty);
    expect(player.at, 0);
    expect(player.finished, isTrue);
  });

  test('speed scales how far a step goes', () {
    final player = playing(marked(), speed: 2);
    expect(names(player.advance(0.25)), ['start', 'middle']);
    expect(player.at, 0.5);
  });

  group('seeking', () {
    test('fires nothing, and is kept within the clip', () {
      final player = ClipPlayer(marked())..seek(0.75);
      expect(player.at, 0.75);
      player.play();
      expect(player.advance(0.1).marks, isEmpty);

      player.seek(5);
      expect(player.at, 1);
      player.seek(-1);
      expect(player.at, 0);
    });

    test('stopping goes back to the start, paused', () {
      final player = playing(marked(whenDone: WhenDone.bounce))..advance(1.5);
      player.stop();
      expect(player.at, 0);
      expect(player.playing, isFalse);
      // Forwards again, whichever way the bounce was going.
      player.play();
      expect(names(player.advance(0.5)), ['start', 'middle']);
    });
  });
}
