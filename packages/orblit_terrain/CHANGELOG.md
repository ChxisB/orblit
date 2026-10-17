# Changelog

## 0.5.0

- **`DistancePolicy`.** Steps a view distance down when frames cost too much
  and back up when there is room. `update(cost, seconds)` takes each frame,
  and once every `settle` seconds it moves `steps` one way on the mean cost
  since the last decision: down above `high`, up below `low`, held between.
  `steps` is a ring count for the clipmap, and `fraction` is the share of a
  scatter layer's range to keep. It reads no clock, so a test can script
  the time. Nothing uses one unless a game makes it.

## 0.4.0

- **`TerrainStreamer`.** Keeps the regions of a `Terrain` that are within
  `loadRadius` metres of a point and lets go of those past `keepRadius`, so a
  world of more than the renderer's 256 regions is still one terrain.
  `follow(x, z)` moves the focus, lets go, then reads what is wanted, nearest
  first, `concurrency` at a time. Called again while it runs, it moves the
  focus and returns the same run, so a frame loop may call it every frame.
- **`RegionSource`.** What a streamer reads from: `keys` and `read(key)`.
  Declared beside the streamer that calls it, so a folder, a pack or a server
  implements it without this package knowing which.
- **Whose regions they are.** A region put in before the streamer, or edited
  after it loaded, is the caller's. It is never removed and it counts against
  `maxResident`. A region that cannot be read, or is not the one its file
  name says, or is another size, is named in `failures` and is not asked for
  again until `retryFailed()`.

## 0.3.0

- **Scatter.** A `ScatterLayer` is a rule for one kind of thing over the
  ground — grass, stones, trees — kept in `Terrain.scatter` and written to the
  `.oterrain` file a layer to a line. It says how many per square metre, which
  sets it grows on, the slopes and heights it keeps to, its size and how much
  that varies, how far it leans with the ground, how far it is sunk, its
  colour, and whether it is a block or a model.
- **Placing.** A `ScatterPlacer` works out where each one stands, region by
  region, as `ScatterGroup`s: a transform and a colour for each, in the flat
  buffers a renderer draws many copies from. The pattern is laid over the
  world, not over each region, from an integer hash of the layer's seed and
  the square, so the same ground scatters the same on every machine, the web
  included, whatever size its regions are. Nothing stands over a hole or off
  the edge, and a layer grows as thick as its sets cover the ground, so
  painting scree over grass takes the grass away.
- **Following edits.** `update` places a region again when its revision
  moves, or when a neighbour changes along their shared edge, and a layer
  again everywhere when its rules change; otherwise it does nothing. A group
  placed again has a new revision and keeps its id, so a renderer resends
  that one group.
- A terrain with nothing scattered writes its file exactly as before.

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
