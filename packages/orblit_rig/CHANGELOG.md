# Changelog

## 0.2.1

- **Two-bone IK bends the joint towards its pole.** It bent it away: the
  solver turned the limb with `Quaternion.rotated`, which turns by the inverse
  of the rotation it is given. A generated limb puts its pole on the side the
  elbow or knee already bends, so an arm under IK was bending backwards.
- A `BoneWidget`'s `roll` turns its shape the same way a bone's `roll` turns
  the bone. It turned it the other way.

## 0.2.0

- `BoneNaming.unique` makes a file's joint names fit to be bone names: an
  unnamed joint is called `joint` and its position, and a repeated name gets
  `.001`. It was private to `orblit_stage`; it is here so a clip imported from
  a model and the model it plays on name every bone the same way.

## 0.1.0

- First cut of `orblit_rig`. Pre-alpha: everything is subject to change.
