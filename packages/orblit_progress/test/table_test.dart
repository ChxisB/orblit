import 'dart:typed_data';

import 'package:orblit_progress/orblit_progress.dart';
import 'package:test/test.dart';

final ProgressSchema schema = ProgressSchema(
  version: 1,
  names: const [
    Whole('flags'),
    Whole('count'),
    Slice('flag.bottom', of: 'flags', low: 0, bits: 1),
    Slice('flag.middle', of: 'flags', low: 4, bits: 8),
    Slice('flag.top', of: 'flags', low: 31, bits: 1),
  ],
);

void main() {
  group('a slice', () {
    test('writes its bits and leaves its neighbours', () {
      var table = ProgressTable(schema);
      table = table.set('flag.middle', 0xAB).table;
      table = table.set('flag.bottom', 1).table;
      table = table.set('flag.top', 1).table;

      expect(table.get('flag.middle'), 0xAB);
      expect(table.get('flag.bottom'), 1);
      expect(table.get('flag.top'), 1);
      expect(table.get('flags'), 0x80000000 | (0xAB << 4) | 1);

      table = table.set('flag.middle', 0).table;
      expect(table.get('flag.bottom'), 1);
      expect(table.get('flag.top'), 1);
    });

    test('reaches bit 0 and the top bit alone', () {
      final bottom = ProgressTable(schema).set('flag.bottom', 1).table;
      final top = ProgressTable(schema).set('flag.top', 1).table;

      expect(bottom.get('flags'), 1);
      expect(top.get('flags'), 0x80000000);
    });

    test('refuses a value that does not fit', () {
      final table = ProgressTable(schema);

      expect(() => table.set('flag.bottom', 2), throwsRangeError);
      expect(() => table.set('flag.middle', 256), throwsRangeError);
      expect(() => table.set('flag.middle', -1), throwsRangeError);
    });
  });

  group('a whole', () {
    test('holds the full 32 bits', () {
      final table = ProgressTable(schema).set('count', 0xFFFFFFFF).table;

      expect(table.get('count'), 0xFFFFFFFF);
      expect(() => table.set('count', 0x100000000), throwsRangeError);
      expect(() => table.set('count', -1), throwsRangeError);
    });

    test('writing it over its slices replaces them', () {
      final table = ProgressTable(
        schema,
      ).set('flag.middle', 0xFF).table.set('flags', 0).table;

      expect(table.get('flag.middle'), 0);
    });
  });

  group('set', () {
    test('returns a new table and leaves the old one', () {
      final before = ProgressTable(schema);
      final (:table, :changes) = before.set('count', 7);

      expect(before.get('count'), 0);
      expect(table.get('count'), 7);
      expect(changes.slots, [1]);
      expect(changes.valueOf(1), 7);
    });

    test('says nothing changed for the value it already had', () {
      final table = ProgressTable(schema).set('count', 7).table;

      expect(table.set('count', 7).changes.isEmpty, isTrue);
    });

    test('a write to a slice changes its whole\'s slot', () {
      final (:table, :changes) = ProgressTable(schema).set('flag.top', 1);

      expect(changes.slots, [0]);
      expect(changes.valueOf(0), table.get('flags'));
    });
  });

  group('changes', () {
    test('applied to an older copy give the newer one', () {
      final older = ProgressTable(schema).set('count', 3).table;
      final newer = older.set('flag.middle', 9).table.set('count', 4).table;

      final caught = older.apply(newer.changesSince(older));

      expect(caught.encode(), newer.encode());
    });

    test('hold only the slots that differ', () {
      final older = ProgressTable(schema).set('count', 3).table;
      final newer = older.set('flag.middle', 9).table;

      expect(newer.changesSince(older).slots, [0]);
      expect(older.changesSince(older).isEmpty, isTrue);
    });

    test('survive encoding', () {
      final older = ProgressTable(schema);
      final newer = older.set('count', 5).table.set('flag.top', 1).table;
      final changes = newer.changesSince(older);

      final read = Changes.tryDecode(schema, changes.encode());

      expect(read, changes);
      expect(read.hashCode, changes.hashCode);
    });

    test('refuse bytes that are not changes', () {
      final good = ProgressTable(schema).set('count', 5).changes.encode();
      final badSlot = Uint8List.fromList(good)..[2] = 9;
      final twice = Uint8List(2 + 12)
        ..[0] = 2
        ..[2] = 1
        ..[8] = 1;

      expect(Changes.tryDecode(schema, Uint8List(0)), isNull);
      expect(
        Changes.tryDecode(schema, good.sublist(0, good.length - 1)),
        isNull,
      );
      expect(
        Changes.tryDecode(schema, Uint8List.fromList([...good, 0])),
        isNull,
      );
      expect(Changes.tryDecode(schema, badSlot), isNull);
      expect(Changes.tryDecode(schema, twice), isNull);
    });

    test('decode never throws on random bytes', () {
      for (var seed = 0; seed < 500; seed++) {
        final bytes = Uint8List.fromList([
          for (var i = 0; i < seed % 40; i++) (seed * 31 + i * 17) & 0xFF,
        ]);
        Changes.tryDecode(schema, bytes);
      }
    });
  });

  group('save and load', () {
    test('give back the same table', () {
      final table = ProgressTable(
        schema,
      ).set('count', 12).table.set('flag.middle', 200).table;

      final loaded = ProgressTable.decode(schema, table.encode());

      expect(loaded.get('count'), 12);
      expect(loaded.get('flag.middle'), 200);
    });

    test('a save of ten names loads under a schema of eleven', () {
      ProgressSchema named(int n, int version) => ProgressSchema(
        version: version,
        names: [for (var i = 0; i < n; i++) Whole('n$i')],
      );
      var old = ProgressTable(named(10, 1));
      for (var i = 0; i < 10; i++) {
        old = old.set('n$i', i + 1).table;
      }

      final loaded = ProgressTable.decode(named(11, 2), old.encode());

      for (var i = 0; i < 10; i++) {
        expect(loaded.get('n$i'), i + 1);
      }
      expect(loaded.get('n10'), 0);
    });

    test('a save from a later schema is refused', () {
      ProgressSchema named(int n, int version) => ProgressSchema(
        version: version,
        names: [for (var i = 0; i < n; i++) Whole('n$i')],
      );

      // A later version with as many slots, and the same version with more.
      final newerVersion = ProgressTable(named(2, 2)).encode();
      final moreSlots = ProgressTable(named(3, 1)).encode();

      expect(
        () => ProgressTable.decode(named(2, 1), newerVersion),
        throwsFormatException,
      );
      expect(
        () => ProgressTable.decode(named(2, 1), moreSlots),
        throwsFormatException,
      );
    });

    test('bytes that are not a table are refused', () {
      final good = ProgressTable(schema).encode();

      expect(
        () => ProgressTable.decode(schema, Uint8List(0)),
        throwsFormatException,
      );
      expect(
        () => ProgressTable.decode(schema, Uint8List.fromList([...good, 0])),
        throwsFormatException,
      );
      expect(
        () => ProgressTable.decode(schema, Uint8List.fromList(good)..[0] = 2),
        throwsFormatException,
      );
      expect(
        () => ProgressTable.decode(schema, good.sublist(0, good.length - 1)),
        throwsFormatException,
      );
    });
  });
}
