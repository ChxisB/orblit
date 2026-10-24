# Changelog

## 0.1.0

`RendererViewport` is an Avalonia control that shows an Orblit renderer.
`Frame(scene, seconds)` describes the scene, draws it at the size of the
control in pixels and puts the picture on screen. It returns false when
nothing was shown: the control has no area or has not chosen how to show
frames, the screen is still busy with the last frames, or the picture was not
ready.

The control picks how to show frames when it enters a window. On macOS it
shares each frame with the compositor as an IOSurface, with two Metal shared
events to say when a frame is drawn and when the compositor is done with
it. No pixels are copied. Everywhere else, and on a Mac whose compositor
cannot import a surface, it reads each frame back into a bitmap.
`Path` says which one is running and `Fallback` says why the shared texture
was not used. `DroppedFrames` counts frames that were skipped.

Leaving the window frees the renderer and the path. Entering another one
starts both again. A fault on the screen's side is thrown from the next
`Frame` call with the original error as its cause.

The shared texture path has run in a real window on macOS, an Apple M4 Pro
with a display scale of 1. It showed 1920 frames, dropped none, kept up
through a resize, and `Frame` took a median of 1.16 ms. The readback path has
run headless, on macOS only. Windows and Linux have run nothing.
