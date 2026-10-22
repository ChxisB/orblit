# Changelog

## 0.5.0

- A port of `orblit_terrain` 0.5.0.
- A texel's place in the whole terrain and a region's key are `long`. A texel
  inside one region and a region's size are `int`.
- `Cover` and `GroundColour` hold a `uint`, since both are thirty-two bits.
- `TerrainStroke` draws its jitter from `System.Random`. A seed gives a
  repeatable stroke.
- A region header that starts with a byte order mark is read.
- `TerrainStreamer` follows a position on a `SynchronizationContext`. Its methods end in `Async`.
