# Changelog

## 0.2.0

- A port of `orblit_camera` 0.2.0.
- A camera body is `ICameraBody` and an aim is `ICameraAim`, so the interface
  is told apart from the classes that implement it. A target is
  `ICameraTarget`.
- `project` is `Framing.Project` and `rotationPlacing` is
  `Framing.RotationPlacing`. `lookRotation`, `rotateVector` and
  `slerpShortest` are on `Rotations`. `damp` and `dampingFactor` are
  `Smoothing.Damp` and `Smoothing.Factor`.
- `Lens`, `Blend`, `ScreenRect`, `CameraGuides` and `ScreenPoint` are
  records, and `copyWith` is `with`.
- `ArmProbe` is a delegate that answers a `double?`.
- `BlendTable.Between` looks a null name up as the text `null`,
  so a rule written with a null name is found only through its `*` half.
- `CameraBrain.Aspect` ignores a value that is not a finite number above zero.
