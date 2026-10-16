# Changelog

## 0.2.0

- `ArmBody` holds another body's camera on the near side of a wall. It
  casts a ball from a point above the target along the arm to the camera
  and stops the camera where the ball stops. Shortening is instant and
  lengthening is the inner body's own catch-up.
- `ArmProbe` is the question it asks of a world: how far a ball gets from
  one point towards another. A sphere cast over `orblit_physics` answers
  it.

## 0.1.0

- First cut of `orblit_camera`. Pre-alpha: everything is subject to change.
