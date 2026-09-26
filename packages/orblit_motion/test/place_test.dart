import 'dart:convert';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:orblit_sequence/orblit_sequence.dart' show ease;
import 'package:test/test.dart';

BlendDocument moves() => BlendDocument(
  name: 'Moves',
  states: [
    BlendState('idle', plays: const BlendClip('idle')),
    BlendState('walk', plays: const BlendClip('walk')),
    BlendState('run', plays: const BlendClip('run')),
    BlendState('jump', plays: const BlendClip('jump')),
    BlendState('land', plays: const BlendClip('land')),
  ],
);

List<(String, double)> shares(BlendPlace place) => [
  for (final (at, share) in place.shares) (at.state, share),
];

void expectShares(BlendPlace place, List<(String, double)> expected) {
  final actual = shares(place);
  expect(
    [for (final (state, _) in actual) state],
    [for (final (state, _) in expected) state],
  );
  for (var i = 0; i < expected.length; i++) {
    expect(actual[i].$2, closeTo(expected[i].$2, 1e-12), reason: '$actual');
  }
}

/// Walk fading in over idle, halfway, and run fading in over that, a
/// quarter of the way.
BlendPlace twoFades() => const BlendPlace(
  'run',
  lap: 0.1,
  from: BlendPlace(
    'walk',
    lap: 1.6,
    from: BlendPlace('idle', lap: 4.25),
    faded: 0.1,
    fade: 0.2,
    shape: Easing.linear,
  ),
  faded: 0.05,
  fade: 0.2,
  shape: Easing.linear,
);

void main() {
  group('say', () {
    test('is all the state being played when nothing fades', () {
      const place = BlendPlace('walk', lap: 0.3);
      expect(place.weight, 1);
      expectShares(place, [('walk', 1)]);
      expect(place.depth, 1);
    });

    test('grows through a fade the way it eases', () {
      const halfway = BlendPlace(
        'walk',
        from: BlendPlace('idle'),
        faded: 0.1,
        fade: 0.2,
        shape: Easing.linear,
      );
      expect(halfway.weight, closeTo(0.5, 1e-12));
      const eased = BlendPlace(
        'walk',
        from: BlendPlace('idle'),
        faded: 0.05,
        fade: 0.2,
      );
      expect(eased.weight, closeTo(ease(Easing.smooth, 0.25), 1e-12));
    });

    test('shares out newest first and adds to one', () {
      final place = twoFades();
      expect(place.depth, 3);
      expectShares(place, [('run', 0.25), ('walk', 0.375), ('idle', 0.375)]);
      expect(
        place.shares.fold(0.0, (sum, share) => sum + share.$2),
        closeTo(1, 1e-12),
      );
    });

    test('stays between nought and one for an easing that overshoots', () {
      const place = BlendPlace(
        'walk',
        from: BlendPlace('idle'),
        faded: 0.7,
        fade: 1,
        shape: Easing.back,
      );
      expect(place.weight, inInclusiveRange(0, 1));
      for (final (_, share) in place.shares) {
        expect(share, inInclusiveRange(0, 1));
      }
    });
  });

  group('entering', () {
    test('cuts with no fade, leaving nothing behind', () {
      final next = twoFades().enter('jump');
      expect(next, const BlendPlace('jump'));
    });

    test('fades in over everything that was playing', () {
      final next = const BlendPlace('walk', lap: 2.5).enter('run', fade: 0.3);
      expect(next.state, 'run');
      expect(next.lap, 0);
      expect(next.from, const BlendPlace('walk', lap: 2.5));
      expect(next.fade, 0.3);
      expect(next.faded, 0);
      expect(next.weight, 0);
    });

    test('keeps in step when asked, as far through the lap', () {
      final next = const BlendPlace(
        'walk',
        lap: 2.25,
      ).enter('run', fade: 0.3, inStep: true);
      expect(next.lap, 0.25);
    });

    test('keeps no more than the deepest few, dropping the oldest', () {
      final deep = twoFades().enter('jump', fade: 0.1).enter('land', fade: 0.1);
      expect(deep.depth, BlendPlace.deepest);
      expect(
        [for (final (at, _) in deep.shares) at.state],
        ['land', 'jump', 'run', 'walk'],
      );
      final oldest = deep.from!.from!.from!;
      expect(oldest, const BlendPlace('walk', lap: 1.6));
      // The rest of the fades are as they were.
      expect(deep.from!.from!.faded, 0.05);
    });

    test('is a cut for a fade that is not a length of time', () {
      expect(
        const BlendPlace('walk').enter('run', fade: double.infinity).from,
        isNull,
      );
      expect(
        const BlendPlace('walk').enter('run', fade: double.nan).from,
        isNull,
      );
    });
  });

  group('in a save file', () {
    test('comes back the same, fades and all', () {
      final blend = moves();
      final place = twoFades();
      final back = BlendPlace.fromJson(
        jsonDecode(jsonEncode(place.toJson())),
        blend,
      );
      expect(back, place);
    });

    test('writes a place with nothing fading as a state and a lap', () {
      expect(const BlendPlace('walk', lap: 1.5).toJson(), {
        'state': 'walk',
        'lap': 1.5,
      });
    });

    test('drops what the blend no longer has', () {
      final blend = moves();
      final json = twoFades().toJson();
      ((json['from']! as Map<String, Object?>)['from']!
              as Map<String, Object?>)['state'] =
          'crawl';
      expect(
        BlendPlace.fromJson(json, blend),
        const BlendPlace(
          'run',
          lap: 0.1,
          from: BlendPlace('walk', lap: 1.6),
          faded: 0.05,
          fade: 0.2,
          shape: Easing.linear,
        ),
      );
      json['state'] = 'swim';
      expect(BlendPlace.fromJson(json, blend), isNull);
      expect(BlendPlace.fromJson('walk', blend), isNull);
    });

    test('mends a lap or a fade it cannot use', () {
      final blend = moves();
      expect(
        BlendPlace.fromJson({'state': 'walk', 'lap': -2}, blend),
        const BlendPlace('walk'),
      );
      expect(
        BlendPlace.fromJson({
          'state': 'walk',
          'fade': 0.2,
          'faded': 9,
          'from': {'state': 'idle'},
        }, blend)!.faded,
        0.2,
      );
    });
  });

  group('as numbers', () {
    test('always takes the same room', () {
      final blend = moves();
      expect(BlendPlace.width, 21);
      expect(const BlendPlace('idle').toNumbers(blend), hasLength(21));
      expect(twoFades().toNumbers(blend), hasLength(21));
    });

    test('comes back the same, fades and all', () {
      final blend = moves();
      for (final place in [
        const BlendPlace('idle'),
        const BlendPlace('run', lap: 7.125),
        twoFades(),
        twoFades().enter('jump', fade: 0.1).enter('land', fade: 0.1),
      ]) {
        expect(BlendPlace.fromNumbers(blend, place.toNumbers(blend)), place);
      }
    });

    test('names states by where they are in the blend', () {
      final numbers = const BlendPlace('run', lap: 0.5).toNumbers(moves());
      expect(numbers.take(3), [1, 2, 0.5]);
    });

    test('drops a state the blend has not got, and all older', () {
      final blend = moves();
      final numbers = twoFades().toNumbers(blend);
      numbers[1 + 5 * 1] = 40;
      expect(
        BlendPlace.fromNumbers(blend, numbers),
        const BlendPlace('run', lap: 0.1),
      );
      numbers[1] = -1;
      expect(BlendPlace.fromNumbers(blend, numbers), isNull);
      expect(BlendPlace.fromNumbers(blend, const [1, 0]), isNull);
    });
  });
}
