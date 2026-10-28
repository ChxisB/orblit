# Changelog

## 0.1.0

- A port of `orblit_examples` 0.35.0, begun. `Example` is the contract:
  a name, a blurb, a section, a view point, a scene for each frame, the
  settings and the code that matters. `EngineExamples.All` lists them.
- `SurfaceExample` and the six lighting examples are ported: `Lights`,
  `PanelShadow`, `Shadows`, `Probes`, `Bounce` and `Field`. Their first
  300 frames match the fixture's digests. So does the scene after each
  move of each control.
- Settings are data. `Setting`, `Choice` and `Toggle` say what a control
  is and what a change does, and the host draws them.
- `ViewPoint` is a record, and `ToRenderCamera` gives the camera for it.
- The first example is called "A scene as a control".
