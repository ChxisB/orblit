# Changelog

## 0.2.1

- A port of `orblit_save` 0.2.1.
- A worker isolate is a worker task. It reads a channel and writes the
  store, so the store is shared with the caller and not copied. A
  `MemorySaveStore` given to `SaveQueue.Spawn` is the same store the caller
  reads, and it takes a lock for that.
- `SaveQueue.KillWorker` ends the worker between two writes. A write that has
  started finishes and is answered. Dart ends the isolate at once.
- `onFailure` runs on the worker task, inside the queue's lock, and gets the
  exception, where Dart gets its text.
- Dart `Autosave` the reason and `AutoSave` the picker differ only in case,
  which CA1708 refuses. The picker is `AutoSaver`.
- Times are `DateTimeOffset`, so a save time must be in the years 1 to 9999.
  `DateTime` in Dart reaches further.
- `dart run orblit_save:history` is the `orblit-save-history` tool, and its
  usage text names it. A time that is not ISO 8601 is a `FormatException`.
  The strings it accepts are .NET's, which are more than Dart's.
- `Not a save reason` carries the label after a colon, where Dart keeps it in
  `FormatException.source`.
- A string with a lone surrogate reads back as it was written, as in Dart.
