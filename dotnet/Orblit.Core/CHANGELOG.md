# Changelog

## 0.1.0

The native library is packed at `runtimes/<rid>/native`, where NuGet looks for it.

Port of `orblit_core` 0.1.0. A `Chunk` is a `ref struct` and its columns are `Span<T>`, so a
view cannot be stored past the call that made it.
