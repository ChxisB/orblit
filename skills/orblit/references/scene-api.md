# Scene API: constructors, fields and defaults

Taken from `dotnet/Orblit.Filament/*.cs` at the engine's `merge/avalonia`
branch (November 2026). Orblit is pre-alpha, so treat this as a map of where to
look, not a contract: open the file named in each heading before relying on a
field, especially one not listed here. Everything below is in the namespace
`Orblit.Filament` unless a heading says otherwise. Vectors and matrices are
`Orblit.VectorMath` types, and all of them are doubles.

Scene values are `sealed record`s that take an object initialiser and are
changed with `with`. The exceptions are `Population`, `SpriteLayer`,
`TerrainRegion`, `RenderTerrain` and `Feed`, which are classes that take
constructor arguments for what they must have and an initialiser for the rest.

## `RendererViewport` (`Orblit.Filament.Avalonia`)

`new RendererViewport()` is an Avalonia `Control`. Call `Frame(scene, seconds)`
once a tick from `RequestAnimationFrame`.

| Member | Meaning |
| --- | --- |
| `Frame(RenderScene scene, double seconds)` | Describes the scene, draws it and shows the result. False if nothing was drawn |
| `Backend` | The graphics backend to start with. Read when the first frame is drawn |
| `Renderer` | The `Renderer`, or null before the first frame and after the control has left its window |
| `Path`, `Fallback` | Which path puts frames on screen, and why the fast one was not taken |
| `DroppedFrames` | Frames not drawn or not shown because the screen had not caught up |

`seconds` is the renderer's own clock (weather, rain, cloud). Pass the same
timestamp the frame carries, or a fixed value to pin it.

## `Renderer`

The viewport owns one, and `viewport.Renderer` reaches it.

- `GetNotes()` returns `Note[]`, each a `(About, Saying)` pair: what the scene
  asked for that could not be given. It is a snapshot, so read it after
  `Frame` and treat an empty array as nothing wrong. It also holds a JSON
  description of each model file the last scene built something new from, under
  keys that start `AssetInfo.NotePrefix` (`orblit.model:`).
  `AssetInfo.Split(all)` takes the notes as a dictionary and returns the other
  notes and the `AssetInfo` for each model.
- `GetStats()` gives `Stats(GpuMilliseconds, CpuMilliseconds, BatchedObjects,
  BatchGroups, PassCount)`, medians of recent frames, nought until the backend
  has reported. `GetPassTimings()` gives one `PassTiming(Milliseconds, Drawn)`
  for each pass.
- `GetProfile()` gives a `DeviceProfile`, whose `Tier` is a `DeviceTier`: `Low`,
  `Medium` or `High`.
- `RequestCapture()` asks for the next frame to be read back, and
  `ReadCapture()` returns it as a `Capture(Width, Height, Rgba)`, or null until
  it arrives, a frame or two later.

The scene is described to the renderer in whole on every `Frame`. Only what
changed crosses to the native side: populations, terrain regions, sprite layers
and models are held there under their keys and revisions.

## `RenderScene`

`new RenderScene { Objects = ..., Camera = ... }`. `Objects` and `Camera` are
`required`. Everything else is optional:

| Field | Default when omitted |
| --- | --- |
| `Lights` | none |
| `Sky` | `new Sky()` |
| `Fog` | `Fog.None` |
| `Precipitation` | `Precipitation.None` |
| `Populations`, `Splats`, `Sprites`, `Terrain`, `Materials`, `Videos`, `Feeds` | none |
| `Post` | `new PostProcess()` |
| `Pipeline` | `new Pipeline()` |
| `Graph` | `RenderGraph.Standard()` |
| `Environment` | `RenderEnvironment.None` |
| `Probes`, `Volumes`, `Decals`, `Distortions` | none |
| `Field` | `IrradianceField.None` |
| `Outline` | `Outline.None` |
| `GodRays` | `GodRays.Off` |
| `Batching` | `true` |
| `DepthPrepass` | `false` |

`scene with { Camera = other }` returns the same scene with fields changed. A
`with` copy shares every list and instance it does not change.

## `RenderObject`

`new RenderObject { Key = ..., Transform = ..., Colour = ... }`

| Field | Type | Default | Notes |
| --- | --- | --- | --- |
| `Key` | `long` | required | Shares one key space with lights, probes and decals. Keep it stable for as long as the object exists, and never reuse one still in the scene |
| `Transform` | `Matrix4` | required | World transform |
| `Colour` | `Vector3` | required | Linear RGB. Colours the placeholder cube only |
| `Mesh` | `string?` | null | Absolute path or `RenderResources.NameFor` name of a glTF or glb (FBX and OBJ are converted). Null is the cube, 2 m across. A file that cannot be read is drawn as the cube |
| `Material` | `long?` | null | `Key` of a `RenderMaterial` in `scene.Materials`. Overrides a model's own materials. A key the scene doesn't list is drawn as the default |
| `CastShadows` | `bool` | true | |
| `ReceiveShadows` | `bool` | true | |
| `Visible` | `bool` | true | |
| `Layer` | `int` | 0 | 0 to `RenderScene.MaxLayer` (6). Out of range is clamped |
| `Animation` | `ModelAnimation?` | null | One of the model's clips. Null plays nothing and returns the object to its file's rest pose |
| `MorphWeights` | `IReadOnlyList<double>?` | null | Nought to one per morph target. Stops the object batching |
| `Variant` | `int?` | null | A glTF material variant, by its place in `AssetInfo.Variants` |
| `Joints` | `IReadOnlyList<JointPose>?` | null | Local joint transforms, applied after any clip |

`ModelAnimation` (`ModelAnimation.cs`):
`new ModelAnimation { Clip = ..., Seconds = 0, Speed = 1, Loop = true, From = null, Fade = 1 }`.
`Clip` is an index into the model's clips (find it with
`AssetInfo.ClipNamed("Walk")`, which returns null for a name the model lacks),
`Seconds` is where the clip is when the scene is sent, `Speed` 0 holds it, and
`From` is the `ModelAnimation` being faded out of.

`JointPose(Skin, Joint, Transform)` names a joint by skin and index.
`AssetInfo.JointNamed("hand.L")` finds the pair.

## `RenderLight`

`new RenderLight { Key = ..., Kind = ..., Intensity = ... }`

| Field | Default | Notes |
| --- | --- | --- |
| `Kind` | required | `RenderLightKind.Directional`, `Point`, `Spot` or `Area` |
| `Intensity` | required | Lux for `Directional`, lumens for the rest |
| `Colour` | `(1, 1, 1)` | Linear RGB |
| `Position` | `(0, 0, 0)` | Ignored by `Directional` |
| `Direction` | `(0, -1, 0)` | The way the light travels |
| `FalloffRadius` | 10 | Metres. Also a culling distance |
| `InnerConeAngle` | 0.5 | Radians, spot only |
| `OuterConeAngle` | 0.6 | Radians, spot only |
| `SunAngularRadius` | 0.263 | Degrees, directional only |
| `SourceRadius` | 0.1 | Shadow softness, read by `Area` and soft shadows only |
| `HaloSize`, `HaloFalloff` | 10, 80 | The sun's halo |
| `CastShadows` | true | |
| `Width`, `Height` | 1, 1 | Metres, area only |
| `Tangent` | `(1, 0, 0)` | The edge `Width` runs along, area only |

Budgets per view: one directional (a second is ignored, note `directional`),
256 point and spot (note `punctual`), 16 area (note `area`). Only the first
area light asking to cast gets a shadow (note `areaShadows`). A note's name is
its `About`.

## `RenderCamera`

`new RenderCamera { Position = ..., Target = ... }`, with `FieldOfView = 50`,
`Orthographic = false`, `ViewHeight = 10`, `Aperture = 16`,
`ShutterSpeed = 1.0 / 125` and `Sensitivity = 100`.

`FieldOfView` is vertical, in degrees. `ViewHeight` is the orthographic view's
height in metres. The exposure defaults are "sunny 16", right for 100,000 lux.
Change one with `camera with { Position = ... }`.

## `Sky`, `Fog`, `Precipitation`

`new Sky { ... }` has `Colour`, `Zenith`, `Horizon`, `Ambient = 28000`,
`ShowBody = true`, `Drawn = true`, `Quality = SkyQuality.Full`, `BodyDirection`,
`BodyColour`, `BodySize`, `Flash`, `FlashDirection`, `FlashSeed` and
`Clouds = Clouds.None`.

`Ambient` is the sky's light in lux. `Drawn = false` skips the dome (for
interiors). `Quality` is `SkyQuality.Lean`, `Fair` or `Full`. `Clouds` has the
presets `Cumulus`, `Stratocumulus`, `Stratus`, `Cirrus` and `Cumulonimbus`, and
`with` changes one. A `RenderEnvironment` skybox replaces the drawn sky; see
`ShowSkybox` below.

`new Fog { Density = 0.05, Distance = 0, Height = 0, HeightFalloff = 1, ... }`,
and `Fog.None` for none. `Distance` is where fog begins, in metres in front of
the camera. `Precipitation` has `Amount`, `Fall`, `Wind`, `Colour` and the
density of the drops, and `Precipitation.None` for none.

## `RenderMaterial`

`new RenderMaterial { Key = ... }`, listed in `scene.Materials` and named from
an object's `Material`.

| Field | Default |
| --- | --- |
| `Shading` | `MaterialShading.Lit` (`Unlit` ignores light, for markers and screens. `Video` shows a feed or a video. `ShadowCatcher` draws only the shadows falling on it) |
| `Blend` | `MaterialBlend.Opaque` (`Transparent`, `Fade`, `Masked`, `Add`) |
| `Culling` | `MaterialCulling.Back` (`Front`, `None`) |
| `BaseColour` | `new Vector4(0.8, 0.8, 0.8, 1)` |
| `Metallic` | 0 |
| `Roughness` | 0.5 |
| `Reflectance` | 0.5 |
| `Emissive`, `EmissiveIntensity` | zero, 0 |
| `DoubleSided` | false |
| `BaseColourMap`, `NormalMap`, `MetallicRoughnessMap`, `OcclusionMap`, `EmissiveMap` | null, each a `Texture` |

It also has `ClearCoat`, `Anisotropy`, sheen, `Wind`, `Tiling`, `Offset`,
`MaskThreshold`, a second surface mixed in with `BlendMode`, and a `Surface`
for a custom shader. `new Texture(path, srgb)` reads PNG, JPEG and KTX2; its
second argument defaults to true, so pass false for data such as normals.

## `RenderEnvironment`

- `new RenderEnvironment { Radiance = ..., Skybox = ..., Intensity = 30000,
  Rotation = 0, ShowSkybox = true, Size = 0 }` takes `cmgen`-baked `.ktx`
  cube maps, or a picture to filter here.
- `RenderEnvironment.FromImage(path)` names one `.hdr` or `.exr` as both the
  light and the backdrop, and filters it while the scene runs. Change the rest
  with `with`.
- `RenderEnvironment.None` is the default.

`Rotation` is radians about the vertical and `Intensity` is lux. With a skybox
showing, the procedural sky is switched off. `ShowSkybox = false` keeps the
environment's lighting and lets `Sky` draw the background.

`new Probe { Key = ..., Position = ..., Radius = 12, Resolution = 256 }` shares
the object key space. Bump `Version` to take its photograph again. Nothing else
does.

## Pipeline and post (`Pipeline.cs`, `PostProcess.cs`)

- `new Pipeline { Shadows, Resolution, Lighting, Samples = 1, Precise = false,
  Culling = true, Refraction = true, Textures }`, or `Pipeline.At(Detail.Low)`
  (`Low`, `Medium`, `High`, `Ultra`), or `Pipeline.ForDisplay(new Display(w, h))`.
- `new ShadowSettings { Kind = ShadowKind.Sharp, MapSize = 1024, Cascades = 2,
  ... }`; kinds are `Sharp`, `Soft`, `Area` and `Variance`.
- `new PostProcess { Enabled = true, AntiAliasing = AntiAliasing.Fxaa, Bloom,
  DepthOfField, Vignette, Occlusion, Reflections, Grading, Dithering = true }`.
  `AntiAliasing` is `Off`, `Fxaa` or `Temporal`.

`Grading.ToneMapping` (`Filmic`, `Aces`, `AcesLegacy`, `Linear`, `None`)
applies even when `Grading.Enabled` is false, so a scene that must not be
graded sets `Linear` and leaves `Enabled` alone.

## `Population` (`Population.cs`)

`new Population(int key, float[] transforms, float[] colours, Bounds bounds)`,
then an initialiser for `Mesh`, `Range = 0`, `Fade = Fade.Sink`, `Revision = 0`,
`CastShadows = false`, `ReceiveShadows = true` and `Layer = 0`.

- `transforms`: 16 floats per member, column-major, world space.
- `colours`: 3 floats per member, linear RGB. The constructor throws
  `ArgumentException` unless the two lengths agree.
- `bounds`: `new Bounds(minimum, maximum)`, one world box around every member.
  Too small and the whole population blinks out as the camera turns.
- `Range`: metres beyond which members fade out (`Fade` is `Sink`, `Shrink` or
  `None`); 0 draws them all.
- `Revision`: bump after writing into either array. The arrays are sent only
  when it changes, so an edit without a bump never shows.

The class is rebuilt each frame around the same two arrays, which is cheap.

```csharp
using Orblit.Filament;
using Orblit.VectorMath;

/// Ten thousand small cubes on a grid, drawn as one population.
class Field
{
    public const int Side = 100;
    public const int Count = Side * Side;

    private readonly float[] _transforms = new float[Count * 16];
    private readonly float[] _colours = new float[Count * 3];
    private int _revision;

    public Field()
    {
        var random = new Random(1);
        for (var i = 0; i < Count; i++)
        {
            var at = i * 16;
            // Column-major: the scale is on the diagonal, the translation at 12, 13 and 14.
            // The cube is 2 m, so a scale of 0.2 is 0.4 m.
            _transforms[at + 0] = 0.2f;
            _transforms[at + 5] = 0.2f;
            _transforms[at + 10] = 0.2f;
            _transforms[at + 12] = (i % Side) - (Side / 2f) + 0.5f;
            _transforms[at + 13] = 0.2f;
            _transforms[at + 14] = (i / Side) - (Side / 2f) + 0.5f;
            _transforms[at + 15] = 1;
            _colours[(i * 3) + 0] = 0.3f + (0.4f * random.NextSingle());
            _colours[(i * 3) + 1] = 0.5f;
            _colours[(i * 3) + 2] = 0.3f;
        }
    }

    /// Moves every member in place, then bumps the revision so the arrays are
    /// sent again. A population that doesn't move never bumps it, and costs
    /// nothing per frame.
    public void Wave(double seconds)
    {
        for (var i = 0; i < Count; i++)
        {
            _transforms[(i * 16) + 13] = (float)(0.2 + (0.2 * Math.Sin((seconds * 2) + (i * 0.1))));
        }

        _revision++;
    }

    public Population Population => new(
        500,
        _transforms,
        _colours,
        new Bounds(new Vector3(-Side / 2.0, -0.2, -Side / 2.0), new Vector3(Side / 2.0, 0.6, Side / 2.0)))
    {
        Revision = _revision,
    };
}
```

Pass `Populations = [field.Population]` in the scene.

## Sprites (`SpriteLayer.cs`, `LayerSprite.cs`)

`new LayerSprite { X = ..., Y = ... }` is required, with `Width = 1`,
`Height = 1`, `Depth = 0`, `Rotation = 0`, `PivotX = 0.5`, `PivotY = 0.5`,
`U0 = 0`, `V0 = 0`, `U1 = 1`, `V1 = 1`, `Red = 1`, `Green = 1`, `Blue = 1` and
`Alpha = 1`. A negative `Width` flips it. `Cut(left, top, width, height,
imageWidth, imageHeight)` sets the four texture numbers from a pixel rectangle.

`new SpriteLayer(int key, float[] sprites)`, then `Image`, `Transform`, `Tint`,
`Order = 0`, `Filter = MaterialFilter.Sharp`, `Snap`, `Blend = SpriteBlend.Alpha`
(or `Add`) and `Revision = 0`. One layer is one image and one draw. `sprites`
comes from `SpriteLayer.Pack(list)`. Changing `Transform` or `Tint` is free;
changing the sprites needs a new `Revision`. Layers draw by `Order`, lowest
first.

```csharp
using Orblit.Filament;
using Orblit.VectorMath;

static RenderScene SpriteScene(int revision)
{
    var layer = new SpriteLayer(
        1,
        SpriteLayer.Pack(
        [
            new LayerSprite { X = 0, Y = 0, Width = 2, Height = 2 },
            new LayerSprite { X = 3, Y = 0, Width = -2, Height = 2 },
        ]))
    {
        // No image draws flat rectangles, useful before the art exists.
        Image = new Texture("/path/to/sheet.png"),
        Revision = revision,
    };

    return new RenderScene
    {
        Objects = [],
        Sprites = [layer],
        Camera = new RenderCamera
        {
            Position = new Vector3(0, 0, 20),
            Target = Vector3.Zero(),
            Orthographic = true,
            ViewHeight = 18,
        },
        // Without these the renderer grades the art like a photograph.
        Post = new PostProcess
        {
            AntiAliasing = AntiAliasing.Off,
            Dithering = false,
            Grading = new Grading { ToneMapping = ToneMapping.Linear },
        },
    };
}
```

`Orblit.Sprite` turns atlases and animations into the `U0`, `V0`, `U1`, `V1`
numbers; see [packages.md](packages.md).

## Terrain (`RenderTerrain.cs`)

`new RenderTerrain(int key, regions, sets, options)`, where `options` is a
`TerrainOptions { RegionSize = 256, Spacing = 1, PicturesRevision = 0,
BlendSharpness = 0.87, AutoSteep = 0, AutoFlat = 1, AutoSlope = 1,
AutoHeightFalloff = 0.1, MeshSize = 64, Levels = 6, CastShadows = true,
ReceiveShadows = true }`. Build it with `TerrainView.From(terrain, key, options)`
from `Orblit.Stage` rather than by hand: it fills every field from an
`Orblit.Terrain` `Terrain` (see [packages.md](packages.md#orblitterrain)).

- `new TerrainRegion(int x, int z, float[] heights, uint[] cover, byte[] colour)`
  is one region's maps, shared, not copied. It crosses to the renderer only
  when its `Revision` differs from the last frame's.
- `new TerrainSet { Albedo, Normal, TileSize = 4, Triplanar = false }` holds
  decoded RGBA rows. A null picture draws plain.
- The grid is `MeshSize` squares across at `Levels` sizes, each twice the last,
  centred on the camera and raised by the GPU. Moving the camera sends no
  ground.
- Out of range (regions under 16 or over 2048 texels, more than 256 regions,
  pictures of different sizes) throws from the constructor, because the
  renderer would leave the ground out and the game would believe it was drawn.

`Orblit.Terrain` has its own `Terrain`, `TerrainRegion` and `TerrainSet` for
the data. Qualify them where `Orblit.Filament` is also in scope.

## Feeds (`Feed.cs`, `FeedSource.cs`)

A feed is a picture the application draws and a scene shows on a screen, with
no copy through managed code. A video is a file the renderer decodes. A feed
is what a camera, a decoder of your own or another view produces.

- `FeedSource.Create(int width, int height)` makes one. `source.Id` is the
  number the producer writes to. `Dispose()` lets it go, and `IsReleased` says
  whether it has been.
- `new Feed(long key, FeedSource source, int rotation = 0, bool mirror = false)`
  goes in `scene.Feeds`. `rotation` is clockwise quarter turns, 0 to 3. A
  mirror is applied first, then the turn.
- A screen is an object whose material has `Shading = MaterialShading.Video`
  and `Video = <feed key>`. A feed and a video share one key space, and a key
  named as both is refused.
- The producer is native. Apple: hand a `CVPixelBuffer` that is
  IOSurface-backed and 32BGRA or biplanar 4:2:0 to `source.Submit`.
- `Create` throws `PlatformNotSupportedException` where the platform has no
  feeds, which today is Linux and Windows.
- The video shading reads a feed and copies nothing. Add `LitScreen = true` to
  the material and the scene's lights shade it and it takes reflections, with
  `Metallic`, `Roughness`, `Reflectance`, `Emissive`, `EmissiveIntensity` and
  `AmbientOcclusion`. A lit screen is opaque only. A feed has no seek, rate,
  loop or volume.
- To use a feed in any other slot, pass `Texture.OfFeed(feed, width, height)`.
  It costs one GPU copy a frame. Videos cannot be used this way.

## `RenderResources`

- `RenderResources.NameFor(string path)` returns `"orblit:resource/" + path`.
- `RenderResources.Provide(string name, ReadOnlySpan<byte> bytes)` hands the
  bytes over. They are copied, so the buffer can be reused at once.
- `RenderResources.Release(string name)` lets them go and says whether there
  were any.

Use the name wherever a path would go: an object's `Mesh`, a material's
textures, an environment, a decal, a splat capture. The renderer looks there
before the disk, including for the files a `.gltf` names beside itself. It
doesn't load a name again once it has it, so changed bytes need a new name (a
content hash in it works). A scene may name something before its bytes
arrive; it draws without them until they do.
