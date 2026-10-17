# Changelog

## 0.3.0

- `NavGrid.addObstacleRect` and `addObstacleCircle` shut cells until the
  `NavObstacle` they return is lifted. Cells are counted, so lifting one never
  opens a wall or a cell another obstacle shuts.
- `NavWalker` takes `isClear` and `lookahead`. With `isClear` it plans again
  from where it stands when a leg ahead is shut, and stands, keeping its goal,
  when no way is left.
- `NavGrid.inflated` treats a cell a standing obstacle shuts as closed.

## 0.2.0

- **`NavLink` and `LinkPlanner`.** A route can cross a gap, climb a ladder,
  use a door or step through a teleporter. A link joins two points and has a
  `kind` name, a `cost`, the `agents` that may use it, an optional `gate` and
  whether it is `twoWay`. `LinkPlanner` takes any `Planner` for the ground, so
  a grid or a navmesh, and `findRoute` answers the `WalkStep`s and `LinkStep`s
  of the cheapest route, or null. `lock` and `unlock` shut and open a gate;
  ask again to plan round the change. Nothing moves an agent. Links are held
  in memory and have no file.

## 0.1.0

- First cut of `orblit_path`. Pre-alpha: everything is subject to change.
- `Spline`: cubic Bezier curves, and a smooth curve through points, with a
  tilt at each knot. Walked by distance, with a frame at every point, a
  nearest-point query and an `.opath` file.
- `PathFollower`, `Rail` and `Patrol`.
- `NavGrid`: A* over weighted cells, pulled tight over cells of one weight.
- `bakeTerrain`: a grid from a `Terrain`, open where the cover's navigation
  bit is set, the slope is walkable and the ground is not a hole.
- `bakeMesh` and `carve`: a grid from triangles, and cells closed where a
  wall or a ceiling stands over them.
- `NavMesh`: rectangles merged from a grid, A* over their shared edges and
  a funnel through them.
- `NavWalker`: an agent that follows a planned route with `orblit_agent`
  steering.
