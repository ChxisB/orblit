# Changelog

## 0.1.0

Port of `orblit_core` 0.1.0 (the Dart package). `World`, `Query` and `Chunk` keep
the Dart shape. A `Chunk` is a `ref struct` and its columns are `Span<T>`, so a
view cannot be stored past the call that made it.
