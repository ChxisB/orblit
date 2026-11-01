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
