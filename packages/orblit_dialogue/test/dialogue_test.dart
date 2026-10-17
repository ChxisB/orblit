import 'dart:convert';

import 'package:orblit_dialogue/orblit_dialogue.dart';
import 'package:orblit_progress/orblit_progress.dart';
import 'package:test/test.dart';

import 'support.dart';

Map<String, Object?> file() => {
  'kind': 'orblit.dialogue',
  'formatVersion': 1,
  'key': 'guide',
  'variants': [
    {'start': 'a'},
  ],
  'nodes': {
    'a': {'type': 'line', 'speaker': 'Guide', 'text': 'Hi'},
  },
};

Dialogue parse(Map<String, Object?> json) => Dialogue.parse(jsonEncode(json));

Matcher failsToLoad([String? containing]) => throwsA(
  isA<DialogueFormatException>().having(
    (e) => e.message,
    'message',
    containing == null ? anything : contains(containing),
  ),
);

void main() {
  group('a dialogue', () {
    test('round trips through its file', () {
      final again = Dialogue.parse(guide().encode());

      expect(again.encode(), guide().encode());
      expect(again.variants, hasLength(2));
      expect(again.variants.first.when.single.value, 1);
      expect(again.nodes.keys, guide().nodes.keys);
      expect((again.nodes['pay']! as LineNode).before, 'take');
      expect((again.nodes['ask']! as ChoiceNode).options[1].when, hasLength(1));
      expect(
        (again.nodes['begin']! as SetNode).writes.single.name,
        'quest.lamp.stage',
      );
    });

    test('names the functions its nodes ask for', () {
      expect(guide().beforeNames, {'take'});
    });

    test('is refused when it breaks a rule', () {
      final a = LineNode(id: 'a', speaker: 'X', text: 'Hi');
      final nodes = [a];
      final variants = [Variant(start: 'a')];

      expect(
        () => Dialogue(key: '', variants: variants, nodes: nodes),
        throwsArgumentError,
      );
      expect(
        () => Dialogue(key: 'k', variants: [], nodes: nodes),
        throwsArgumentError,
      );
      expect(
        () => Dialogue(key: 'k', variants: variants, nodes: [a, a]),
        throwsArgumentError,
      );
      expect(
        () => Dialogue(
          key: 'k',
          variants: [Variant(start: 'zz')],
          nodes: nodes,
        ),
        throwsArgumentError,
      );
      expect(
        () => Dialogue(
          key: 'k',
          variants: variants,
          nodes: [LineNode(id: 'a', speaker: 'X', text: 'Hi', next: 'zz')],
        ),
        throwsArgumentError,
      );
      expect(
        () => Dialogue(
          key: 'k',
          variants: variants,
          nodes: [
            a,
            ChoiceNode(
              id: 'c',
              options: [ChoiceOption(text: 'x', next: 'zz')],
            ),
          ],
        ),
        throwsArgumentError,
      );
    });

    test('is refused when nodes go round with nothing to show', () {
      expect(
        () => Dialogue(
          key: 'k',
          variants: [Variant(start: 'a')],
          nodes: [
            JumpNode(id: 'a', next: 'b'),
            SetNode(id: 'b', writes: [], next: 'a'),
          ],
        ),
        throwsArgumentError,
      );
      expect(
        () => Dialogue(
          key: 'k',
          variants: [Variant(start: 'a')],
          nodes: [JumpNode(id: 'a', next: 'a')],
        ),
        throwsArgumentError,
      );
    });

    test('may go round when a line is on the way', () {
      final loop = Dialogue(
        key: 'k',
        variants: [Variant(start: 'a')],
        nodes: [
          LineNode(id: 'a', speaker: 'X', text: 'Hi', next: 'b'),
          JumpNode(id: 'b', next: 'a'),
        ],
      );

      expect(loop.nodes, hasLength(2));
    });

    test('is refused for an empty node, option or write', () {
      expect(
        () => LineNode(id: '', speaker: 'X', text: 'Hi'),
        throwsArgumentError,
      );
      expect(
        () => LineNode(id: 'a', speaker: 'X', text: 'Hi', before: ''),
        throwsArgumentError,
      );
      expect(() => ChoiceNode(id: 'a', options: []), throwsArgumentError);
      expect(() => Write('', 1), throwsArgumentError);
      expect(() => Write('gold', -1), throwsArgumentError);
      expect(() => Write('gold', 0x100000000), throwsArgumentError);
    });
  });

  group('a file', () {
    test('is read', () {
      expect(parse(file()).nodes, hasLength(1));
    });

    test('is refused when it is not a dialogue', () {
      expect(() => Dialogue.parse('nope'), failsToLoad());
      expect(() => Dialogue.parse('[]'), failsToLoad());
      expect(() => parse(file()..['kind'] = 'orblit.quest'), failsToLoad());
    });

    test('is refused from a newer Orblit and without a version', () {
      expect(() => parse(file()..['formatVersion'] = 2), failsToLoad('newer'));
      expect(() => parse(file()..remove('formatVersion')), failsToLoad());
      expect(() => parse(file()..['formatVersion'] = 0), failsToLoad());
    });

    test('is refused when a part is missing or the wrong kind', () {
      final bad = <Map<String, Object?>>[
        file()..remove('key'),
        file()..['variants'] = 'x',
        file()..['variants'] = [1],
        file()
          ..['variants'] = [
            {'when': <Object?>[]},
          ],
        file()..['nodes'] = [1],
        file()..['nodes'] = {'a': 1},
        file()
          ..['nodes'] = {
            'a': {'type': 'wave'},
          },
        file()
          ..['nodes'] = {
            'a': {'type': 'line', 'speaker': 'X'},
          },
        file()
          ..['nodes'] = {
            'a': {'type': 'line', 'speaker': 'X', 'text': 1},
          },
        file()
          ..['nodes'] = {
            'a': {'type': 'line', 'speaker': 'X', 'text': 'Hi', 'next': 4},
          },
        file()
          ..['nodes'] = {
            'a': {'type': 'choice', 'options': 'x'},
          },
        file()
          ..['nodes'] = {
            'a': {
              'type': 'choice',
              'options': [1],
            },
          },
        file()
          ..['nodes'] = {
            'a': {'type': 'choice', 'options': <Object?>[]},
          },
        file()
          ..['nodes'] = {
            'a': {
              'type': 'set',
              'writes': [
                {'name': 'g'},
              ],
            },
          },
        file()
          ..['nodes'] = {
            'a': {
              'type': 'set',
              'writes': [1],
            },
          },
        file()
          ..['nodes'] = {
            'a': {
              'type': 'set',
              'writes': [
                {'name': 'g', 'value': -1},
              ],
            },
          },
        file()
          ..['nodes'] = {
            'a': {'type': 'jump'},
          },
        file()
          ..['variants'] = [
            {'start': 'a', 'when': 'x'},
          ],
        file()
          ..['variants'] = [
            {'start': 'zz'},
          ],
      ];
      for (final json in bad) {
        expect(() => parse(json), failsToLoad(), reason: '$json');
      }
    });

    test('says what was wrong with a rule it broke', () {
      expect(
        () => parse(
          file()
            ..['variants'] = [
              {'start': 'zz'},
            ],
        ),
        failsToLoad('zz'),
      );
    });
  });

  group('a book', () {
    Dialogue one(Node node, {List<Condition> when = const []}) => Dialogue(
      key: 'one',
      variants: [Variant(start: node.id, when: when)],
      nodes: [node],
    );

    DialogueBook make(Dialogue d, [Map<String, Before>? hooks]) =>
        DialogueBook(schema: schema, hooks: hooks ?? {}, dialogues: [d]);

    test('is refused for a before nobody registered', () {
      final d = one(
        LineNode(id: 'a', speaker: 'X', text: 'Hi', before: 'ghost'),
      );

      expect(() => make(d), throwsArgumentError);
      expect(() => make(d, {'take': (t) => t}), throwsArgumentError);
      expect(make(d, {'ghost': (t) => t}).dialogues, hasLength(1));
    });

    test('is refused for a name a condition uses that the schema lacks', () {
      final variant = one(
        LineNode(id: 'a', speaker: 'X', text: 'Hi'),
        when: [Condition('ghost', Compare.equal, 1)],
      );
      final option = one(
        ChoiceNode(
          id: 'a',
          options: [
            ChoiceOption(
              text: 'x',
              next: null,
              when: [Condition('ghost', Compare.equal, 1)],
            ),
          ],
        ),
      );

      expect(() => make(variant), throwsArgumentError);
      expect(() => make(option), throwsArgumentError);
    });

    test('is refused for a write to a name the schema lacks', () {
      final d = one(SetNode(id: 'a', writes: [Write('ghost', 1)]));

      expect(() => make(d), throwsArgumentError);
    });

    test('is refused for a value that does not fit its name', () {
      final tooBig = one(SetNode(id: 'a', writes: [Write('met', 2)]));
      final fits = one(SetNode(id: 'a', writes: [Write('met', 1)]));
      final whole = one(SetNode(id: 'a', writes: [Write('gold', 0xFFFFFFFF)]));

      expect(() => make(tooBig), throwsArgumentError);
      expect(make(fits).dialogues, hasLength(1));
      expect(make(whole).dialogues, hasLength(1));
    });

    test('is refused when two dialogues share a key', () {
      expect(
        () => DialogueBook(
          schema: schema,
          hooks: hooks().hooks,
          dialogues: [guide(), guide()],
        ),
        throwsArgumentError,
      );
    });

    test('is refused for a dialogue it does not have', () {
      expect(() => book().start('ghost', at()), throwsArgumentError);
      expect(() => book().dialogue('ghost'), throwsArgumentError);
    });
  });
}
