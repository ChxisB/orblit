# Changelog

## 0.2.1

- A port of `orblit_sequence` 0.2.1.
- `Sequence` is `CutSequence`, because a type named like its namespace is
  hard to use.
- A key names a slope as a `Slope<T>`, so a slope of zero is not the same as no
  slope. A whole number converts to one where `T` is `double`.
- `PropertyTrack<T>.TryValueAt` answers with a bool and an out value, because a `double` has no null.
- `Mixer<T>` is `IMixer<T>`, and the four const mixers are `Mixers.OfDouble`,
  `OfVector3`, `OfQuaternion` and `OfBool`.
- A channel with no keys, and a clip with no length or no speed, are refused
  always.
