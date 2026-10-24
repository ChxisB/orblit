# Changelog

## 0.42.0

Port of `orblit_filament` 0.42.0, begun. This part loads the renderer
library and declares all 49 calls of its C API, checked against the header.
`Renderer` starts a renderer, resizes it, swaps its surface, draws, reads back
a frame and reports stats, pass timings and notes. Result codes below zero
throw `RendererException`.

The scene core follows. `RenderScene` holds objects, lights, a camera, a sky
with clouds, fog and precipitation. Its `ToMessage` packs them into the
arrays the renderer reads, and `Renderer.Apply` sends them in the renderer's
own order. Objects carry a layer, morph weights, a clip to play and joint
poses. `AssetInfo` reads a model description, `Bounds` and `Bvh` answer
box, frustum and ray queries over a scene, and `Bvh.Refit` follows what
moved. A fixture from an independent run of the original packers checks the
arrays byte for byte.

Lights and materials follow. `RenderMaterial` carries shading, blend, maps,
wind, clear coat, anisotropy, sheen, a screen that plays a `Video`, a second
blended surface and a `CustomSurface` shader. A scene also holds `Decals`, an
`Outline` of chosen objects, `GodRays` and `Distortion`. The same picture on
several materials is sent once. `with` on a material keeps every field. The
original dropped the screen and blend fields when it copied.

The picture follows. `RenderEnvironment` sets the image lighting and the sky
box, `Probe` and `IrradianceField` set reflections and bounced light, and
`PostProcess` holds the image work. `Pipeline` picks a `Detail` for a
`Display`, with `ShadowSettings`, `Lighting` and `Resolution`. `RenderGraph`
schedules scene, reflection and effect passes over named targets and reports
`GraphProblem` values. A graph with problems sends no passes. `MotionBlur`
builds its own passes and `FrameCapture` names the slowest. The screen
effects are added to the graph only when the scene has something to draw
them. Closing a renderer that had drawn an irradiance field aborted in the
native code. That is fixed there.

The world follows. A `Population` is thousands of copies of one mesh. The
renderer is sent its transforms only when its revision moves. `Held` is what
the renderer already has, and `Renderer` fills it after every `Apply`, so
the next message leaves out what the renderer keeps. A call with no
population clears every one the renderer held. `LevelOfDetail` picks a mesh
by distance, with a margin that stops a thing flickering at a boundary, and
`LevelOfDetailState` remembers the step for each thing. `RenderTerrain`
packs a landscape as regions of heights, cover and colour with sets of
pictures, and a held terrain sends its settings and only the regions that
moved. A terrain key used twice throws. `EnvironmentVolume` is a box or a
sphere with a weight, a priority and a blend distance. `RenderScene.Resolved`
folds the volumes the camera is in into the fog, sky, exposure, environment
and image work before the scene is sent, so the renderer is told one look. A
scene with no volumes pays nothing. Splats and the rest follow.
