# Changelog

## 0.2.0

- A port of `orblit_agent` 0.2.0.
- A node is keyed in `Memory` by reference. Dart canonicalises two equal
  `const` nodes into one object, so they share a place there. Two nodes built
  with `new` never do.
- `Tick.Read<T>` gives `default` for a missing key. For a value type that is
  zero or false, where Dart gives null.
- A label writes a whole number as Dart does, `wait 1.0s`, for numbers from
  0.0001 up to below 1e15.
