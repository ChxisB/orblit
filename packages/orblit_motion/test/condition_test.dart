import 'dart:convert';

import 'package:orblit_motion/orblit_motion.dart';
import 'package:test/test.dart';

double Function(String) reading(Map<String, double> values) =>
    (input) => values[input] ?? 0;

bool holds(
  BlendCondition condition,
  Map<String, double> values, {
  double lap = 0,
}) => condition.holds(reading(values), lap);

void main() {
  test('always holds', () {
    expect(holds(BlendCondition.always, {}), isTrue);
  });

  test('compares an input with a number, strictly', () {
    const fast = BlendCondition.above('speed', 1);
    const slow = BlendCondition.below('speed', 1);
    expect(holds(fast, {'speed': 2}), isTrue);
    expect(holds(fast, {'speed': 1}), isFalse);
    expect(holds(slow, {'speed': 0.5}), isTrue);
    expect(holds(slow, {'speed': 1}), isFalse);
  });

  test('takes an input that is anything but nought as on', () {
    const jumping = BlendCondition.on('jump');
    expect(holds(jumping, {'jump': 1}), isTrue);
    expect(holds(jumping, {'jump': -1}), isTrue);
    expect(holds(jumping, {}), isFalse);
  });

  test('waits for the state being left to play through', () {
    const done = BlendCondition.through(1);
    expect(holds(done, {}, lap: 0.99), isFalse);
    expect(holds(done, {}, lap: 1), isTrue);
    expect(holds(done, {}, lap: 3.5), isTrue);
  });

  test('puts conditions together', () {
    const landing = BlendCondition.all([
      BlendCondition.on('grounded'),
      BlendCondition.not(BlendCondition.above('fall', 3)),
    ]);
    expect(holds(landing, {'grounded': 1, 'fall': 1}), isTrue);
    expect(holds(landing, {'grounded': 1, 'fall': 5}), isFalse);
    expect(holds(landing, {'fall': 1}), isFalse);

    const either = BlendCondition.any([
      BlendCondition.on('hit'),
      BlendCondition.through(2),
    ]);
    expect(holds(either, {'hit': 1}), isTrue);
    expect(holds(either, {}, lap: 2), isTrue);
    expect(holds(either, {}, lap: 1), isFalse);
    expect(holds(const BlendCondition.all([]), {}), isTrue);
    expect(holds(const BlendCondition.any([]), {}), isFalse);
  });

  test('lists the inputs it reads, once each', () {
    const condition = BlendCondition.all([
      BlendCondition.on('grounded'),
      BlendCondition.any([
        BlendCondition.above('speed', 1),
        BlendCondition.below('speed', -1),
      ]),
      BlendCondition.through(1),
    ]);
    expect(condition.inputs.toList(), ['grounded', 'speed']);
  });

  group('in a file', () {
    test('reads back what it writes', () {
      const condition = BlendCondition.any([
        BlendCondition.all([
          BlendCondition.on('grounded'),
          BlendCondition.above('speed', 0.1),
        ]),
        BlendCondition.not(BlendCondition.below('height', 2)),
        BlendCondition.through(0.8),
      ]);
      final text = jsonEncode(condition.toJson());
      final problems = <String>[];
      final back = BlendCondition.fromJson(
        jsonDecode(text),
        'The change',
        problems,
      );
      expect(problems, isEmpty);
      expect(jsonEncode(back!.toJson()), text);
    });

    test('writes always as nothing, and reads nothing as always', () {
      expect(BlendCondition.always.toJson(), isEmpty);
      expect(
        BlendCondition.fromJson(const <String, Object?>{}, 'The change', []),
        same(BlendCondition.always),
      );
    });

    test('is nothing, with a note, when it cannot be read', () {
      for (final raw in <Object?>[
        'speed > 1',
        {'input': 'speed', 'above': 'fast'},
        {'through': 'twice'},
        {'all': 'of them'},
        {
          'any': [
            {'input': 'jump'},
            {'wobble': 1},
          ],
        },
        {
          'not': {'below': 1},
        },
      ]) {
        final problems = <String>[];
        expect(
          BlendCondition.fromJson(raw, 'The change', problems),
          isNull,
          reason: '$raw',
        );
        expect(problems, hasLength(1), reason: '$raw');
        expect(problems.single, startsWith('The change'));
      }
    });
  });
}
