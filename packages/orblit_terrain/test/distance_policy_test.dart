import 'package:orblit_terrain/orblit_terrain.dart';
import 'package:test/test.dart';

/// Costs are frame times in milliseconds: over 16 is too slow and under 12
/// has room.
DistancePolicy policy({int ceiling = 4, int floor = 1, double settle = 2}) =>
    DistancePolicy(
      ceiling: ceiling,
      floor: floor,
      high: 16,
      low: 12,
      settle: settle,
    );

/// Feeds [cost] every tenth of a second from [from] up to and including [to].
void feed(DistancePolicy policy, double cost, double from, double to) {
  for (var tenth = (from * 10).round(); tenth <= (to * 10).round(); tenth++) {
    policy.update(cost, tenth / 10);
  }
}

void main() {
  group('a distance policy', () {
    test('starts at the full distance', () {
      final p = policy();

      expect(p.steps, 4);
      expect(p.fraction, 1);
    });

    test('steps down one at a time, once every settle', () {
      final p = policy();

      feed(p, 30, 0, 1.9);
      expect(p.steps, 4, reason: 'nothing inside the first interval');
      feed(p, 30, 2, 3.9);
      expect(p.steps, 3);
      feed(p, 30, 4, 4);
      expect(p.steps, 2);
    });

    test('decides when settle seconds have passed exactly', () {
      final p = policy();

      p.update(30, 0);
      p.update(30, 1.99);
      expect(p.steps, 4);
      p.update(30, 2);
      expect(p.steps, 3);
    });

    test('never goes below the floor', () {
      final p = policy(floor: 2);

      feed(p, 30, 0, 30);

      expect(p.steps, 2);
      expect(p.fraction, 0.5);
    });

    test('steps up when there is room, to the ceiling and no further', () {
      final p = policy();
      feed(p, 30, 0, 6);
      expect(p.steps, 1);

      feed(p, 5, 6.1, 8);
      expect(p.steps, 2);
      feed(p, 5, 8.1, 40);
      expect(p.steps, 4);
    });

    test('holds when the cost is between the two thresholds', () {
      final p = policy();
      feed(p, 14, 0, 20);

      expect(p.steps, 4);
    });

    test('a cost exactly on a threshold holds', () {
      final p = policy();
      feed(p, 16, 0, 20);
      expect(p.steps, 4, reason: 'high is the most that is still fine');

      final q = policy();
      feed(q, 30, 0, 6);
      expect(q.steps, 1);
      feed(q, 12, 6.1, 30);
      expect(q.steps, 1, reason: 'low is the least that has no room');
    });

    test('judges the mean of the interval, not its last frame', () {
      final p = policy();

      feed(p, 14, 0, 1.9);
      p.update(30, 2);

      expect(p.steps, 4);
    });

    test('does not step back up on the cost its own step brought', () {
      // A cost of 14 is under 16, so it is no longer too slow, and over 12, so
      // there is still no room. One threshold would flip here.
      final p = policy();
      feed(p, 30, 0, 2);
      expect(p.steps, 3);

      feed(p, 14, 2.1, 30);

      expect(p.steps, 3);
    });

    test('one slow frame in an interval moves nothing', () {
      final p = policy();

      feed(p, 10, 0, 1.9);
      p.update(100, 1.95);
      feed(p, 10, 2, 2);

      expect(p.steps, 4);
    });

    test('an interval is judged on its own frames', () {
      // If the first interval's cost were carried over, the second would not
      // be low enough to step back up.
      final p = policy();
      feed(p, 30, 0, 2);
      expect(p.steps, 3);

      feed(p, 5, 2.1, 4);

      expect(p.steps, 4);
    });

    test('starts counting at its first frame, whatever the clock says', () {
      final p = policy();

      p.update(30, 100);
      expect(p.steps, 4);
      p.update(30, 102);
      expect(p.steps, 3);
    });

    test('refuses what cannot work', () {
      DistancePolicy make({int floor = 1, double low = 12, double s = 2}) =>
          DistancePolicy(
            ceiling: 4,
            floor: floor,
            high: 16,
            low: low,
            settle: s,
          );

      expect(() => make(floor: 0), throwsArgumentError);
      expect(() => make(floor: 5), throwsArgumentError);
      expect(() => make(low: 16), throwsArgumentError);
      expect(() => make(low: 20), throwsArgumentError);
      expect(() => make(s: 0), throwsArgumentError);
      expect(() => make(s: double.nan), throwsArgumentError);
      expect(() => make(low: double.nan), throwsArgumentError);
    });
  });
}
