import 'package:orblit_dialogue/orblit_dialogue.dart';
import 'package:test/test.dart';

import 'support.dart';

String saying(DialogueRunner runner) => (runner.current! as LineNode).text;

void main() {
  group('the variants', () {
    test('give a player at stage 0 the first conversation', () {
      final runner = book().start('guide', at());

      expect(saying(runner), 'Hello.');
    });

    test('give a player at stage 1 the other one', () {
      final runner = book().start('guide', at(stage: 1));

      expect(saying(runner), 'Back so soon?');
    });

    test('start nothing when none holds', () {
      final only = Dialogue(
        key: 'quiet',
        variants: [
          Variant(when: [stageAtLeast(2)], start: 'a'),
        ],
        nodes: [LineNode(id: 'a', speaker: 'X', text: 'Hi')],
      );

      final runner = book([only]).start('quiet', at());

      expect(runner.isOver, isTrue);
      expect(runner.current, isNull);
    });
  });

  group('a line', () {
    test('goes on with next and ends where it has no next', () {
      final runner = book().start('guide', at(stage: 1));

      expect(runner.isOver, isFalse);
      runner.next();

      expect(runner.isOver, isTrue);
    });

    test('is the only node that goes on with next', () {
      final runner = book().start('guide', at())..next();

      expect(runner.current, isA<ChoiceNode>());
      expect(runner.next, throwsStateError);
    });

    test('is the only node a choice is refused at', () {
      final runner = book().start('guide', at());

      expect(() => runner.choose(0), throwsStateError);
    });
  });

  group('a choice', () {
    DialogueRunner atAsk({int gold = 0}) =>
        book().start('guide', at(gold: gold))..next();

    String texts(DialogueRunner r) =>
        [for (final o in r.options) o.text].join('|');

    test('offers the options whose conditions hold, in file order', () {
      expect(texts(atAsk()), 'The lamp?|Goodbye.');
      expect(texts(atAsk(gold: 5)), 'The lamp?|I have gold.|Goodbye.');
    });

    test('is picked by an index into what is offered', () {
      final runner = atAsk()..choose(1);

      expect(runner.isOver, isTrue);
    });

    test('refuses an index that is not offered', () {
      final runner = atAsk();

      expect(() => runner.choose(2), throwsRangeError);
      expect(() => runner.choose(-1), throwsRangeError);
      expect(runner.current, isA<ChoiceNode>());
    });

    test('ends the conversation when nothing is offered', () {
      final stuck = Dialogue(
        key: 'stuck',
        variants: [Variant(start: 'a')],
        nodes: [
          ChoiceNode(
            id: 'a',
            options: [
              ChoiceOption(text: 'No', next: null, when: [stageAtLeast(3)]),
            ],
          ),
        ],
      );

      final runner = book([stuck]).start('stuck', at());

      expect(runner.isOver, isTrue);
      expect(runner.options, isEmpty);
    });

    test('has no options when the node is a line', () {
      expect(book().start('guide', at()).options, isEmpty);
    });

    test('leaves no options behind when it goes on to a line', () {
      final runner = atAsk()..choose(0);

      expect(saying(runner), 'In the cave.');
      expect(runner.options, isEmpty);
    });
  });

  group('a set', () {
    test('writes the table and the next condition sees it', () {
      final runner = book().start('guide', at())
        ..next()
        ..choose(0);
      expect(saying(runner), 'In the cave.');

      runner.next();

      expect(saying(runner), 'Go and find it.');
      expect(runner.table.get('quest.lamp.stage'), 1);
    });

    test('is seen by a choice that comes after it', () {
      final after = Dialogue(
        key: 'after',
        variants: [Variant(start: 'w')],
        nodes: [
          SetNode(id: 'w', writes: [Write('gold', 9)], next: 'c'),
          ChoiceNode(
            id: 'c',
            options: [
              ChoiceOption(text: 'Rich', next: null, when: [goldAtLeast(9)]),
            ],
          ),
        ],
      );

      final runner = book([after]).start('after', at());

      expect(runner.options.single.text, 'Rich');
    });

    test('gives the changes to send, and leaves the table it was given', () {
      final start = at();
      final runner = book().start('guide', start)
        ..next()
        ..choose(0)
        ..next();

      expect(start.get('quest.lamp.stage'), 0);
      expect(runner.table.changesSince(start).isEmpty, isFalse);
      expect(
        start.apply(runner.table.changesSince(start)).get('quest.lamp.stage'),
        1,
      );
    });

    test('writes in order, so the last write to a name wins', () {
      final twice = Dialogue(
        key: 'twice',
        variants: [Variant(start: 'w')],
        nodes: [
          SetNode(
            id: 'w',
            writes: [Write('gold', 1), Write('gold', 2)],
            next: 'l',
          ),
          LineNode(id: 'l', speaker: 'X', text: 'Hi'),
        ],
      );

      expect(book([twice]).start('twice', at()).table.get('gold'), 2);
    });
  });

  group('a jump', () {
    test('moves on without showing anything', () {
      final jumping = Dialogue(
        key: 'jumping',
        variants: [Variant(start: 'j')],
        nodes: [
          JumpNode(id: 'j', next: 'l'),
          LineNode(id: 'l', speaker: 'X', text: 'Landed'),
        ],
      );

      expect(saying(book([jumping]).start('jumping', at())), 'Landed');
    });
  });

  group('before', () {
    test('runs before the node is shown and its table goes on', () {
      final h = hooks();
      final b = DialogueBook(
        schema: schema,
        hooks: h.hooks,
        dialogues: [guide()],
      );
      final runner = b.start('guide', at(gold: 7))
        ..next()
        ..choose(1);
      expect(h.seen, [7]);
      expect(saying(runner), 'Thanks.');
      expect(runner.table.get('gold'), 0);
    });

    test('does not run for a node never reached', () {
      final h = hooks();
      final b = DialogueBook(
        schema: schema,
        hooks: h.hooks,
        dialogues: [guide()],
      );

      b.start('guide', at(gold: 7)).next();

      expect(h.seen, isEmpty);
    });

    test('runs for a set and a jump too', () {
      final calls = <String>[];
      final d = Dialogue(
        key: 'quiet',
        variants: [Variant(start: 'j')],
        nodes: [
          JumpNode(id: 'j', next: 's', before: 'a'),
          SetNode(id: 's', writes: [], next: 'l', before: 'b'),
          LineNode(id: 'l', speaker: 'X', text: 'Hi', before: 'c'),
        ],
      );
      Before note(String name) => (table) {
        calls.add(name);
        return table;
      };
      final b = DialogueBook(
        schema: schema,
        hooks: {'a': note('a'), 'b': note('b'), 'c': note('c')},
        dialogues: [d],
      );

      b.start('quiet', at());

      expect(calls, ['a', 'b', 'c']);
    });
  });
}
