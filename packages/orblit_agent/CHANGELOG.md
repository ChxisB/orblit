# Changelog

## 0.2.0

- `Avoid(preferred, neighbours)`: steps round neighbours it would hit within
  a horizon. It picks the nearest velocity to the one the route wants that
  hits nothing, or hits the latest. Two agents meeting head on pass on
  opposite sides. The same crowd in the same order gives the same run.
- `Steerable.priority`: `Avoid` ignores a neighbour of lower priority.

## 0.1.0

- First cut of `orblit_agent`. Pre-alpha: everything is subject to change.
