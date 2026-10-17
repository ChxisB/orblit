import 'package:orblit_progress/orblit_progress.dart';
import 'package:orblit_quest/orblit_quest.dart';

final ProgressSchema schema = ProgressSchema(
  version: 1,
  names: const [
    Whole('flags'),
    Whole('gold'),
    Slice('quest.lamp.stage', of: 'flags', low: 0, bits: 2),
    Slice('quest.well.stage', of: 'flags', low: 2, bits: 3),
  ],
);

/// Three stages, so the middle one is neither the start nor the end.
Quest lamp() => Quest(
  key: 'lamp',
  title: 'The Lamp',
  stages: [
    Stage(id: 'unknown', journal: ''),
    Stage(id: 'find', journal: 'Find the lamp.'),
    Stage(
      id: 'light',
      journal: 'Light the lamp.',
      requires: [Condition('gold', Compare.greaterOrEqual, 5)],
    ),
  ],
);

QuestBook book() => QuestBook(schema: schema, quests: [lamp()]);

ProgressTable at(int stage, {int gold = 0}) => ProgressTable(
  schema,
).set('quest.lamp.stage', stage).table.set('gold', gold).table;
