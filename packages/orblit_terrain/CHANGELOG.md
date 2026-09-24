# Changelog

## 0.2.0

- **Brushes.** A `TerrainStroke` is one press of a brush: `moveTo` carries it
  along, laying a dab every `Brush.spacing`, and writes straight into the
  regions' maps and touches them, so a renderer resends only the regions the
  brush is in. `BrushTool` raises, lowers, smooths, flattens, slopes, paints
  cover, colour and roughness, and punches holes; `invert` runs each
  backwards. One `Brush` for every tool: size, strength, falloff, jitter and
  spacing. Strength is per pass, not per dab, so closer spacing makes a
  smoother stroke rather than a stronger one. Ground with no region under it
  is left alone.
- **Undo.** Each `moveTo` hands back a `TerrainPatch`: the tiles it touched,
  32 texels across, before and after, for the one map its tool writes.
  `followedBy` folds a stroke's patches into one, and `apply` and `revert`
  write it back. A small brush costs kilobytes, not a copy of the region.
  `TerrainRecorder` is how any other edit makes one.
- **Picking.** `Terrain.raycast` finds where a ray first meets the ground
  (through holes and off the edge, it meets nothing), for putting a brush
  under the pointer.

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
