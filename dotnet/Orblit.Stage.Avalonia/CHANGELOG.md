# Changelog

## 0.2.0

- `PanelHost` and `ControlPanel` put a tree of Avalonia controls on a
  `WorldPanel`. This settles decision 85 as (c). The host is one hidden
  window the stage keeps, with no frame, no taskbar entry, full clearness
  and a place off the screen. `PanelHost.Open` stacks each panel in it.
- `ControlPanel.Paint` draws the control into a bitmap and returns PNG
  bytes at the panel's size times its scale. `Send` raises routed pointer
  events on the control under the pointer, or on the one that captured it,
  so a button clicks, a slider follows a drag and a drag keeps going off
  the panel.
- A panel is out of date after a pointer, after a layout pass, and after
  `Invalidate`. A colour that changes alone shows in none of these, so say
  so with `Invalidate`.
- There is no hover and no keyboard focus. A pointer lands once the host has
  drawn a frame or two with the panel in it.
- `PanelHost.Open` refuses a width, a height or a scale that is not above
  zero, because a bitmap of no pixels throws only when the panel paints.

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
