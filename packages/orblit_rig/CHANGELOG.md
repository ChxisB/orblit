# Changelog

## 0.2.0

- `BoneNaming.unique` makes a file's joint names fit to be bone names: an
  unnamed joint is called `joint` and its position, and a repeated name gets
  `.001`. It was private to `orblit_stage`; it is here so a clip imported from
  a model and the model it plays on name every bone the same way.

## 0.1.0

- First cut of `orblit_rig`. Pre-alpha: everything is subject to change.
