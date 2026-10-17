# Changelog

## 0.4.0

- **Infinite Tiled maps are read.** A layer saved as chunks is joined into
  one grid, and chunks at negative cells are fine. `TileLayer.left` and
  `top` say which cell its first tile sits in, and `at` takes cells
  relative to the map as before. Before, a map with `"infinite": true`
  came back as null. Chunks that span more than four million cells still
  come back as null.
- **Flipped tiles are looked up by their number.** A tile number carries
  three flip bits above it. `at` leaves them off, so `setFor` and `rectOf`
  find the right set and tile again, and `flipsAt` returns them as
  `TileFlips`. Before, a flipped tile found no tile at all in its set.
- **A tile larger than the grid knows where it is drawn.** `TileMap.placeOf`
  returns the rectangle in world pixels, anchored to the bottom left of the
  cell. The rectangle a tile is cut from was already right.
- A layer with fewer tiles than its width and height now comes back as null.
  Before, `at` threw at its last cells.

## 0.3.0

- The PNG codec moves out of `bin/src/` and into the package. It was already
  written and tested; it was simply somewhere only `atlas_cook` could reach,
  and the asset pipeline's atlas importer needs the same decode to feed the
  same packer. `decodePng` and `encodePng` are now part of the API.

## 0.2.0

- **An atlas packer.** `packAtlas` packs sprites with MaxRects — trying five
  placement heuristics and keeping the best, or the one asked for — trimming
  transparent borders, padding and extruding edges so filtering never bleeds a
  neighbour, spilling onto further pages, and sharing one rectangle between
  identical sprites. The same sprites pack the same way whatever order they
  arrive in. Nine hundred mixed sprites fill sixteen pages at 92%, where a
  shelf packer needs twenty at 73%. Pass the device's texture size budget as
  `maxPageSize`. Rotation is available and off by default, because nothing
  draws a rotated region yet.
- `packAtlasInBackground` runs it on an isolate; `AtlasSet` looks a region up
  across pages; `writeAtlas` writes the descriptor `Atlas.read` reads, so a
  pack round-trips exactly.
- `dart run orblit_sprite:atlas_cook <folder>` packs a folder of PNGs. Its PNG
  codec lives with the command, so the library gains no dependency.

## 0.1.0

- First cut of `orblit_sprite`. Pre-alpha: everything is subject to change.
