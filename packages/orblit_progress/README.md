# orblit_progress

Named small integers for a game's progress: quest stages, skill levels,
flags, counts. Pure Dart.

A `ProgressSchema` names the numbers. A name is a `Whole`, a full unsigned 32
bit integer, or a `Slice`, some bits of a whole. Wholes are numbered in the
order they are listed. Adding names at the end keeps every older save
loading.

A `ProgressTable` holds the numbers and never changes. `set` returns a new
table and the `Changes` that were made. Changes are slots, not names, and
encode to six bytes each. A game sends them to the one player they belong to
and saves the whole table with `encode`.

```dart
final schema = ProgressSchema(
  version: 1,
  names: const [
    Whole('flags'),
    Whole('gold'),
    Slice('flag.met_guide', of: 'flags', low: 0, bits: 1),
    Slice('quest.cook.stage', of: 'flags', low: 1, bits: 4),
  ],
);

var table = ProgressTable(schema);
final (:table: next, :changes) = table.set('quest.cook.stage', 2);
table = next;

final bytes = changes.encode();
final seen = Changes.tryDecode(schema, bytes); // null for bad bytes
```

A slice write leaves the other bits of its whole as they were. A value that
does not fit its bits throws `RangeError`. `ProgressTable.decode` throws
`FormatException` for bytes that are not a table, or that were written under
a later schema.

Taking a name out or reordering them moves slots. Read the old save under the
old schema and write what you want to keep by name.
