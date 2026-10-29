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
- The four atmosphere examples are ported: `DayAndNight`, `GodRays`,
  `Weather` and `Volumes`. Their scenes match the fixture's digests.
  Dawn, dusk, the walk back out of the hall and a lightning strike fall
  outside the fixture's five seconds, so they are pinned to digests from
  the Dart examples. The weather example uses `Orblit.Weather` for its
  strikes.
- The four effects examples are ported: `Post`, `MotionBlur`,
  `Distortion` and `Outline`. So are four performance examples: `Many`,
  `Batching`, `Overdraw` and `Pipeline`. Their scenes match the
  fixture's digests. The pipeline example reads `ORBLIT_DETAIL` from the
  environment it is given.
- Settings are data. `Setting`, `Choice` and `Toggle` say what a control
  is and what a change does, and the host draws them. A `Caption` is a
  line of text between them.
- The rest of the basics are ported: `SceneFiles` and `Cameras`. So are
  four content examples: `Meshes`, `Imported`, `Sprites` and `Video`.
  Their scenes match the fixture's digests. The fixture ran with no model,
  sheet or film, so the scenes with one are pinned in tests of their own.
  The sprite sheet matches the Dart one byte for byte. The coins come
  from another generator, so as decision 77 has it they match on shape.
- `TextEntry` is a line to type into. `Buttons` is a row of buttons whose
  press is an event, so none stays chosen.
- `Example.Note` says what went wrong with a file the scene named.
  `Example.Models` holds what each model file holds, as the renderer
  reported it. The host fills both. `Example.Needs` names a
  `Downloadable`: what the repository does not carry, and the command
  that fetches it.
- The imported models example reads its files through `ISampleFiles`.
  `DiskSamples` reads them from disk and hands them to the renderer. It
  reads `ORBLIT_SAMPLES` and `ORBLIT_MODEL`. The meshes example reads
  `ORBLIT_MESH` and the video example reads `ORBLIT_VIDEO`.
- The package uses `Orblit.Camera`, `Orblit.Scene`, `Orblit.Stage`,
  `Orblit.Motion` and `Orblit.Sequence`.
- `ViewPoint` is a record, and `ToRenderCamera` gives the camera for it.
- The first example is called "A scene as a control".
- The three heavy examples are ported: `Splats`, `Crowd` and
  `Benchmark`. Their clouds and fields come from `Random`, so as
  decision 77 has it they match the fixture on shape. The rest of each
  scene matches on value, and so does the rest of the scene after each
  move. Each seed is pinned to this port's own run.
- The splats example reads `ORBLIT_SPLAT`, `ORBLIT_SPLAT_COUNT`,
  `ORBLIT_SPLAT_HARMONICS`, `ORBLIT_SPLAT_LIMIT`, `ORBLIT_SPLAT_SORT`,
  `ORBLIT_SPLAT_PILLAR`, `ORBLIT_SPLAT_COARSE` and `ORBLIT_SPLAT_DEVICE`
  from the environment it is given. With `Example.Device` set, it holds
  the cloud to what the device can carry.
- The terrain example is ported: `Terrain`. Its population revisions
  match on shape, because the scatter count runs across the whole
  process. `Orblit.Examples` uses `Orblit.Noise`.
- The blocks example is ported: `Voxel`. Its trees come from `Random`
  and change how many blocks are drawn, so its population keys are left
  out of the fixture match. Each world with its trees off matches Dart.
- The two Bistro examples are ported: `BistroExterior` and
  `BistroInterior`. They read `ORBLIT_BISTRO` from the environment they
  are given, or look in `../orblit/assets/bistro`. Each is lit by the
  fixtures the fetch script read out of its model.
