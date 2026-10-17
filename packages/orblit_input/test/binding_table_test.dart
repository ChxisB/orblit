import 'dart:convert';

import 'package:orblit_input/orblit_input.dart';
import 'package:test/test.dart';

import 'action_fixture.dart';

void main() {
  const space = KeyControl('Space');
  const left = KeyControl('ArrowLeft');

  late ActionMap map;
  late BindingTable table;

  setUp(() {
    map = actionMap();
    table = map.bindings;
  });

  List<Control> controlsOf(String action, {String context = 'play'}) => [
    for (final binding in table.bindingsIn(context)[action]!) binding.control,
  ];

  /// What a player's file holds, as it would be written and read back.
  Object? saved(BindingTable table) => jsonDecode(jsonEncode(table.toJson()));

  group('reading', () {
    test('gives what each context was made with', () {
      expect(controlsOf('jump'), [
        space,
        const PadButtonControl(PadButton.south),
      ]);
      expect(controlsOf('confirm', context: 'menu'), [
        const KeyControl('Enter'),
        space,
      ]);
      expect(table.blocks('menu'), isTrue);
      expect(table.blocks('play'), isFalse);
      expect(table.hasContext('menu'), isTrue);
      expect(table.hasContext('nope'), isFalse);
    });

    test('hands out views that cannot be changed', () {
      expect(
        () => table.bindingsIn('play')['jump']!.add(const Binding(space)),
        throwsUnsupportedError,
      );
      expect(() => table.bindingsIn('play')['x'] = [], throwsUnsupportedError);
    });

    test('does not change a list somebody already holds', () {
      final before = table.bindingsIn('play')['jump']!;

      table.rebind('jump', left, context: 'play');

      expect(before.first.control, space);
      expect(controlsOf('jump').first, left);
    });

    test('refuses a context that does not exist', () {
      expect(() => table.bindingsIn('nope'), throwsArgumentError);
      expect(() => table.blocks('nope'), throwsArgumentError);
    });
  });

  group('clash', () {
    test('names the action that has the control', () {
      expect(table.clash('fire', space, context: 'play'), 'jump');
    });

    test('is not a clash with the slot being changed', () {
      expect(table.clash('jump', space, context: 'play'), isNull);
    });

    test('is a clash with another slot of the same action', () {
      expect(table.clash('jump', space, context: 'play', slot: 1), 'jump');
    });

    test('is only a clash inside the context', () {
      expect(table.clash('fire', space, context: 'play'), 'jump');
      expect(table.clash('jump', space, context: 'vehicle'), isNull);
      expect(
        table.clash('confirm', space, context: 'menu'),
        'confirm',
        reason: 'the menu\'s own second slot',
      );
    });

    test('finds nothing for a control nobody has', () {
      expect(table.clash('fire', left, context: 'play'), isNull);
    });
  });

  group('rebind', () {
    test('replaces the control and keeps the scale', () {
      table.rebind('steer', left, context: 'play');

      expect(controlsOf('steer'), [left, const KeyControl('KeyD')]);
      expect(table.bindingsIn('play')['steer']!.first.scale, -1);
      map.update(frame(keys: {'ArrowLeft'}), now: ms(0));
      expect(map.value('steer'), -1);
      map.update(frame(keys: {'KeyA'}), now: ms(16));
      expect(map.value('steer'), 0);
    });

    test('keeps the half of a two-axis action', () {
      table.rebind('move', left, context: 'play', slot: 3);

      expect(table.bindingsIn('play')['move']![3].part, AxisPart.y);
    });

    test('leaves the other contexts alone', () {
      table.rebind('jump', left, context: 'play');

      expect(controlsOf('jump', context: 'vehicle'), [
        const KeyControl('KeyE'),
      ]);
    });

    test('adds a binding one past the last', () {
      table.rebind('jump', left, context: 'play', slot: 2);

      expect(controlsOf('jump'), [
        space,
        const PadButtonControl(PadButton.south),
        left,
      ]);
      expect(table.bindingsIn('play')['jump']![2].scale, 1);
    });

    test('refuses a slot that is not there', () {
      expect(
        () => table.rebind('jump', left, context: 'play', slot: 3),
        throwsRangeError,
      );
      expect(
        () => table.rebind('jump', left, context: 'play', slot: -1),
        throwsRangeError,
      );
    });

    test('refuses a control the action cannot take', () {
      expect(
        () => table.rebind(
          'jump',
          const PadStickControl(PadStick.left),
          context: 'play',
        ),
        throwsArgumentError,
      );
      expect(controlsOf('jump').first, space);
    });

    test('refuses a context or an action that does not exist', () {
      expect(
        () => table.rebind('jump', left, context: 'nope'),
        throwsArgumentError,
      );
      expect(
        () => table.rebind('fire', left, context: 'menu'),
        throwsArgumentError,
        reason: 'the menu does not bind fire',
      );
      expect(
        () => table.rebind('nope', left, context: 'play'),
        throwsArgumentError,
      );
    });

    test('throws a clash and changes nothing when another action has it', () {
      expect(
        () => table.rebind('fire', space, context: 'play'),
        throwsA(
          isA<BindingClash>()
              .having((e) => e.action, 'action', 'jump')
              .having((e) => e.control, 'control', space),
        ),
      );

      expect(controlsOf('fire'), [const KeyControl('KeyF')]);
      expect(controlsOf('jump').first, space);
    });

    test('throws a clash for another slot of the same action', () {
      expect(
        () => table.rebind(
          'jump',
          const PadButtonControl(PadButton.south),
          context: 'play',
        ),
        throwsA(isA<BindingClash>()),
      );
    });

    test('rebinding to what the slot already has is not a clash', () {
      table.rebind('jump', space, context: 'play');

      expect(controlsOf('jump').first, space);
    });

    test('swaps with the action that had it', () {
      table.rebind('fire', space, context: 'play', swap: true);

      expect(controlsOf('fire'), [space]);
      expect(controlsOf('jump').first, const KeyControl('KeyF'));
    });

    test(
      'refuses a swap the other action cannot take, and changes nothing',
      () {
        expect(
          () =>
              table.rebind('move', space, context: 'play', slot: 4, swap: true),
          throwsArgumentError,
          reason: 'jump cannot take the stick that move would give it',
        );

        expect(controlsOf('jump').first, space);
        expect(controlsOf('move')[4], const PadStickControl(PadStick.left));
      },
    );

    test('refuses a swap on a binding that is being added', () {
      expect(
        () => table.rebind('fire', space, context: 'play', slot: 1, swap: true),
        throwsA(
          isA<ArgumentError>().having(
            (e) => e.message,
            'message',
            contains('nothing to swap'),
          ),
        ),
      );
      expect(controlsOf('fire'), [const KeyControl('KeyF')]);
      expect(controlsOf('jump').first, space);
    });

    test('swap with nothing in the way is a plain rebind', () {
      table.rebind('fire', left, context: 'play', swap: true);

      expect(controlsOf('fire'), [left]);
    });
  });

  group('profile', () {
    test('has nothing in it until something is changed', () {
      expect(table.toJson(), {'contexts': <String, Object?>{}});
    });

    test('holds only the actions that were changed', () {
      table.rebind('steer', left, context: 'play');

      expect(saved(table), {
        'contexts': {
          'play': {
            'steer': [
              {'key': 'ArrowLeft'},
              {'key': 'KeyD'},
            ],
          },
        },
      });
    });

    test('drops an action that is put back', () {
      table.rebind('fire', left, context: 'play');
      table.rebind('fire', const KeyControl('KeyF'), context: 'play');

      expect(table.toJson(), {'contexts': <String, Object?>{}});
    });

    test('loads into a new map as it was', () {
      table.rebind('steer', left, context: 'play');
      table.rebind('jump', const KeyControl('KeyJ'), context: 'play', slot: 2);
      final fresh = actionMap();

      fresh.bindings.load(saved(table));

      expect(saved(fresh.bindings), saved(table));
      fresh.update(frame(keys: {'ArrowLeft'}), now: ms(0));
      expect(
        fresh.value('steer'),
        -1,
        reason: 'the scale came from the default',
      );
      fresh.update(frame(keys: {'KeyJ'}), now: ms(16));
      expect(fresh.isDown('jump'), isTrue);
    });

    test('lets a changed default through to a player who left it alone', () {
      table.rebind('steer', left, context: 'play');
      final newer = actionMap(jumpKey: 'KeyJ');

      newer.bindings.load(saved(table));

      expect(
        [
          for (final b in newer.bindings.bindingsIn('play')['jump']!) b.control,
        ].first,
        const KeyControl('KeyJ'),
      );
      expect(newer.bindings.bindingsIn('play')['steer']!.first.control, left);
    });

    test('keeps the defaults a shorter list does not reach', () {
      table.load({
        'contexts': {
          'play': {
            'jump': [
              {'key': 'KeyJ'},
            ],
          },
        },
      });

      expect(controlsOf('jump'), [
        const KeyControl('KeyJ'),
        const PadButtonControl(PadButton.south),
      ]);
    });

    test('adds the bindings a longer list brings', () {
      table.load({
        'contexts': {
          'play': {
            'fire': [
              {'key': 'KeyF'},
              {'key': 'KeyG'},
              {'touch': 'fire'},
            ],
          },
        },
      });

      expect(controlsOf('fire'), [
        const KeyControl('KeyF'),
        const KeyControl('KeyG'),
        const TouchControl('fire'),
      ]);
    });

    test('skips a context or an action the game no longer has', () {
      table.load({
        'contexts': {
          'gone': {
            'jump': [
              {'key': 'KeyZ'},
            ],
          },
          'play': {
            'gone': [
              {'key': 'KeyZ'},
            ],
            'steer': [
              {'key': 'ArrowLeft'},
            ],
          },
          'menu': {
            'fire': [
              {'key': 'KeyZ'},
            ],
          },
        },
      });

      expect(controlsOf('steer').first, left);
      expect(controlsOf('jump').first, space);
    });

    test('skips a control the action can no longer take', () {
      table.load({
        'contexts': {
          'play': {
            'jump': [
              {'padStick': 'left'},
            ],
            'steer': [
              {'key': 'ArrowLeft'},
            ],
          },
        },
      });

      expect(controlsOf('jump').first, space);
      expect(controlsOf('steer').first, left);
    });

    test('is replaced, not added to, by the next load', () {
      table.rebind('steer', left, context: 'play');

      table.load({
        'contexts': {
          'play': {
            'fire': [
              {'key': 'KeyG'},
            ],
          },
        },
      });

      expect(controlsOf('steer').first, const KeyControl('KeyA'));
      expect(controlsOf('fire'), [const KeyControl('KeyG')]);
    });

    test('goes back to the defaults when loaded empty', () {
      table.rebind('steer', left, context: 'play');
      table.rebind('jump', const KeyControl('KeyJ'), context: 'play', slot: 2);

      table.load(<String, Object?>{});

      expect(table.toJson(), {'contexts': <String, Object?>{}});
      expect(controlsOf('jump'), hasLength(2));
    });

    test('is refused whole when it is not a profile', () {
      table.rebind('steer', left, context: 'play');
      final before = saved(table);
      const bad = <Object?>[
        null,
        'profile',
        {'contexts': 3},
        {
          'contexts': {'play': 3},
        },
        {
          'contexts': {
            'play': {'jump': 'KeyZ'},
          },
        },
        {
          'contexts': {
            'play': {
              'steer': [
                {'key': 'KeyZ'},
              ],
              'jump': [
                {'key': 1},
              ],
            },
          },
        },
      ];

      for (final json in bad) {
        expect(() => table.load(json), throwsFormatException, reason: '$json');
        expect(saved(table), before, reason: 'changed by $json');
      }
    });
  });
}
