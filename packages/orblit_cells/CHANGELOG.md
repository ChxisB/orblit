# Changelog

## 0.1.0

- First cut of `orblit_cells`. Pre-alpha: everything is subject to change.
- `Grid` and `Cell`: square cells on the ground plane.
- `CellWindow`: which cells to load, nearest and ahead first, and which to
  drop, with a margin so a cell on the edge does not flicker. The cell the
  point is in always comes first.
- `Grid.cellOf` returns null for a point beyond `Grid.reach`, a billion
  cells, as it does for one that is not finite.
