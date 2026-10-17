import 'package:orblit_progress/orblit_progress.dart';
import 'package:test/test.dart';

final ProgressSchema schema = ProgressSchema(
  version: 1,
  names: const [
    Whole('flags'),
    Whole('gold'),
    Slice('quest.cook.stage', of: 'flags', low: 0, bits: 4),
  ],
);

ProgressTable tableWith(int stage, int gold) => ProgressTable(
  schema,
).set('quest.cook.stage', stage).table.set('gold', gold).table;

void main() {
  group('a comparison', () {
    test('holds for each operator at, below and above the number', () {
      final cases = <(Compare, bool, bool, bool)>[
        (Compare.equal, false, true, false),
        (Compare.notEqual, true, false, true),
        (Compare.less, true, false, false),
        (Compare.lessOrEqual, true, true, false),
        (Compare.greater, false, false, true),
        (Compare.greaterOrEqual, false, true, true),
      ];
      for (final (compare, below, at, above) in cases) {
        expect(compare.holds(4, 5), below, reason: '$compare below');
        expect(compare.holds(5, 5), at, reason: '$compare at');
        expect(compare.holds(6, 5), above, reason: '$compare above');
      }
    });

    test('is found by its symbol and by no other text', () {
      for (final compare in Compare.values) {
        expect(Compare.bySymbol(compare.symbol), compare);
      }
      expect(Compare.bySymbol('=>'), isNull);
      expect(Compare.bySymbol(''), isNull);
    });
  });

  group('a condition', () {
    test('reads the named value from the table', () {
      final condition = Condition(
        'quest.cook.stage',
        Compare.greaterOrEqual,
        2,
      );

      expect(condition.holds(tableWith(1, 0)), isFalse);
      expect(condition.holds(tableWith(2, 0)), isTrue);
      expect(condition.holds(tableWith(3, 0)), isTrue);
    });

    test('is refused for an empty name or a value that cannot be stored', () {
      expect(() => Condition('', Compare.equal, 1), throwsArgumentError);
      expect(() => Condition('gold', Compare.equal, -1), throwsArgumentError);
      expect(
        () => Condition('gold', Compare.equal, 0x100000000),
        throwsArgumentError,
      );
      expect(Condition('gold', Compare.equal, 0xFFFFFFFF).value, 0xFFFFFFFF);
    });

    test('throws for a name the schema does not have', () {
      final condition = Condition('nope', Compare.equal, 0);

      expect(() => condition.holds(tableWith(0, 0)), throwsArgumentError);
    });

    test('round trips through JSON', () {
      final condition = Condition('gold', Compare.notEqual, 7);

      final again = Condition.fromJson(condition.toJson());

      expect(again.name, 'gold');
      expect(again.compare, Compare.notEqual);
      expect(again.value, 7);
    });

    test('is refused from JSON that is not one', () {
      final bad = <Object?>[
        null,
        'gold',
        <String, Object?>{},
        {'name': 'gold', 'op': '==', 'value': '1'},
        {'name': 'gold', 'op': '=>', 'value': 1},
        {'name': '', 'op': '==', 'value': 1},
        {'name': 'gold', 'op': '==', 'value': -1},
        {'name': 'gold', 'op': '==', 'value': 0x100000000},
        {'name': 'gold', 'op': '==', 'value': 1.5},
        {'name': 1, 'op': '==', 'value': 1},
      ];
      for (final json in bad) {
        expect(
          () => Condition.fromJson(json),
          throwsFormatException,
          reason: '$json',
        );
      }
    });
  });

  group('a list', () {
    test('holds when every condition does, and when it is empty', () {
      final both = [
        Condition('quest.cook.stage', Compare.greaterOrEqual, 1),
        Condition('gold', Compare.greater, 10),
      ];

      expect(Condition.allHold(both, tableWith(1, 11)), isTrue);
      expect(Condition.allHold(both, tableWith(0, 11)), isFalse);
      expect(Condition.allHold(both, tableWith(1, 10)), isFalse);
      expect(Condition.allHold(const [], tableWith(0, 0)), isTrue);
    });

    test('is read from JSON, and null is empty', () {
      final list = Condition.listFromJson([
        {'name': 'gold', 'op': '>', 'value': 1},
      ]);

      expect(list, hasLength(1));
      expect(Condition.listFromJson(null), isEmpty);
      expect(() => Condition.listFromJson({'a': 1}), throwsFormatException);
      expect(() => Condition.listFromJson([1]), throwsFormatException);
    });
  });
}
