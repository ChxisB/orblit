import 'dart:math';

import 'package:orblit_save/orblit_save.dart';
import 'package:test/test.dart';

void main() {
  AutoSave make({int perTick = 10, int every = 100, int jitter = 0}) =>
      AutoSave(
        perTick: perTick,
        every: every,
        jitter: jitter,
        random: Random(7),
      );

  group('claim', () {
    test('with 1,000 keys no tick claims more than perTick', () {
      final auto = make(perTick: 10, every: 200, jitter: 20);
      final keys = [for (var i = 0; i < 1000; i++) 'p$i'];
      final lastSeen = <String, int>{};
      var most = 0;

      for (var tick = 0; tick < 2000; tick++) {
        final picked = auto.claim(keys, tick);
        most = max(most, picked.length);
        for (final key in picked) {
          final before = lastSeen[key];
          if (before != null) {
            expect(tick - before, greaterThanOrEqualTo(200), reason: key);
          }
          lastSeen[key] = tick;
        }
      }

      expect(most, lessThanOrEqualTo(10));
      expect(most, 10, reason: 'a busy tick fills its quota');
      expect(lastSeen.length, 1000, reason: 'every key was saved');
    });

    test('keys that join together do not come due together', () {
      final auto = make(perTick: 1000, every: 100);
      final keys = [for (var i = 0; i < 500; i++) 'p$i'];
      final perTick = <int>[];

      for (var tick = 0; tick <= 100; tick++) {
        perTick.add(auto.claim(keys, tick).length);
      }

      expect(perTick.reduce(max), lessThan(50));
      expect(perTick.reduce((a, b) => a + b), 500);
    });

    test(
      'a key is claimed again no sooner than every, and by every plus jitter',
      () {
        final auto = make(perTick: 1000, every: 30, jitter: 10);
        final at = <int>[];

        for (var tick = 0; tick < 400; tick++) {
          if (auto.claim(['ana'], tick).isNotEmpty) at.add(tick);
        }

        expect(at.length, greaterThan(5));
        final gaps = [for (var i = 1; i < at.length; i++) at[i] - at[i - 1]];
        for (final gap in gaps) {
          expect(gap, inInclusiveRange(30, 40));
        }
        expect(gaps.any((gap) => gap > 30), isTrue, reason: 'jitter is used');
      },
    );

    test('the longest overdue goes first when the quota is short', () {
      final auto = make(perTick: 1, every: 1);
      final first = auto.claim(['a', 'b', 'c'], 0);
      expect(first, isEmpty, reason: 'new keys wait their first turn');

      final order = <String>[];
      for (var tick = 1; tick < 4; tick++) {
        order.addAll(auto.claim(['a', 'b', 'c'], tick));
      }

      expect(order, ['a', 'b', 'c']);
    });

    test('a key that leaves the list is forgotten and starts again', () {
      final auto = make(perTick: 100, every: 50);
      var tick = 0;
      for (; tick < 60; tick++) {
        auto.claim(['ana'], tick);
      }
      for (; tick < 70; tick++) {
        auto.claim(['bo'], tick);
      }
      var claimedAt = -1;
      for (; tick < 200 && claimedAt < 0; tick++) {
        if (auto.claim(['ana'], tick).isNotEmpty) claimedAt = tick;
      }

      expect(claimedAt, greaterThan(70));
    });

    test('a key that returns is not due when it left', () {
      final auto = make(perTick: 100, every: 20);
      var tick = 0;
      for (; tick <= 30; tick++) {
        auto.claim(['ana'], tick);
      }
      auto.claim(['bo'], tick++);
      for (; tick < 41; tick++) {
        auto.claim(['bo'], tick);
      }

      expect(auto.claim(['ana'], tick), isEmpty, reason: 'it starts again');
    });

    test('a duplicate in the list counts once', () {
      final auto = make(perTick: 100, every: 1);
      auto.claim(['a', 'a'], 0);

      expect(auto.claim(['a', 'a'], 1), ['a']);
    });
  });

  test('a quota, interval or jitter out of range is refused', () {
    expect(() => make(perTick: 0), throwsArgumentError);
    expect(() => make(every: 0), throwsArgumentError);
    expect(() => make(jitter: -1), throwsArgumentError);
  });
}
