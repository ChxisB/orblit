# A complete small game

A dodging game in one file: arrow keys steer a blue cube, orange cubes come
down the lane faster and faster, and a hit ends the run until Space is
pressed. It shows the shape most Orblit games take:

- **State lives in the window's fields; the scene is built from it** in
  `BuildScene`, fresh every frame. Nothing is added to or removed from the
  renderer. A hazard that leaves the list leaves the screen.
- **Keys come from reserved ranges**, because objects and lights share one key
  space: lights 1 to 9, fixed objects 10 to 999, hazards from 1000 up. Each
  hazard keeps its key for its whole life, and no key is reused.
- **One clock**, the time `RequestAnimationFrame` hands over, and a `dt`
  guarded against pauses and hitches.
- **Input through Avalonia**: `OnKeyDown` and `OnKeyUp` on the window, holding
  the set of keys that are down. Touch would be a pointer handler on the same
  window.
- **The interface is ordinary Avalonia** laid over the viewport in a `Grid`.
  For a styled game interface, see `Orblit.Ui.Avalonia` in
  [packages.md](packages.md).
- **Scene notes on screen**, which the renderer repeats every frame, so the
  text is set and Avalonia ignores an unchanged value.

It builds against `Orblit.Filament` 0.42.0 and `Orblit.Filament.Avalonia`
0.1.0, and has not been run in a window. Names may have moved since; check
them against the source.

```csharp
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Orblit.Filament;
using Orblit.Filament.Avalonia;
using Orblit.VectorMath;

class DodgeApp : Application
{
    [STAThread]
    static int Main(string[] args) =>
        AppBuilder.Configure<DodgeApp>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);

    public override void Initialize() => Styles.Add(new FluentTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new DodgeWindow();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

class Hazard(int id, double x, double z)
{
    public int Id { get; } = id; // never reused, so the renderer never confuses two hazards
    public double X { get; } = x;
    public double Z { get; set; } = z;
}

class DodgeWindow : Window
{
    // One key space for objects and lights, so each kind gets its own range.
    private const long SunKey = 1;
    private const long FloorKey = 10;
    private const long PlayerKey = 11;
    private const long FirstHazardKey = 1000;

    private readonly RendererViewport _viewport = new();
    private readonly TextBlock _score = TextAt(VerticalAlignment.Top);
    private readonly TextBlock _notes = TextAt(VerticalAlignment.Bottom);
    private readonly HashSet<Key> _held = [];
    private readonly Random _random = new(7);
    private readonly List<Hazard> _hazards = [];
    private TimeSpan? _last;
    private double _clock;
    private double _nextSpawn;
    private double _x;
    private int _spawned;
    private bool _over;

    public DodgeWindow()
    {
        Title = "Dodge";
        Content = new Grid { Children = { _viewport, _score, _notes } };
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        RequestAnimationFrame(OnFrame);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        _held.Add(e.Key);
        if (e.Key == Key.Space && _over)
        {
            Restart();
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        _held.Remove(e.Key);
    }

    private static TextBlock TextAt(VerticalAlignment side) => new()
    {
        Foreground = Brushes.White,
        FontSize = 18,
        Margin = new Thickness(16),
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = side,
    };

    private void OnFrame(TimeSpan now)
    {
        var dt = _last is { } last ? (now - last).TotalSeconds : 0;
        _last = now;
        if (dt < 0 || dt > 0.5)
        {
            dt = 0; // a pause or a hitch, not game time
        }

        Update(Math.Min(dt, 0.05));
        _score.Text = _over ? $"Hit after {_clock:0.0} s. Space to go again." : $"{_clock:0.0} s";
        if (_viewport.Frame(BuildScene(), now.TotalSeconds))
        {
            var notes = _viewport.Renderer?.GetNotes() ?? [];
            _notes.Text = string.Join('\n', notes.Select(note => $"{note.About}: {note.Saying}"));
        }

        RequestAnimationFrame(OnFrame);
    }

    private void Update(double dt)
    {
        if (_over)
        {
            return;
        }

        _clock += dt;

        var right = _held.Contains(Key.Right) ? 1 : 0;
        var left = _held.Contains(Key.Left) ? 1 : 0;
        _x = Math.Clamp(_x + (right - left) * 8 * dt, -4, 4);

        if (_clock >= _nextSpawn)
        {
            _hazards.Add(new Hazard(_spawned++, _random.NextDouble() * 8 - 4, -30));
            _nextSpawn = _clock + 0.6;
        }

        var speed = 10 + _clock * 0.5;
        foreach (var hazard in _hazards)
        {
            hazard.Z += speed * dt;
            // Both cubes are 1 m across, so they touch within 1 m on each axis.
            if (Math.Abs(hazard.X - _x) < 1 && Math.Abs(hazard.Z) < 1)
            {
                _over = true;
            }
        }

        _hazards.RemoveAll(hazard => hazard.Z > 8);
    }

    private void Restart()
    {
        _hazards.Clear();
        _clock = 0;
        _nextSpawn = 0;
        _x = 0;
        _over = false;
    }

    /// The placeholder cube is 2 m across, so a scale of 0.5 makes a 1 m cube.
    private static Matrix4 Cube(double x, double z)
    {
        var transform = Matrix4.TranslationValues(x, 0.5, z);
        transform.ScaleByDouble(0.5, 0.5, 0.5, 1);
        return transform;
    }

    private RenderScene BuildScene()
    {
        // 12 m wide, 40 m long, its top at y = 0.
        var floor = Matrix4.TranslationValues(0, -0.05, -10);
        floor.ScaleByDouble(6, 0.05, 20, 1);

        return new RenderScene
        {
            Camera = new RenderCamera
            {
                Position = new Vector3(0, 6, 10),
                Target = new Vector3(0, 0, -6),
            },
            Lights =
            [
                new RenderLight
                {
                    Key = SunKey,
                    Kind = RenderLightKind.Directional,
                    Direction = new Vector3(-0.3, -1, -0.5).Normalized(),
                    Intensity = 100000,
                },
            ],
            Objects =
            [
                new RenderObject
                {
                    Key = FloorKey,
                    Transform = floor,
                    Colour = new Vector3(0.18, 0.19, 0.21),
                    CastShadows = false,
                },
                new RenderObject
                {
                    Key = PlayerKey,
                    Transform = Cube(_x, 0),
                    Colour = new Vector3(0.2, 0.45, 0.9),
                },
                // Objects that share a mesh and a material are merged into fewer
                // draws (RenderScene.Batching, on by default); each keeps its own key.
                .. _hazards.Select(hazard => new RenderObject
                {
                    Key = FirstHazardKey + hazard.Id,
                    Transform = Cube(hazard.X, hazard.Z),
                    Colour = new Vector3(0.85, 0.42, 0.16),
                }),
            ],
        };
    }
}
```

## Going further

- **Thousands of things** (debris, crowds, terrain) belong in a `Population`;
  see [scene-api.md](scene-api.md). Keep objects for things the game tracks
  one by one.
- **A following camera** that doesn't jitter: `Orblit.Camera`'s `CameraBrain`
  and virtual cameras (see [packages.md](packages.md)), or at least
  `Smoothing.Damp` on each coordinate of the camera position. `CameraBrain.Aspect`
  ignores a zero or non-finite ratio, so a zero-sized first layout does no harm.
- **Enemies that chase**: `Orblit.Agent`'s steering behaviours for movement and
  behaviour trees for decisions. Round walls, plan the route with
  `Orblit.Pathing`'s `NavGrid` or `NavMesh` and walk it with a `NavWalker`.
- **Controls a player can rebind**: `Orblit.Input`'s `ActionMap`. Name `jump`
  and `move`, bind keys, pad and touch regions to them in contexts, and ask by
  name each frame. `Bindings.Rebind` and `Bindings.ToJson` cover a settings
  screen.
- **Hits beyond boxes**: `Orblit.Collide` for shapes, raycasts and overlaps.
- **Juice** (a flash, a shake, a pop): `Orblit.Effect`, sampled at the game
  clock.
- **Models**: `Mesh` with a `.glb`, provided as bytes through
  `RenderResources.Provide` in a sandboxed macOS app.

The gallery's runner (`dotnet/Orblit.Examples/Showcases/RunnerExample*.cs` in
the engine) is a longer game built the same way, with state in fields and a
`Scene` method that returns the whole scene each frame. It adds an autopilot,
sparks and a followed camera, and is worth reading before building anything
larger.

## A game as a class

The game above owns its window. A game written as a class can also run in the
editor, so a person builds the scene and presses Play. The class implements
`IGame` from `Orblit.Game`, which has no interface types, and a host calls it.

- `Start(context)` runs once, before the first frame.
- `Update(context, seconds)` runs once a frame. `seconds` is never more than
  0.1, so a stall is one slow step and not a jump.
- `Stop(context)` runs once at the end, when the window closes or the game
  threw. Let go of what `Start` took.

What a game can reach:

- **The scene** is a `SceneDocument` in `context.Scene.Document`. It does not
  change under you. Build the scene you want and apply the difference with
  `context.Scene.Apply(SceneDiff.Between(before, after))`. An empty diff is
  ignored. `SceneDocument.WithEntity` and `SceneEntity.WithComponent` make the
  next scene.
- **The camera** is an entity with a `camera` component. Move it as you move
  anything else. There is no camera call.
- **Keys** are in `context.Input.Controls.Keys`, named by place on the
  keyboard: `KeyW`, `Space`, `ArrowLeft`, `Digit1`, `ShiftLeft`. A French
  layout keeps WASD under the same fingers.
- **The pointer** is `context.Input.Pointer`, null while it is not over the
  view. `Origin` and `Direction` are a ray into the world. `Pressed` is true
  for the one update after a primary click. `Drag` is the right-button drag
  since the last update, and null when that button is up. `Wheel` is the notches
  since the last update.
- **The log** is `context.Log`. `ProjectFolder` is what paths in the scene are
  relative to.
- **A throw** from any call ends the game. The host logs it, calls `Stop` and
  shows the throw. It does not reach the editor.

```csharp
using Orblit.Game;
using Orblit.Scene;
using Orblit.VectorMath;

namespace Crate;

/// <summary>A crate that slides left and right on A and D.</summary>
public sealed class CrateGame : IGame
{
    private const double MetresPerSecond = 4;
    private double _x;

    public void Start(GameContext context) => context.Log.Say("Crate started.");

    public void Update(GameContext context, double seconds)
    {
        var keys = context.Input.Controls.Keys;
        var step = ((keys.Contains("KeyD") ? 1 : 0) - (keys.Contains("KeyA") ? 1 : 0)) * MetresPerSecond * seconds;
        var before = context.Scene.Document;
        if (step == 0 || before["crate"] is not { } crate)
        {
            return;
        }

        _x += step;
        var moved = crate.WithComponent(SceneComponents.Transform, new TransformComponent(position: new Vector3(_x, 1, 0)));
        context.Scene.Apply(SceneDiff.Between(before, before.WithEntity("crate", moved)));
    }

    public void Stop(GameContext context)
    {
    }
}
```

To run it on its own, add a `Program.cs` that hands the class, a scene and the
project folder to `GameRunner`. The project references `Orblit.Game` and
`Orblit.Game.Avalonia`, from the feed that [dotnet.md](dotnet.md) sets up.

```csharp
using Crate;
using Orblit.Game.Avalonia;
using Orblit.Scene;

var folder = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var load = SceneDocument.Decode(File.ReadAllText(Path.Combine(folder, "scenes/level.oscene")));
return GameRunner.Run(new CrateGame(), load.Document, folder);
```

To run it in the editor, name it in `orblit.project.json`. `project` is the
game's `.csproj`, relative to the project folder with no `..`. `type` is the
class's full name. It needs a public class with a public constructor that takes
nothing.

```json
"game": { "project": "game/Game.csproj", "type": "Crate.CrateGame" }
```

An editor that reads this entry builds the project when Play is pressed and
runs the class on a copy of the scene. It asks first the time it meets a
project's code in a folder. Opening a project never builds or runs anything.

A game with a score, a menu or a bar of buttons implements `IInterfaceGame`
from `Orblit.Game.Avalonia` instead. It adds `CreateInterface()`, which returns
an Avalonia `Control`. The host calls it once after `Start`, puts the control
over the view and removes it when the game stops. A throw from it ends the game
like any other. `Control` also names a type in `Orblit.Input`, so alias one of
the two (see [packages.md](packages.md#name-clashes)).

The control can be XAML. An `avares://` address inside it reaches the game's own
assembly, so a style file shipped with the game loads. The editor asks for the
control once for the whole play, so keep its state in the game and not in a
second call. A view closed and opened again shows the same control.

In the editor the game view takes the keys when Play starts. While it has the
focus every key is the game's, Escape and the editor's tool keys included, so
stop with the Stop button. The keys go back to the editor when the focus leaves
the view. A scene with no camera gives the game its keys and a null pointer.

## Tag a thing as multiplayer

A networked game is the same `IGame` class on every copy, and the scene says
what is shared. It needs `Orblit.Game` 0.2.0 and `Orblit.Net.Game` 0.1.0.
Neither is on a public feed, so pack them as [dotnet.md](dotnet.md) says. A
game built for `Orblit.Game` 0.1.x is refused by 0.2.0.

**The tag** is a `net` component on an entity in the scene file.

```json
"net": { "authority": "owner", "sync": [ "transform" ] }
```

- `authority` is `server` or `owner`. The server's game moves a `server`
  entity. An `owner` entity is moved by the client the server names, and by the
  server until it names one. A write from any other client is refused.
- `sync` lists what travels. `transform` is the entity's place. Any other name
  is a `NetVar` value.
- No `net` component means local. The server reads the tag when the entity
  appears, so a change on a live entity does nothing until the entity is removed
  and added again.
- In code the tag is `new NetComponent(NetAuthority.Owner, ["transform"])` under
  `SceneComponents.Net`. The editor's inspector adds, removes and edits it.

**The role** is `context.Net`, an `IGameNet`.

| Copy | `Role` | `LocalClient` | `IsServer` |
| --- | --- | --- | --- |
| Offline, one player | `Offline` | `null` | `false` |
| Host | `Server` | `null` | `true` |
| Guest | `Client` | the guest's id | `false` |

`OwnerOf(id)` is the client that owns an entity, or `null`. A game that never
reads `Net` runs offline.

**A call** asks the server to act. Call `Bind(this)` from `Start`, mark the
method `[ServerRpc]`, and call it by name from any copy. Arguments are `bool`,
`int`, `float`, `string`, `Vector3` or `NetEntity`, at most 16. A server call
runs at once. A guest's call runs on the server at the start of its next
update. Offline it runs at once. `[ClientRpc]` marks a method the server calls
with `CallClient(clientId, name, ...)` or `CallAll(name, ...)`.

```csharp
using Orblit.Game;
using Orblit.Net.Game;
using Orblit.VectorMath;

public sealed class CrateGame : IGame
{
    private static readonly string[] Crates = ["crate", "crate2"];

    public bool Marked { get; private set; }

    public void Start(GameContext context)
    {
        context.Net.Bind(this);
    }

    public void Update(GameContext context, double seconds)
    {
        if (Mine(context.Net) is not null && context.Input.Pointer is { Pressed: true })
        {
            context.Net.Call(nameof(PlaceMarker), new Vector3(2.5, 0, -1.5));
        }
    }

    // Runs on the server. The arguments come from a client, so check them as input.
    [ServerRpc]
    public void PlaceMarker(Vector3 at)
    {
        Marked = Math.Abs(at.X) < 6 && Math.Abs(at.Z) < 6;
    }

    public void Stop(GameContext context)
    {
    }

    // The crate this copy plays, or null while it owns none. Nobody owns the host's crate, so
    // offline and on the host it is the first one. A guest plays the one the server gave it.
    private static string? Mine(IGameNet net) => Crates.FirstOrDefault(id => net.OwnerOf(id) == net.LocalClient);
}
```

Write a check as "inside" and not as "outside". A `NaN` fails every
comparison, so a check that asks whether a place is outside lets it through.

**Owners** are given out in document order. In the editor the host keeps the
earliest `owner` entity and each later one goes to the next guest in join order,
once, when Play starts. A host you build calls
`NetworkedServer.Assign(entityId, clientId)`. When the server adds an entity,
use `document.WithEntity(id, entity)`, which appends. A raw `AddEntity` inserts
at the head unless it is given `after`, and the new entity would take the
host's place.

**A value** that is not a place is a `NetVar<T>`: a `bool`, `int`, `float` or
`Vector3` on an entity. Declare its name and type in an `INetGame`, list the
name in the entity's `sync`, and ask for a handle with `Net.Var<T>(id, name)`.
The server writes it, or the owner of an `owner` entity. A client write throws
`InvalidOperationException`. Offline the handle keeps its own value. The editor
reads `NetVars` before it makes any net, so the schema cannot depend on `Start`.
A schema holds at most 63 names, and a mismatch between ends is not detected.

```json
"net": { "authority": "server", "sync": [ "health" ] }
```

```csharp
using Orblit.Game;
using Orblit.Net.Game;

public sealed class TowerGame : IGame, INetGame
{
    public NetVarSchema NetVars { get; } = NetVarSchema.Empty.With<int>("health");

    public void Start(GameContext context)
    {
        var health = context.Net.Var<int>("tower", "health");
        if (!context.Net.IsClient)
        {
            health.Value = 100;
        }
    }

    public void Update(GameContext context, double seconds)
    {
    }

    public void Stop(GameContext context)
    {
    }
}
```

In the editor, a count beside Play chooses 1 to 4 players. One player runs the
game offline. More start a host and one guest each over `LoopbackLink`, with a
game view per player. The keys go to the player shown.

What it does not do:

- No prediction or rollback, and no interest radius.
- No dedicated server, lobby or relay.
- After a spawn only the place and the `NetVar` values stay in step. Another
  component changed later stays on the copy that changed it.
- Owners are given out once. Nobody joins after Play starts.
- Every offline game in one process shares the handlers `Bind` made, so the last
  `Bind` wins.
- A networked world is not saved. Playing in a browser is not tried.
- It has run headless over `LoopbackLink` and not in a live editor window.

The crate in `orblit-examples` is the worked example, and
`/guides/multiplayer-game/` walks it.
