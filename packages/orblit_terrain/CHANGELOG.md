# Changelog

## 0.1.0

- First cut of `orblit_terrain`. Pre-alpha: everything is subject to change.
- **Regions.** A `Terrain` is a grid of heights kept in square
  `TerrainRegion`s, a power of two texels across (256 unless the project says
  otherwise), made only where there is ground. Each has three maps: heights in
  metres, a `Cover` word per texel, and a `GroundColour` per texel.
- **Cover.** Thirty-two bits a texel: a base set and an overlay set out of 32,
  how much of the overlay shows, a turn and a scale for the textures, and
  flags for a hole, for navigation and for automatic cover, which chooses the
  sets by slope and height through `AutoCover`.
- **Files.** The settings are a `.oterrain` file, one key to a line. Each
  region is an `.oregion` file beside it: a line of JSON saying what follows,
  then the maps that are not at their defaults, little-endian. Both have a
  marker, a format version and a migration list. Reading and writing them is
  bytes and text only, so the package runs without a file system.
- **Height queries.** `heightAt` interpolates across the same two triangles a
  square of texels is drawn as, and is null over holes and where there is no
  region; `normalAt` blends the slope at the four nearest texels, as the
  renderer lights it. Neither needs physics.
