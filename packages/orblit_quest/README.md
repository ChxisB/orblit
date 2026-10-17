# orblit_quest

Quests as data, kept in a progress table. Pure Dart.

A quest is a list of stages in order. The first is "not begun" and the last
is "complete". A player's place is one integer, the index of a stage, kept
under `quest.<key>.stage` in an `orblit_progress` table. Saving it and
sending it to the owning player are the table's job.

A `.oquest` file:

```json
{
  "kind": "orblit.quest",
  "formatVersion": 1,
  "key": "lamp",
  "title": "The Lamp",
  "stages": [
    {"id": "unknown", "journal": ""},
    {"id": "find", "journal": "Find the lamp."},
    {
      "id": "light",
      "journal": "Light the lamp.",
      "requires": [{"name": "gold", "op": ">=", "value": 5}]
    }
  ]
}
```

```dart
final schema = ProgressSchema(
  version: 1,
  names: const [
    Whole('flags'),
    Whole('gold'),
    Slice('quest.lamp.stage', of: 'flags', low: 0, bits: 2),
  ],
);
final book = QuestBook(schema: schema, quests: [Quest.parse(text)]);

var table = ProgressTable(schema);
switch (book.advance(table, 'lamp', 1)) {
  case Moved(table: final next, :final changes, :final completed):
    table = next; // send `changes` to the player, give the reward if `completed`
  case Refused(:final reason):
    // QuestRefusal.notForward or QuestRefusal.requirement
}
book.journal(table, 'lamp'); // 'Find the lamp.'
```

`QuestBook` checks every quest against the schema when the game starts: the
stage name must exist with bits for every stage, and every requirement must
name a value the schema has.

A quest goes forward. The stage asked for must be ahead of the player and
its requirements must hold. A stage may be skipped to. `allowBack: true`
sends a player to an earlier stage and ignores that stage's requirements.
The result has `back` set, so the game can log why. `completed` is true
when a quest enters its last stage from an earlier one. A quest sent back
and forward again completes again.

A quest that branches uses more than one name in the table. `Quest.parse`
throws `QuestFormatException` for text that is not a quest, for one from a
newer Orblit and for one that breaks a rule.
