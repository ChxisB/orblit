import 'package:orblit_progress/orblit_progress.dart';
import 'package:test/test.dart';

void main() {
  ProgressSchema build(List<ProgressName> names, {int version = 1}) =>
      ProgressSchema(version: version, names: names);

  test('numbers the wholes in order and puts a slice in its whole\'s slot', () {
    final schema = build([
      const Whole('a'),
      const Whole('b'),
      const Slice('b.low', of: 'b', low: 0, bits: 4),
    ]);

    expect(schema.slotCount, 2);
    expect(schema.placeOf('a'), (slot: 0, low: 0, bits: 32));
    expect(schema.placeOf('b'), (slot: 1, low: 0, bits: 32));
    expect(schema.placeOf('b.low'), (slot: 1, low: 0, bits: 4));
  });

  test('a slice may come before the whole it slices', () {
    final schema = build([
      const Slice('s', of: 'w', low: 8, bits: 8),
      const Whole('w'),
    ]);

    expect(schema.placeOf('s').slot, 0);
  });

  test('refuses an empty or repeated name', () {
    expect(() => build([const Whole('')]), throwsArgumentError);
    expect(
      () => build([const Whole('a'), const Whole('a')]),
      throwsArgumentError,
    );
    expect(
      () =>
          build([const Whole('a'), const Slice('a', of: 'a', low: 0, bits: 1)]),
      throwsArgumentError,
    );
  });

  test('refuses a slice of nothing, of a slice, or past bit 32', () {
    expect(
      () => build([const Slice('s', of: 'w', low: 0, bits: 1)]),
      throwsArgumentError,
    );
    expect(
      () => build([
        const Whole('w'),
        const Slice('s', of: 'w', low: 0, bits: 4),
        const Slice('t', of: 's', low: 0, bits: 1),
      ]),
      throwsArgumentError,
    );
    for (final (low, bits) in [(0, 0), (-1, 1), (31, 2), (0, 33)]) {
      expect(
        () => build([
          const Whole('w'),
          Slice('s', of: 'w', low: low, bits: bits),
        ]),
        throwsArgumentError,
      );
    }
  });

  test('refuses two slices that share a bit, and allows ones that touch', () {
    expect(
      () => build([
        const Whole('w'),
        const Slice('a', of: 'w', low: 0, bits: 8),
        const Slice('b', of: 'w', low: 7, bits: 8),
      ]),
      throwsArgumentError,
    );
    expect(
      build([
        const Whole('w'),
        const Slice('a', of: 'w', low: 0, bits: 8),
        const Slice('b', of: 'w', low: 8, bits: 8),
      ]).slotCount,
      1,
    );
  });

  test('a slice of the whole word overlaps every other slice', () {
    for (final order in [
      [32, 1],
      [1, 32],
    ]) {
      expect(
        () => build([
          const Whole('w'),
          for (final (i, bits) in order.indexed)
            Slice('s$i', of: 'w', low: 0, bits: bits),
        ]),
        throwsArgumentError,
      );
    }
  });

  test('refuses more wholes than a table can number', () {
    expect(
      () => build([for (var i = 0; i < 0x10000; i++) Whole('n$i')]),
      throwsArgumentError,
    );
    expect(
      build([for (var i = 0; i < 0xFFFF; i++) Whole('n$i')]).slotCount,
      0xFFFF,
    );
  });

  test('the same bits of two wholes are two different places', () {
    final schema = build([
      const Whole('a'),
      const Whole('b'),
      const Slice('a.low', of: 'a', low: 0, bits: 8),
      const Slice('b.low', of: 'b', low: 0, bits: 8),
    ]);

    expect(schema.placeOf('a.low').slot, 0);
    expect(schema.placeOf('b.low').slot, 1);
  });

  test('refuses a version outside 1 to 65535', () {
    expect(() => build([], version: 0), throwsArgumentError);
    expect(() => build([], version: 0x10000), throwsArgumentError);
  });

  test('knows a name and throws for one it does not', () {
    final schema = build([const Whole('a')]);

    expect(schema.has('a'), isTrue);
    expect(schema.has('b'), isFalse);
    expect(() => schema.placeOf('b'), throwsArgumentError);
  });
}
