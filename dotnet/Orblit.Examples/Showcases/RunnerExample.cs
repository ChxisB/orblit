using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>Where a run is.</summary>
public enum RunnerPhase
{
    /// <summary>Standing at the start, waiting to be told to go.</summary>
    Ready,

    /// <summary>Running, and taking the moves it is given.</summary>
    Running,

    /// <summary>Stopped part way, where it was left.</summary>
    Paused,

    /// <summary>Crashed, and waiting to be told to go again.</summary>
    Over,
}

/// <summary>
/// A three-lane runner, played.
/// </summary>
/// <remarks>
/// The genre is the one everybody knows. The world comes at you, and you change
/// lane, jump and slide to stay in it, picking up coins on the way. What makes
/// it a game rather than a picture of one is that every one of those is real. A
/// hurdle is cleared by being higher than it at the moment you reach it. A bar
/// is cleared by being lower, and a container by not being in its lane. Getting
/// it wrong ends the run.
///
/// Everything drawn is a model, made in code by <see cref="RunnerArt"/> and
/// handed to the renderer the way a downloaded one would be. The world is made
/// as it is reached and forgotten once it is behind. The scenery comes in forty
/// metre chunks whose keys are reused as the chunks come round. The grass is one
/// population per chunk, sent once when the chunk is laid.
///
/// The runner really does run. Holding it still and moving the world past would
/// mean moving every blade of grass every frame. So the world stays where it was
/// put and the runner and camera go through it. Every kilometre, the lot is
/// moved back by a kilometre so the numbers never get large enough for a float
/// to notice.
/// </remarks>
public sealed partial class RunnerExample : Example
{
    private readonly Action<string, byte[]> _provide;
    private bool _requested;
    private bool _artReady;

    private double? _last;
    private double _clock;
    private double _readySince;
    private double _runTime;
    private double _along;
    private double _prevAlong;
    private double _ran;
    private double _origin;
    private double _speed = StartSpeed;
    private double _slow = 1;
    private int _lane = 1;
    private double _x;
    private double _prevX;
    private double _y;
    private double _vy;
    private double _slideLeft;
    private bool _slideQueued;
    private double _jumpBuffer;
    private double _landedAt = -10;
    private double _impact;
    private double _stumbledAt = -100;
    private double _crashedAt;
    private double _spin;
    private double _intro = 1;
    private double _shake;
    private double _step;
    private int _bonus;
    private int _streak;
    private int _popup;
    private double _lastCoinAt = -100;
    private int _runs;
    private Random _chance = new(1);
    private double _nextRow;
    private int _hazardCount;
    private int _coinCount;

    private readonly List<Hazard> _hazards = [];
    private readonly List<Coin> _road = [];
    private readonly Spark[] _sparks = [.. Enumerable.Range(0, 48).Select(_ => new Spark())];
    private int _nextSpark;

    private readonly int[] _slotChunk = [.. Enumerable.Repeat(Unlaid, Chunks)];
    private readonly double[] _slotOrigin = [.. Enumerable.Repeat(double.NaN, Chunks)];
    private readonly List<RenderObject>[] _props = [.. Enumerable.Range(0, Chunks).Select(_ => new List<RenderObject>())];
    private readonly float[][] _grass = [.. Enumerable.Range(0, Chunks).Select(_ => new float[BladesPerChunk * 16])];
    private readonly float[][] _grassColours = [.. Enumerable.Range(0, Chunks).Select(_ => new float[BladesPerChunk * 3])];
    private readonly int[] _grassRevision = new int[Chunks];
    private readonly Bounds[] _grassBounds = new Bounds[Chunks];

    /// <param name="provide">Hands bytes to the renderer under a name, as <see cref="RenderResources.Provide"/> does.</param>
    /// <param name="environment">Reads an environment variable, or null when it is not set.</param>
    public RunnerExample(Action<string, byte[]> provide, Func<string, string?> environment)
    {
        _provide = provide;
        // Something recording the gallery has no hands. ORBLIT_AUTOPILOT=1
        // starts a run and keeps it going, and the panel's own switch still
        // works over the top of it.
        Autopilot = environment("ORBLIT_AUTOPILOT") == "1";
        Reset();
    }

    public override string Name => "Runner";

    public override ExampleSection Section => ExampleSection.Showcases;

    public override string Blurb => "A runner you can play: dodge, jump, slide and pick up coins.";

    // Only a starting hint for whatever is showing this. The game gives its own
    // camera every frame, because a runner is followed rather than orbited.
    public override ViewPoint ViewPoint => new() { Distance = 13, Pitch = 0.24, Height = 1.6, Yaw = 0 };

    /// <summary>Where the run is.</summary>
    public RunnerPhase Phase { get; private set; }

    /// <summary>
    /// Whether the game plays itself. Off by default, because somebody who
    /// opens this wants to play it.
    /// </summary>
    public bool Autopilot { get; set; }

    /// <summary>Whether hitting something only slows you down.</summary>
    public bool Invincible { get; set; }

    /// <summary>
    /// How fast a run gets, in metres a second. It starts at fourteen and
    /// closes on this over the first few minutes.
    /// </summary>
    public double TopSpeed { get; set; } = 30;

    /// <summary>How far this run went, in metres, up to the moment it ended.</summary>
    public double Travelled => _ran;

    /// <summary>Coins picked up this run.</summary>
    public int Coins { get; private set; }

    /// <summary>Metres run, plus what the coins were worth.</summary>
    public int Score => (int)Math.Floor(_ran) + _bonus;

    /// <summary>The best score since the game was opened.</summary>
    public int Best { get; private set; }

    /// <summary>
    /// The view's width over its height. A narrow view widens the field of
    /// view so all three lanes stay in it.
    /// </summary>
    public double Aspect { get; set; } = 16.0 / 9;

    /// <summary>Starts running, from the start.</summary>
    public void Start()
    {
        if (Phase != RunnerPhase.Ready)
        {
            return;
        }

        Phase = RunnerPhase.Running;
        _intro = 0;
    }

    /// <summary>Starts a new run, straight into running.</summary>
    public void Restart()
    {
        Reset();
        Phase = RunnerPhase.Running;
        _intro = 1;
    }

    public void Pause()
    {
        if (Phase == RunnerPhase.Running)
        {
            Phase = RunnerPhase.Paused;
        }
    }

    public void Resume()
    {
        if (Phase == RunnerPhase.Paused)
        {
            Phase = RunnerPhase.Running;
        }
    }

    /// <summary>One lane to the left for -1 and to the right for 1.</summary>
    public void Steer(int direction)
    {
        var to = _lane + direction;
        if (Phase != RunnerPhase.Running || to < 0 || to >= Lanes.Length)
        {
            return;
        }

        _lane = to;
    }

    public void Jump()
    {
        if (Phase != RunnerPhase.Running)
        {
            return;
        }

        if (Grounded)
        {
            _vy = JumpSpeed;
            _slideLeft = 0;
        }
        else
        {
            // Pressed a moment before landing, which is when people press it.
            // It is kept and acted on at the landing rather than thrown away.
            _jumpBuffer = 0.15;
        }
    }

    public void Slide()
    {
        if (Phase != RunnerPhase.Running)
        {
            return;
        }

        if (_y <= 0)
        {
            // Still on the ground, even if a jump was asked for a moment ago.
            // The later of the two wins, as it would if they were a frame apart.
            _vy = 0;
            _slideLeft = SlideTime;
        }
        else
        {
            // In the air, down means down now. It drops to the ground and
            // slides when it gets there, which is how a jump that was a
            // mistake is undone.
            _vy = Math.Min(_vy, -18);
            _slideQueued = true;
        }
    }

    private bool Grounded => _y <= 0 && _vy <= 0;

    private bool IsSliding => _slideLeft > 0 && Grounded;

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        Prepare();

        var dt = seconds - (_last ?? seconds);
        _last = seconds;
        if (dt is < 0 or > 0.5)
        {
            // The example was put away and brought back, which starts its
            // clock again, or the application stalled. Either way the time
            // that passed is not time anybody played through.
            if (!Autopilot)
            {
                Pause();
            }

            dt = 0;
        }

        Update(Math.Min(dt, 0.05));

        var sun = new Vector3(-0.5, -0.75, -0.43).Normalized();
        var horizon = Tint.Hex(0xBFE3FA).Linear;
        return new RenderScene
        {
            Camera = Camera(),
            Objects = Objects(),
            Populations = Grass(),
            Materials =
            [
                // The road and the fields are white, so the textures are the colour.
                new RenderMaterial { Key = 1, BaseColourMap = new Texture(RunnerArt.Asphalt), Roughness = 0.9 },
                new RenderMaterial { Key = 2, BaseColourMap = new Texture(RunnerArt.Grass), Roughness = 0.95 },
                // The coins glow a little. They are not metal, though a coin is.
                // A metal shows its surroundings and nothing else, and the
                // surroundings here are a painted sky. So a metal coin comes out
                // a dull brown.
                new RenderMaterial
                {
                    Key = 3,
                    BaseColour = new Vector4(1.0, 0.54, 0.01, 1),
                    Roughness = 0.3,
                    Emissive = new Vector3(1.0, 0.62, 0.08),
                    EmissiveIntensity = 0.9,
                },
                new RenderMaterial
                {
                    Key = 4,
                    BaseColour = new Vector4(1.0, 0.62, 0.05, 1),
                    Emissive = new Vector3(1.0, 0.7, 0.12),
                    EmissiveIntensity = 4,
                },
            ],
            Lights =
            [
                new RenderLight
                {
                    Key = 1,
                    Kind = RenderLightKind.Directional,
                    Direction = sun,
                    Colour = Tint.Hex(0xFFF4E0).Linear,
                    Intensity = 95000,
                    CastShadows = true,
                },
            ],
            // Clear, because the clouds are models drawn with everything else.
            Sky = new Sky
            {
                Zenith = Tint.Hex(0x2B78D6).Linear,
                Horizon = horizon,
                Ambient = 34000,
                BodyDirection = -sun,
                Clouds = Clouds.None,
            },
            // Finished by the end of the world. The scenery stops a little over
            // three hundred metres ahead, and a haze that is still thin there
            // shows the edge of it.
            Fog = new Fog { Colour = horizon, Density = 0.012, Distance = 45, HeightFalloff = 0.12 },
            Pipeline = new Pipeline
            {
                Shadows = new ShadowSettings { Kind = ShadowKind.Soft, Cascades = 2, MapSize = 2048, Distance = 70 },
                // Grass is a thousand edges a pixel wide, and those shimmer
                // under anything less.
                Samples = 4,
                Resolution = new Resolution { Adaptive = true, MinScale = 0.7 },
            },
            Post = new PostProcess
            {
                AntiAliasing = AntiAliasing.Off,
                Bloom = new Bloom { Enabled = true, Strength = 0.08 },
                Grading = new Grading { Enabled = true, Saturation = 1.12, Vibrance = 1.1 },
            },
        };
    }

    /// <summary>
    /// Moves the game on by <paramref name="dt"/> seconds. What
    /// <see cref="Scene"/> does each frame before drawing, and public so a test
    /// can run a minute of the game without drawing any of it.
    /// </summary>
    public void Update(double dt)
    {
        LaySceneryAround();
        if (Phase == RunnerPhase.Paused)
        {
            return;
        }

        _clock += dt;
        _shake = Math.Max(0, _shake - dt * 1.6);
        UpdateSparks(dt);

        switch (Phase)
        {
            case RunnerPhase.Ready when Autopilot && _clock - _readySince > 1.2:
                Start();
                break;
            case RunnerPhase.Running:
                Run(dt);
                break;
            case RunnerPhase.Over:
                Tumble(dt);
                if (Autopilot && _clock - _crashedAt > 2.5)
                {
                    Restart();
                }

                break;
        }
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Caption(
            "Click the game, then use the arrow keys or WASD. Left and right change lane, up jumps a "
            + "hurdle and down slides under a bar. Go round the containers. Space starts and Esc pauses."),
        new Toggle("Autopilot", Autopilot, value => Autopilot = value)
        {
            Note = "Plays itself, with the same three moves you have",
        },
        new Toggle("Can't crash", Invincible, value => Invincible = value)
        {
            Note = "Hitting something slows you down instead of ending the run",
        },
        new Setting("Top speed", TopSpeed, 20, 40, value => TopSpeed = value) { Decimals = 0, Unit = " m/s" },
        new Buttons("Run", ["Start again"], _ => Restart()),
        new Caption(
            $"{_props.Sum(props => props.Count)} pieces of scenery and {BladesPerChunk * Chunks} blades of grass "
            + $"in {Chunks} chunks. {_hazards.Count} obstacles and {_road.Count} coins on the road ahead. "
            + "Every model is made in code and sent to the renderer once."),
    ];

    public override string Code => """
        // Every model is made in code, written out as a glb, and handed to the
        // renderer by name, the same path a downloaded file takes.
        foreach (var (name, bytes) in RunnerArt.Build())
        {
            RenderResources.Provide(name, bytes);
        }

        // The world is laid in chunks as the runner reaches them. A chunk that
        // falls behind hands its keys to the one being laid ahead, so the
        // renderer reuses what it made rather than making more.
        var key = 1000 + type * 100 + slot * cap + index;

        // Grass is one population per chunk, sent when the chunk is laid and
        // never again. The revision only changes when the blades do.
        new Population(1 + slot, blades, shades, new Bounds(low, high))
        {
            Revision = revisions[slot],
            Range = 60,
        };

        // A hurdle is cleared by being above it when you reach it, a bar by
        // being below it, and a container by not being in its lane.
        var blocked = hazard.Kind switch
        {
            Kind.Hurdle => y < 0.92,
            Kind.Bar => y + (sliding ? 0.8 : 1.3) > 1.05,
            Kind.Container => true,
        };
        """;

    private List<RenderObject> Objects()
    {
        // The road and the fields, each one long piece moved along in whole
        // repeats of its texture, so the move cannot be seen.
        var snap = Math.Floor(_along / RunnerArt.RoadRepeat) * RunnerArt.RoadRepeat;
        List<RenderObject> objects =
        [
            new RenderObject
            {
                Key = 10,
                Mesh = RunnerArt.Road,
                Material = 1,
                Transform = RunnerArt.Place((0, 0, Z(snap)), (1, 1, 1)),
                Colour = White,
                CastShadows = false,
            },
            new RenderObject
            {
                Key = 11,
                Mesh = RunnerArt.Ground,
                Material = 2,
                Transform = RunnerArt.Place((0, -0.03, Z(snap)), (1, 1, 1)),
                Colour = White,
                CastShadows = false,
            },
        ];

        DrawRunner(objects);
        DrawClouds(objects);
        foreach (var props in _props)
        {
            objects.AddRange(props);
        }

        DrawRoad(objects);
        DrawSparks(objects);

        // Until the renderer has the files, a model it is asked for is drawn as
        // a placeholder cube. So the models are left out until then, and the
        // world arrives whole rather than as a field of boxes.
        if (!_artReady)
        {
            objects.RemoveAll(one => one.Mesh is not null);
        }

        return objects;
    }

    private List<Population> Grass() =>
    [
        .. Enumerable.Range(0, Chunks)
            .Where(slot => _slotChunk[slot] != Unlaid)
            .Select(slot => new Population(1 + slot, _grass[slot], _grassColours[slot], _grassBounds[slot])
            {
                Revision = _grassRevision[slot],
                Range = GrassRange,
            }),
    ];
}
