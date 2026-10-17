import 'package:orblit_dialogue/orblit_dialogue.dart';
import 'package:orblit_progress/orblit_progress.dart';

final ProgressSchema schema = ProgressSchema(
  version: 1,
  names: const [
    Whole('flags'),
    Whole('gold'),
    Slice('quest.lamp.stage', of: 'flags', low: 0, bits: 2),
    Slice('met', of: 'flags', low: 2, bits: 1),
  ],
);

ProgressTable at({int stage = 0, int gold = 0}) => ProgressTable(
  schema,
).set('quest.lamp.stage', stage).table.set('gold', gold).table;

Condition stageAtLeast(int n) =>
    Condition('quest.lamp.stage', Compare.greaterOrEqual, n);

Condition goldAtLeast(int n) => Condition('gold', Compare.greaterOrEqual, n);

/// Two variants. A player at stage 1 or more hears "again", others "hello".
/// "hello" leads to a choice, one option of which sets the stage.
Dialogue guide() => Dialogue(
  key: 'guide',
  variants: [
    Variant(when: [stageAtLeast(1)], start: 'again'),
    Variant(start: 'hello'),
  ],
  nodes: [
    LineNode(id: 'hello', speaker: 'Guide', text: 'Hello.', next: 'ask'),
    ChoiceNode(
      id: 'ask',
      options: [
        ChoiceOption(text: 'The lamp?', next: 'lamp'),
        ChoiceOption(text: 'I have gold.', next: 'pay', when: [goldAtLeast(5)]),
        ChoiceOption(text: 'Goodbye.', next: null),
      ],
    ),
    LineNode(id: 'lamp', speaker: 'Guide', text: 'In the cave.', next: 'begin'),
    SetNode(id: 'begin', writes: [Write('quest.lamp.stage', 1)], next: 'told'),
    LineNode(id: 'told', speaker: 'Guide', text: 'Go and find it.'),
    LineNode(id: 'pay', speaker: 'Guide', text: 'Thanks.', before: 'take'),
    LineNode(id: 'again', speaker: 'Guide', text: 'Back so soon?'),
  ],
);

/// Registers the hook [guide] names, and counts its calls.
({Map<String, Before> hooks, List<int> seen}) hooks() {
  final seen = <int>[];
  return (
    seen: seen,
    hooks: {
      'take': (table) {
        seen.add(table.get('gold'));
        return table.set('gold', 0).table;
      },
    },
  );
}

DialogueBook book([List<Dialogue>? dialogues]) => DialogueBook(
  schema: schema,
  hooks: hooks().hooks,
  dialogues: dialogues ?? [guide()],
);
