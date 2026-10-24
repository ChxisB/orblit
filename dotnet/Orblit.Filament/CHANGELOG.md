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
scene with no volumes pays nothing.

`SplatCloud` is a cloud of 3D Gaussians, read from a file or packed into
32-byte records with `SplatCloud.Pack`. Its flags carry the sort, the colour
degree, a coarse order and a limit of up to 16777215 splats, so a large limit
reads back as a negative `int` on the wire and the same bits in the renderer.
`SpriteLayer` is a layer of flat pictures. `LayerSprite` is one sprite in it,
16 floats, and `Cut` turns a rectangle in pixels into fractions of the image.
A held cloud or layer sends its settings and its records only when its
revision changes. A file cloud never sends records. A scene with neither pays
nothing.

`DeviceProfile` is what the device under a viewport can do, built from the
renderer's eight answers with `DeviceProfile.FromCapabilities` or from a
running renderer with `Renderer.GetProfile`. A missing or negative answer
reads as the least a device has. Its `Tier` is low, medium or high, and the
texture size, upload, splat and colour degree budgets follow the tier.
`TextureCandidates` lists the cooked siblings of a texture the device can
sample, best first. `SceneRows.DrawRows` has a row for each object and
population a scene submits, and `SceneRows.MaterialRow` has the packed
values of one material and the objects made of it. `NamedPassTiming.ToJson` is the
row for one pass.

`RenderResources.Provide` keeps bytes under a name for every renderer in the
process, and `Release` says whether there was anything to let go of.
`NameFor` puts the `orblit:resource/` prefix on a path. `AssetFiles` asks a
package source where an asset sits as a path, an offset and a length. No
source ships yet, so it answers null everywhere. `Texture.OfTarget` samples
what a pass drew, and `Texture.Target` reads the name back.

`FeedSource.Create` makes a picture the application draws itself, and
`Submit` hands it each finished pixel buffer. Apple platforms take them. Other
platforms throw `PlatformNotSupportedException`. A `Feed` in
`RenderScene.Feeds` names a source with a key, a quarter turn count from 0 to
3 and a mirror flag. A screen points at the key with `RenderMaterial.Video`.
A feed travels after the videos at rate 1 and volume 1, and a key used by a
video and a feed in one scene throws. `Texture.OfFeed` copies a feed into any
map at its own size or a smaller one. The rest follow.
