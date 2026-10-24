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
native code. That is fixed there. Volumes, splats and the rest follow.
