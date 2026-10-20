# Changelog

## 0.4.0

- A port of `orblit_sprite` 0.4.0.
- Sizes, ids and offsets are `long`, since a tile map file can hold a number
  past 32 bits. An index into a list is `int`.
- `Atlas.Sequence` sorts names with a stable sort. Dart's sort is stable up to
  33 names and not above that, so names that read as the same number, such as
  `k5` and `k0005`, can come out in another order for a sequence of 34 or more.
- `Tileset.RectOf` never gives a negative column. Dart's `%` is never
  negative, and the difference of two far apart ids wraps.
- `AtlasSprite` refuses a negative side. Dart checks only the byte count, and
  two negative sides give a count that passes.
- `AtlasPackOptions` refuses a size that cannot hold a page every time. Dart
  asserts it in debug builds only.
- An undefined `MaxRectsHeuristic` throws `InvalidOperationException`.
- `PngCodec.Decode` refuses a size too large to hold with `PngFormatException`.
- Reads and writes JSON through `Orblit.Json`, so a number, a repeated key and
  an escape come out as they do in Dart.
