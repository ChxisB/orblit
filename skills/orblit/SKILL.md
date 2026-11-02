---
name: orblit
description: Use when building a game, app or scene with Orblit, the 3D and 2D engine for .NET with Avalonia. Covers C# that uses the Orblit.* packages (RenderScene, RenderObject, RenderLight, RenderCamera, Population, SpriteLayer, RendererViewport, Orblit.Filament.Avalonia, the local NuGet feed and `dotnet` commands); adding Orblit to a .NET project on macOS, Windows or Linux; materials, shaders an author writes as `.oshader` files, scene documents, glTF, GLB and OBJ import and export, Gaussian splats, cooked and networked assets; clips, blend graphs, cutscenes and retargeting with Orblit.Motion (.oclip, .oblend, .ocutscene, root motion, retargeting); terrain with Orblit.Terrain (heights, sets, brushes, HeightAt, grass, stones and trees scattered by rule); physics, cylinders, convex hulls, compound and scaled shapes, static mesh collision, triggers, zones, belts, shape queries, joints, cars with wheels and an engine, walking characters, smooth display at any frame rate, snapshots to roll back to and terrain collision with Orblit.Physics; and questions about the API, lighting units, platforms, or why a scene is black or missing something. Orblit is pre-alpha and its names change between commits, so this skill says how to check the real API first. It is for building with Orblit, not for working on the engine itself.
---

# Building with Orblit

Orblit is a set of .NET packages over Google's Filament renderer, with Avalonia
for the window. A game states its whole scene as a value every frame, as a
`RenderScene`, and a `RendererViewport` control draws it. Everything that
isn't drawing (cameras, agents, collision, effects, cutscenes, 2D atlases,
scene files) is plain C# and runs anywhere .NET does.

## It is pre-alpha: check before you write

Nothing is API-stable. Names, fields and defaults change between commits, the
packages aren't on nuget.org, and anything you remember about Orblit is likely
stale or wrong. Before you use a class, member or named argument you haven't
seen in this session, confirm it in one of these, in this order:

1. **The source the project actually restores.** It is in the checkout that
   packed the feed: `dotnet/Orblit.<Name>/*.cs`, and physics in
   `orblit-physics/dotnet/Orblit.Physics*/`. The `///` comments are long and
   are the real reference. A project that uses a `ProjectReference` on a
   sibling checkout reads that checkout.
2. **The gallery examples**, `dotnet/Orblit.Examples/` in the engine checkout.
   Each is a working scene against that exact commit, so copy patterns from
   them rather than from memory.
3. **The docs site.** https://orblitengine.com/llms.txt is an index written for
   models, https://orblitengine.com/llms-full.txt is every page in one long
   file, and any page is also Markdown with `.md` on the end, such as
   https://orblitengine.com/docs/guides/lighting.md. The gallery is at
   `https://orblitengine.com/docs/gallery/<section>/`, where `<section>` is one of
   `basics`, `lighting`, `materials`, `atmosphere`, `effects`, `content`,
   `performance` or `showcases`. If an `.md` or `llms` URL returns 404, fetch
   the HTML page (`https://orblitengine.com/docs/guides/lighting/`) or go
   back to the source. Where the docs and the source disagree, the source
   wins.

Then run `dotnet build` on what you wrote. Say so when you couldn't verify a
name, rather than guessing.

## Adding Orblit to a project

You need the .NET 10 SDK, Git, CMake and a C++ compiler (Xcode on a Mac). The
packages are not on nuget.org. The engine packs them into a local folder, and
a project restores from it. The commands, the order they must run in and the
traps are in [references/dotnet.md](references/dotnet.md). Read it before you
write a project file.

```sh
git clone https://github.com/ChxisB/orblit.git
cd orblit
packages/orblit_filament/darwin/setup.sh   # fetches Filament, compiles the materials
tool/build_native.sh                       # builds the native libraries
tool/pack_local.sh                         # packs into ~/.orblit/feed, or $ORBLIT_FEED
```

Build the natives before packing. A package packed first carries no native
library and the renderer fails to load. A project then adds the feed once and
the packages by name:

```sh
dotnet new console -o game && cd game
dotnet nuget add source "$HOME/.orblit/feed" --name orblit
dotnet add package Avalonia.Desktop --version 12.1.4
dotnet add package Avalonia.Themes.Fluent --version 12.1.4
dotnet add package Orblit.Filament.Avalonia
```

The package id is the namespace. `dotnet add package Orblit.Pathing` gives
`using Orblit.Pathing;`. Physics is built from the `orblit-physics`
repository, with its own `tool/pack_local.sh` into the same feed.

To prove the stack works, a window with a `RendererViewport` and a scene that
holds one `RenderObject` draws a cube. That is the quickstart at
https://orblitengine.com/docs/start/installing-dotnet/. It builds and runs.

### Which platforms draw

| Platform | State |
| --- | --- |
| macOS | The reference. Apple silicon only. The renderer has drawn here, in a window and headless |
| Windows | The packages build. **No frame has ever been drawn on Windows** |
| Linux | The packages build. **No frame has ever been drawn on Linux** |

The packages that do not draw run anywhere .NET does. When a user is on
Windows or Linux, tell them plainly how little has been seen to work there.
Per-platform setup is in [references/platforms.md](references/platforms.md).

## The model: state the whole scene, every frame

- **Build a new `RenderScene` each time something changes.** It lists every
  object, light and population that exists. There is no `Add`, `Remove` or
  `AddChild`; an object missing from this frame's list is gone. A scene is a
  `sealed record`, so change one with `scene with { Lights = [...] }`.
  `Objects` and `Camera` are `required`. Everything else defaults to empty.
- **`viewport.Frame(scene, seconds)` describes the whole scene on every call**
  and draws it. It does not skip a scene it has seen before, so do not edit
  a list inside a scene you have handed over and expect nothing, or expect
  something. Build the scene from your state each frame. It returns false when
  nothing was drawn, because the control has no size yet or the screen is
  behind.
- **Keys are `long`s you choose.** Keep them stable across frames (a changed
  key destroys the old thing and creates a new one) and unique in the scene
  (two objects on one key draw only one, with a `keys` note). **Objects and
  lights share one key space**, so a light keyed 1 and an object keyed 1 are a
  clash. Probes and decals use the same space. Reserve ranges, for example
  lights 1 to 9, fixed props 10 to 999, spawned things from 1000 up, and don't
  reuse a key for a new thing while the old one is still in the scene.
- **Each `Transform` is a world transform** (`Matrix4`). There is no
  hierarchy in `RenderScene`; compose parent and child yourself.
- **`Vector3` and `Matrix4` are `Orblit.VectorMath` classes of doubles**, not
  `System.Numerics`. They are mutable. A `with` copy shares the instance, so
  do not edit a vector that two scenes hold. `Clone()` makes a copy.
- **`Colour` is linear RGB as a `Vector3`**, not an Avalonia `Color` and not
  sRGB. It colours the placeholder cube only. A model keeps its own
  materials unless you give the object a `Material` key from
  `RenderScene.Materials`, which overrides them.
- **`Mesh = null` is the placeholder cube, 2 m across** (-1 to 1 on each
  axis). Scale by 0.5 for a 1 m cube.
- **The control needs a size.** A `RendererViewport` that has not been laid
  out is 0 by 0, and `Frame` returns false until it has a size.
- **Members are PascalCase.** The scene types are records with an object
  initialiser: `new RenderLight { Key = 10, Kind = RenderLightKind.Directional }`.
  Most other types take constructor arguments, called by name:
  `new Brush(size: 12, strength: 0.8)`. Check which form a type uses before you
  write it, because the wrong one does not compile. An interface starts with
  `I`, as in `IPanel`.

## A minimal scene

This compiles against the engine at the time of writing. Check the names
against the source if it doesn't. It is the quickstart with a spinning cube,
a floor and the scene notes on screen.

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Orblit.Filament;
using Orblit.Filament.Avalonia;
using Orblit.VectorMath;

class Spin : Application
{
    [STAThread]
    static int Main(string[] args) =>
        AppBuilder.Configure<Spin>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);

    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new SpinWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

class SpinWindow : Window
{
    private readonly RendererViewport _viewport = new();
    private readonly TextBlock _notes = new()
    {
        Foreground = Brushes.White,
        Margin = new Thickness(16),
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom,
    };

    public SpinWindow()
    {
        Title = "Spin";
        Content = new Grid { Children = { _viewport, _notes } };
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        RequestAnimationFrame(OnFrame);
    }

    private void OnFrame(TimeSpan now)
    {
        if (_viewport.Frame(SceneAt(now.TotalSeconds), now.TotalSeconds))
        {
            ShowNotes();
        }
        RequestAnimationFrame(OnFrame);
    }

    // Notes repeat on every frame, so set the text and let Avalonia ignore an unchanged value.
    private void ShowNotes()
    {
        var notes = _viewport.Renderer?.GetNotes() ?? [];
        _notes.Text = string.Join('\n', notes.Select(note => $"{note.About}: {note.Saying}"));
    }

    // A new scene every frame, built from state. Keys: 1 and 2 are objects,
    // 10 is the sun, because objects and lights share one key space.
    private static RenderScene SceneAt(double seconds)
    {
        var cube = Matrix4.RotationY(seconds);
        cube.SetTranslation(new Vector3(0, 1, 0));
        var floor = Matrix4.TranslationValues(0, -0.05, 0);
        floor.ScaleByDouble(10, 0.05, 10, 1);

        return new RenderScene
        {
            Camera = new RenderCamera
            {
                Position = new Vector3(5, 4, 7),
                Target = new Vector3(0, 1, 0),
            },
            Objects =
            [
                new RenderObject { Key = 1, Transform = cube, Colour = new Vector3(0.85, 0.42, 0.16) },
                new RenderObject
                {
                    Key = 2,
                    Transform = floor,
                    Colour = new Vector3(0.18, 0.19, 0.21),
                    CastShadows = false,
                },
            ],
            Lights =
            [
                new RenderLight
                {
                    Key = 10,
                    Kind = RenderLightKind.Directional,
                    Direction = new Vector3(-0.4, -1, -0.6).Normalized(),
                    Intensity = 100000, // lux: full daylight
                },
            ],
        };
    }
}
```

The cube sits on the floor: it spans 2 m, so centred at y = 1 its base is at
0, and the floor slab's top is at 0. The default sky adds 28,000 lux of
ambient light, and the default camera exposure is set for daylight.

A small complete game (input, a clamped game loop, spawning and despawning
with stable keys) is in [references/game-loop.md](references/game-loop.md).

## Lighting and exposure

Lights use real photometric units, and the camera has a real exposure.

| `RenderLightKind` | Unit of `Intensity` | Realistic values |
| --- | --- | --- |
| `Directional` | lux | 100,000 full daylight, 400 heavy overcast |
| `Point` | lumens | 1,600 a bright domestic bulb, 450 a dim one |
| `Spot` | lumens | As point, in a cone (`InnerConeAngle`, `OuterConeAngle`, radians) |
| `Area` | lumens | As point, off one face of a `Width` by `Height` rectangle |

- **One directional light per scene.** A second is ignored and reported with
  a `directional` note. For a second sun-like source, use a spot or area
  light.
- `Direction` is the way the light travels, so a sun overhead points down,
  `(0, -1, 0)`, which is the default.
- A view shades at most 256 point and spot lights (the furthest from the
  camera drop out, `punctual` note) and 16 area lights (`area` note). Only the
  first area light that asks to cast gets a shadow.
- `FalloffRadius` (default 10 m) is a culling distance as much as a physical
  one. Making it far larger than the light reaches costs time and changes
  nothing.
- **Exposure.** `RenderCamera` defaults to `Aperture = 16`,
  `ShutterSpeed = 1.0 / 125`, `Sensitivity = 100`, the "sunny 16" rule for a
  scene at 100,000 lux. A black or murky interior usually means daylight
  exposure on a room: for about 300 lux use ISO 800 and f/2.8, rather than
  turning a sun down to 300.
- `Sky` (default `Ambient = 28000` lux) lights the scene as well as drawing
  the background. `new Sky { Drawn = false }` skips the dome for interiors.
- **Sky versus environment.** Set both a `RenderEnvironment` with a skybox and
  a procedural `Sky`, and the HDRI wins, so the sky and its clouds vanish.
  Set `ShowSkybox = false` on the environment to keep its lighting behind a
  procedural sky.
- Objects cast and receive shadows by default; populations don't cast by
  default. Shadow quality lives on `RenderScene.Pipeline`
  (`new Pipeline { Shadows = new ShadowSettings { ... } }`), not on the light.
- For lights an artist states in watts, metres and degrees, the `Orblit.Light`
  package converts them to these units.

## Time is sampled, not stepped

Keep one clock, usually the `TimeSpan` that `RequestAnimationFrame` hands you.
Anything with a playhead is asked for a moment rather than advanced: an
effect is `effect.At(seconds)`, a sprite animation `animation.At(seconds)`, a
cutscene `sequence.SampleAt(seconds)`, and a model's clip is
`new ModelAnimation { Clip = index, Seconds = whereInTheClip }`. That makes
scrubbing, replays and late joiners give the same answer. Physics, behaviour
trees and blends do step, because they have history. A blend keeps its history
in a `BlendPlace`, a plain value that can be saved or sent. `ClipLayer` adds a
masked one-shot whose `LayerPlace` clock is saved separately. The layer,
additive and graph APIs are in the `Orblit.Motion` entry of
[references/packages.md](references/packages.md).

For the parts that step, work out `dt` from the clock and guard it the way the
gallery's runner does:

```csharp
double? last;

void Frame(double seconds, Action<double> update)
{
    var dt = seconds - (last ?? seconds);
    last = seconds;
    if (dt < 0 || dt > 0.5) dt = 0; // a pause or a hitch, not game time
    update(Math.Min(dt, 0.05));
}
```

Smooth towards a target with `1 - Math.Exp(-dt * rate)` rather than a fixed
fraction per frame, or use `Smoothing.Damp(current, target, damping, delta)`
from `Orblit.Camera`. Its `damping` is the lag in seconds, not a rate, so
larger is slower. The `seconds` you pass to `Frame` is the renderer's own
clock (weather, rain, cloud), so pass a fixed value when you need reproducible
frames such as screenshots and tests.

## Many things

- **Batching is on by default.** Four or more placeholder cubes with the same
  material, colour and flags are merged into draws of up to 64 while keeping
  their own keys. Named meshes are not; see "Merging static meshes" below.
  `Batching = false` on the scene turns it off.
- **Past a few thousand, use a `Population`.** One mesh, one flat `float[]` of
  transforms (16 floats each, column-major, world space) and one of colours (3
  floats each, linear RGB), with one world-space `Bounds` that must cover
  every member or the lot blinks out. Members have no keys and can't be picked
  or moved individually by the renderer.
- **Bump `Revision` after writing into a population's buffers.** The renderer
  is sent the buffers only when `Revision` differs from what it holds, so a
  changed buffer with the same revision never reaches the screen, and a
  static one costs nothing per frame. `SpriteLayer`s follow the same rule.

A population example is in [references/scene-api.md](references/scene-api.md).

## A world in cells

A world too big to hold at once is cut into square cells on a grid, one scene
file each, written in world coordinates. `CellStreamer` keeps the cells near
a point in a scene and no others.

```csharp
using Orblit.Cells;
using Orblit.Scene;
using Orblit.Stage;

var grid = new Orblit.Cells.Grid(64); // metres across one cell
var view = new DocumentView(new SceneDocument());
var streamer = new CellStreamer(
    window: new CellWindow(grid, loadRadius: 2),
    fetch: async cell => await ReadCell(cell), // scene file bytes, or null
    apply: view.Apply);

// Once a frame, then draw view.Scene.
streamer.Step(player.X, player.Z, facingX: look.X, facingZ: look.Z);
```

`Grid` also names an Avalonia control, so qualify it in a file that uses both.

- **Ids take the cell.** An entity `rock` in cell (3, -1) is
  `cell_3_-1:rock` in the scene. Use `CellStreamer.IdIn` to build one. A cell
  file has no root entity, so nothing sits above the cells.
- **`apply` is yours.** Send the diff to the document view, and to physics too
  (`ScenePhysics.Apply`) so a body joins when its cell loads and leaves with
  it.
- **A frame has a budget** (4 ms by default). A cell is never split, so a
  frame can run over by one cell. Null from `fetch` means an empty cell. It is
  asked for again only after the cell leaves the window and comes back. A
  throw, or a fetch slower than `fetchTimeout` (30 s), is asked for again after
  `retryAfter`.
- **Prefab instances stay links** unless you pass `prefabs`, the same lookup
  `Instances.Expand` takes. Their parts take the cell's prefix too.
- **A cell stays while it is within `loadRadius + 1`**, so standing on a
  border does not flicker. Pass `pinned` for cells that must stay, such as the
  one a character stands in while its ground is not there yet.
- `onLoad` gets a `CellLoad` for each cell: its fetch time, apply time, bytes,
  entity count, `Problems` from the file reader and any `Error`. A cell with
  problems still loads.
- The cell the point is in loads first, whichever way it faces.

## Merging static meshes

A named mesh costs a draw for each primitive of each object, so a thousand
rocks that never move cost a thousand draws or more. `StaticMerge` in
`Orblit.Stage` watches a scene and swaps still copies of one model for one
baked file. `GltfBake.BakeInstancesAsync` in `Orblit.Asset` makes the file.

```csharp
using Orblit.Asset;
using Orblit.Stage;

var merge = new StaticMerge(new StaticMergeOptions
{
    Bake = async (mesh, placements) =>
        await GltfBake.BakeInstancesAsync(await File.ReadAllBytesAsync(mesh), placements),
    OnReady = Publish, // build the scene again
});

// Each time you publish a scene, in this order.
merge.Observe(scene);
var shown = merge.Replace(scene);
```

- **Who qualifies.** At least `Minimum` (8) objects of one mesh, material,
  colour, shadow flags and layer, in one square of ground `CellSize` (64)
  wide, that have held still for `Settle` (30) observations. Smaller groups
  stay single.
- **One that moves, hides or leaves** puts its group back to single objects
  at once, and the rest bake again without it. A merged object cannot be
  picked as its members. A pick lands on the merged object, whose key counts
  up from `KeyBase`.
- **Left alone:** objects with a clip, morph weights, joints or a variant,
  hidden ones, and ones with no mesh. The built-in cube batches natively.
- **A model the baker will not take**, one with a skin, an animation, a morph
  target, compressed buffers, an instanced node, material variants or
  vertices that are not float, stays single for good. `merge.Declined` lists
  them.
- **It costs.** A bake is managed work. The renderer keeps what it loaded
  from a file until it is torn down, so a group bakes at most `MaxBakes` (8)
  times and then stays single. The merged file also holds a second copy of the
  geometry. Lights and cameras inside the model are not carried over.
- **Not handled.** A blended material loses its depth order when copies
  join, so return null from your baker for such a model. The baked file keeps
  the model's texture references as they are, and only models with the
  textures inside the GLB were checked. Positions are 32-bit floats in world
  space, so keep `CellSize` small far from the origin. A baker that says no or
  throws declines its model for good.
- **`Observe` changes state and `Replace` answers.** Call `DisposeAsync()`
  when you are done, and it releases every file.
- **Checked headless on macOS only.** A merged group draws within a few
  percent of the pixels its objects drew, and a sharp highlight can differ by
  a few levels. It has not been run in a window.

## Models and files

- `Mesh` is an absolute path to a `.glb` or `.gltf` (`.fbx` and `.obj` are
  converted on first use), or a name registered with `RenderResources`.
- A file that can't be read is drawn as the placeholder cube, with a scene
  note keyed by its path.
- A packaged app's assets may be inside its archive or a resource stream
  rather than on disk, so hand bytes over by name:

  ```csharp
  using Orblit.Filament;

  string ProvideCrate(Stream crate)
  {
      using var bytes = new MemoryStream();
      crate.CopyTo(bytes);
      var name = RenderResources.NameFor("crate.glb");
      RenderResources.Provide(name, bytes.ToArray());
      return name; // use this wherever a path would go
  }
  ```

- A sandboxed macOS app can't read arbitrary absolute paths, so models
  silently become cubes with a note. Provide bytes as above, or leave the app
  unsandboxed for development, as the gallery does.
- `AssetInfo` describes each model: its clips (`ClipNamed`), joints
  (`JointNamed`), variants, materials, lights and bounds.

## Materials, scene files and assets

Two packages hold everything a project keeps on disk, and neither has a
window, a filesystem or a renderer in it.

`Orblit.Scene` has the scene document (`.oscene`), the material document
(`.omat`) and the glTF, GLB and OBJ writers and readers:

```csharp
var library = new MaterialLibrary(materials: materials); // a dictionary by project path
var view = new DocumentView(document, materials: library, look: "winter");

SceneWritten written = document.WriteAs(SceneFormat.Glb, materials: library);
SceneImported read = SceneImport.ReadSceneFrom(written.First.Bytes);
// read.Document is the scene, read.Problems says what did not survive.
```

A scene Orblit wrote comes back as itself: the same ids, order, parents and
components, including ones this build has never heard of. A glTF from anywhere
else is interpreted instead and says so. Neither the writer nor the reader
throws over one thing it could not carry: both return a list of `Problems`, and
code that does not show them makes a lossy export look successful.

A scene can also hold a `portrait` and a `landscape` layout, each a
`SceneDiff` of field, component and visibility edits against the base scene.
`LayoutFollower.Fit(width, height)` shows the one that suits the window, and
`document.InLayout(shape)` gives the scene as it looks in one.

`Orblit.Asset` cooks assets for a device and fetches them over a network. The
policy is checked before any connection is made, a 404 is distinguished from
an unreachable server, and a staged fetch draws a texture's own coarse mip
levels while the rest of it is still arriving.

Full API, parameter lists and the traps: [references/assets.md](references/assets.md).

## Interface in the world

A screen on a wall and a name over a head are in the scene, not over the view.
An `IPanel` (in `Orblit.Stage.Avalonia`) paints itself to PNG bytes and takes a
pointer. `PanelHost.Open(control, size)` makes one from an Avalonia control
tree. The host keeps a window nobody sees, so make it after the application has
started and keep it as long as its panels. `WorldPanel` shows a panel on the +z
face of the built-in cube squashed thin, and turns a `PointerRay` into a click
on it. `Nameplates` draws many short labels from one `LabelAtlas` picture (in
`Orblit.Ui.Avalonia`).

```csharp
using Avalonia;
using Avalonia.Controls;
using Orblit.Filament;
using Orblit.Stage.Avalonia;
using Orblit.VectorMath;

var host = new PanelHost(); // after the application has started
var panel = host.Open(new Button { Content = "Open the door" }, new Size(800, 450));

var transform = Matrix4.Identity();
transform.SetTranslation(new Vector3(0, 2.25, -2.49));
transform.ScaleByDouble(3, 1.6875, 0.005, 1); // half the size on each axis

var wall = new WorldPanel(
    panel, // 800 by 450 pixels, the shape of the face
    transform,
    new WorldPanelOptions { Id = "booth", ObjectKey = 3, MaterialKey = 3 });

RenderScene SceneFor(RenderCamera camera) => new()
{
    Camera = camera,
    Objects = [wall.Quad],
    Materials = [wall.Material],
};

void Click(RenderCamera camera, Point at, Size view)
{
    var ray = PointerRay.FromCamera(camera, at, view);
    wall.Down(ray);
    wall.Up(ray);
}
```

- Give the panel the same shape in pixels as the face has in metres.
- Call `wall.Sync()` after a change, then publish the scene again, because
  `Material` names the new picture. A name loads once, so each picture has a
  new name. Releasing the old one frees its bytes and not its texture, which
  the renderer keeps until it is disposed. A change costs a picture of memory
  for good, about 1.4 MB at 800 by 450. It suits a screen somebody presses
  and not one that animates.
- A panel takes one pointer. Send `Down`, `Move`, `Up` and `Cancel` for one
  finger.
- A panel has no keyboard focus and no hover. A text field cannot be typed
  into.
- `new Nameplates(atlas, new NameplatesOptions { FadeStart = ..., FadeEnd = ... })`
  (both required, distances from the camera): `plates.Sync(texts)`, then every frame
  `plates.Build(camera, labels)` takes `Nameplate(Id, Text, Position)` values
  and returns a `NameplateBatch` of `Objects` and `Materials` for the scene.
  Labels do not test against walls. A string wider than the atlas throws, so
  trim long names.

Guide: `/guides/world-interfaces/`. The gallery's Interface in the world
example runs all of it.

## Physics

Rigid bodies are in a separate repository, `ChxisB/orblit-physics`, packed
into the same feed by its own `tool/pack_local.sh`: `Orblit.Physics` (the
solver, the `Physics` class), `Orblit.Physics.Scene` (`ScenePhysics`, which
simulates a scene document), `Orblit.Physics.Terrain` (`TerrainPhysics`, which
lays a terrain as ground), `Orblit.Physics.Vehicle` (cars, which the scene
package brings in), `Orblit.Physics.Soft` (cloth and rope, stepped beside the
world) and `Orblit.Physics.Rig` (bodies and joints from a rig). An entity gets
a body from a `body` component (`BodyComponent` in `Orblit.Scene`), and each
`Advance` answers with a `SceneDiff` for the view:

```csharp
using Orblit.Physics.Scene;

using var scene = new ScenePhysics(document);
var moved = scene.Advance(seconds); // already in scene.Document
if (!moved.IsEmpty) view.Apply(moved);
```

`Physics` and `ScenePhysics` hold native memory, so `Dispose` them.

Body ids in the solver are `ulong`, vectors are `double[]` (or any
`IReadOnlyList<double>`), and entity names in a scene are strings.
`scene.EntityOf(id)` turns a body id back into the entity, or null.

Edits go to both `scene.Apply` and `view.Apply`, and are teleports. Never hand
`Advance`'s own diff back to `scene.Apply`. Read collisions from
`scene.Events`, a list of `PhysicsEvent(Kind, A, B, At, Normal, Force)`, not
from `scene.Physics.Events`, which holds only the last step. `Shape.Box` takes
half sizes where the component's `Size` is edge to edge.

```csharp
using Orblit.Physics;

var physics = new Physics();
physics.Add(1, Shape.Box(0.5, 0.5, 0.5), motion: PhysicsMotion.Free, at: [0, 5, 0]);
physics.Add(2, Shape.Sphere(0.3), at: [0, 8, 0], restitution: 0.6);
```

`ScenePhysics(document, smooth: true)` blends each body between its last two
steps, so a 60 Hz world moves on every frame of a 144 Hz screen. The cost is
drawing a body up to a step behind what `Physics` and `scene.Events` say.
After moving a body with `scene.Physics.Place`, call `scene.ResetSmoothing(id)`
so it is not drawn crossing the gap. `StopSmoothing(id)` shows a subtree as the
world has it, until `StartSmoothing(id)`.

Anything that walks is a character, not a free body:
`physics.AddCharacter(id, at: [...])`, then before every step
`Drive(id, velocity: [...])` with the velocity it wants, **gravity included**,
built from `FootingOf(id)!.Velocity`. The world decides what it gets. It
slides along walls, climbs steps, rides platforms and pushes crates, and
crates cannot push it. Root motion goes in the same way: the clip's step,
turned to world space and divided by the tick, is the velocity to ask for.
`SetStance(id, shape)` crouches it, and answers false when there is no room to
stand.

In a scene, a body with `BodyMotion.Character` is one: `scene.Walk(entity,
new RootMotionWalk(player))` asks for its root motion once per step, and
`scene.Crouch(entity, true)` crouches it with its feet where they were.

A car is a `vehicle` component (`VehicleComponent`) on a free body with
`wheel` components (`WheelComponent`) on its direct children, each wheel placed
where it sits at rest. `scene.Pilot(entity, new VehicleInput(throttle: 1,
steer: -0.3))` drives it every step, `Pilot(entity, null)` lets go, and
`scene.VehicleOf(entity)` answers a `Vehicle` that reads the `Gear`, the `Rpm`
and each of the `Wheels`. Front is minus z. The scene writes each wheel's
suspension, roll and steer back to its entity. A body the vehicle refuses is
just a body, with no error.

Joints are `physics.Join(id, Joint.Hinge(...), a: holder, at: [...], b: body,
rotation: [...])`, in **radians**, with 0 for the world at either end. In a
document a `joint` component (`JointComponent`) names no ids: it holds the
nearest body at or above its entity to the nearest body above that, or the
world, at the entity's own place and axes, in **degrees**. So a hinge is a
child entity of the door, placed and turned where the hinge is.

A body with `trigger` (fixed or driven only) is a place: nothing collides with
it, and it reports `PhysicsEventKind.Entered` and `Exited`, plus `Inside` every
step when either body was added with `stay`, naming the trigger first. A `zone`
component (`ZoneComponent`) on an entity with a body makes it a trigger that
changes gravity and drag for the free bodies inside. `CastAll`, `CastAny` and
`Overlap` join `Cast`, and `triggers: true` asks about triggers instead of
solids. `SetSurface` makes a belt and `SetRule` changes one pair's friction,
bounce and mass scale. A rule is data, not a callback.

A body says how it moves with `Locks`, `GravityScale`, `MaxSpeed`, `MaxSpin`,
`Continuous`, `CentreOfMass` and `Inertia` on its component, or with
`physics.SetControls(id, controls)` in the world, where `PhysicsControls` is a
record you build in one go. `SetMotion` switches a body between fixed, driven
and free, and `SetGravity` changes the world's. `new PhysicsRule(ignore: true)`
passed to `SetRule`, or `scene.Ignore(entityA, entityB)` for two entities,
makes a pair pass through each other. A character reads that rule too.

`physics.Snapshot()` copies the world and `physics.Restore(snapshot)` goes
back to it, for an undo, a rollback or a replay test. A snapshot is native
memory, so `Dispose` it. The same commands and step sizes after a restore give
the same bits, on one build and no further. `ScenePhysics` has the same two
calls, and `scene.Restore(snapshot)` returns the `SceneDiff` to give
`view.Apply`. `physics.Contacts` and `physics.Stats` say what the last step
did. Nothing draws contacts yet.

A body is a `Box`, `Sphere`, `Capsule`, `Cylinder`, `Hull` or `Compound`. A
hull is the convex solid round a list of points on `BodyComponent.Hull`, and
bodies cut from the same points at the same scale share one. In a world, lay
it with `physics.LayHull(id, points)` and name it with `Shape.Hull(id)`.
Points that enclose no volume give a body that does nothing. A compound's
`Parts` are `BodyPart` values with local centre, scale and quaternion
rotation. `BodyComponent.ShapeScale` stretches geometry independently of
visual scale. Low-level `LayCompound(id, parts, scale)` takes `ShapePart`
values, and `Shape.Compound(id)` names the result. Parts retain their empty
gaps. Each `Lay` call answers false and changes nothing when its input is
unusable.

Static model collision uses `physics.LayMesh(id, vertices, indices)` and
`Shape.Mesh(id)` on a fixed body. A mesh is a two-sided surface with empty
openings and no solid inside. Coincident vertices weld and shared flat edges
produce no rim impulse. `await CollisionMesh.FromGltfAsync(id, bytes, source)`
in `Orblit.Asset` bakes the active model scene. Turning on the `collision`
import setting for a model writes a `collision.json` sidecar. Scene bodies use
`BodyShape.Mesh`, `MeshVertices` and `MeshIndices`. Entity scale and
`ShapeScale` stretch the geometry. The editor's **Collide with this model**
action reads imported or authored triangles.

Ground is `physics.LayGround(id, heights, columns, rows, spacing: ...)`, and a
terrain is laid for you, the regions near the camera, by
`new TerrainPhysics(physics, terrain).Sync(x, z, radius)` every frame. Pass
`refresh: false` while a brush stroke is still being drawn.

Install, API and the traps: [references/physics.md](references/physics.md).

## Terrain

Ground is data in `Orblit.Terrain` (plain C#): a `Terrain` of square
`TerrainRegion`s, each a map of heights, cover words and colours, made only
where there is ground. `Orblit.Filament` draws it as a `RenderTerrain`, and
`TerrainView.From` in `Orblit.Stage` builds one from a `Terrain`:

```csharp
using Orblit.Filament;
using Orblit.Stage;
using Orblit.Terrain;

var terrain = new Terrain(regionSize: 64, spacing: 2, sets:
[
    new Orblit.Terrain.TerrainSet("rock", albedo: "rock.png", normal: "rock_n.png",
        tileSize: 12, triplanar: true),
    new Orblit.Terrain.TerrainSet("grass", albedo: "grass.png", normal: "grass_n.png"),
]);
terrain.FillHeights(new RegionKey(0, 0), (x, z) => Math.Sin(x / 20) * 4);

var scene = new RenderScene
{
    Objects = things,
    Camera = camera,
    Terrain =
    [
        TerrainView.From(terrain, 1, new TerrainViewOptions { Pixels = path => decodedRgba[path] }),
    ],
};

var y = terrain.HeightAt(x, z); // null off the ground or over a hole
var up = terrain.NormalAt(x, z);

// A brush: one press is one stroke, and its patches fold into one undo step.
var stroke = new TerrainStroke(terrain, BrushTool.Raise, new Brush(size: 12, strength: 0.8));
var patch = stroke.MoveTo(10, 10);
patch = patch.FollowedBy(stroke.MoveTo(30, 10));
patch.Revert(terrain); // undo

// Grass, stones and trees, by rule: saved with the terrain, placed again
// only where the ground changes, drawn as a population a region.
terrain.Scatter.Add(new ScatterLayer("grass", seed: 1, density: 0.5, sets: [1],
    maxSlope: 35, size: (0.4, 0.3, 0.4), colour: 0x5E8C3A, range: 70));
var placer = new ScatterPlacer();
placer.Update(terrain); // every frame; cheap when nothing moved
var drawn = ScatterView.From(placer, key: 100); // when placer.Revision moves
var grassScene = new RenderScene
{
    Camera = camera,
    Objects = drawn.Objects,
    Populations = drawn.Populations,
};
```

`Terrain` is also the name of the namespace, so inside a `namespace Orblit.…`
block the bare name means the namespace. Alias it, as `TerrainView` does.
`Orblit.Filament` has a `TerrainSet` and a `TerrainRegion` of its own for the
renderer, so qualify the data ones as above.

Build it every frame. It is cheap, because a region crosses only when its
`Revision` moves, which every edit does. `HeightAt` interpolates exactly as
the mesh does, so a thing placed with it sits on the drawn surface, and it
needs no physics. For things that fall and roll on it, `TerrainPhysics` in
`Orblit.Physics.Terrain` lays it in a physics world, triangle for triangle the
same ground. A scene file names a terrain with a `terrain` component
(`new TerrainComponent(file: "terrain/hills/hills.oterrain")`), which the
editor draws and shapes (Add › Terrain, then the Terrain mode) but
`DocumentView` does not load yet: load the files and call `TerrainView.From`
yourself. Only macOS has been seen to draw it. A scatter layer with no `mesh`
is a block (the cube stretched to its `size`); one with a `mesh` is an object
each, so keep those to thousands. API and the traps:
[references/packages.md](references/packages.md#orblitterrain).

## Scene notes, not silence

The renderer reports what it couldn't do through `Renderer.GetNotes()`, an
array of `Note` values. Each has an `About` (what was asked for: `directional`,
`punctual`, `area`, `keys`, a file's path, `environment`, `skybox` and others)
and a `Saying`, a sentence saying what went wrong. A viewport holds its
renderer in `RendererViewport.Renderer`, which is null until the first frame
has drawn.

- Read them from the first scene and put the text on screen during
  development. When something is black, missing or a cube, read the notes
  before changing code.
- It is a poll, not an event. The array is empty when nothing is wrong, so
  it tells you when a problem has cleared. Read it after each `Frame` that
  returned true.
- The same notes come back on every frame. Set a label's text only when it
  changed, or you schedule a layout pass every frame.

## Traps

- **Zero size.** A viewport not yet laid out is 0 by 0. `Frame` returns false
  and draws nothing. Guard any aspect you work out yourself before dividing.
  `CameraBrain`'s `Aspect` setter ignores bad values, but its constructor
  doesn't.
- **Camera roll.** A camera that only pitches and yaws still needs its roll
  pinned, or a sequence of aims accumulates one.
- **Quaternions.** `Quaternion` in `Orblit.VectorMath` has no method that
  rotates a vector. Use `Rotations.RotateVector(q, v)` from `Orblit.Camera`,
  and test rotation through the matrix.
- **Name clashes.** `Orblit.Collide` and `Orblit.VectorMath` both have a `Sphere`
  and a `Ray`. Qualify one or use an alias. These names each exist in more
  than one Orblit package: `Blend` (Agent, Camera), `Emitter` (Audio,
  Particles), `Frame` (Sprite, Test), `Hit` (Collide, Particles), `Layers`
  (Collide, Physics), `Node` (Agent, Dialogue), `Shape` (Collide, Mesh,
  Particles, Physics), `TerrainRegion` and `TerrainSet` (Filament, Terrain) and
  `Wait` (Agent, Effect). Avalonia's `Grid` clashes with `Orblit.Cells.Grid`,
  and `Terrain` is both a namespace and a class. Orblit renames a type that
  shares a name with its package: `NoiseField` in `Orblit.Noise`,
  `CutSequence` in `Orblit.Sequence`, `TimedEffect` in `Orblit.Effect`. The
  behaviour-tree time limit is `Deadline`, not `Timeout`. The full table is in
  [references/packages.md](references/packages.md).
- **Steering.** `Separate` must not be normalised (it pushes harder the closer
  things are). `Arrive` does overshoot; plan for it.
- **Spatial hashes.** `x*a ^ y*b ^ z*c` collides on symmetric coordinates.
  Mix the coordinates sequentially.
- **Scene files.** `new TransformComponent(position)` on its own resets
  rotation and scale. Pass the rotation and scale you aren't changing.
- **Sprite atlases.** Turn rotation off in the packer (rotated regions can't
  be drawn yet) and respect `OffsetX`, `OffsetY` and `PlacedSize` for trimmed
  frames.
- **2D colours.** Sprites want an orthographic camera and
  `ToneMapping.Linear`, or the renderer grades the art like a photograph.
- **Mutable vectors.** `Vector3` and `Matrix4` are classes. A field you copy
  with `with` still points at the same instance, and a method such as
  `Normalize` changes it in place. `Clone()` first, or use the `Normalized()`
  form that returns a new one.

## The packages

`Orblit.Filament` draws. The rest are mostly plain C#: `Orblit.Camera`
(cameras as shots, blends, damping, an arm that stops at a wall),
`Orblit.Agent` (steering and behaviour trees), `Orblit.Pathing` (splines,
patrols and routes round walls), `Orblit.Collide` (shapes, raycasts,
overlaps), `Orblit.Effect` (change as a function of time), `Orblit.Sequence`
(cutscenes), `Orblit.Motion` (animation clips, on entities and bones alike,
retargeted onto another skeleton, blends that mix and fade them, and
cutscenes), `Orblit.Terrain` (ground as regions of heights, standing on it,
and what is scattered over it), `Orblit.Cells` (a grid and the window of cells
to hold around a point), `Orblit.Progress` (named small integers for quest
stages, levels and flags), `Orblit.Quest` (quests as `.oquest` files, kept as
a stage in the progress table), `Orblit.Dialogue` (conversations as
`.odialogue` files that read and write the progress table), `Orblit.Save`
(saves for a game server, written off the tick), `Orblit.Sprite` (atlases,
sprite animation, parallax, tile maps), `Orblit.Ui` and
`Orblit.Ui.Avalonia` (game interfaces, and a panel that draws a control tree
with no window), `Orblit.Scene` and `Orblit.Stage` (scene documents and
staging them), `Orblit.Stage.Avalonia` (a panel on a face in the scene and
labels over heads), `Orblit.Light`, `Orblit.Mesh`, `Orblit.Rig`,
`Orblit.Noise`, `Orblit.Particles` (emitters that are a function of time),
`Orblit.Profile` (frame times, spans and a heap trend), `Orblit.Weather`,
`Orblit.Input`, `Orblit.Audio` (a mixer, voices, music, cues and contact
sounds), `Orblit.AudioDevice` (plays them on a speaker), `Orblit.Test` (tests
with no window), `Orblit.Asset`, `Orblit.Cook` (cooks a project's assets for
a target), `Orblit.Pack` (one `.opack` file for a cooked bundle), `Orblit.Kit`
(packs, checks and installs kits), `Orblit.Native` (C++ scripts),
`Orblit.VectorMath` (the double precision `Vector3`, `Matrix4` and
`Quaternion`), `Orblit.Json`, `Orblit.Core` (the entity-component world over a
native core), `Orblit.Net` (multiplayer, in the `orblit-net` repository) and
`Orblit.Remote` (a socket to drive a debug game, and `Orblit.Remote.Tool`, a
tool server so an assistant can). What each does and the entry points worth
knowing are in [references/packages.md](references/packages.md). Add each the
same way as `Orblit.Filament.Avalonia`, with `dotnet add package`. Physics and
`Orblit.Net` are the exceptions: they are in their own repositories. Physics
packs into the same feed with its own `tool/pack_local.sh`. `orblit-net` has no
pack script, so reference its project with a `ProjectReference`.

## Reference files

Load these when the task needs them:

- [references/dotnet.md](references/dotnet.md): the local NuGet feed, the
  renderer's names, the `dotnet` commands and the gates.
- [references/scene-api.md](references/scene-api.md): constructors, fields
  and defaults for the scene types, with a population and a sprite layer.
- [references/game-loop.md](references/game-loop.md): a complete small game.
- [references/platforms.md](references/platforms.md): per-platform setup and
  what has actually been seen to work.
- [references/assets.md](references/assets.md): materials and looks, shaders
  an author writes, scene files and their export and import, splats, cooked
  and networked assets.
- [references/physics.md](references/physics.md): the physics world,
  questions asked of it, characters and root motion on them, ground and
  terrain, triggers, zones, belts and contact rules, joints, bodies and joints
  in scene documents, and simulating a document.
- [references/packages.md](references/packages.md): the other packages,
  terrain among them.
- [references/ragdoll.md](references/ragdoll.md): bodies and joints from a
  rig's bones, limp and powered limbs, a hit that spreads, and a character
  that falls and gets up.

## Licence

Orblit is under MPL-2.0, the Mozilla Public License, which is open source and
a file-level copyleft (not plain MIT). Games made with it, commercial ones
included, are fine and stay the author's own. Changes to Orblit's own files
stay under the same licence. For anything beyond that, point the user at
`LICENSE` in the repository rather than interpreting it.
