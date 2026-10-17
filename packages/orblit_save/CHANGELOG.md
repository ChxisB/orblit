# Changelog

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
