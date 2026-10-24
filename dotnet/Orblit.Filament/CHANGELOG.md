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
arrays byte for byte. Materials, the render graph, volumes, splats and the
rest follow.
