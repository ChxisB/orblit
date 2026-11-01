# Changelog

## 0.3.0

- A port of `orblit_path` 0.3.0. It holds `Spline`, `PathKnot`, `PathFrame`,
  `PathNearest`, `NavGrid`, `NavObstacle`, `NavMesh`, `NavLink`,
  `LinkPlanner`, `PathFollower`, `Rail`, `Patrol`, `NavWalker` and the bake
  functions on `NavBake`.
- The bake functions are static methods of `NavBake`: `FromTerrain`,
  `FromMesh`, `Carve` and `Marked`. C# has no free functions.
- `Planner` and `Clear` are delegates, so `grid.FindPath` and `grid.IsClear`
  fit as method groups.
- `MeshWalk` is a `readonly record struct`. Indices are `long`, and a vertex off the end of `Positions` throws.
- A grid is `int` wide and deep, and one that holds more cells than an array
  can is refused by the constructor. Cell numbers and index parameters stay `long`.
- A route step is a class, so two steps are equal only when they are the
  same step. The path types are classes.
- A `NavLink` copies its set of agent kinds. A later change to the original does not reach it.
- The walker's `lookahead` and the follower indices are `long`. A `Lookahead`
  that overflows when added to the index ends the look.
- A `PathEnd` that is not one of the three is refused with an
  `InvalidOperationException`.
