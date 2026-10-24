# Changelog

## 0.1.0

- A port of `PointerRay`, `Nameplates` and `WorldPanel` from `orblit_stage`
  0.18.0 to Avalonia.
- `PointerRay.FromCamera` turns a point on a view into a line through the
  world. A perspective ray fans out from the camera. An orthographic ray
  starts on the lens.
- `Nameplates` draws each distinct string once into a shared `LabelAtlas`
  and shows each label as a quad that squares up to the view. `Sync` adds
  strings and hands the renderer the new picture. `Build` returns the
  objects and materials for one frame, nearest first.
- `WorldPanel` stands an `IPanel` in the scene on a flat face and turns a
  `PointerRay` into pointer events on the panel.
- `Sync` and `Dispose` are plain calls here. The Dart methods were futures
  because painting was asynchronous. `IPanel.Paint` returns the bytes.
- `Nameplates` refuses a fade that ends where it starts with an
  `ArgumentException`. Dart asserted. It refuses a negative `MaxLabels`
  with an `ArgumentOutOfRangeException` when it is made. Dart threw when
  it built.
- Labels at the same distance keep the order they were given in. Dart's sort
  is stable up to 32 labels and not past that.
- `WorldPanel` takes an `IPanel`, declared beside it, and not a `UiPanel`.
  The host that lets an interface be drawn and pressed without a window
  waits on decision 85, so no class here implements `IPanel` yet.
