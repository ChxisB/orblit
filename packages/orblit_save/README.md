# orblit_save

Saves for a game server. Pure Dart, but it uses `dart:io` and
`dart:isolate`, so it is for servers and tools and not the web.

A game turns a player's state into bytes and gives them to a `SaveQueue`
with a `SaveReason`. `save` returns at once. A worker isolate writes through
a `SaveStore`, so the tick never waits on a disk.

```dart
final queue = SaveQueue.spawn(
  FileSaveStore(Directory('saves')),
  clock: DateTime.now,
  onFailure: (key, error) => log('save of $key failed: $error'),
);

queue.save('mira', bytes, const Autosave());

final record = await queue.read('mira'); // newest, even before it is on disk
final stuck = await queue.close(); // keys that never reached the store
```

A read sees the newest save before it reaches the store. A write that fails
is reported to `onFailure` and kept in memory, so reads stay right and
`flush` tries it again. A worker that ends early starts again and writes the
saves that had no answer.

`FileSaveStore` keeps one file for each key, named `<key>.save`. A write goes
to a temporary file and replaces the real one by a rename, so a crash leaves
the old save whole. A file that is damaged, or was written by a later
version, throws `FormatException` and names the key. Keys are letters,
digits and `._-`, 128 at most, and do not start with a dot.

`SaveReason` is stored beside the bytes, so history can tell a routine save
from one made as a player fell off the network. `Custom` names a reason of
the game's own.

`AutoSave` picks the keys to save on a tick. Each key comes due once in
`every` ticks plus up to `jitter`. A tick claims at most `perTick` of the due
keys, the longest overdue first. A key seen for the first time comes due
somewhere in its first `every` ticks, so players who joined together do not
save together. The dice are the caller's.

```dart
final auto = AutoSave(
  perTick: 10,
  every: 600,
  jitter: 60,
  random: Random(),
);

for (final key in auto.claim(livePlayers, tick)) {
  queue.save(key, encode(key), const Autosave());
}
```

`SaveQueue.inline` writes on the calling isolate. Use it in tests, or for a
store that does no heavy work. A `MemorySaveStore` given to `spawn` is
copied into the worker, so the queue and the test would see two stores.
