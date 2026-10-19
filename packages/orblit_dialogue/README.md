# orblit_dialogue

Conversations as data, over a progress table. Pure Dart.

A dialogue is a set of nodes and a list of variants. A variant names the node
a conversation starts at and the conditions it needs. The first variant that
holds starts, so one character says something new as a quest moves on. The
conditions read an `orblit_progress` table, the same one `orblit_quest` keeps
its stages in.

A node is one of four:

- `line`: a speaker says text, then `next`. No `next` ends the conversation.
- `choice`: options, each with `text`, `next` and optional `when`. An option
  is offered only while its conditions hold. `next` null ends it.
- `set`: `writes` of a name and a whole number, then `next`.
- `jump`: go to `next`.

Any node may name a `before` function. The game registers it, and the runner
calls it with the table before it shows or runs the node. It returns the
table to go on with, so it can move a quest or hand over an item.

A `.odialogue` file:

```json
{
  "kind": "orblit.dialogue",
  "formatVersion": 1,
  "key": "guide",
  "variants": [
    {"start": "again", "when": [{"name": "quest.lamp.stage", "op": ">=", "value": 1}]},
    {"start": "hello"}
  ],
  "nodes": {
    "hello": {"type": "line", "speaker": "Guide", "text": "Hello.", "next": "ask"},
    "ask": {
      "type": "choice",
      "options": [
        {"text": "The lamp?", "next": "told"},
        {"text": "Goodbye."}
      ]
    },
    "told": {
      "type": "line",
      "speaker": "Guide",
      "text": "Go and find it.",
      "before": "begin-lamp"
    },
    "again": {"type": "line", "speaker": "Guide", "text": "Back so soon?"}
  }
}
```

```dart
final book = DialogueBook(
  schema: schema,
  hooks: {
    'begin-lamp': (table) => switch (quests.advance(table, 'lamp', 1)) {
      Moved(:final table) => table,
      Refused() => table,
    },
  },
  dialogues: [Dialogue.parse(text)],
);

final runner = book.start('guide', table);
while (!runner.isOver) {
  draw(runner.current, runner.options);
  switch (runner.current) {
    case LineNode():
      runner.next();
    case ChoiceNode():
      runner.choose(0);
    default:
  }
}
send(runner.table.changesSince(table));
```

`DialogueBook` checks every dialogue when the game starts. Every name a
condition or a write uses must be in the schema, a value must fit its name's
bits, and every `before` must be registered. A typo fails there, and not when
a player talks.

Move a quest from a `before` hook that calls `QuestBook.advance`, as above,
and not with a `set` node on `quest.<key>.stage`. A `set` skips the quest's
forward-only rule and its requirements, and never says the quest completed.
A hook is game code, so it can give the reward when `Moved.completed` is true.
Use `set` for flags and counters.

`dialogue.startFor(table)` names the node a conversation would start at, or
null, and runs nothing. Use it to show that a character has something to say.

The runner holds no screen. It starts when made. When no variant holds,
`isOver` is true at once. A hook that throws leaves the runner where it was.
It works on the table it was given, so a game that lets other things change
the player's table during a conversation ends the conversation when they do. `next` throws `StateError` anywhere but a line and
`choose` throws it anywhere but a choice. `choose` takes an index into
`options`, which is the options that hold, so a client sends that index and
the server refuses one it does not have. A choice with nothing to offer ends
the conversation.

A ring of `set` and `jump` nodes with no line in it would never stop, so the
file is refused. So is a variant with no conditions that is followed by
another, since the second could never start. `Dialogue.parse` throws `DialogueFormatException` for text
that is not a dialogue, for one from a newer Orblit and for one that breaks a
rule.
