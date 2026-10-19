import 'dart:convert';

import 'package:orblit_progress/orblit_progress.dart';
import 'package:orblit_quest/orblit_quest.dart';
import 'package:test/test.dart';

import 'support.dart';

Map<String, Object?> file() => {
  'kind': 'orblit.quest',
  'formatVersion': 1,
  'key': 'lamp',
  'title': 'The Lamp',
  'stages': [
    {'id': 'unknown', 'journal': ''},
    {'id': 'find', 'journal': 'Find the lamp.'},
  ],
};

void main() {
  group('a quest', () {
    test('round trips through its file', () {
      final again = Quest.parse(lamp().encode());

      expect(again.key, 'lamp');
      expect(again.title, 'The Lamp');
      expect(
        [for (final s in again.stages) s.id],
        ['unknown', 'find', 'light'],
      );
      expect(again.stages[1].journal, 'Find the lamp.');
      final need = again.stages[2].requires.single;
      expect(
        (need.name, need.compare, need.value),
        ('gold', Compare.greaterOrEqual, 5),
      );
      expect(again.encode(), lamp().encode());
    });

    test('is refused when it breaks a rule', () {
      Stage stage(String id) => Stage(id: id, journal: '');

      expect(() => Stage(id: '', journal: ''), throwsArgumentError);
      expect(
        () =>
            Quest(key: 'bad key', title: 'T', stages: [stage('a'), stage('b')]),
        throwsArgumentError,
      );
      expect(
        () => Quest(key: 'a.b', title: 'T', stages: [stage('a'), stage('b')]),
        throwsArgumentError,
      );
      expect(
        () => Quest(key: 'k', title: '', stages: [stage('a'), stage('b')]),
        throwsArgumentError,
      );
      expect(
        () => Quest(key: 'k', title: 'T', stages: [stage('a')]),
        throwsArgumentError,
      );
      expect(
        () => Quest(key: 'k', title: 'T', stages: [stage('a'), stage('a')]),
        throwsArgumentError,
      );
    });

    test('keeps a stage list that the caller cannot change', () {
      final stages = [Stage(id: 'a', journal: ''), Stage(id: 'b', journal: '')];
      final quest = Quest(key: 'k', title: 'T', stages: stages);

      stages.clear();

      expect(quest.stages, hasLength(2));
      expect(() => quest.stages.clear(), throwsUnsupportedError);
    });
  });

  group('a file', () {
    Quest parse(Map<String, Object?> json) => Quest.parse(jsonEncode(json));

    test('is read', () {
      expect(parse(file()).stages, hasLength(2));
    });

    test('is refused when it is not a quest', () {
      expect(
        () => Quest.parse('not json'),
        throwsA(isA<QuestFormatException>()),
      );
      expect(() => Quest.parse('[]'), throwsA(isA<QuestFormatException>()));
      expect(
        () => parse(file()..['kind'] = 'orblit.clip'),
        throwsA(isA<QuestFormatException>()),
      );
    });

    test('is refused from a newer Orblit and without a version', () {
      expect(
        () => parse(file()..['formatVersion'] = 2),
        throwsA(
          isA<QuestFormatException>().having(
            (e) => e.message,
            'message',
            contains('newer'),
          ),
        ),
      );
      expect(
        () => parse(file()..remove('formatVersion')),
        throwsA(isA<QuestFormatException>()),
      );
      expect(
        () => parse(file()..['formatVersion'] = 0),
        throwsA(isA<QuestFormatException>()),
      );
    });

    test('is refused when a part is missing or the wrong kind', () {
      for (final broken in [
        file()..remove('key'),
        file()..['title'] = 3,
        file()..['stages'] = 'none',
        file()..['stages'] = [1],
        file()
          ..['stages'] = [
            {'id': 'a'},
          ],
        file()
          ..['stages'] = [
            {'id': 'a', 'journal': '', 'requires': 'gold'},
            {'id': 'b', 'journal': ''},
          ],
        file()
          ..['stages'] = [
            {'id': 'a', 'journal': ''},
          ],
        file()..['key'] = 'a b',
      ]) {
        expect(
          () => parse(broken),
          throwsA(isA<QuestFormatException>()),
          reason: '$broken',
        );
      }
    });

    test('says what was wrong with a rule it broke', () {
      expect(
        () => parse(file()..['key'] = 'a b'),
        throwsA(
          isA<QuestFormatException>().having(
            (e) => e.message,
            'message',
            contains('a b'),
          ),
        ),
      );
    });

    test('names the stage id it found twice', () {
      final twice = file()
        ..['stages'] = [
          {'id': 'same', 'journal': ''},
          {'id': 'same', 'journal': 'x'},
        ];

      expect(
        () => parse(twice),
        throwsA(
          isA<QuestFormatException>().having(
            (e) => e.message,
            'message',
            contains('same'),
          ),
        ),
      );
    });
  });
}
