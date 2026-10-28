# Changelog

## 0.1.0

- A port of `orblit_examples` 0.35.0, begun. `Example` is the contract:
  a name, a blurb, a section, a view point, a scene for each frame, the
  settings and the code that matters. `EngineExamples.All` lists them.
- `SurfaceExample` and the six lighting examples are ported: `Lights`,
  `PanelShadow`, `Shadows`, `Probes`, `Bounce` and `Field`. Their first
  300 frames match the fixture's digests. So does the scene after each
  move of each control.
- The four material examples are ported: `Materials`, `Textures`,
  `Blending` and `Decals`. Their scenes match the fixture's digests too.
  The textures they draw are kept in an `IPictureFolder`, and
  `TempPictureFolder` keeps them as PNG files. `EngineExamples.All` takes
  the folder. The textures example reads its files from the environment
  and names them once the host sets `Example.Device`.
- Settings are data. `Setting`, `Choice` and `Toggle` say what a control
  is and what a change does, and the host draws them. A `Caption` is a
  line of text between them.
- `ViewPoint` is a record, and `ToRenderCamera` gives the camera for it.
- The first example is called "A scene as a control".
