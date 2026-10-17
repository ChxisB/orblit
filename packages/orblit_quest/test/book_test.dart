import 'dart:typed_data';

import 'package:orblit_progress/orblit_progress.dart';
import 'package:orblit_quest/orblit_quest.dart';
import 'package:orblit_save/orblit_save.dart';
import 'package:test/test.dart';

import 'support.dart';

Moved moved(QuestMove move) => move as Moved;

void main() {
  group('the journal', () {
    test('follows the stage', () {
      final b = book();

      expect(b.journal(at(0), 'lamp'), '');
      expect(b.journal(at(1), 'lamp'), 'Find the lamp.');
      expect(b.journal(at(2), 'lamp'), 'Light the lamp.');
    });

    test('says whether the quest is begun and whether it is complete', () {
      final b = book();

      expect(
        [for (var s = 0; s < 3; s++) b.isStarted(at(s), 'lamp')],
        [false, true, true],
      );
      expect(
        [for (var s = 0; s < 3; s++) b.isComplete(at(s), 'lamp')],
        [false, false, true],
      );
    });

    test('is refused for a quest the book does not have', () {
      expect(() => book().journal(at(0), 'nope'), throwsArgumentError);
      expect(() => book().quest('nope'), throwsArgumentError);
    });
  });

  group('advance', () {
    test('moves forward and says what changed', () {
      final move = moved(book().advance(at(0), 'lamp', 1));

      expect(book().stageOf(move.table, 'lamp'), 1);
      expect(move.completed, isFalse);
      expect(move.back, isFalse);
      expect(move.changes.isEmpty, isFalse);
    });

    test('refuses the stage the player is in and any behind it', () {
      for (final to in [1, 0]) {
        final move = book().advance(at(1), 'lamp', to);

        expect(move, isA<Refused>());
        expect((move as Refused).reason, QuestRefusal.notForward);
      }
    });

    test('goes back only when allowed, and says so', () {
      final move = moved(
        book().advance(at(2, gold: 9), 'lamp', 1, allowBack: true),
      );

      expect(book().stageOf(move.table, 'lamp'), 1);
      expect(move.back, isTrue);
      expect(move.completed, isFalse);
    });

    test('going back does not ask for the requirements of that stage', () {
      final well = Quest(
        key: 'well',
        title: 'The Well',
        stages: [
          Stage(id: 'a', journal: ''),
          Stage(
            id: 'b',
            journal: '',
            requires: [Condition('gold', Compare.greater, 0)],
          ),
          Stage(id: 'c', journal: ''),
        ],
      );
      final b = QuestBook(schema: schema, quests: [well]);
      final start = ProgressTable(schema).set('quest.well.stage', 2).table;

      final move = b.advance(start, 'well', 1, allowBack: true);

      expect(move, isA<Moved>());
    });

    test('allowBack does not let a quest stay or skip a requirement', () {
      expect(book().advance(at(1), 'lamp', 1, allowBack: true), isA<Refused>());
      expect(book().advance(at(0), 'lamp', 2, allowBack: true), isA<Refused>());
    });

    test('is refused until the requirements of the stage hold', () {
      final refused = book().advance(at(1, gold: 4), 'lamp', 2);
      final allowed = book().advance(at(1, gold: 5), 'lamp', 2);

      expect((refused as Refused).reason, QuestRefusal.requirement);
      expect(allowed, isA<Moved>());
    });

    test('may skip a stage', () {
      final move = moved(book().advance(at(0, gold: 5), 'lamp', 2));

      expect(move.completed, isTrue);
      expect(book().stageOf(move.table, 'lamp'), 2);
    });

    test('completes once, on entering the last stage', () {
      final b = book();
      final first = moved(b.advance(at(1, gold: 5), 'lamp', 2));
      final again = b.advance(first.table, 'lamp', 2);

      expect(first.completed, isTrue);
      expect(again, isA<Refused>());
    });

    test('throws for a stage the quest does not have', () {
      expect(() => book().advance(at(0), 'lamp', 3), throwsRangeError);
      expect(() => book().advance(at(0), 'lamp', -1), throwsRangeError);
    });

    test('leaves the table it was given as it was', () {
      final start = at(0);

      book().advance(start, 'lamp', 1);

      expect(book().stageOf(start, 'lamp'), 0);
    });

    test('changes only the stage of its own quest', () {
      final start = ProgressTable(schema).set('quest.well.stage', 5).table;

      final move = moved(book().advance(start, 'lamp', 1));

      expect(move.table.get('quest.well.stage'), 5);
    });
  });

  group('a book', () {
    Quest quest(String key, int stages) => Quest(
      key: key,
      title: key,
      stages: [for (var i = 0; i < stages; i++) Stage(id: 's$i', journal: '')],
    );

    test('is refused for a quest the schema has no name for', () {
      expect(
        () => QuestBook(schema: schema, quests: [quest('ghost', 2)]),
        throwsArgumentError,
      );
    });

    test('is refused when the stage bits are too few', () {
      expect(
        () => QuestBook(schema: schema, quests: [quest('lamp', 5)]),
        throwsArgumentError,
      );
      expect(
        QuestBook(schema: schema, quests: [quest('lamp', 4)]).quests,
        hasLength(1),
      );
    });

    test('is refused when a requirement names an unknown value', () {
      final bad = Quest(
        key: 'lamp',
        title: 'L',
        stages: [
          Stage(id: 'a', journal: ''),
          Stage(
            id: 'b',
            journal: '',
            requires: [Condition('nope', Compare.equal, 1)],
          ),
        ],
      );

      expect(
        () => QuestBook(schema: schema, quests: [bad]),
        throwsArgumentError,
      );
    });

    test('is refused when two quests share a key', () {
      expect(
        () => QuestBook(
          schema: schema,
          quests: [quest('lamp', 2), quest('lamp', 2)],
        ),
        throwsArgumentError,
      );
    });
  });

  group('a stage', () {
    test('survives a save and a load through the store', () async {
      final store = MemorySaveStore();
      final queue = SaveQueue.inline(
        store,
        clock: () => DateTime.utc(2026),
        onFailure: (_, _) {},
      );
      final reached = moved(book().advance(at(0), 'lamp', 1)).table;
      queue.save('player', reached.encode(), const Leave());
      await queue.flush();

      final record = await queue.read('player');
      final loaded = ProgressTable.decode(
        schema,
        Uint8List.fromList(record!.bytes),
      );

      expect(book().stageOf(loaded, 'lamp'), 1);
      expect(book().journal(loaded, 'lamp'), 'Find the lamp.');
    });

    test('reaches the owner as a delta that rebuilds the table', () {
      final move = moved(book().advance(at(0), 'lamp', 1));

      final seen = Changes.tryDecode(schema, move.changes.encode())!;

      expect(book().stageOf(at(0).apply(seen), 'lamp'), 1);
    });
  });
}
