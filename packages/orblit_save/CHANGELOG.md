# Changelog

## 0.2.1

- `SaveQueue.keys` lists every key with a save, including saves not yet
  written.

## 0.2.0

- `HistoryStore` keeps every version a store writes in a `SaveHistory`.
  Bytes are gzipped once for each distinct content and named by their
  SHA-256. `prune` thins old versions by a `Spacing` and deletes copies
  no version names.
- `SaveTools` restores a save to an earlier version, plans and applies a
  restore of every save to one time, and edits a save with a function. It
  refuses a save a player is using. A restore keeps the old save as
  `pre-restore`.
- `dart run orblit_save:history` lists versions and restores them with
  the server stopped.

## 0.1.0

- `SaveStore` is the interface a queue writes through. `MemorySaveStore`
  is for tests. `FileSaveStore` keeps one file a key and writes to a
  temporary file first, so a crash leaves the old save whole.
- `SaveQueue` takes a save and returns at once. A worker isolate writes
  it. A read sees a save before it reaches disk. A worker that ends
  early starts again and writes what had no answer.
- `SaveReason` says why a save was made and is stored with the bytes.
- `AutoSave` picks which keys to save each tick, at most a set number,
  each no sooner than its own interval.
