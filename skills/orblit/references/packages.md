# The packages

Every package is a NuGet package built from `dotnet/<Name>` in
`https://github.com/ChxisB/orblit.git`, with the id `Orblit.<Name>`.
[dotnet.md](dotnet.md) says how to get them into a project. Each project's
`///` comments are the fullest reference, and its `CHANGELOG.md` says what
changed. Only the renderer and the `*.Avalonia` packages need Avalonia; the
rest run anywhere .NET does. The code blocks below build against the versions
in the table, and none of them has been run in a window.

| Package | Version | What it is | Start with |
| --- | --- | --- | --- |
| `Orblit.Filament` | 0.42.0 | The renderer over its C API. A scene is described in whole batches and drawn. See [scene-api.md](scene-api.md) | `Renderer`, `RenderScene`, `RenderObject`, `RenderLight`, `RenderResources` |
| `Orblit.Filament.Avalonia` | 0.1.0 | A control that shows a renderer | `RendererViewport` |
| `Orblit.Camera` | 0.2.0 | Cameras as shots: say what to frame, and a brain works out where to be and blends between shots. An arm holds a camera on the near side of a wall | `CameraBrain`, `VirtualCamera`, `FollowBody`, `HardLookAt`, `Lens` |
| `Orblit.Agent` | 0.2.0 | Steering behaviours (where to go) and behaviour trees (what to want) | `Steerable`, `Pursue`, `Separate`, `Brain` |
| `Orblit.Pathing` | 0.3.0 | Splines to ride, a grid and a navmesh to search with the cheapest route pulled tight, links for doors and ladders, and followers and walkers that move along what they find | `Spline`, `Rail`, `NavMesh`, `NavWalker`, `FindPath` |
| `Orblit.Collide` | 0.1.0 | Shapes, raycasts and overlap tests, 3D and 2D, and a grid to find what is near. Queries, not a physics simulation | `Sphere`, `Box`, `Capsule`, `Broadphase`, `Contact`, `Hit` |
| `Orblit.Effect` | 0.1.0 | Move, turn, grow, tint and fade as functions of time, sequenced and combined | `TimedEffect`, `Move`, `Turn`, `Parallel`, `Repeat`, `Playing` |
| `Orblit.Sequence` | 0.2.1 | Cutscenes: tracks of clips over a playhead | `CutSequence`, `SequenceFrame`, `Director` |
| `Orblit.Motion` | 0.10.0 | Animation clips played on a scene's entities and a model's bones, with marks and root motion, imported from glTF, and retargeted onto another skeleton. Blends are graphs of states that mix clips. Cutscenes key the scene and cut between cameras | `ClipDocument`, `ClipPlayer`, `ClipScene`, `GltfImport`, `ClipRetarget`, `BlendDocument`, `BlendPlayer`, `CutsceneDocument` |
| `Orblit.Progress` | 0.1.0 | Named small integers for a game's progress: quest stages, skill levels, flags. A schema names them, a table holds them and never changes, and a write gives the changes to send to one player | `ProgressSchema`, `Whole`, `Slice`, `ProgressTable`, `Changes` |
| `Orblit.Quest` | 0.1.0 | Quests as data: an ordered list of stages kept as one integer in a progress table, read from `.oquest` files | `QuestDefinition`, `Stage`, `QuestBook`, `Moved`, `Refused` |
| `Orblit.Dialogue` | 0.1.0 | Conversations as data: lines, choices, writes to a progress table and jumps, read from `.odialogue` files. The first variant whose conditions hold starts it | `DialogueDefinition`, `DialogueBook`, `DialogueRunner`, `Before` |
| `Orblit.Save` | 0.2.1 | Saves for a game server: bytes under a key with a reason, written off the tick by a worker, whole after a crash, with staggered autosaves, a history of versions, restore and offline edit | `SaveQueue`, `ISaveStore`, `FileSaveStore`, `SaveReason`, `AutoSaver`, `HistoryStore`, `SaveTools` |
| `Orblit.Save.History` | 0.2.1 | A .NET tool that lists the versions of a saved key and restores one, with the server stopped | `orblit-save-history` |
| `Orblit.Terrain` | 0.5.0 | Ground as data: regions of height, cover and colour kept as `.oterrain` and `.oregion` files, with the height and slope anywhere, the brushes that shape it, and what is scattered over it. No physics | `Terrain`, `TerrainSet`, `Cover`, `TerrainStroke`, `Brush`, `ScatterLayer`, `ScatterPlacer`, `TerrainStreamer` |
| `Orblit.Sprite` | 0.4.0 | 2D data: atlases and a packer, sprite animation, parallax, tile maps. Draws nothing, and `SpriteLayer` in the scene draws | `Atlas`, `Region`, `SpriteAnimation`, `Parallax`, `TileMap` |
| `Orblit.Ui` | 0.3.0 | A game's words in each language: strings tables, language files and plural rules | `UiStrings` |
| `Orblit.Ui.Avalonia` | 0.3.0 | Avalonia controls for a game interface: a flex layout, the image loader, touch controls and the label atlas | `FlexPanel`, `PanelHost`, `TouchStick`, `LabelAtlas` |
| `Orblit.Cells` | 0.1.0 | A world cut into square cells: which cells to load and drop around a moving point | `Grid`, `CellPlan` |
| `Orblit.Scene` | 0.24.0 | A scene as a document: entities with stable ids, components, migrations and diffs | `SceneDocument`, `SceneDiff`, `TransformComponent` |
| `Orblit.Stage` | 0.5.0 | Stages a scene document for the renderer, and a terrain and its scatter. Plays cutscenes and sounds over it, draws a scene's particle emitters, a grid of particles such as a cloth and a trail ribbon, swaps still copies of one model for one baked mesh, and draws debug segments | `DocumentView`, `TerrainView`, `ScatterView`, `Cutscenes`, `ParticleSheet`, `ParticleView`, `TrailRibbon`, `StaticMerge`, `SoundView`, `DebugLines` |
| `Orblit.Stage.Avalonia` | 0.2.0 | Interface in a staged scene: a panel's picture on a quad in the world, the line from a point on the screen into the world, and name labels | `WorldPanel`, `PointerRay`, `Nameplates` |
| `Orblit.Light` | 0.3.0 | Lights stated in watts, metres and degrees, converted to photometric units | `Light`, `Photometry` |
| `Orblit.Mesh` | 0.6.0 | Building and editing geometry, writing it out and reading it back as OBJ, STL, PLY and glTF, with limits for untrusted files | `Mesh`, `MeshExport`, `ReadLimits` |
| `Orblit.Rig` | 0.3.0 | Armatures, poses, bone constraints, IK, and a modifier stack that adjusts a pose after playback: reaching, looking, joint limits, springs, foot placement | `Armature`, `Pose`, `ModifierStack`, `TwoBoneIk`, `LookAt`, `SpringBones`, `FootPlacement` |
| `Orblit.Noise` | 0.1.0 | Deterministic value and gradient noise | `NoiseField` |
| `Orblit.Particles` | 0.2.0 | Smoke, sparks, dust and fire. An emitter is data, and a particle is worked out from a seed, its number and the time, so the same time gives the same frame. Collision and turbulence are an opt-in stepped mode | `Emitter`, `Burst`, `ConeShape`, `ParticleBuffer`, `ParticleSim`, `Trail`, `TrailRing` |
| `Orblit.Profile` | 0.1.0 | Where a frame went on the managed side: named spans, gauges, counters, a ring of frame times and a heap trend | `Profiler` |
| `Orblit.Weather` | 0.1.0 | A day cycle for the sun and moon, a camera exposure to see by it, named weather states, and lightning worked out from the clock | `WeatherState`, `DayCycle`, `CameraExposure` |
| `Orblit.Input` | 0.6.0 | Named actions with contexts, gestures and rebinding, over gamepads as per-frame state | `ActionMap`, `FrameControls`, `TouchControls`, `PadState` |
| `Orblit.Audio` | 0.2.0 | Every sound call a game makes, run without a speaker: a mixer with a bus tree, voices that fade and loop, and a listener that hears a positioned sound by distance, direction and Doppler. Silent until a backend plays it | `AudioPlayer`, `Mixer`, `Voice`, `CueBoard`, `ContactSounds`, `RecordedAudio` |
| `Orblit.AudioDevice` | 0.2.0 | The backend that plays `Orblit.Audio` through the output device: WAV, FLAC, MP3 and Ogg Vorbis, with long files streamed. Tested on rendered samples and **not yet heard on a device** | `DeviceAudio` |
| `Orblit.Test` | 0.1.0 | Tests for a game with no window: a stage that steps sampled time and holds the controls, finders and expectations for a scene and for recorded sound, and a text capture of a frame's changes | `TestStage`, `ScriptedControls`, `SceneFinders` |
| `Orblit.Asset` | 0.25.0 | Asset ids, content hashes, sources layered over each other, the manifest, import settings, importers and the cook cache. See [assets.md](assets.md) | `AssetId`, `IAssetSource`, `AssetManifest`, `GltfBake`, `KitExtraction` |
| `Orblit.Core` | 0.1.0 | An entity-component world over a native core. Components are views onto native memory | `World`, `ComponentKind` |
| `Orblit.Native` | 0.1.0 | Compiling and loading C++ scripts. A script is handed a table of what it may call and answers start, step and stop | `NativeScript` |
| `Orblit.Remote` | 0.4.0 | A control surface in a debug game: a loopback WebSocket that lists entities, reads and sets components, pauses and steps time, waits for a field to hold, and compares snapshots | `RemoteServer`, `RemoteHost`, `CoreWorldInspector`, `TimeGate`, `LogTail` |
| `Orblit.Remote.Tool` | 0.4.0 | A .NET tool that serves that control surface to an assistant, as tools | `orblit-remote` |
| `Orblit.Cook`, `Orblit.Pack`, `Orblit.Kit` | 0.1.0 | .NET tools that cook a project's assets for a target, pack a bundle into one `.opack` file, and pack, check and install kits. See [assets.md](assets.md) | `orblit-cook` |
| `Orblit.VectorMath` | 0.20.0 | Double precision vectors, matrices and quaternions | `Vector3`, `Matrix4`, `Quaternion` |
| `Orblit.Json` | 0.4.0 | JSON read and written the way Orblit files hold it. Whole numbers are `long`, the rest `double`, and a map keeps its keys in order | `JsonText` |
| `Orblit.Codegen` | 0.1.0 | A source generator that reads types marked `OrblitComponent` and writes their registration, typed column access and the component manifest | `OrblitComponent` |

Four more packages serve the engine's own projects. `Orblit.Interop` finds the
native libraries in a package's `runtimes` folder. `Orblit.HeadlessSupport`
runs one headless Avalonia session for tests. `Orblit.Parity` reads the
fixtures a port is checked against. `Orblit.Examples` holds the gallery's
scenes.

Other repositories hold more. `orblit-net` is multiplayer: replication,
ownership and interpolation. `orblit-physics` simulates the bodies and joints
of a scene document and lays a terrain as ground. [physics.md](physics.md)
covers it. `orblit-editor` is the editor app, and `orblit-examples` is the
gallery app.

## Name clashes

A clash is an error only where the name is used. These were found by listing
every public type name that more than one package defines:

| Name | Clashes between | Fix |
| --- | --- | --- |
| `Blend` | `Orblit.Agent` and `Orblit.Camera` | Alias one: `using CameraBlend = Orblit.Camera.Blend;` |
| `Wait` | `Orblit.Agent` and `Orblit.Effect` | Alias one, or write `Orblit.Effect.Wait` |
| `Node` | `Orblit.Agent` and `Orblit.Dialogue` | Alias one, or write the full name |
| `Emitter` | `Orblit.Audio` and `Orblit.Particles` | Alias one, or write the full name |
| `Hit` | `Orblit.Collide` and `Orblit.Particles` | Alias one, or write the full name |
| `Range` | `Orblit.Particles` and `System` | Write `Orblit.Particles.Range` in full |
| `Shape` | `Orblit.Collide`, `Orblit.Mesh`, `Orblit.Particles` and `Orblit.Physics` | Alias one, or write the full name |
| `Layers` | `Orblit.Collide` and `Orblit.Physics` | Alias one, or write the full name |
| `ComponentType` | `Orblit.Asset` and `Orblit.Core` | Alias one, or write the full name |
| `TerrainRegion`, `TerrainSet` | `Orblit.Filament` (what the renderer draws) and `Orblit.Terrain` (the ground as data) | Alias one, or write the full name |
| `Frame` | `Orblit.Sprite` and `Orblit.Test` | Alias one, or write the full name |
| `Grid` | `Orblit.Cells` and Avalonia's `Grid` | Alias one, or write the full name |

Some type names are also namespace names, which matters only inside a
namespace that starts `Orblit.`: `Orblit.Agent.Sequence` and the namespace
`Orblit.Sequence`, `Orblit.Mesh.Mesh`, `Orblit.Terrain.Terrain`,
`Orblit.Quest.Stage` and the namespace `Orblit.Stage`, and
`Orblit.Audio.Motion` and the namespace `Orblit.Motion`. Write the full name
there.

The behaviour-tree time limit is `Deadline`, not `Timeout`.

## Orblit.Camera

A `VirtualCamera` has a `Body` (where to stand: `StaticBody`, `FollowBody`,
`OrbitBody`, `FramingBody`, `ScreenFollowBody`), an `Aim` (where to look:
`HardLookAt`, `StaticAim`, `ComposerAim`, `HeadAim`, `PovAim`), a `Lens`, a
`Priority` and `Enabled`. The `CameraBrain` shows the highest-priority enabled
camera and blends when that changes. What a camera follows is any
`ICameraTarget`. `FixedTarget` is the simplest, with a settable `Position`
and `Rotation`.

```csharp
using Orblit.Camera;
using Orblit.Filament;
using Orblit.VectorMath;

// The game moves this each frame: `player.Position = ...`.
var player = new FixedTarget(Vector3.Zero());

var brain = new CameraBrain();
brain.Add(new VirtualCamera(
    "chase",
    priority: 20,
    follow: player,
    lookAt: player,
    // Behind and above, in the player's own frame. Damping is seconds of
    // lag per axis, so the camera trails but never bobs.
    body: new FollowBody(new Vector3(0, 2.4, 7), damping: new Vector3(0.35, 0.18, 0.5)),
    aim: new HardLookAt(),
    lens: new Lens(FieldOfView: 55)));
brain.Snap();

// Once a frame, with a clamped delta. The result goes in the scene.
RenderCamera Shot(double delta, double width, double height)
{
    brain.Aspect = width / height; // a zero or non-finite ratio is ignored
    brain.Update(delta);
    var state = brain.State;
    return new RenderCamera
    {
        Position = state.Position,
        Target = state.Position + state.Forward,
        FieldOfView = state.Lens.FieldOfView,
    };
}
```

`new ArmBody(inner, probe, radius: 0.3, height: 1.5)` wraps a body and holds
its camera on the near side of a wall. `probe` is an `ArmProbe`,
`double? ArmProbe(Vector3 from, Vector3 to, double radius)`: how far a ball
gets before it meets something, or null. Over `Orblit.Physics` that is a cast
with a sphere. Pass the player's body to `ignore`, or the arm starts inside
it and the camera sits on the player.

```csharp
using Orblit.Camera;
using Orblit.Physics;
using Orblit.VectorMath;

var body = new ArmBody(
    new FollowBody(new Vector3(0, 2.4, 7)),
    (start, end, radius) =>
    {
        var line = end - start;
        return physics.Cast(
            [start.X, start.Y, start.Z],
            [line.X, line.Y, line.Z],
            line.Length,
            Shape.Sphere(radius),
            ignore: playerBody)?.Distance;
    },
    radius: 0.3);
```

`Rotations.LookRotation(direction)` turns a heading into a `Quaternion` for a
target's `Rotation`. `Smoothing.Damp(current, target, seconds, delta)` smooths
one number the same way at any frame rate. Worked example:
https://orblitengine.com/docs/examples/a-camera-that-follows/ and the guide at
`/guides/cameras/`.

## Orblit.Agent

Steering: a `Steerable` has a `Position`, a `Velocity`, a `MaxSpeed` of 4, a
`MaxForce` of 8 and a `Radius` of 0.5. It is moved by
`agent.Integrate(force, seconds)`, where the force comes from a behaviour's
`Force(agent)`. The behaviours are `Seek(target)`, `Flee`,
`Arrive(target, slowingRadius)`, `Pursue`, `Evade`, `Wander(seed, at)`,
`Separate(others, radius)`, `Align`, `Cohere` and `FollowPath`. Combine them
with `Blend`, a list of behaviour and weight pairs.

```csharp
using Orblit.Agent;
using Orblit.VectorMath;

var guard = new Steerable(new Vector3(0, 0, 0), maxSpeed: 3);
var hero = new Steerable(new Vector3(10, 0, 0));
var friends = new List<Steerable> { new(new Vector3(2, 0, 1)) };

// Chase the hero and keep clear of the other guards.
var chase = new Blend([(new Pursue(hero), 1.0), (new Separate(friends, radius: 2), 0.6)]);

// Once a frame.
void StepGuard(double seconds) => guard.Integrate(chase.Force(guard), seconds);
```

Crowds: `Avoid(preferred, neighbours, horizon: 2)` returns the force for the
velocity nearest `preferred` that hits no neighbour within `horizon` seconds.
Pass the velocity the route wants, such as the direction of the next path
point times `MaxSpeed`. Give a `Steerable` a `priority` to make others give
way: a neighbour of lower priority is not avoided. Update the agents in the
same order each frame and a run repeats. It looks at every neighbour in the
list, so pass the near ones for a big crowd. A crowd packed tighter than one
agent width per lane jams and overlaps.

Behaviour trees: build a `Node` from `Selector([...])`, `Sequence([...])`,
`Check(name, tick => bool)`, `Act(name, tick => { ... })` and
`Do(name, tick => Status.Running)`, plus `Invert`, `Succeed`, `Repeat`,
`Wait`, `Cooldown`, `Deadline` and `Parallel`. Give each agent a `Brain(tree)`
and call `brain.Tick(seconds, blackboard)` with the time since the last call.
Inside a leaf, `tick.Blackboard`, `tick.Read<T>(key)`, `tick.Write(key, value)`
and `tick.Seconds` are what the tree knows. `Read` gives `default` for a key
that is missing or of another type, so a missing `bool` reads as false. Nodes
hold no state, so one tree can be shared by many brains. Each brain keeps its
place in its own `Memory`, and `brain.Interrupt()` forgets it.

```csharp
using Orblit.Agent;

Node tree = new Selector([
    new Sequence([
        new Check("sees the hero", tick => tick.Read<bool>("seesHero")),
        new Act("chase", tick => tick.Write("mode", "chase")),
    ]),
    new Sequence([
        new Act("patrol", tick => tick.Write("mode", "patrol")),
        new Wait(2),
    ]),
]);

var brain = new Brain(tree);

// Once a frame, with the time since the last frame.
Status Think(double seconds, bool seesHero) =>
    brain.Tick(seconds, new Dictionary<string, object?> { ["seesHero"] = seesHero });
```

Worked example: https://orblitengine.com/docs/examples/something-that-chases-you/ and
`/guides/agents/`.

## Orblit.Pathing

A `Spline` is cubic Bezier segments read by distance in metres:
`Spline.Through(points, closed: true)`, `Spline.Line(points)` and
`Spline.Bezier([new PathKnot(position, tilt, handleIn, handleOut)])`, with
`Length`, `PositionAt(d)`, `FrameAt(d)` (a `PathFrame` of `Position`,
`Forward`, `Up` and `Right`) and `Closest(point)`. `Encode()` and
`Spline.Decode(text)` are the `.opath` file, and `PathNames.Extension` is its
extension. A bad file throws `PathFormatException`. A
`PathFollower(spline, speed, end: PathEnd.Loop)` rides one with
`Advance(seconds)`. A `Rail` slides along one towards a target with
`Track(target, seconds)`. A `Patrol([new PatrolStop(at, wait)])` is told
`Update(position, seconds)` and answers `Target`.

Routes: `new NavGrid(width, depth, cell)` with `BlockRect`, `BlockCircle` and
`FindPath(from, to)`, or `NavMesh.FromGrid(grid, radius)` with the same
`FindPath` for a big map. `NavBake.FromTerrain(terrain, cell, maxSlope)` opens
a cell where the cover's navigation bit is set, and the slope is in radians.
`NavBake.FromMesh(mesh)` and `NavBake.Carve(grid, mesh)` read triangles, where
`mesh` is a `MeshWalk` of `Positions`, `Indices`, `AgentHeight`, `StepHeight`
and `MaxSlope`. A `NavWalker(body, planner)` walks a `Steerable` along a
route with `GoTo(goal)` and `Update(seconds)`, and `HasRoute` is false when
there was none. Any method of the shape `IReadOnlyList<Vector3>? (Vector3 from,
Vector3 to)` is a `Planner`, so pass `mesh.FindPath`.

```csharp
using Orblit.Agent;
using Orblit.Pathing;
using Orblit.VectorMath;

// A 40 by 40 grid of 1 m cells with a wall across most of it.
var grid = new NavGrid(40, 40, 1);
grid.BlockRect(10, 0, 11, 30);
var mesh = NavMesh.FromGrid(grid, radius: 0.4);

var body = new Steerable(new Vector3(2, 0, 2));
var walker = new NavWalker(body, mesh.FindPath);
walker.GoTo(new Vector3(30, 0, 5));
if (!walker.HasRoute)
{
    // No way through, and the body is left standing.
}

// Once a frame.
void Walk(double seconds) => walker.Update(seconds);
```

Links: a `NavLink(from, to, kind, cost: 0, agents: [], gate: null, twoWay:
false)` is a jump, a ladder, a door or a teleporter that the ground does not
give. `new LinkPlanner(mesh.FindPath, links)` plans over the ground and the
links at once. `FindRoute(from, to, agent: "climber")` answers a list of
`WalkStep` (with `Points`) and `LinkStep` (with `Link`, `Reversed`, `Start`
and `End`), or null. The cost of a link is added to the metres walked. A link
with `agents` is open only to those kinds. A link with a `gate` is shut while
`Lock(gate)` is in force, and `Unlock(gate)` opens it. Ask again to plan round
the change. The planner moves nothing: the game plays the crossing by
`link.Kind`. A `NavWalker` follows points, not steps, so walk each `WalkStep`
with it. Links are not in a file.

```csharp
using Orblit.Pathing;
using Orblit.VectorMath;

var ladder = new NavLink(new Vector3(5, 0, 5), new Vector3(5, 4, 5), "ladder", cost: 3, agents: ["climber"], twoWay: true);
var door = new NavLink(new Vector3(12, 0, 5), new Vector3(14, 0, 5), "door", gate: "red");
var mesh = NavMesh.FromGrid(new NavGrid(40, 40, 1));
var planner = new LinkPlanner(mesh.FindPath, [ladder, door]);

planner.Lock("red");
var steps = planner.FindRoute(new Vector3(1, 0, 1), new Vector3(20, 4, 5), agent: "climber");
foreach (var step in steps ?? [])
{
    switch (step)
    {
        case WalkStep walk:
            // Hand walk.Points to a walker.
            break;
        case LinkStep link:
            // Play the crossing for link.Link.Kind, from link.Start to link.End.
            break;
    }
}
```

Doors and crates: `grid.AddObstacleRect(minX, minZ, maxX, maxZ)` and
`grid.AddObstacleCircle(x, z, radius)` shut cells and answer a `NavObstacle`.
`Lift()` opens them again, except cells a wall or another obstacle shuts. To
move one, lift it and add another. Give a walker `isClear: grid.IsClear` and
the planner of the same grid, and it checks the next `lookahead` legs (3 by
default) each `Update`, plans again from where it stands when one is shut, and
stands with its `Goal` kept when there is no way. A `NavMesh` and an
`Inflated` grid are snapshots and do not follow an obstacle.

A route is planned once unless the walker checks, a navmesh is a snapshot, a
cell has one floor, and agents do not avoid each other (use `Separate`).
Guide: https://orblitengine.com/docs/guides/paths/.

## Orblit.Audio

`new AudioPlayer(backend)` is what a game calls. A backend plays the numbers
it is given. `DeviceAudio.Open(options)` from `Orblit.AudioDevice` plays them
and throws `AudioDeviceException` when the machine has no output device. A
test uses `RecordedAudio`, which writes every call down in `Calls`, or
`DeviceAudio.Offline()`, which mixes real files with no device and hands the
samples back from `Render(frames)` as interleaved stereo floats.
`audio.Mixer.Add("music", under: parent)` makes a bus, and a bus has `Volume`,
`Muted` and `Gain`, the product of every bus above it. A bus the mixer does
not own makes `Play` throw. `audio.Play("clip", bus, loop, volume, pitch, at)`
returns a `Voice` with `Volume`, `Pitch`, `FadeTo(volume, seconds)` and
`Stop(fadeSeconds)`.

A sound with a place takes `at: new Emitter(position, falloff, cone, doppler:
1)`, where `Falloff(near, far, curve)` says how it fades with distance and
`Cone(inner, outer, outerGain)` how it fades off axis. Move it with
`emitter.MoveTo(position, seconds)` and the listener with
`audio.Listener.MoveTo(...)`. Velocity is the last two positions. Use
`Teleport` for a cut, or Doppler hears a speed.

```csharp
using Orblit.Audio;
using Orblit.AudioDevice;
using Orblit.VectorMath;

var audio = new AudioPlayer(DeviceAudio.Open(new DeviceAudioOptions { Directory = "sounds" }));
var music = audio.Mixer.Add("music");
audio.Play("theme.ogg", bus: music, loop: true, volume: 0.6);

var siren = new Emitter(new Vector3(30, 0, 0), new Falloff(near: 2, far: 60));
audio.Play("siren.ogg", loop: true, at: siren);

// Once a frame, with 0 when the game is paused.
void Sound(double seconds, Vector3 carAt, Vector3 playerAt)
{
    siren.MoveTo(carAt, seconds);
    audio.Listener.MoveTo(playerAt, seconds);
    audio.Advance(seconds);
}
```

Nothing reaches the backend until `audio.Advance(seconds)`, which also moves
fades on. Call it once a frame, with 0 when the game is paused. A clip that
ends is noticed on the next advance. `audio.Play(..., offset:)` begins that
many seconds into the clip. `DeviceAudioOptions.Directory` is the folder clips
are found under, and a file larger than `StreamAbove` bytes (1 MiB by default)
is read as it plays instead of decoded once.

`new Music(audio, tracks, moves)` takes tracks as
`{ name: new MusicTrack(clip, bpm, beatsPerBar) }` and moves as
`new MusicMove(to, from, when: MusicWhen.NextBar, fade)`. It loops one track
at a time. `Go(name)` takes the first move that fits, else cuts.
`Advance(seconds)` fires a change that waits for the beat. `Stinger(clip)`
plays over the top and turns the track down to `duck` while it lasts.
`new ContactSounds(audio, rules)` with a rule such as
`new ContactRule("wood", null, "thud")` has `Hit(a, b, strength, at)`, which
picks a clip by the two surface names and scales it by `strength` between
`quiet` and `loud`. A rule that names both surfaces wins over one that names
one. Feed it from physics touch events, using the event's force as the
strength. `CueBoard` plays the sounds a sequence says should be going.
`Update(cues, seeked)` starts each once. After a seek a clip in `beds` starts
part-way in and any other starts nothing.

In a scene, `SoundSourceComponent` (a `Clip`, `Bus`, `Loop`, `Volume`, `Near`,
`Far` and `Curve`) is heard from its entity, `SoundListenerComponent` marks
where sound is heard from, and `SoundSurfaceComponent(name)` names what an
entity is made of. `new SoundView(view, audio)` from `Orblit.Stage` plays
them. Call `sound.Advance(delta, step)` and then `audio.Advance(delta)` once a
frame, with the step `Cutscenes.Advance` returned or none. A looping source
plays while its entity is shown. One that does not loop plays on appearing,
and again on `sound.Play(id)`. A bus the mixer lacks throws, naming the
entity. `sound.SurfaceOf(id)` is the name to hand `ContactSounds`.

`DeviceAudio` plays WAV, FLAC, MP3 and Ogg Vorbis. It is tested on rendered
samples and has not been heard on a device.

Traps:

- A cue bound to an entity is heard from where the entity stood when it
  started. It does not follow the entity.
- Without a `SoundListenerComponent` the sound is heard from the camera,
  taken to have the world's up. A camera looking straight up hears no pan.
- Contact volume is linear in strength.

## Orblit.Collide

Shapes are positioned by their centre: `new Sphere(position, radius)`,
`new Box(position, size)`, `new Capsule(position, height, radius)`.
`Shapes.Contact(a, b)` gives a `Contact` (`Point`, `Normal`, `Depth`) or null,
and `Shapes.Overlaps(a, b)` is the cheaper yes or no. `new Ray(from,
direction, distance)` normalises its direction. `Queries.Raycast(ray, shape)`
gives a `Hit` (`Distance`, `Point`, `Normal`, `Inside`), and
`Queries.RaycastFirst(ray, shapes)` the nearest with its index.
`Broadphase` buckets many shapes, and `Layers` (what a body is, and what it
cares about) filters pairs. The 2D side is `Circle`, `Rectangle`,
`Hitboxes.Touching` and `World2`.

```csharp
using Orblit.Collide;
using Orblit.VectorMath;

// Which of `targets` a shot from `origin` along `direction` hits first.
int? ShotHits(Vector3 origin, Vector3 direction, IReadOnlyList<Shape> targets) =>
    Queries.RaycastFirst(new Ray(origin, direction, 50), targets)?.Index;

// Whether a player half a metre wide has reached a 1 m crate.
bool Reached(Vector3 player, Vector3 crate) =>
    Shapes.Overlaps(new Sphere(player, 0.5), new Box(crate, Vector3.All(1)));
```

## Orblit.Effect

A `TimedEffect` is asked what it looks like at a moment, `effect.At(seconds)`,
and answers a `Change` (`Move`, `Turn`, `Grow`, `Tint`, `Fade`). Tweens:
`MoveBy(offset, duration)`, `TurnBy(angle, duration)` (radians, about an
`axis` that is up by default), `GrowTo(factor, duration)`,
`TintTo(colour, duration)`, `FadeTo(opacity, duration)` and
`Shake(strength, duration)`, each with an `ease` from `Eases`. Combine with
`Then([...])`, `Both([...])`, `Again(inner, times)`, `OutAndBack(inner)`,
`After(delay, inner)` and `Wait(duration)`.

Apply a change with `Effects.Applied(transform, change)`, never by rotating a
`Quaternion` yourself, which turns the wrong way. `Applied` handles move, turn
and grow. Tint and fade are yours to multiply into a colour.

```csharp
using Orblit.Effect;
using Orblit.VectorMath;

// A hop with a half turn, then a small shake. Built once, sampled by time.
TimedEffect hop = new Then([
    new Both([
        new OutAndBack(new MoveBy(new Vector3(0, 1, 0), 0.2)),
        new TurnBy(Math.PI, 0.4),
    ]),
    new Shake(0.05, 0.3),
]);

// The transform `seconds` after the hop began.
Matrix4 Hopping(Matrix4 resting, double seconds) =>
    Effects.Applied(resting, hop.At(Math.Clamp(seconds, 0, hop.Duration)));
```

For effects started by events, a `Playing` holds several: `Start(effect)`,
`Advance(seconds)` (returns what finished), and `Change` for the sum. Finished
effects keep their end state until `Bake()` hands it over to fold into the
object's resting transform.

## Orblit.Sprite

Data only. `SpriteLayer` and `LayerSprite` in `Orblit.Filament` draw (see
[scene-api.md](scene-api.md)). `Atlas.Grid(...)` cuts an evenly spaced sheet
into regions named `<name>_0` upwards in reading order. `Atlas.Read(text)`
reads what TexturePacker and Aseprite write, and returns null if it can't.
`SpriteAnimation.From(atlas, prefix, fps: ...)` or
`SpriteAnimation.At(regions, fps: ...)` (default 12, `loop` true,
`pingPong` false) and `animation.At(seconds)` gives the `Region` to show.
`region.Uv(imageWidth, imageHeight)` gives the fractions a `LayerSprite`
takes, with `V0` at the top on both sides.

```csharp
using Orblit.Filament;
using Orblit.Sprite;

// A 256 by 64 sheet of four 64-pixel frames: run_0 to run_3.
var sheet = Atlas.Grid("hero.png", 256, 64, 64, 64, name: "run");
var run = SpriteAnimation.From(sheet, "run", fps: 10);

// The hero standing at (x, y) at `seconds` into the run.
LayerSprite Hero(double x, double y, double seconds)
{
    if (run.At(seconds) is not { } frame)
    {
        return new LayerSprite { X = x, Y = y };
    }

    var (u0, v0, u1, v1) = frame.Uv(sheet.Width, sheet.Height);
    return new LayerSprite
    {
        X = x,
        Y = y,
        PivotY = 0, // the feet, not the middle
        U0 = u0,
        V0 = v0,
        U1 = u1,
        V1 = v1,
    };
}
```

A packed atlas may have trimmed regions: place them with `OffsetX`,
`OffsetY` and `PlacedSize`. `AtlasPackOptions` has `AllowRotation` off by
default. Keep it off, since `LayerSprite` takes an upright rectangle.
`new Parallax(layers).At(camera, seconds)` gives each layer's offset.
`TileMap.Read` reads a Tiled JSON map, infinite ones too. `TileLayer.At`
leaves the flip bits off and `FlipsAt` returns them. `PlaceOf` gives where
a tile is drawn, anchored to the bottom left of its cell. `Frame` here is
also a name in `Orblit.Test` (see Name clashes). Guide:
`/guides/two-dimensions/`.

## Orblit.Ui

The interface is Avalonia. `Orblit.Ui.Avalonia` adds what a game wants on top
of it, and `Orblit.Ui` holds the part that needs no window.

`FlexPanel` is a row or a column that shares out its room the way a flex box
does. Set `Orientation`, `Gap`, `MainAxis` (`FlexMain`) and `CrossAxis`
(`FlexCross`), and mark a child with `FlexPanel.SetGrow(child, 1)` to have it
take what the others leave. Growing needs a bounded size to share. With none,
a child keeps its own size. `SetFillWidth` and `SetFillHeight` stretch a child
across.

```csharp
using Avalonia.Controls;
using Avalonia.Layout;
using Orblit.Ui.Avalonia;

var menu = new FlexPanel
{
    Orientation = Orientation.Vertical,
    Gap = 12,
    CrossAxis = FlexCross.Stretch,
};
var resume = new Button { Content = "Resume" };
FlexPanel.SetGrow(resume, 1);
menu.Children.Add(new TextBlock { Text = "Paused" });
menu.Children.Add(resume);
```

Worked example: https://orblitengine.com/docs/examples/a-menu-over-the-game/
and `/guides/interfaces/`.

For a touch screen, `TouchStick` raises `Tilted`, `TouchHold` raises `Held`
and `TouchSwipe` raises `Swiped`. Each wraps the control that is its face and
follows the first finger that lands on it alone, so a thumb on a stick and
another on a jump button do not compete. A stick's push and a swipe are up and
to the right positive. A swipe is in logical pixels, and a touch with no
movement is not a swipe. `TouchControls` in `Orblit.Input` takes them and
gives `FrameControls` its `touch` and `sticks`. The two packages never
reference each other.

```csharp
using Avalonia.Controls;
using Avalonia.Media;
using Orblit.Input;
using Orblit.Ui.Avalonia;
using Orblit.VectorMath;

var touch = new TouchControls();

var stick = new TouchStick(new Border { Width = 160, Height = 160 }, Brushes.White);
stick.Tilted += push => touch.MoveStick(PadStick.Left, new Vector2(push.X, push.Y));

var jump = new TouchHold(new Border { Width = 96, Height = 96 });
jump.Held += held => touch.Hold("jump", held);

var look = new TouchSwipe(new Border { Width = 320, Height = 320 });
look.Swiped += by => touch.Swipe(new Vector2(by.X, by.Y));

// Once a frame, after the game has read the controls.
FrameControls Read() => new(touch: touch.Held, sticks: touch.Sticks);
touch.EndFrame();
```

Name each element once. Two buttons called `jump` share one slot, so lifting
one finger releases the other. A push or a swipe that is not a finite number
is ignored.

For other languages, build a `UiStrings("en")` and fill it with
`Add("fr", table)` or `AddJson("fr", fileText)`. `strings.Use("fr")` changes
the language and raises `Changed`, which is how a live interface redraws.
`strings.Text("play")` answers in the language in use. `{name}` in a text is
filled from the arguments, and a whole `count` argument picks `play.one`,
`play.few` and so on by the language's own rule, with `play` itself as the last
resort. A key a language lacks falls back to its parent language (`fr-ca` to
`fr`), then to the default, then shows the key. `Find` answers null where
`Text` would show the key. `Gaps()` lists the keys each language has no text
for. `AddJson` reads one object of text and throws `FormatException` for
anything else.

```csharp
using Orblit.Ui;

var strings = new UiStrings("en");
strings.Add("en", new Dictionary<string, string>
{
    ["greeting"] = "Hello, {name}",
    ["apples.one"] = "{count} apple",
    ["apples.other"] = "{count} apples",
});
strings.Add("fr", new Dictionary<string, string>
{
    ["greeting"] = "Bonjour, {name}",
    ["apples.one"] = "{count} pomme",
    ["apples.other"] = "{count} pommes",
});

strings.Use("fr");
var hello = strings.Text("greeting", new Dictionary<string, object?> { ["name"] = "Ann" });
var count = strings.Text("apples", new Dictionary<string, object?> { ["count"] = 3L });
```

Pass `count` as a `long`, or as a `double` with no fraction. An `int` is not
a count, and picks no plural form. Nothing here lays out right to left.

`LabelAtlas` packs whole strings into one PNG: `Add`, `RectOf`, `Paint`. A
string keeps its rectangle, and the picture only grows in height. Set the
typeface, size and width in `LabelAtlasOptions`. `WebImages(http).LoadAsync(url)`
fetches a picture an interface names, reads only http and https, and returns
null for anything it can't fetch or decode.

`ControlPanel`, `WorldPanel` and `Nameplates` in `Orblit.Stage.Avalonia` put
real controls in the scene. `PanelHost` is the one hidden window they are
drawn in: make it after the application has started and keep it as long as
its panels. `host.Open(control, size, scale)` returns a `ControlPanel`, which
paints to PNG bytes and takes pointer events in its own pixels. It has no
hover and no keyboard focus, so a text box cannot be typed into. A
`WorldPanel` stands one in the scene on a flat face. Put its `Quad` and
`Material` in the scene, call `Sync()` each frame and put the scene together
again afterwards. `PointerRay.FromCamera(camera, point, viewSize)` turns a
click into a ray, and `Down`, `Move` and `Up` take it. Pass the events of one
pointer only. `Nameplates` draws two hundred names over two hundred heads from
one `LabelAtlas`, nearest first, fading between `FadeStart` and `FadeEnd`. It
has no test against walls, so a label behind one shows through it. Each new
picture adds a texture the renderer keeps until it is disposed, so change a
panel when somebody presses something and not every frame. See
`/guides/world-interfaces/`.

## Orblit.Cells

`new Grid(cellSize)` turns a point into a `Cell`, `new Cell(3, -1)`, with
`CellOf(x, z)`, and a cell back into its centre with `CenterOf`. `CellOf`
returns null for a point that is not finite or is over a billion cells from
the origin, since that is a physics blow-up and not a place.
`new CellWindow(grid, loadRadius: 2)` plans a frame:
`Plan(x, z, loaded: ..., pinned: ...)` returns a `CellPlan` whose `Load` is the
cells to bring in, nearest first, and whose `Drop` is the cells to let go,
farthest first. Distance is the largest of the two cell differences, so the
window is a square. A cell is kept while it is within `DropRadius`, one more
than `LoadRadius` unless set, which stops a point walking up and down a border
from loading and dropping the same cell every few frames. Pass `facingX` and
`facingZ` and cells behind count as farther (the window's `behindWeight`), so the ones ahead
load first. A pinned cell is wanted wherever it is and is never dropped. A point
that is not finite plans nothing.

```csharp
using Orblit.Cells;

var window = new CellWindow(new Grid(64), loadRadius: 2);
var loaded = new HashSet<Cell>();

CellPlan Frame(double x, double z, double facingX, double facingZ)
{
    var plan = window.Plan(x, z, facingX, facingZ, loaded);
    foreach (var cell in plan.Drop)
    {
        loaded.Remove(cell);
    }

    foreach (var cell in plan.Load)
    {
        loaded.Add(cell);
    }

    return plan;
}
```

`CellStreamer` in `Orblit.Scene` does the loading: it fetches a cell file,
reads it, and puts it in a scene as one diff with the cell on every id. Call
`Step(x, z)` once a frame. The "A world in cells" section of the skill has an
example. `orblit-net` has its own `Grid` for interest management.

## Orblit.Scene and Orblit.Stage

Scene files as documents, for games built in the editor.
`SceneDocument.Decode(text)` returns a `SceneLoad` with `Document` and
`Problems`. `new DocumentView(document, projectRoot: ..., materials: ...,
look: ...)` stages it. Draw `view.Scene` and change it with
`view.Apply(SceneDiff.Between(before, after))`, or `view.Replace(next)` to do
both. A `TransformComponent` built with only `position` resets rotation and
scale, so pass the ones you aren't changing. Guide: `/guides/scene-files/`.

```csharp
using Orblit.Scene;
using Orblit.Stage;

var load = SceneDocument.Decode(File.ReadAllText("scenes/level.oscene"));
foreach (var problem in load.Problems)
{
    Console.WriteLine(problem);
}

var view = new DocumentView(load.Document, projectRoot: "game");

// Later, with an edited copy of the document.
void Edit(SceneDocument edited) => view.Apply(SceneDiff.Between(view.Document, edited));
```

`Orblit.Scene` also holds the material document (`.omat`, `MaterialLibrary`,
`MaterialComponent`, its looks and its `Values` for a shader the author wrote,
see [assets.md](assets.md)) and the scene writers and readers:
`document.WriteAs(SceneFormat.Glb, ...)` and `SceneImport.ReadSceneFrom(...)`
for glTF, GLB and OBJ. Both report what they could not carry rather than
throwing. Details and the traps: [assets.md](assets.md).

`new DocumentView(document, profiler: profiler)` times its work as the spans
`scene build` and `diff`. It defaults to `Profiler.Off`, which records nothing.
`Profiler.Create()` records. Call `profiler.Frame()` once per frame and read
`profiler.Snapshot()`. Pass the resident size to `Create(heap: ...)`, for
instance `() => Environment.WorkingSet`, and set your own gauges with
`Gauge`. The renderer writes no spans of its own. Guide: `/guides/profiling/`.

## Orblit.Asset

Assets as bytes, wherever they come from. Cooking source files for a device
(`GltfImporter`, `AtlasImporter`, `RuntimeImport`, a content-hashed cache),
and fetching them over a network (`AssetOrigin`, `FetchPolicy`,
`AssetFetcher`, `NetworkAssetSource`, `HttpTransport`, and `MapTransport` for
tests). `fetcher.FetchInStagesAsync(id, standIn: ...)` gives a stand-in, then
a texture's own coarse mip levels while the rest arrives, then the whole file.
`GltfBake.BakeInstancesAsync` bakes copies of a glTF model at a list of
placements into one model, or returns null for a skin, an animation or
another model it cannot place. `StaticMerge` in `Orblit.Stage` takes it as a
`StaticBaker`. A kit is a pack for the target `source` that a person installs
into a project with `KitExtraction.ExtractAsync`. `KitStoreBuilder` turns a
folder of kit entries into the index files a kit browser reads,
`KitStoreClient` reads them, and `kits.lock` (`KitLock`) lists what an install
put in a project. Details: [assets.md](assets.md).

## Orblit.Sequence

A `CutSequence` is tracks of clips over a playhead. `SampleAt(seconds)` gives
a `SequenceFrame` of every value at that moment, with nothing remembered
between samples. Marks (events on an interval) come only from advancing a
`Director`, so scrubbing never fires them. `Advance(seconds)` returns the
frame and the marks it crossed, in order, however big the step, so a dropped
frame does not lose an event. `Seek` moves the playhead and fires nothing,
and a negative `Speed` plays backwards and fires nothing either.

```csharp
using Orblit.Sequence;

var score = new CutSequence(
    [new MarkTrack([new Mark(1, "open"), new Mark(2, "shut")])],
    duration: 5);
var director = new Director(score, WhenDone.Hold);
director.Play();

// Once a frame.
void Tick(double seconds)
{
    var step = director.Advance(seconds);
    foreach (var mark in step.Marks)
    {
        Console.WriteLine(mark.Name);
    }
}
```

## Orblit.Motion

Clips you own, as `.oclip` files the editor's timeline also writes. A
`ClipDocument` (`Name`, `Duration`, `WhenDone`, `Rate`, `Channels`, `Marks`,
`RootMotion`) holds `ClipChannel`s. Each names a `Target` entity id (empty for
whatever plays the clip), a `Bone` or null, and a `Property`
(`transform.position` or `light.power` on an entity; `position`, `rotation` or
`scale` on a bone). It has a `ChannelKind` (`Number`, `Vector`, `Rotation` or
`Flag`) and `Key<T>(at, value, hold: ...)`s. `Key<T>`, `Hold`, `Easing`,
`Mark` and `WhenDone` live in `Orblit.Sequence`. `clip.Encode()` writes the
file and `ClipDocument.Decode(text)` gives a `ClipLoad` (`Clip`, `Problems`).

```csharp
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.VectorMath;

var lift = new ClipChannel<Vector3>(
    "lamp",
    "transform.position",
    ChannelKind.Vector,
    [
        new Key<Vector3>(0, new Vector3(0, 1, 0)),
        new Key<Vector3>(2, new Vector3(0, 3, 0), hold: Hold.Linear),
    ]);
var rise = new ClipDocument("rise", duration: 2, whenDone: WhenDone.Loop, channels: [lift]);
```

`clip.SampleAt(seconds)` is a `ClipFrame`, with no state. A `ClipPlayer`
starts **paused**: call `Play()`, then `Advance(dt)` once a frame for a
`ClipStep` with the `Frame`, the `Marks` passed (every lap included) and the
root motion `Moved`. `Seek` fires no marks and moves nothing.

```csharp
using Orblit.Motion;
using Orblit.Scene;

var player = new ClipPlayer(clip);
player.Play();
var scope = ClipScope.InScene(view.Document, lamp);

// Once a frame.
void Tick(double seconds)
{
    var step = player.Advance(seconds);
    var ops = ClipScene.OpsFor(step.Frame, view.Document, scope);
    if (ops.Count > 0)
    {
        view.Apply(new SceneDiff(ops));
    }
}
```

`ClipScope.InScene` maps a prefab's ids onto the placed instance's, so one
clip plays on every copy. `ClipScope.WholeScene` takes the ids as they are.
For a model's skeleton, hand the frame's bones to the skin binding and leave
the object's `Animation` null. Build one `SkinBinding` per skin, once, and put
what `JointsFrom` answers into `RenderObject.Joints`. `PoseFrom` writes into an
`Orblit.Rig` `Pose` instead, for a rig to adjust.

```csharp
using Orblit.Filament;
using Orblit.Motion;
using Orblit.Stage;

var binding = new SkinBinding(SkinRig.ArmatureOf(skin), skin, index: 0);

// Every frame.
RenderObject Posed(RenderObject model, ClipFrame frame) =>
    frame.Bones.TryGetValue("", out var locals)
        ? model with { Joints = binding.JointsFrom(locals) }
        : model;
```

Root motion: `clip.CopyWith(rootMotion: new RootMotion(bone: "hips", turns: true))`,
then each frame turn the step by the heading, and turn the heading by the
step. Go through the matrix. Turning by the quaternion itself turns the other
way. On a physics character, divide the turned step by the tick and `Drive`
with it instead of adding it to a position ([physics.md](physics.md)).

```csharp
using Orblit.Motion;
using Orblit.VectorMath;

var walking = clip.CopyWith(rootMotion: new RootMotion(bone: "hips", turns: true));
var walker = new ClipPlayer(walking);
walker.Play();
var position = new Vector3(0, 0, 0);
var heading = Quaternion.Identity();

// Once a frame.
void Step(double seconds)
{
    var moved = walker.Advance(seconds).Moved;
    position += heading.AsRotationMatrix().Transformed(moved.Position);
    heading *= moved.Rotation;
    heading.Normalize();
}
```

`GltfImport.Clips(bytes)` gives `ClipsImported` (`Clips`, `Problems`), naming
bones the way the binding does. A scene file's `motion` component
(`MotionComponent`: `Clips`, `Autoplay`) is data only. Nothing plays it. A
`ClipPlayer` on its own cuts from clip to clip. A blend fades.

### Retargeting

`ClipRetarget.Of(clip, from, to)` makes a clip built for skeleton `from` move
skeleton `to`, and gives a `ClipRetargeted` (`Clip`, `Problems`). Each skeleton
is a `RestSkeleton`. `GltfImport.Skeletons(bytes)` reads one per skin, with
bones named as `GltfImport.Clips` names them. Or build one with
`new RestSkeleton(names, parents, local, above)`, parents before children.
Bone channels hold absolute local values, so a clip means something only
beside the skeleton it was made for. `skeleton.PosedOrigins(locals)` answers
where each joint stands for a map of bone name to `BoneLocal`, and a bone the
map lacks stays at rest. `PosedWorlds(locals)` answers the same as matrices,
turn and scale included.

`LimbReach.Of(skeleton, locals, root, mid, tip, target, pole)` works out the
two rotations that put the tip of a three joint limb, such as a hip, a knee
and an ankle, on `target` in the model's space. It answers a `LimbReach`
(`Root`, `Mid`, `End`, `Reachable`), where `Root` and `Mid` are local
rotations a clip can key and `End` is where the tip lands. Neither bone is
stretched, so a target past the reach leaves the limb straight and
`Reachable` false. Without a `pole` the joint keeps bending the way it does
now, and a straight limb bends the way the rest pose does. It throws
`ArgumentException` for a name that is not a bone or three bones that are not
a chain.

```csharp
using Orblit.Motion;
using Orblit.VectorMath;

var reach = LimbReach.Of(
    skeleton,
    locals,
    "thigh.L",
    "shin.L",
    "foot.L",
    target: new Vector3(0.1, 0, 0.3),
    pole: new Vector3(0, 0, 1));
var legReaches = reach.Reachable;
```

Each bone of `to` is turned to stand in the world as the bone driving it did,
measured from where each rests. Keys, easing and cubic slopes come across as
they were when a bone and its parent line up with their drivers. Where they
do not, because `to` has fewer spine bones or a parent nothing drives, the
turn is worked out at every key of the bones involved and joined with
straight lines. Positions are the change from rest, made larger by the ratio
of the skeletons' `Reach`. Root motion follows its bone, and its `Up` is
turned into the frame of `to`. Entity channels, marks, length and rate are
kept.

`BoneNames.Match(from, to)` pairs bones by name and forgives a namespace such
as `mixamorig:`, a `DEF-` prefix and `LeftHand` against `hand.L`. A name two
bones share once cleaned is left out, not guessed. `bones:` adds a pair or
overrides one, target bone first, as in `bones: new Dictionary<string, string>
{ ["hand.L"] = "LeftHand" }`. It throws `ArgumentException` for a name that is
not in the skeleton it belongs to. Read `Problems`: a channel for a bone
`from` lacks, a bone of `from` that nothing in `to` takes, and root motion
with no bone to carry it are each left out and named there.

`humanoid: true` also pairs bones by the part of a person each is, which
carries a clip between rigs that share no names. `HumanoidMap.Of(skeleton)`
finds those parts (`HumanoidBone`, 22 slots) from how the bones hang and where
they stand: two legs that reach the floor, a spine that reaches the head, two
arms off its top. Names are read only to tell left from right, and where they
say nothing the left is `up × forward`, +X for a glTF rig. Pass `up` and
`forward` for another. `HumanoidMap.Match(from, to)` pairs two skeletons slot
for slot, only the slots both have. Order is names, then the body, then
`bones`. A skeleton that is not a person says why in `Problems` and is not
paired by body. A name for an end site that has none of `end`, `top`, `tip`,
`nub` or `site` in it is read as a bone.

`fixRest: true` first turns each driven bone to point the way its driver does
at rest, so an A-pose target follows a T-pose source by where limbs point. A
bone this changes gets a key even when the source never moves it. A bone with
more than one driven bone below it is left alone, and so is one pointing more
than 134 degrees from its driver, which `Problems` names.

`unpaired: new HashSet<string> { "tail" }` leaves bones of `to` that nothing
moves, even where a name would pair them. It throws `ArgumentException` for a
bone `to` lacks and for one also named in `bones`. A `BoneMap` keeps `Bones`,
`Unpaired`, `Humanoid` and `FixRest` as one `.obonemap` file with `FromAsset`
and `ToAsset`, which are only labels. `Encode()` sorts it so two maps diff as
the pairs that changed. `BoneMap.Decode(text)` answers a `BoneMapLoad` (`Map`,
`Problems`) and throws `BoneMapFormatException` on a file that is not a bone
map or is a newer one. `map.Apply(clip, from, to)` is `ClipRetarget.Of` with
those arguments. `WithPair(bone, source)`, `WithUnpaired(bone)` and
`WithAuto(bone)` answer a new map with that one bone changed, and a bone is
never in both sets. `map.Stale(from, to)` names what the map says that either
skeleton lacks, which makes `Apply` throw, and `map.Fitted(from, to)` drops
it. `map.Pairing(from, to)` answers `Drivers`, target bone name to source bone
name, and `Problems`, which is what `Apply` would carry.

```csharp
using Orblit.Motion;

var map = new BoneMap("Mixamo", "Hero", humanoid: true, fixRest: true)
    .WithPair("hand.L", "LeftHand")
    .WithUnpaired("tail");
File.WriteAllText("hero.obonemap", map.Encode());

var loaded = BoneMap.Decode(File.ReadAllText("hero.obonemap"));
var onHero = loaded.Map.Apply(clip, sourceSkeleton, targetSkeleton);
```

`new LiveRetarget(from, to, bones, unpaired, humanoid, fixRest)` works the same
turns out a frame at a time. `Follow(source)` takes a map of bone name to
`BoneLocal` for the source, as `ClipFrame.Bones[""]` holds, and answers one
for the target that `SkinBinding.PoseFrom` and `JointsFrom` take. A baked clip
and a live frame agree at its keys. `PointOf(bone, source)` is where a source
bone is in the target's own size, assuming both stand with their origin at the
floor. `Pinned(bone, source: () => frame)` is that as a `TwoBoneIk` target,
null while `source` answers null.

`KeyReducer.Reduce(clip, angle, distance)` drops the keys a bake does not
need. Only channels of straight lines and no cuts are thinned.

```csharp
using Orblit.Motion;

ClipRetargeted WalkOnto(byte[] animations, byte[] model)
{
    var source = GltfImport.Skeletons(animations)[0];
    var target = GltfImport.Skeletons(model)[0];
    return ClipRetarget.Of(GltfImport.Clips(animations).Clips[0], source, target);
}

var live = new LiveRetarget(sourceSkeleton, targetSkeleton, humanoid: true);
var targetLocals = live.Follow(clip.SampleAt(0.5).Bones[""]);
```

### Blends

A `BlendDocument` (`.oblend`, `Encode()` and `BlendDocument.Decode(text)` for
a `BlendLoad`) is a graph of `BlendState`s, each playing a `BlendSource`. That
is a `BlendClip(name)`, a `BlendLine(input, [new LinePoint(at, source)])`
mixing the two points either side of the input's value, or a
`BlendPlane(x, y, [new PlanePoint(x, y, source)])` across two inputs. A
point's source can itself be a line or a plane. `BlendChange`s move between
states: `from` (null for anywhere), `to`, `when`, `fade` in seconds, `shape`,
and `inStep`, which starts the state entered as far through its lap as the
one left was, so a walk turning into a strafe keeps its feet. A
`BlendCondition` is `Above`, `Below`, `On` (not nought), `Through(laps)` of
the state being left, or `AllOf`, `AnyOf` and `Not` of others. Clips are
named, not held. The blend is shared, and each character hands over its own
clips by those names.

```csharp
using Orblit.Motion;
using Orblit.Sequence;

var moves = new BlendDocument(
    "Moves",
    [
        new BlendState("idle", new BlendClip("idle")),
        new BlendState(
            "move",
            new BlendLine("speed", [new LinePoint(1.4, new BlendClip("walk")), new LinePoint(4, new BlendClip("run"))])),
        new BlendState("jump", new BlendClip("jump"), whenDone: WhenDone.Hold),
    ],
    inputs: new Dictionary<string, double> { ["speed"] = 0 },
    changes:
    [
        // First, so it wins over the rest: the list is the precedence.
        new BlendChange("jump", when: BlendCondition.On("jump"), fade: 0.1),
        new BlendChange("idle", from: "jump", when: BlendCondition.Through(1), fade: 0.2),
        new BlendChange("move", from: "idle", when: BlendCondition.Above("speed", 0.1), fade: 0.2),
        new BlendChange("idle", from: "move", when: BlendCondition.Below("speed", 0.1), fade: 0.3),
    ]);

var blender = new BlendPlayer(moves, clips);

BlendStep Tick(double speed, bool jumped, double dt)
{
    blender.Inputs["speed"] = speed;
    blender.Inputs["jump"] = jumped ? 1 : 0;
    return blender.Advance(dt);
}
```

`new BlendPlayer(blend, clips)` holds the `Inputs` and the `Place`.
`Advance(dt)` gives a `BlendStep` with the `Frame` (applied exactly as a
clip's, through `ClipScene.OpsFor` or a skin binding), the `Marks`, the root
motion `Moved`, the new `Place` and the `Change` taken, if any. `Sample()`
poses without moving. `Enter(state, fade: ...)` forces a state. `Travel(state)`
follows a shortest directed route, overriding conditions and waiting for each
fade. `BlendGraph(document)` nests a graph in a state. Child leaves are named
`parent/child`, and entering a parent enters its start. Nested inputs share
names. Outer defaults and outer changes take precedence. Graphs belong
directly in states, not in line or plane points.

`new BlendState(name, plays, sync: ["left", "right"])` maps a cyclic lap to
named contacts in each clip. Each contact must occur once, in the declared
cyclic order. Missing, repeated or misordered contacts fall back to ordinary
laps. `new BlendFade(fromClip, toClip, seconds)` in
`new BlendDocument(..., fades: [...])` overrides a transition's fade for the
dominant ordered pair. `fromPose: true` on `BlendChange` or `player.Enter`
freezes the current visible pose.

A `BlendDocument` never changes. `blend.CopyWith(states: ..., changes: ...)`
answers another one and throws `ArgumentException` when a change names a state
that is gone. `WrittenStates`, `WrittenChanges`, `WrittenFades` and
`WrittenStart` are the lists as a file writes them, with a nested graph as one
state, which is what an editor changes. `States` and `Changes` are the
expanded ones a player reads. `BlendState.CopyWith` and `BlendChange.CopyWith`
change one part of a state or a change. `clearWhenDone` hands each clip back
its own ending, and `clearFrom` makes a change one from anywhere.

The `BlendPlace` is everything a blend remembers: the state, the `Lap` (how
many times through it, so 2.5 is halfway through the third), and what is
fading out (`From`, `Faded`, `Fade`, `Shape`), nested up to
`BlendPlace.Deepest` (4) with the oldest dropped. Saving is `place.ToJson()`
and `BlendPlace.FromJson(json, blend)`, which answers null for what is not a
place. Set `player.Place` to restore. For `orblit-net`, which moves component
columns, register `world.RegisterComponent("BlendPlace", ComponentKind.Float64,
BlendPlace.Width)` and write `place.ToNumbers(blend)` into it.
`BlendPlace.FromNumbers(blend, numbers)` reads it back. The numbers name
states by their index, so both ends need the same blend, and inputs travel
separately. That fixed column covers places without a frozen pose or route.
Extended places append the length and the UTF-8 bytes of JSON metadata as
numbers, so `ToNumbers` is then longer than `Width`. Use JSON or a
variable-length transport for them.

```csharp
using Orblit.Core;
using Orblit.Motion;

var column = world.RegisterComponent("BlendPlace", ComponentKind.Float64, BlendPlace.Width);
var avatar = world.CreateEntity();
world.Add<double>(avatar, column, new BlendPlace("move", lap: 0.5).ToNumbers(moves));
```

A test needs no clips and no frames. Build a place by hand and ask
`blend.ChangeFor(place, inputs)`, `blend.WeightsAt(place, inputs)` or
`blend.Advance(place, inputs, 0, clips: new Dictionary<string, ClipDocument>())`.

```csharp
using Orblit.Motion;

var place = new BlendPlace("idle", lap: 1);
var inputs = new Dictionary<string, double> { ["speed"] = 3 };
var change = moves.ChangeFor(place, inputs);
var weights = moves.WeightsAt(place, inputs);
```

Traps:

- One change per step, the first in the list whose condition holds. An
  interruption from anywhere must come before the ordinary changes, or an
  earlier one takes the step. A change from anywhere never re-enters the
  state already playing. Name a change from a state to itself to restart it.
- `Through(n)` counts laps of the state being left. A state played once, a
  jump or a landing, wants `whenDone: WhenDone.Hold` as well as its change on
  `Through(1)`. A looping clip starts again while it fades out.
- Marks come only from the loudest clip of the state being entered or played,
  never from what is fading out, so a walk mixed with a run takes one
  footstep, not two.
- A plane's two inputs want comparable units. A plane across metres a second
  on one side and radians on the other shares badly.
- A clip the player was not handed is left out without a word, and the rest
  share its say. Check `blend.ClipNames` against the clips.
- Pass `rest: FrameLayers.RestOf(new Dictionary<string, RestSkeleton> { [""] = skeleton })`
  to `BlendPlayer` so a channel that disappears fades to rest. Without rest,
  sparse channels keep their old behaviour: only clips that author a channel
  share it.
- A state's `speed` is never negative. A walk backwards is its own clip.

Guide: `/guides/animation/`.

### Layers and additive poses

`new BoneMask(new Dictionary<string, double> { ["arm"] = 1 }, target: "")`
selects named bones. `BoneMask.Below(rest, "spine")` includes a root and every
descendant. Weights lie between 0 and 1. Unnamed bones and other targets have
no influence.

`new ClipLayer(clip, mask: mask, fadeIn: 0.1, fadeOut: 0.1, influence: 1)` is
a reusable one-shot. Start it with `new LayerPlace()`. Advance the clock with
`layer.Advance(place, dt)` and store `step.Place`. It reports `Marks` and
`Finished`. Compose it with `layer.SampleAt(baseFrame, place, rest)`. The
graph keeps advancing and provides all character root motion. Multiple layers
compose in the order you apply them. Save each `LayerPlace.ToJson()` beside
the graph place and restore with `LayerPlace.FromJson(raw)`.

```csharp
using Orblit.Motion;

var restFrame = FrameLayers.RestOf(new Dictionary<string, RestSkeleton> { [""] = skeleton });
var upperBody = BoneMask.Below(skeleton, "spine");
var wave = new ClipLayer(waveClip, mask: upperBody, fadeIn: 0.1, fadeOut: 0.2);
var layerPlace = new LayerPlace();

// Every frame, after the graph.
ClipFrame Over(ClipFrame graphFrame, double seconds)
{
    var step = wave.Advance(layerPlace, seconds);
    layerPlace = step.Place;
    return wave.SampleAt(graphFrame, layerPlace, restFrame);
}
```

`FrameLayers.Layer(baseFrame, overlay, rest, mask: mask, weight: 0.5)`
overrides only channels the overlay authors. `FrameLayers.Add` adds
translation and local rotation relative to `reference`, which defaults to
rest. Scales use a ratio, and a zero reference scale uses an offset.
`new ClipLayer(clip, additive: true)` does the same over its fade envelope. A
mask affects bones only. Unmasked additive layers also add numeric, vector and
quaternion entity properties, and flags stay at the base value.
`FrameLayers.Complete(frame, rest)` fills all missing channels.

### Cutscenes

A `CutsceneDocument` (`.ocutscene`, `CutsceneDocument.Extension`) is the scene
moving while it is watched through cameras that take turns. Its `Motion` is a
`ClipDocument` whose targets are the scene's own ids, played with
`ClipScope.WholeScene`. It keys no bones and has no root motion. Its `Shots`
are `new CutsceneShot("eye", start: 0, duration: 3)`, naming the camera by its
path, and two that overlap blend over the overlap. Its `Sounds` are
`new CutsceneSound("sounds/swell.ogg", start: 0, duration: 4)`. `Encode()`
writes the file and `CutsceneDocument.Decode(text)` gives a `CutsceneLoad`
(`Cutscene`, `Problems`). `cutscene.Sequence` holds the shots, sounds and marks
for a `Director`, and `ShotsAt(seconds)` gives each `ShotAt` (`Camera`,
`Weight`).

`Cutscenes(view, cutscenes)` in `Orblit.Stage` plays them on a `DocumentView`,
one at a time:

```csharp
using Orblit.Motion;
using Orblit.Sequence;
using Orblit.Stage;

var intro = new CutsceneDocument(
    new ClipDocument("intro", duration: 4),
    [new CutsceneShot("eye", start: 0, duration: 3), new CutsceneShot("wide", start: 2, duration: 2)],
    [new CutsceneSound("sounds/swell.ogg", start: 0, duration: 4)]);
var cutscenes = new Cutscenes(view, [intro]);

// The marks a clip or a blend passed this frame. One named after a cutscene
// starts it.
void Heard(IReadOnlyList<Mark> marks) => cutscenes.StartFrom(marks);

void Tick(double seconds)
{
    var step = cutscenes.Advance(seconds);
    foreach (var sound in step.Sounds)
    {
        // Play sound.Sound from sound.At seconds in, if it is not playing.
    }

    if (step.Ended)
    {
        // Hand the player their controls back.
    }
}
```

`Start(name)` starts one by name, and `Playing` names the one running, or null.
Each `Advance(dt)` keys the scene and sets `view.Through` to the shots'
camera, blended where two overlap, and returns a `CutsceneStep` (`Marks`,
`Sounds`, `Ended`). At the end `Through` is null again, so the view looks
through the scene's own camera. `Stop()` ends one as though it had reached
its end. `WhenDone.Hold` on the cutscene's `Motion`, the default, leaves the
scene where the cutscene put it, and `WhenDone.Release` puts back what it
moved.

Traps:

- The scene's own camera is the first shown camera entity. Hide the
  cutscene's cameras, or the first of them becomes the game's camera.
  `view.CameraOf(id)` finds a camera shown or not.
- Nothing is looked through before the first shot, in a gap between shots or
  at the very end. There the view falls back to the scene's own camera.
- The step says what should be playing and how far in. `SoundView.Advance`
  plays it, and a game that does not use `SoundView` plays it itself.
- A cutscene runs on the scene, not on a copy. Keep the game's own animators
  off what it keys while it runs.

## Orblit.Rig modifiers

A `ModifierStack` adjusts a pose after the clip has written it and before the
skin reads it. It needs no window and no renderer. The frame flow is these
four steps, every frame:

```csharp
using Orblit.Filament;
using Orblit.Motion;
using Orblit.Rig;

// Once a frame. `binding` is the model's SkinBinding and `pose` its Pose.
List<JointPose> Posed(ClipFrame frame, double dt)
{
    if (frame.Bones.TryGetValue("", out var locals))
    {
        binding.PoseFrom(locals, pose);
    }

    pose.Evaluate();
    stack.Apply(pose, new ModifierFrame(dt));
    return binding.JointsFor(pose);
}
```

Evaluate once, then `Apply`. `Apply` does not evaluate again, because a second
`Evaluate` solves every `IkChain` again and moves a partly blended one.
`Pose.Refresh(from)` is the version that gives the same answer however often
it runs, and the stack calls it after each step.

```csharp
using Orblit.Rig;
using Orblit.VectorMath;

var stack = new ModifierStack();
stack.Add("feet", new FootPlacement(
    [new FootLeg("thigh.L", "shin.L", foot: "foot.L"), new FootLeg("thigh.R", "shin.R", foot: "foot.R")],
    Probe,
    pelvis: "hips"));
stack.Add("look", new LookAt("head", (pose, frame) => aim, limit: 1.0, ease: 0.15));
stack.Add("hair", new SpringBones(["hair.0", "hair.1"]), influence: 1);
```

- Order is the order added. Each step reads what the one before left. Put
  feet first, aiming next, limits after what they limit, springs last.
  `Add` throws `ArgumentException` for a name already in the stack.
- A target is a `ModelPoint`, a function from the `Pose` and the
  `ModifierFrame` to a `Vector3?` in the **model's** space. Null leaves the
  bone alone. `frame.ToModel(worldPoint)` converts. `new ModifierFrame(dt,
  modelToWorld)` is how a modifier knows where the model stands, so give it
  the character's matrix. A matrix that squashes space flat throws
  `ArgumentException`.
- `slot.Influence` (0 to 1), `slot.Enabled` and `influenceProperty` (a pose
  property) blend the step. `Add` returns the slot, and `stack["feet"]` finds
  it again. A modifier never blends itself. Zero or disabled resets the
  modifier's memory.
- `dt` is seconds. Springs, easing and `SpeedLimit` move nothing at `dt: 0`.
  The first frame passes as animated. After a teleport call `stack.Reset()`.
- A `Modifier` is also yours to write. Override `Solve(pose, frame)` and
  write the bones at full strength. Override `Check(armature)` to throw
  `ArmatureException` for a bone it needs and lacks, which the stack calls
  before the first solve, and `Reset()` to forget what it remembers. A
  modifier gives the same pose when it runs twice on the same input.

| Step | Does |
| --- | --- |
| `TwoBoneIk(root, mid, target, pole, stretch)` | Reaches a limb for a point. Without a `pole` the joint keeps the clip's bend. `mid` must be a child of `root` |
| `ChainIk(root, tip, target, iterations)` | FABRIK over any number of bones |
| `SplineIk(root, tip, points, detail)` | Lays a chain along a Catmull-Rom curve, bone lengths kept |
| `LookAt(bone, target, axis, limit, ease)` | Faces a point. The limit is on the added turn. Exponential ease |
| `Constrain(bone, constraint)` | Runs a `BoneConstraint` as a step. A constraint with its own influence is refused. Use the slot |
| `ConeLimit(bone, swing, twist)`, `HingeLimit(bone, min, max, axis)` | Keeps a joint inside a range |
| `SpeedLimit(bones, turnSpeed, moveSpeed)` | Caps radians a second and units a second |
| `TwistSpread(source, bones, shares)` | Shares a roll along forearm bones |
| `BoneAttachment(bone, offset)` | Reads where a socket is as a `Matrix4`: `Model` and `World`. Writes nothing |
| `SpringBones(bones, stiffness, drag, gravity, margin, colliders, snapDistance)` | World-space swing for hair, tails and cloaks. List bones parents first. Colliders are `SphereCollider`, `CapsuleCollider` and `PlaneCollider` |
| `FootPlacement(legs, probe, pelvis, up, rise, depth, ease, maxTilt, airborne)` | Stands feet on what `probe(from, to)` finds, in world space |

`LookAt` takes the bone's own axis that points at the target, a `TrackAxis`
(`Y` by default). A `FootLeg` names the thigh, the shin (its tail is the
ankle), an optional foot bone, `ankleHeight` and `soleAxis` (`NegativeZ` by
default, for a foot bone that lies along the ground with no roll).
`FootPlacement` snaps on its first frame and eases after, and it lowers the
pelvis by the most a foot had to go down.

`GroundProbe` is a function from two world points to a `GroundHit?`, which
holds a `Point` and a `Normal` in the world. The rig does not know what the
ground is made of. With `Orblit.Physics` it is one cast, and the character's
own body is ignored:

```csharp
using Orblit.Physics;
using Orblit.Rig;
using Orblit.VectorMath;

GroundHit? Probe(Vector3 start, Vector3 end)
{
    var line = end - start;
    var hit = physics.Cast(
        [start.X, start.Y, start.Z],
        [line.X, line.Y, line.Z],
        line.Length,
        ignore: playerBody);
    return hit is null
        ? null
        : new GroundHit(
            new Vector3(hit.At[0], hit.At[1], hit.At[2]),
            new Vector3(hit.Normal[0], hit.Normal[1], hit.Normal[2]));
}
```

Traps:

- A target that is a world point must go through `frame.ToModel`. A world
  point read as a model point aims at the wrong place for any model that is
  not standing at the origin.
- `SpringBones` colliders are in the model's space, or a bone's when `bone`
  is set. The simulation itself runs in the world, so a character that runs
  and stops leaves its hair swinging on.
- A head that moves further than `snapDistance` in one frame is taken to have
  teleported, and the springs start again from the animation.
- Limits go after what they limit. A limit that runs first is undone by the
  next step that turns the bone.
- Nothing in a scene file or the editor refers to a `ModifierStack` yet. It
  is code you wire up.

## Orblit.Terrain

Ground as data. `Terrain` is a grid of heights, one every `spacing` metres,
kept in square regions of `regionSize` texels (a power of two) that exist only
where there is ground. The package needs no window and no renderer.
`AddRegion(new RegionKey(x, z))` makes a flat one, `FillHeights(key, (x, z) =>
height)` fills one from world positions, `RegionAt(key)` finds one and
`PutRegion` adds a decoded one. Texel `(i, j)` of region `(x, z)` is at
`((x * regionSize + i) * spacing, (z * regionSize + j) * spacing)`.

```csharp
using Orblit.Terrain;
using Orblit.VectorMath;

var terrain = new Terrain(
    regionSize: 64,
    spacing: 1,
    sets:
    [
        new TerrainSet("grass", "textures/grass.png", "textures/grass_n.png"),
        new TerrainSet("rock", "textures/rock.png", "textures/rock_n.png", tileSize: 6, triplanar: true),
    ],
    autoCover: new AutoCover(steep: 1, flat: 0));

var region = terrain.AddRegion(new RegionKey(0, 0));
terrain.FillHeights(region.Key, (x, z) => 4 * Math.Sin(x / 10) * Math.Cos(z / 10));
region.SetCover(10, 10, Cover.Of(@base: 1, overlay: 0, blend: 0.5));
region.SetColour(10, 10, GroundColour.Of(200, 180, 160, roughness: 0.2));

double? height = terrain.HeightAt(12.5, 20.25);
Vector3? normal = terrain.NormalAt(12.5, 20.25);
```

A `TerrainRegion` holds three maps: `Heights` (`float[]`), `Cover` (`uint[]`)
and `Colour` (`byte[]`, RGBA), written through `SetHeight`, `SetCover` and
`SetColour(i, j, value)`. Each setter moves the region's `Revision`. Writing
the arrays directly does not, so call `Touch()` after that.

- `Cover.Of(@base, overlay, blend, angle, scale, hole, navigation,
  automatic)` is one 32-bit word. It holds two sets (0 to 31), how much of
  the overlay shows (0 to 1), a turn in sixteenths, a scale step
  (`Cover.Scales`: 0, 20, 40, 60, 80, −60, −40, −20 percent), a hole, a
  navigation mark the renderer ignores, and `automatic`, which ignores the
  sets named and chooses by slope and height. `WithHole`, `WithAngle` and the
  like change one part. A set or a step out of range wraps into its own bits,
  and `blend` is clamped. `@base` needs the `@`. A new region is all
  `Cover.Auto`.
- `GroundColour.Of(red, green, blue, roughness)` tints what the sets draw and
  nudges roughness (−1 to 1). `GroundColour.None` changes nothing.
- `TerrainSet(name, albedo, normal, tileSize = 4, triplanar = false)` names
  its pictures by path. The albedo's alpha is a height, and the taller of two
  blending sets shows through (`BlendSharpness`, 0 a fade, 1 a hard edge). The
  normal map's alpha is roughness. `tileSize` is metres per copy. `triplanar`
  stops cliffs stretching, at three reads instead of one.
- `AutoCover(steep = 0, flat = 1, slope = 1, heightFalloff = 0.1)`: at
  `slope` 1, ground tilted 60° is all `steep`, and at 2, 41°.
  `heightFalloff` is per hundred metres.

`HeightAt(x, z)` splits each square of four texels into the same two
triangles as the mesh, so what it says is what is drawn. It is null off the
regions and on a triangle with a hole or a missing region at any corner.
`NormalAt(x, z)` is smoother. It blends the slopes of the four nearest texels,
so it turns across a triangle and is not the flat face's normal. It is null
only where none of the four texels exists, and a hole makes no difference to
it. `CoverAt(x, z)` gives the cover of the nearest texel as stored. For
collision, `TerrainPhysics` in `Orblit.Physics.Terrain` lays the same triangles
as height fields in a physics world ([physics.md](physics.md#ground)).
`Orblit.Terrain` itself knows nothing of physics.

Files: `terrain.Encode()` is the `.oterrain` text, and `region.Encode()` is a
region's `.oregion` bytes, named `region.Key.FileName` (`x0_z-1.oregion`).
`Terrain.Decode(text)` gives a `TerrainLoad` (`Terrain`, `Regions` to fetch,
`Problems`), and `TerrainRegion.Decode(bytes)` a `RegionLoad` (`Region`,
`Problems`). The parts are read leniently, with a note in `Problems` for what
was dropped. The whole is read strictly. A file that is not a terrain, is from
a newer Orblit or has a map of the wrong length throws `TerrainFormatException`
or `RegionFormatException`. Neither takes a path, so the same calls read from a
folder, a pack or a socket.

```csharp
using Orblit.Terrain;

File.WriteAllText("world.oterrain", terrain.Encode());
foreach (var region in terrain.Regions)
{
    File.WriteAllBytes(region.Key.FileName, region.Encode());
}

var load = Terrain.Decode(File.ReadAllText("world.oterrain"));
foreach (var key in load.Regions)
{
    var read = TerrainRegion.Decode(File.ReadAllBytes(key.FileName));
    load.Terrain.PutRegion(read.Region);
}
```

Brushes: `TerrainStroke(terrain, tool, brush, invert, set, colour, roughness,
height, seed, tileSize)` is one press, and everything after `tool` is called
by name. `MoveTo(x, z)` lays a dab every `brush.Spacing * brush.Size` metres
along the way from the last point, writes the maps, touches the regions and
returns a `TerrainPatch`. `BrushTool` is `Raise`, `Lower`, `Smooth`,
`Flatten` (to `height`, or where the stroke began), `Slope` (a ramp from the
start to the farthest point reached), `Cover` (lays set `set`), `Colour`,
`Roughness` (−1 to 1) and `Hole`. `invert` runs raise, lower, cover, colour,
roughness and hole backwards. `Brush(size = 16, strength = 0.5, falloff = 0.5,
jitter = 0, spacing = 0.25)` is shared by every tool. Strength is per pass,
not per dab. `seed` repeats a stroke's jitter.

```csharp
using Orblit.Terrain;
using Orblit.VectorMath;

// `origin` and `direction` are a ray from the camera through the pointer.
if (terrain.Raycast(origin, direction) is { } hit)
{
    var stroke = new TerrainStroke(terrain, BrushTool.Raise, new Brush(size: 12, strength: 0.6), seed: 1);
    var edit = stroke.MoveTo(hit.X, hit.Z)
        .FollowedBy(stroke.MoveTo(hit.X + 3, hit.Z));

    edit.Revert(terrain); // undo
    edit.Apply(terrain);  // redo
}
```

A patch keeps the 32-texel tiles a stroke touched, before and after, for the
one map its tool writes. `FollowedBy` folds a stroke's patches into one, and
`Apply` and `Revert` write it back and answer which regions changed.
`TerrainRecorder(terrain, layers)` makes a patch for any other edit: call
`Keep(i0, j0, i1, j1)` on the texels before changing them, then `Finish()`.
Ground with no region is left alone. `terrain.Raycast(origin, direction,
maxDistance)` is where a ray first meets the drawn surface, for putting a brush
under a pointer. It steps half a texel at a time, so a ridge thinner than that
can be missed at a glancing angle.

In a scene: `TerrainComponent(file, castShadows = true, receiveShadows =
true)` in `Orblit.Scene` (`SceneComponents.Terrain`, JSON key `terrain`) names
the `.oterrain` by project path. The ground sits where its texels say,
whatever the entity's transform. `DocumentView` does not draw it yet.

Drawing: `TerrainView.From(terrain, key, options)` in `Orblit.Stage` gives the
`RenderTerrain` that goes in `RenderScene.Terrain`. `TerrainViewOptions` has
`Pixels`, which returns the decoded RGBA rows for a path (square, and every
picture the same size), `PicturesRevision`, `MeshSize` (64), `Levels` (6),
`CastShadows` and `ReceiveShadows`. A picture `Pixels` can't supply draws
plain.

```csharp
using Orblit.Filament;
using Orblit.Stage;

// `pixels` maps a set's path to its decoded RGBA rows.
var scene = new RenderScene
{
    Camera = camera,
    Objects = [],
    Terrain =
    [
        TerrainView.From(terrain, key: 1, new TerrainViewOptions { Pixels = path => pixels.GetValueOrDefault(path) }),
    ],
};
```

The renderer takes regions of 16 to 2048 texels, at most 256 regions with all
of them less than 128 regions apart each way, 32 sets, pictures up to 4096
across, a mesh of 16 to 256 cells (an even number) and 1 to 12 levels. Beyond
those `TerrainView.From` throws `ArgumentException` saying why.

Scatter: `terrain.Scatter` is a list of `ScatterLayer`s, rules for grass,
stones and trees, saved in the `.oterrain` a layer to a line.
`ScatterLayer(name, seed, density, sets, minSlope, maxSlope, minHeight,
maxHeight, size, minScale, maxScale, lean, turn, lift, colour,
colourVariation, groundTint, range, castShadows, mesh, material)` is built by
name after `name`:

- `density` is per square metre, up to `ScatterLayer.MaxDensity` (100). It is
  thinned to the share of the ground its `sets` hold. An empty list is any
  set, and automatic cover counts as its flat and steep sets. Nothing stands
  over a hole, off the regions, or outside the slope (degrees) and height
  (metres).
- With no `mesh` it is a block: the cube stretched to `size` metres, standing
  on its bottom face. With a `mesh` (a project path) `size` scales the model,
  which stands on its origin, and `material` names an `.omat`.
- `lean` 0 stands upright, 1 lies square to the ground. `lift` raises, or when
  negative sinks, the base in metres at full size. `turn` spins each about its
  up. `colour` is sRGB `0xRRGGBB`, varied by `colourVariation` (0.2 is 80% to
  120%) and tinted by the ground's colour map by `groundTint`.
- Layers with the same `seed` and `density` land on the same spots with the
  same turn and scale. That is how a trunk and a crown are two layers. Give
  every other layer its own seed.

`ScatterPlacer` does the placing. Call `placer.Update(terrain)` every frame.
It places a region again only when its `Revision` moves or a neighbour changes
along their shared edge, and a layer everywhere only when its rules change.
Otherwise it costs a few comparisons a region. It answers whether anything
changed. `placer.Groups` are `ScatterGroup`s: `Key`, `Layer` (an index into
`placer.Layers`), a stable `Id`, `Transforms` (16 floats each), `Colours` (3
floats each, linear), `Minimum`, `Maximum`, `Count` and `Revision`.
`placer.Revision` moves when anything does. The pattern is laid over the world
from an integer hash, so the same ground scatters the same on every machine,
whatever the region size.

```csharp
using Orblit.Stage;
using Orblit.Terrain;

terrain.Scatter.Add(new ScatterLayer(
    "grass", seed: 1, density: 4, sets: [0], maxSlope: 35,
    size: (0.1, 0.5, 0.1), minScale: 0.6, maxScale: 1.2,
    colour: 0x4F8A3B, colourVariation: 0.2, range: 60));
terrain.Scatter.Add(new ScatterLayer(
    "pines", seed: 2, density: 0.02, sets: [0], maxSlope: 30,
    mesh: "models/pine.glb", material: "materials/bark.omat"));

var placer = new ScatterPlacer();

// Once a frame.
ScatterDrawn? Frame()
{
    if (!placer.Update(terrain))
    {
        return null;
    }

    return ScatterView.From(
        placer,
        key: 100,
        new ScatterViewOptions { ObjectKey = 1_000_000_000, Mesh = path => $"game/{path}" });
}
```

`ScatterView.From(placer, key, options)` in `Orblit.Stage` returns a
`ScatterDrawn`. A block layer is one `Population` per region, keyed `key +
group.Id` and sharing the group's buffers. They go in
`RenderScene.Populations`. A model layer is one `RenderObject` each, because
populations draw only the cube, keyed `ObjectKey + group.Id * 1048576 +
index`. They go in `RenderScene.Objects`. `ScatterViewOptions` also takes
`Mesh` (a layer's model path to the file the renderer reads), `Material` (an
`.omat` path to a material key in the scene), `Models = false` to leave model
layers out for a view that can't resolve their files, and `RangeScale`, which
scales every block layer's `Range`. A layer with no range stays unlimited.
Rebuild when `Update` answers true, not every frame.

View distance: `DistancePolicy(ceiling, high, low, floor = 1, settle = 2)`
steps the distance down when frames cost too much and back up when there is
room. `Update(cost, seconds)` takes one frame's cost and the time it was drawn
at, in any unit, so long as `high` and `low` use it. Once every `settle`
seconds it judges the mean cost since the last decision and moves one step:
down above `high`, up below `low`, held between, so a cost that falls with the
distance does not step it straight back. `Steps` is the clipmap's `Levels` for
`TerrainView`, and `Fraction` is the share to pass as `RangeScale`. Nothing
uses a policy unless the game makes one.

```csharp
using Orblit.Stage;
using Orblit.Terrain;

var policy = new DistancePolicy(ceiling: 6, high: 0.020, low: 0.014);

// Once a frame, with the frame's cost in seconds and a clock that only goes forward.
void Tune(double frameSeconds, double clockSeconds)
{
    policy.Update(frameSeconds, clockSeconds);
}

var terrainOptions = new TerrainViewOptions { Levels = policy.Steps };
var scatterOptions = new ScatterViewOptions { RangeScale = policy.Fraction };
```

Streaming: the renderer draws 256 regions at most, so a larger world is held
a part at a time. `TerrainStreamer(terrain, source, loadRadius = 512,
keepRadius, maxResident = 256, concurrency = 2)` keeps the regions within
`loadRadius` metres of a point in the terrain, and lets go of those past
`keepRadius` (the load radius plus one region, unless given). `source` is an
`IRegionSource`: `Keys` lists the regions there are, and `ReadAsync(key)`
gives a file's bytes. It can be a folder, a pack, a server or a function that
makes ground from noise. `await streamer.FollowAsync(x, z)` moves the focus,
lets go, then reads what is wanted, nearest first. It may be called every
frame without awaiting, and a call made while it runs moves the focus and
returns the same run. `Resident`, `Loading` and `Failures` say where it is,
and `RetryFailed()` makes the failed regions eligible again. Regions already
in the terrain, and any edited after loading, are the caller's. They are
never removed, and they count against `MaxResident`. `HeightAt` is null over a
region that is not loaded yet, so a body there has no ground to stand on. Hold
it, or keep the focus ahead of it.

A terrain is not safe to share between threads. The streamer changes it on the
synchronization context that `FollowAsync` was called on, so call it from the
thread that draws, and read the terrain only between runs when there is no
context.

Cooked: the cook carries `.oterrain` and `.oregion` files into the bundle as
written, after checking them (`TerrainImporter` in `Orblit.Asset`, in the
default importers). A terrain depends on every region it lists and every
image, model and material it names, so a missing one fails its cook. A region
of another size than its terrain does too. In a pack the files are
`world.oterrain.oterrain` and `x0_z0.oregion.oregion`.
`await CookedTerrain.OpenAsync(source, AssetId.Parse("terrain/world.oterrain"))`
reads the terrain with no regions in it, and gives an `IRegionSource` over the
pack. Give both to a `TerrainStreamer`. A region the pack lacks is named in
`Problems` and never asked for.

```csharp
using Orblit.Asset;
using Orblit.Terrain;

var cooked = await CookedTerrain.OpenAsync(source, AssetId.Parse("terrain/world.oterrain"));
var streamer = new TerrainStreamer(cooked.Terrain, cooked.Regions, loadRadius: 512);

// Once a frame, without awaiting. `player` is where the camera stands.
_ = streamer.FollowAsync(player.X, player.Z);
```

Traps:

- **Pictures that change in place.** They cross again only when the sets or
  their size change. Move `PicturesRevision` when only the pixels do.
- **A list of sets kept elsewhere.** `Terrain` copies the sets it is given, so
  a list you keep is not the terrain's. Change `terrain.Sets`.
- **Settings cost nothing.** `AutoCover`, `BlendSharpness`, `MeshSize` and
  `Levels` travel every frame, and no region is resent for them. Except to
  the scatter. An `AutoCover` change places every layer again, since it moves
  what counts as grass.
- **Placing is not free.** A region is placed again whole, so a brush on a
  dense grass layer costs that per region per step. Keep grass `density` near
  1 or below, and give it a `range`.
- **Model layers are objects.** Thousands, not hundreds of thousands. A model
  layer's `Range` is ignored, because an object has none. A layer that puts
  1,048,576 models or more in one region throws `ArgumentException`. Draw it
  as blocks.
- **Two layers, one seed.** Layers sharing a seed and density stand in each
  other. That is right for a trunk and a crown and wrong for anything else.
- **Slope at the outer edge.** A texel with no neighbour on one side takes its
  own height for the missing one, so `NormalAt` and the slope rules see gentler
  ground at the terrain's last texel.
- **Name clashes.** `Orblit.Filament` has its own `TerrainRegion` and
  `TerrainSet`, the renderer's copies. Import only one of the two namespaces in
  a file that names them, or write `Orblit.Terrain.TerrainRegion`. The type
  `Orblit.Terrain.Terrain` has the name of its namespace, so inside a
  namespace that starts `Orblit` it is easiest to write `Orblit.Terrain.Terrain`
  in full.

Guide: `/guides/terrain/`.

## Orblit.Remote

A running debug game can be driven from outside. A script or an assistant
connects to a loopback WebSocket, lists entities, reads a component, sets a
field, holds time still, steps it and compares two snapshots. Use it to find
out what a game did, not to guess from the source.

```csharp
using Orblit.Core;
using Orblit.Remote;

var world = new World();
var position = world.RegisterComponent("Position", ComponentKind.Float64, 3);
var health = world.RegisterComponent("Health", ComponentKind.Float64);

var gate = new TimeGate();
var loop = new GameLoopContext();
var host = new RemoteHost
{
    World = new CoreWorldInspector(world, [position, health]),
    Gate = gate,
    Log = new LogTail(),
};
await using var server = await RemoteServer.Start(host, loop);

// In the game's loop, once a frame.
void Frame(double delta)
{
    loop.Pump();
    world.Tick(gate.Advance(delta));
}
```

`Start` prints `orblit-remote ws://127.0.0.1:PORT/?token=TOKEN` and keeps the
address as `server.Uri`. Connect to that exact address. Set
`RemoteServerOptions.Announce` to receive the line instead of the console.
Each request is JSON-RPC 2.0 with named params. A request with no `id` is a
notification and gets no answer.

The server posts every verb to the `SynchronizationContext` it is given, so a
verb reads and writes the world on the game's thread. A game that has a
context of its own passes it. A game with none passes a `GameLoopContext` and
calls `Pump()` once a frame, as above. Dispose the server to end every
connection and let time run again.

`RemoteClient` is the other end, for a script or a test:

```csharp
using Orblit.Remote;

// `address` is the line the game printed, without its `orblit-remote ` prefix.
await using var client = await RemoteClient.Connect(new Uri(address));
var listed = await client.Call("world.entities", new Dictionary<string, object?>
{
    ["filter"] = new[] { "Position" },
});
var stepped = await client.Call("time.step", new Dictionary<string, object?> { ["n"] = 5L });
```

A whole number in params is a `long`. A refusal throws `RemoteException` with
its `Code`. `deadline` bounds each call and is 30 seconds unless set.

| Method | Params | Answers |
| --- | --- | --- |
| `world.entities` | `filter`: component names | ids with component names |
| `entity.get` | `id` | components by field |
| `entity.set` | `id`, `component`, `field`, `value` | nothing |
| `time.pause`, `time.resume` | none | `paused` |
| `time.step` | `n` | `paused` and `stepped`, once the loop passed `n` frames |
| `world.wait` | `id`, then `component`, `field`, `op` and `value`, or `gone`; `timeout_ms` | `met`, `value` and `waited_ms` |
| `world.snapshot` | `label`, optional | an `id` with the `entities` and `extras` |
| `world.diff` | `a`, `b`: snapshot ids | each path that differs, as `path`, `from` and `to` |
| `input.send` | `event`: an object | nothing |
| `view.screenshot` | none | `png` as base64 |
| `log.tail` | `n`, default 50 | the last lines |
| `perf.summary` | none | what the host reports |
| `view.passes` | none | a row for each pass of the last frame: `name`, `milliseconds`, `draws` |
| `view.draws` | `limit`, default 100 | `count`, the first `limit` rows as `draws`, and `truncated` |
| `view.draw` | `id`: `object:<key>` or `population:<key>` | one draw row |
| `view.material` | `key`: a whole number | the material's `values` by name, its `maps` and `usedBy` |

`world.wait` checks every ten milliseconds, inside the game, so a script need
not step and read in a loop. `op` is one of `==`, `!=`, `<`, `<=`, `>`, `>=`
and the field must hold a number. `timeout_ms` is 5000 unless given and 20000
at most. A wait that runs out answers `TimedOut` and names the last value
read. A game held by the time gate does not move on its own, so step it or
resume it first.

A `label` on `world.snapshot` becomes the snapshot's `id`, so `world.diff`
can be asked for `before the jump` and `after`. A label used again replaces
the older snapshot. A label is 1 to 40 characters and cannot look like `s3`.

The five `view.` verbs need a renderer. The surface does not depend on one,
so the game hands `RemoteHost` a function for each. `Orblit.Filament` has the
rows: `DrawRows()` and `MaterialRow(key)` on a `RenderScene`, and
`GetPassTimings()` on the `Renderer`.

```csharp
using Orblit.Filament;
using Orblit.Remote;
using Orblit.Sprite;

// `scene` is read when a verb runs, so an answer is about the scene on screen.
RemoteHost RenderHost(Renderer renderer, Func<RenderScene> scene) => new()
{
    Passes = () =>
    {
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows =
        [
            .. scene().PassNames.Zip(
                renderer.GetPassTimings(),
                (name, time) => new NamedPassTiming(name, time.Milliseconds, time.Drawn).ToJson()),
        ];
        return Task.FromResult(rows);
    },
    Draws = () => scene().DrawRows(),
    Material = key => scene().MaterialRow(key),
    Screenshot = async () =>
    {
        renderer.RequestCapture();

        // A capture arrives a frame or two after it is asked for.
        await Task.Delay(100);
        return renderer.ReadCapture() is { } frame
            ? PngCodec.Encode(frame.Width, frame.Height, frame.Rgba.ToArray())
            : throw new InvalidOperationException("No frame has been read back yet");
    },
};
```

Combine the two hosts with `with`, for instance
`RenderHost(renderer, scene) with { World = ..., Gate = gate }`.

Traps:

- **Pause needs the loop's help.** The stage has no clock. Time holds still
  only if the game's loop passes its delta through `gate.Advance`. A
  `time.step` on a loop that does not answers `-32003` once `Timeout` passes
  with no frame, ten seconds unless set, and leaves the game paused.
- **Debug builds only.** `RemoteServer.Start` throws `InvalidOperationException`
  in a game whose entry assembly was built with optimisations, so a release
  build refuses. Do not look for a way round that.
- **A component with no field names cannot be set.** It is listed by name and
  its layout. The registry that `Orblit.Codegen` writes has a `Manifest`
  string. Decode it with `JsonText.Decode` and pass the entries of its
  `components` list as the third argument of `CoreWorldInspector` to name the
  fields. A built-in `orblit.LocalTransform` is always named: `x`, `y`, `z`,
  `rotationX` to `rotationW`, `scaleX` to `scaleZ`. The constructor throws
  `ArgumentException` for a manifest entry whose field count is not the
  component's element count.
- **A float that is not finite reads as text.** `"NaN"` and `"Infinity"`,
  because JSON cannot carry the number. That is usually the broken value you
  were looking for.
- **Physics is not in a snapshot.** Its own snapshot is native memory with no
  JSON form. Put what you want compared, such as contacts, in
  `RemoteHost.Extras`, a name for each function that answers JSON.
- **Snapshots are eight deep.** The ninth drops the oldest. A diff of an id
  that was dropped answers `-32001`.
- **Take the token from the printed line.** It changes at every start. A wrong
  one, a browser `Origin` or a foreign `Host` gets a 403 before the upgrade.
  Never write the token to a file the project commits.
- **One request at a time per connection.** Open a second connection to pause
  during a long `time.step`. A ninth connection gets a 503. When the last one
  leaves, time runs again.
- **A render verb answers what the host gave it.** `view.screenshot`,
  `view.passes`, `view.draws`, `view.draw` and `view.material` each need their
  own function in `RemoteHost`. A host without it answers `-32004`.
- **A callback that hangs is cut off, one that runs on is not.** `Timeout`
  bounds the wait for the next stepped frame, the input sink, the screenshot
  and the pass list, and answers `-32003`. `Perf`, `Draws` and `Material` run
  to the end on the game's thread, so keep them quick.
- **A draw row is what the scene submitted.** Culling, range fade and batching
  come after it, so a row can be listed and never drawn. A pass row is what
  the renderer did, so use it to check a draw count. Only objects and
  populations have rows, so a pass can count more draws than there are rows.
  A row with `materialListed: false` names a material the scene does not list,
  and the renderer draws it in its own colour. `materialListed` is null when
  the row names no material. A null `mesh` is the built-in cube.
- **An empty pass list is not proof that nothing ran.** It is empty until the
  renderer has reported timings.
- **Material values are what the scene packs.** They are not read back from
  the renderer, so a texture that failed to load still shows its path.
- **Read only is narrow.** `ReadOnly = true` refuses `entity.set` and
  `input.send` with `-32002`. Pause, resume and step still work.
- **No depth.** A pixel of another pass, and a scan for NaN or depth fighting,
  are not verbs.

### As tools for an assistant

`orblit-remote` is also a program, from the `Orblit.Remote.Tool` package. It
installs as a .NET tool. Hand it the printed address and it speaks the tool
protocol on stdin and stdout. In Claude Code:

```sh
dotnet tool install --global Orblit.Remote.Tool
claude mcp add orblit -- orblit-remote 'ws://127.0.0.1:PORT/?token=TOKEN'
```

Install it from the feed that holds the other Orblit packages. The address is
new at every game start, so register again after a restart. To keep the token
out of the process list, set `ORBLIT_REMOTE_ADDRESS` and give no argument.

Add `--allow-write` to let the assistant change a value. Without it the
assistant sees six tools, and a call to `set` is refused as unknown.

| Tool | Does |
| --- | --- |
| `observe` | Counts entities and lists the first `limit`, 20 unless asked, or reads one by `id` in full. `filter` is component names. |
| `step` | Pauses, then passes `n` frames, 1 to 300. The game stays paused until the session ends. |
| `snapshot` | Records the world and answers an id with counts. The last eight are kept. |
| `diff` | The first 200 paths that differ between snapshot ids `a` and `b`. |
| `screenshot` | One PNG. |
| `perf` | The host's phase times. |
| `set` | `id`, `component`, `field`, `value`. Only with `--allow-write`. |

What not to trust:

- **A screenshot is one frame.** It says nothing about motion. A paused game
  gives the same picture each time, so step before you look again.
- **A diff holds only what its snapshots held.** A field the host left out of
  `Extras`, and anything that changed and changed back between the two, is
  invisible. Take the snapshots around the one thing you changed.
- **`truncated: true` means rows were cut.** Narrow with `filter`. Do not
  read the first twenty as the whole world. One entity, `perf` and the values
  inside a diff are not cut.
- **A refusal is an error result, not a crash.** Read its code. `-32004` means
  the game did not give the surface that part, and no retry fixes it.

Quote the address, because zsh reads `?` as a pattern. The host must be
`127.0.0.1` or `localhost`. A missing or bad address or an unknown flag exits
with 64. A game that cannot be reached, or that goes away mid session, exits
with 69.

Verified by tests over a real loopback socket, with an `Orblit.Core` world
and a stand-in loop, and with the built tool run as a child process. The
render verbs are tested with stand-in rows and no renderer. Nothing has driven
a game in a window, and no assistant has connected to it.

## Orblit.Particles

An `Emitter` is the recipe for sparks, smoke, dust or fire. `Sample(time,
buffer)` fills a `ParticleBuffer` with every particle alive at that moment. It
keeps nothing between calls, so a seek, a pause and a replay give the same
frame. A steady `rate` and a list of `Burst`s add up, and bursts ignore
`duration`.

```csharp
using Orblit.Particles;
using Orblit.VectorMath;

var sparks = new Emitter(
    seed: 3,
    shape: new ConeShape(angle: 0.5),
    rate: 0,
    bursts: [new Burst(0, 40)],
    lifetime: new Orblit.Particles.Range(0.4, 0.9),
    speed: new Orblit.Particles.Range(3, 6),
    gravity: new Vector3(0, -9.8, 0),
    size: [0.08, 0],
    colour: [1, 0.8, 0.3, 1, 0.2, 0]);
var frame = new ParticleBuffer(sparks.MaxParticles);

sparks.Sample(0.3, frame);

// An emitter that moves. A fixed one needs no placement.
sparks.Sample(0.3, frame, place: time => Matrix4.TranslationValues(4 * time, 0, 0));
```

- Every argument is optional, so call them by name. `lifetime`, `speed` and
  `spin` are ranges from low to high. `Orblit.Particles.Range` clashes with
  `System.Range`, so write it in full.
- `size` and `colour` are ramps over a particle's life. `size` is one or more
  edge lengths. `colour` is three values, red, green and blue, for each stop.
  A size of 0 at the end of the ramp is how a particle fades.
- Shapes are `PointShape`, `SphereShape(radius)`, `ConeShape(angle, radius)`
  and `BoxShape(halfExtents)`. The cone angle is in radians from straight up,
  and its radius is the disc the particles start on.
- A `Placement` is a function from a time to a `Matrix4`. With
  `ParticleSpace.World`, a particle is left where its emitter was when it was
  born, so it asks for that moment. With `ParticleSpace.Local` it rides with
  the emitter, so it asks for the moment it is drawn.
- The buffer holds `Positions` and `Colours` as three floats a particle,
  `Sizes` as an edge length and `Angles` in radians about the view axis. `Count`
  says how many are live.
- When more particles are due than the cap, the oldest die first. The cap is
  the smallest of `MaxParticles`, the buffer's `Capacity` and the optional
  `limit` argument, which a scene that shares one budget lowers.
- A `time` below 0 or not finite leaves `Count` at 0.
- The constructor throws `ArgumentException` for a negative or non-finite
  `rate` or `duration`, a `lifetime` that starts at 0 or less, a range that
  runs high to low, an empty `size`, a `colour` that is not a multiple of 3,
  a negative `drag` or `noiseStrength`, a `noiseScale` of 0 or less, and a
  `maxParticles` below 1. A `Burst` takes a time of 0 or more and a count of
  1 to 65535.
- `CopyWith` takes the same names. It cannot clear `duration`, so build a new
  `Emitter` to remove it.

`ParticleSim` is the stepped form, for a particle that must meet a surface.
Give it a `HitTest` from two points to a `Hit` with a `Point` and a `Normal`,
and a `bounce` from 0 to 1. Without a `bounce`, a particle dies on a surface.
`turbulence` adds a smooth push in metres a second squared.

```csharp
using Orblit.Particles;
using Orblit.VectorMath;

// `sparks` is an Emitter and `frame` a ParticleBuffer.
var landing = new ParticleSim(
    sparks,
    hit: (start, end) => end.Y < 0
        ? new Hit(new Vector3(end.X, 0, end.Z), new Vector3(0, 1, 0))
        : null,
    bounce: 0.4);

// Once a frame.
landing.Step(1.0 / 60);
landing.Fill(frame);
```

The two points are reused, so do not keep them. A peer that steps the
simulation with different time slices drifts apart from another, so it is for
looks and not for anything a game decides on.

A scene draws its particles with `ParticleView`. It reads every shown entity
that has a `ParticleEmitterComponent` and gives back populations to merge into
the frame:

```csharp
using Orblit.Filament;
using Orblit.Stage;

// `view` is the DocumentView.
var particles = new ParticleView(view, key: 1000);

// Once a frame.
RenderScene Staged(double dt)
{
    particles.Advance(dt);
    var staged = view.Scene;
    return staged with { Populations = [.. staged.Populations, .. particles.Populations] };
}
```

- `view.Scene` builds the scene on every read, so keep it in a local, as above.
- Each emitter's `key` is `key` plus a number given in the order the view
  first saw it, and it stays. Leave the range clear of every other
  population's key.
- Each entity has its own clock. It starts at 0 when the entity is first
  shown, restarts if the entity is hidden and shown again, and carries on when
  the component changes.
- `budget` caps all emitters together. Earlier emitters keep what they asked
  for and later ones are cut to what is left. An emitter with no live particle
  has no population.
- A particle is a camera-facing cube, lit, as wide as its size. There is no
  alpha, no material and no flipbook.
- `Rejected` maps the id of an entity to the message of an emitter that would
  not build. It clears when the scene changes. A file with numbers an emitter
  cannot take reads as the default for that number, so only a component made
  in code is rejected.
- Build a `ParticleEmitterComponent` by name: `shape` (`EmitterShape`),
  `rate`, `bursts` (`EmitterBurst(time, count)`), `lifetimeMin`, `speedMax`,
  `space` (`EmitterSpace`) and the rest.

A `Trail` is the line behind a moving thing. It does not store a history.
`SampleAlong` asks a path for the points it needs, so a replay and a scrub give
the same strip:

```csharp
using Orblit.Particles;
using Orblit.VectorMath;

var trail = new Trail(life: 0.5, rate: 30, width: [0.2, 0]);
var strip = new TrailStrip(trail.Points);

trail.SampleAlong(2, time => new Vector3(4 * time, 0, 0), strip);
```

A trail behind something you cannot ask for its past is recorded by a
`TrailRing`:

```csharp
using Orblit.Particles;
using Orblit.VectorMath;

var ring = new TrailRing(new Trail(life: 0.5, rate: 30));
var strip = new TrailStrip(ring.Trail.Points);

// Once a frame, with the game's clock and where the thing is.
void Follow(double time, Vector3 position)
{
    ring.Record(time, position);
    ring.Fill(strip);
}
```

- `life` and `rate` are above 0, and `life * rate` is at most 127. A skin holds
  256 joints and a trail point takes two. `Points` is `ceil(life * rate) + 1`.
- `width` is one or more values from the newest point to the oldest.
- A strip lists points newest first. `Head` is the newest, and the origin
  when the strip is empty.
- `Record` clears the ring when time runs backwards or a step is longer than
  `life`. It ignores a time that is not finite. A seek forward that is shorter
  than the life looks like a slow frame, so call `Clear()` on a seek you know
  of.

`TrailRibbon` in `Orblit.Stage` is the mesh for a strip. It is a
`ParticleSheet` two columns wide with a row for each point. Hand it to the
renderer as `physics.md` hands over a cloth:

```csharp
using Orblit.Filament;
using Orblit.Particles;
using Orblit.Stage;
using Orblit.VectorMath;

var ribbon = new TrailRibbon(points: 16);
RenderResources.Provide("trail/v1/trail.glb", ribbon.ToGlb());

// Every frame. `strip` is a TrailStrip and `eye` the camera's position.
RenderObject Drawn(TrailStrip strip, Vector3 eye)
{
    var head = strip.Head;
    return new RenderObject
    {
        Key = 7000,
        Mesh = "trail/v1/trail.glb",
        Colour = new Vector3(1, 1, 1),
        Transform = Matrix4.TranslationValues(head.X, head.Y, head.Z),
        Joints = ribbon.Poses(strip, eye),
    };
}
```

- A ribbon has 2 to 128 points. Make it as long as the trail's `Points`.
- `Poses` is relative to the strip's `Head`, so move the object to the head.
  Rows past the strip's `Count` fold onto the last point and draw nothing.
- The face turns toward `eye`. Where a point has no direction or no width, or
  lies in line with the eye, the two edges meet and the ribbon pinches.
- The renderer culls from the sheet at rest, a box 1 m wide that hangs
  `Points - 1` m below the head. A trail outside that box can be culled while
  it is in view.
- The ribbon is white. It has no colour curve, and no scene component or
  editor section makes a trail.

## Orblit.Progress

A game's progress as a few numbers that save compactly and change by a small
diff. A `ProgressSchema` lists names. A `Whole` is a full unsigned 32-bit
number. A `Slice` is some bits of a `Whole`, and writing a slice leaves the
other bits alone.

```csharp
using Orblit.Progress;

var schema = new ProgressSchema(
    1,
    [
        new Whole("coins"),
        new Whole("flags"),
        new Slice("met_innkeeper", of: "flags", low: 0, bits: 1),
        new Slice("door", of: "flags", low: 1, bits: 2),
    ]);
var table = new ProgressTable(schema);

var (next, changes) = table.Set("coins", 12);
var rich = new Condition("coins", Compare.GreaterOrEqual, 10).Holds(next);

// The whole table for a save. The changes alone for a message.
var saved = next.Encode();
var restored = ProgressTable.Decode(schema, saved);
var sent = changes.Encode();
```

- `ProgressTable` never changes. `Set` returns the new table and the
  `Changes` it made. `Get` reads a value as a `long`.
- A value that does not fit the bits throws `ArgumentOutOfRangeException`.
  `schema.Fits(name, value)` asks first.
- The version is 1 to 65535. Slots are numbered from 0 in the order the
  `Whole`s are listed, and a `Slice` shares the slot of its `Whole`. Adding
  names at the end keeps older slots. Removing or reordering moves them, so
  read an old save under the old schema and write what to keep by name.
- `Decode` throws `FormatException` for bytes that are not a table or that
  were written under a later schema. A name added since starts at zero.
- `Changes.TryDecode(schema, bytes)` gives null for bad bytes, a slot the
  schema lacks, or a repeated slot. Apply what it returns with
  `table.Apply(changes)`. `table.ChangesSince(older)` makes one.
- A `Condition` is a name, a `Compare` and a value from 0 to 4294967295.
  `Condition.AllHold(conditions, table)` asks a list. There is no `or`. Write
  two lists and let the first that holds win. `Condition.FromJson` and
  `ListFromJson` read one from a file.

## Orblit.Quest

A quest is a list of stages, saved as one number in the table, under the name
`QuestNames.StageName(key)`, which is `quest.<key>.stage`. The file extension
is `.oquest`.

```csharp
using Orblit.Progress;
using Orblit.Quest;

var rats = new QuestDefinition(
    "rats",
    "Rats in the cellar",
    [
        new Stage("start", "Ask the innkeeper about the noise."),
        new Stage(
            "cellar",
            "Clear the cellar.",
            requires: [new Condition("met_innkeeper", Compare.Equal, 1)]),
        new Stage("done", "Collect your reward."),
    ]);
var schema = new ProgressSchema(
    1,
    [new Whole(QuestNames.StageName("rats")), new Whole("met_innkeeper")]);
var book = new QuestBook(schema, [rats]);

var table = new ProgressTable(schema).Set("met_innkeeper", 1).Table;

// The game decides `to`. A client never sends it.
switch (book.Advance(table, "rats", to: 1))
{
    case Moved moved:
        table = moved.Table;
        Console.WriteLine(book.Journal(table, "rats"));
        break;
    case Refused refused:
        Console.WriteLine(refused.Reason);
        break;
}
```

- A key is letters, digits, `_` and `-`. A quest has a title and at least 2
  stages with unique ids.
- `QuestBook` checks at construction. Keys must be unique, the stage name must
  be in the schema with bits for the last stage, and every condition must name
  something the schema has. It throws `ArgumentException` otherwise.
- `Advance` gives `Moved` with the new `Table`, the `Changes`, `Completed` and
  `Back`, or `Refused` with `QuestRefusal.NotForward` or
  `QuestRefusal.Requirement`. A move ahead needs the conditions of every stage
  it enters. A move back needs `allowBack: true`. A stage the quest does not
  have throws `ArgumentOutOfRangeException`.
- `Completed` is true when a forward move enters the last stage.
- `StageOf`, `IsStarted`, `IsComplete` and `Journal` read the table. `Journal`
  is empty for a stage the quest no longer has.
- `QuestDefinition.Parse(text)` throws `QuestFormatException` for text that is
  not a quest, a file from a newer Orblit, or a broken rule. `Encode()` writes
  one back.

## Orblit.Dialogue

A conversation is nodes that read and write the same table. The file extension
is `.odialogue`. A `Variant` names where to start. The first whose conditions
hold wins, and a variant with none is the default, so it goes last.

```csharp
using Orblit.Dialogue;
using Orblit.Progress;

var schema = new ProgressSchema(1, [new Whole("met_innkeeper")]);
var table = new ProgressTable(schema);

var talk = new DialogueDefinition(
    "innkeeper",
    [
        new Variant("hello", [new Condition("met_innkeeper", Compare.Equal, 0)]),
        new Variant("again"),
    ],
    [
        new LineNode("hello", "Innkeeper", "Mind the cellar.", next: "ask"),
        new ChoiceNode(
            "ask",
            [new ChoiceOption("Tell me more.", next: "more"), new ChoiceOption("Goodbye.", next: null)]),
        new LineNode("more", "Innkeeper", "Rats, I think.", before: "meet"),
        new LineNode("again", "Innkeeper", "Back again?"),
    ]);

var hooks = new Dictionary<string, Before>
{
    ["meet"] = current => current.Set("met_innkeeper", 1).Table,
};
var book = new DialogueBook(schema, hooks, [talk]);

var runner = book.Start("innkeeper", table);
while (!runner.IsOver)
{
    switch (runner.Current)
    {
        case LineNode line:
            Console.WriteLine($"{line.Speaker}: {line.Text}");
            runner.Next();
            break;
        case ChoiceNode:
            runner.Choose(0);
            break;
    }
}

table = runner.Table;
```

- The nodes are `LineNode`, `ChoiceNode`, `SetNode` and `JumpNode`. Each can
  name a `before` hook, which runs as the node is entered and gives back the
  table. A `SetNode` writes `Write(name, value)` pairs and goes on to `next`.
  A `JumpNode` goes straight to its `next`.
- A choice shows its `Options`. An option with a `when` list is offered only
  while the conditions hold. A choice with nothing to offer ends the talk.
  An option or line with no `next` ends it too.
- A ring of sets and jumps that never reaches a line is refused.
- `DialogueBook` checks at construction. Keys are unique, every name is in the
  schema, every written value fits its bits, and every `before` is in `hooks`.
- A runner starts as soon as it is made. If no variant holds, `IsOver` is true
  at once and nothing ran. `Next()` works only on a line and `Choose(index)`
  only on a choice, or they throw `InvalidOperationException`. A bad index
  throws `ArgumentOutOfRangeException`.
- A runner works on the table it was given and does not see changes made
  meanwhile. A game that lets other things change the table ends the talk, or
  reads the table again before it keeps `runner.Table`.
- To move a quest from a conversation, call `QuestBook.Advance` in a hook and
  return the table it gave back. A `SetNode` that writes the stage name skips
  the quest's rules.
- `DialogueDefinition.Parse(text)` throws `DialogueFormatException`.
  `StartFor(table)` names the start node, or null, and changes nothing.

## Orblit.Save

Saves for a server or a tool. A `SaveRecord` is bytes, a `SaveReason` and a
time. The reasons are `Autosave`, `Leave`, `Disconnect`, `Shutdown`, `Failed`
and `Custom(text)`, which takes 1 to 64 characters on one line. A key is
letters, digits and `.`, `_` and `-`, at most 128, and does not start with a
dot. A bad key throws `ArgumentOutOfRangeException`.

```csharp
using Orblit.Save;

// `directory` holds a file for each key. `bytes` is the game's own encoding.
var queue = SaveQueue.Spawn(
    new FileSaveStore(directory),
    () => DateTimeOffset.UtcNow,
    (key, error) => Console.Error.WriteLine($"{key}: {error.Message}"));

queue.Save("player-1", bytes, new Autosave());

var record = await queue.ReadAsync("player-1");
var stuck = await queue.CloseAsync();
```

- `Save` returns at once and copies the bytes. `ReadAsync`, `ExistsAsync` and
  `KeysAsync` also see a save that has not reached the store.
- `Spawn` runs a worker task, started again if it ends early. `onFailure` runs
  on the worker with the queue's lock held, so keep it short and do not wait on
  another thread. `Inline` writes on the caller, for tests and light stores.
- `FlushAsync` tries failed writes again and gives back the keys still not
  stored. `CloseAsync` flushes, stops the worker and gives back the stuck keys.
- `FileSaveStore` writes a temp file, flushes and renames it, so a crash leaves
  the old save whole. A damaged file throws `FormatException`.
  `MemorySaveStore` is the in-memory form.
- `AutoSaver(perTick, every, jitter, random)` says who is due.
  `Claim(live, tick)` gives at most `perTick` keys, longest overdue first. A
  key comes due once in `every` ticks plus up to `jitter`, and a key that is no
  longer live is forgotten.

`HistoryStore` keeps a version of every save. `SaveTools` restores one:

```csharp
using Orblit.Save;

// `directory` is the saves folder and `online` the keys now in play.
var store = HistoryStore.Files(directory);
var tools = new SaveTools(store, key => online.Contains(key), () => DateTimeOffset.UtcNow);

var versions = await store.History.ListAsync("player-1");
await tools.RestoreAsync("player-1", versions[0].At);
await store.History.PruneAsync(Spacings.Default, DateTimeOffset.UtcNow);
```

- Saves go in the directory and history in `directory/history`. `WriteAsync`
  writes the save and then its version.
- Bytes are kept once for each distinct content, compressed and named by their
  SHA-256, so a save that did not change adds an entry and no copy.
- `ListAsync(key)` is newest first. `PruneAsync` keeps what a `Spacing` allows
  and gives back the number removed. `Spacings.Default` keeps every version for
  a day, one an hour for a week, one a day for a month, and nothing older.
- `RestoreAsync(key, at)` needs the exact time of a version. It throws
  `InvalidOperationException` when there is no such version or the key is in
  use. The save it replaces stays in history as `pre-restore`.
- `RestorePlanAsync(before)` lists what a restore of everything would change,
  and the live keys it skipped. `ApplyRestoreAsync(plan)` stops with
  `InvalidOperationException` if a key has gone live since, and the keys before
  it stay restored.
- `EditAsync(key, change, reason)` rewrites one save through a function.

A server that is stopped can also use the command, from the
`Orblit.Save.History` package:

```sh
dotnet tool install --global Orblit.Save.History
orblit-save-history <saves-directory> list [key]
orblit-save-history <saves-directory> restore <key> <time> --server-stopped
orblit-save-history <saves-directory> restore-all <time> [--apply] --server-stopped
```

`list` prints the time, the reason and the first twelve characters of the hash
of each version. `<time>` is the ISO 8601 form `list` prints. `restore-all`
only lists what it would change until `--apply` is given. The command cannot
see who is playing, so a write needs `--server-stopped`. It exits with 64 for
bad usage and 1 when a restore is refused.

## More worked examples

Each page is a short game or tool built from the packages above:

- [A model on screen](https://orblitengine.com/docs/examples/a-model-on-screen/)
- [A thousand things](https://orblitengine.com/docs/examples/a-thousand-things/)
- [A camera that follows](https://orblitengine.com/docs/examples/a-camera-that-follows/)
- [Something that chases you](https://orblitengine.com/docs/examples/something-that-chases-you/)
- [A menu over the game](https://orblitengine.com/docs/examples/a-menu-over-the-game/)

Append `.md` to a page's address for Markdown. If that gives a 404, use the
page as it is.
