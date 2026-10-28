# Changelog

## 0.1.0

- A port of `orblit_examples` 0.35.0, begun. `Example` is the contract:
  a name, a blurb, a section, a view point, a scene for each frame, the
  settings and the code that matters. `EngineExamples.All` lists them.
- `SurfaceExample` and `BounceExample` are ported. Their first 300
  frames match the fixture's digests.
- Settings are data. `Setting`, `Choice` and `Toggle` say what a control
  is and what a change does, and the host draws them.
- `ViewPoint` is a record, and `ToRenderCamera` gives the camera for it.
- The first example is called "A scene as a control".
