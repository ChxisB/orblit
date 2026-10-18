import 'dart:convert';

import 'package:orblit_profile/orblit_profile.dart';
import 'package:test/test.dart';

// A clock the test moves by hand, in microseconds.
final class _Clock {
  int now = 0;
  int call() => now;
}

void main() {
  late _Clock clock;

  setUp(() => clock = _Clock());

  Map<String, Object?> section(Map<String, Object?> from, String name) =>
      from[name]! as Map<String, Object?>;

  group('frames', () {
    test('are the time between one frame call and the next', () {
      final profiler = Profiler(clock: clock.call)..frame();
      for (final ms in [16, 17, 33]) {
        clock.now += ms * 1000;
        profiler.frame();
      }

      final frames = section(profiler.snapshot(), 'frames');
      expect(frames['count'], 3);
      expect(frames['maxMs'], 33);
      expect(frames['meanMs'], closeTo(22, 1e-9));
      expect(frames['fps'], closeTo(1000 / 22, 1e-9));
      expect(frames['recentMs'], [16, 17, 33]);
    });

    test('keep only the last few, so memory does not grow', () {
      final profiler = Profiler(frames: 4, clock: clock.call)..frame();
      for (var ms = 1; ms <= 10; ms++) {
        clock.now += ms * 1000;
        profiler.frame();
      }

      expect(section(profiler.snapshot(), 'frames')['recentMs'], [7, 8, 9, 10]);
    });

    test('say nothing about speed before there is a frame to measure', () {
      final frames = section(Profiler(clock: clock.call).snapshot(), 'frames');

      expect(frames['count'], 0);
      expect(frames['fps'], isNull);
    });
  });

  group('spans', () {
    test('add up within a frame and are read per frame', () {
      final profiler = Profiler(clock: clock.call)..frame();
      for (var frame = 0; frame < 2; frame++) {
        for (var i = 0; i < 3; i++) {
          final started = profiler.begin();
          clock.now += 2000;
          profiler.end('update', started);
        }
        clock.now += 10000;
        profiler.frame();
      }

      final update = section(section(profiler.snapshot(), 'spans'), 'update');
      expect(update['lastMs'], 6);
      expect(update['meanMs'], 6);
    });

    test('count a frame where the span did not run as nothing', () {
      final profiler = Profiler(clock: clock.call)..frame();
      final started = profiler.begin();
      clock.now += 4000;
      profiler.end('diff', started);
      profiler.frame();
      clock.now += 1000;
      profiler.frame();

      final diff = section(section(profiler.snapshot(), 'spans'), 'diff');
      expect(diff['meanMs'], 2);
      expect(diff['lastMs'], 0);
      expect(diff['maxMs'], 4);
    });

    test('ignore what ran before the first frame', () {
      final profiler = Profiler(clock: clock.call);
      final started = profiler.begin();
      clock.now += 9000;
      profiler.end('load', started);
      profiler.frame();
      clock.now += 1000;
      profiler.frame();

      expect(
        section(section(profiler.snapshot(), 'spans'), 'load')['maxMs'],
        0,
      );
    });
  });

  test('a gauge holds the last value and a count adds up', () {
    final profiler = Profiler(clock: clock.call)
      ..gauge('entities', 12)
      ..gauge('entities', 30)
      ..count('mesh')
      ..count('mesh', 4);

    final snapshot = profiler.snapshot();
    expect(section(snapshot, 'gauges'), {'entities': 30});
    expect(section(snapshot, 'counts'), {'mesh': 5});
  });

  group('heap', () {
    test('is read once in the interval, not every frame', () {
      var reads = 0;
      final profiler = Profiler(heap: () => ++reads, clock: clock.call);
      for (var i = 0; i < 300; i++) {
        profiler.frame();
        clock.now += 16667;
      }

      // 300 frames of 16.7 ms are five seconds.
      expect(reads, inInclusiveRange(5, 6));
    });

    test('names a leak as bytes a minute', () {
      var bytes = 100000000;
      final profiler = Profiler(heap: () => bytes, clock: clock.call);
      for (var second = 0; second < 120; second++) {
        profiler.frame();
        clock.now += 1000000;
        bytes += 50000;
      }

      final heap = section(profiler.snapshot(), 'heap');
      expect(heap['bytesPerMinute'] as double, closeTo(3000000, 1));
    });

    test('is flat when nothing grows', () {
      final profiler = Profiler(heap: () => 80000000, clock: clock.call);
      for (var second = 0; second < 30; second++) {
        profiler.frame();
        clock.now += 1000000;
      }

      expect(
        section(profiler.snapshot(), 'heap')['bytesPerMinute'] as double,
        closeTo(0, 1e-6),
      );
    });

    test('has no line without a reader, or with one sample', () {
      final none = Profiler(clock: clock.call)..frame();
      final one = Profiler(heap: () => 1, clock: clock.call)..frame();

      expect(section(none.snapshot(), 'heap')['samples'], isEmpty);
      expect(section(one.snapshot(), 'heap')['bytesPerMinute'], isNull);
    });

    test('keeps the last samples, in seconds from the first', () {
      final profiler = Profiler(
        heapSamples: 3,
        heap: () => 7,
        clock: clock.call,
      );
      for (var second = 0; second < 6; second++) {
        profiler.frame();
        clock.now += 1000000;
      }

      expect(section(profiler.snapshot(), 'heap')['samples'], [
        [0, 7],
        [1, 7],
        [2, 7],
      ]);
    });
  });

  test('a snapshot is plain JSON', () {
    final profiler = Profiler(heap: () => 5, clock: clock.call)..frame();
    final started = profiler.begin();
    clock.now += 1000;
    profiler.end('update', started);
    profiler.gauge('draws', 4);
    profiler.frame();

    expect(() => jsonEncode(profiler.snapshot()), returnsNormally);
  });

  test('off takes every call and keeps nothing', () {
    const profiler = Profiler.off();

    profiler.frame();
    profiler.end('update', profiler.begin());
    profiler
      ..gauge('draws', 4)
      ..count('mesh');

    expect(profiler.snapshot(), isEmpty);
  });

  test('a ring with no room is refused', () {
    expect(() => Profiler(frames: 0), throwsArgumentError);
  });
}
