# Changelog

## 0.42.0

Port of `orblit_filament` 0.42.0, begun. This part loads the renderer
library and declares all 49 calls of its C API, checked against the header.
`Renderer` starts a renderer, resizes it, swaps its surface, draws, reads back
a frame and reports stats, pass timings and notes. Result codes below zero
throw `RendererException`. The scene batches follow.
