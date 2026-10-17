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
        {"text": "The lamp?", "next": "begin"},
        {"text": "Goodbye."}
      ]
    },
    "begin": {
      "type": "set",
      "writes": [{"name": "quest.lamp.stage", "value": 1}],
      "next": "told"
    },
    "told": {"type": "line", "speaker": "Guide", "text": "Go and find it."},
    "again": {"type": "line", "speaker": "Guide", "text": "Back so soon?"}
  }
}
```

```dart
final book = DialogueBook(
  schema: schema,
  hooks: {'give-lamp': (table) => table},
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
condition or a write uses must be in the schema, a written value must fit its
name's bits, and every `before` must be registered. A typo fails there, and
not when a player talks.

The runner holds no screen. It starts when made. When no variant holds,
`isOver` is true at once. `next` throws `StateError` anywhere but a line and
`choose` throws it anywhere but a choice. `choose` takes an index into
`options`, which is the options that hold, so a client sends that index and
the server refuses one it does not have. A choice with nothing to offer ends
the conversation.

A ring of `set` and `jump` nodes with no line in it would never stop, so the
file is refused. `Dialogue.parse` throws `DialogueFormatException` for text
that is not a dialogue, for one from a newer Orblit and for one that breaks a
rule.
