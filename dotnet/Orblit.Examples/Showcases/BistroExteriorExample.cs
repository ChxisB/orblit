using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// The Bistro's street, at night or in daylight.
/// </summary>
/// <remarks>
/// The reference images are two scenes, and this is the first of them. Night
/// is the interesting one: a hundred small sources, six colours of festoon
/// bulb and almost no ambient. That is the case a renderer either sells or
/// does not. <see cref="BistroFiles"/> says where the scene comes from.
/// </remarks>
public sealed class BistroExteriorExample : Example
{
    private const string Asset = "BistroExterior";

    /// <summary>The moon, in lux. A real full moon is about a quarter of one.</summary>
    /// <remarks>
    /// This was 900 lux, and that one number was most of why the night did not
    /// read as night. It is roughly three thousand times a real full moon, so
    /// it flooded every surface evenly and the hundred lamps, which are the
    /// whole point, added almost nothing next to it. A scene lit flat has no
    /// depth, and no tuning of the lamps fixes a fill light that drowns them.
    /// </remarks>
    private const double Moon = 4;

    /// <summary>Walking pace, in metres a second.</summary>
    private const double Pace = 1.3;

    /// <summary>How long setting off and pulling up each take, in seconds.</summary>
    private const double Gather = 2.0;

    /// <summary>Half the stretch of path the heading is taken across, in metres.</summary>
    private const double Reach = 2.5;

    /// <summary>
    /// How long each half-turn at the end of the walk takes. Long enough to
    /// read as a turn, not a spin. At 3.2 s, once the rest of the walk was
    /// smoothed, the half-turns were the fastest the camera ever swung, at
    /// eighty-five degrees a second.
    /// </summary>
    private const double TurnTime = 4.5;

    /// <summary>
    /// Where the Vespa is: the middle of it, a little low. Read out of the
    /// model rather than found by eye. It stands at about forty-five degrees
    /// to the street with its headlight towards −x and −z, on pavement 0.37 m up.
    /// </summary>
    private static readonly Vector3 Vespa = new(-7.4, 1.25, 0.5);

    /// <summary>Where the walk goes.</summary>
    /// <remarks>
    /// Searched, not chosen, and then checked. Two earlier attempts were
    /// guesses dressed up as reasoning. The first followed the street lamps,
    /// on the argument that lamps stand along a street. They stand on the
    /// pavement with the building between them, so it walked through the
    /// restaurant. The second read an occupancy map by eye at three-metre
    /// resolution and picked a corridor out of it, which clipped eighteen
    /// samples in a hundred.
    ///
    /// These come from a breadth-first search of the open ground. Every
    /// primitive at the height a person stands becomes a solid box. The free
    /// space around the plaza is flooded at half a metre with three quarters
    /// of a metre of clearance, and the walk follows the longest route through
    /// it: two hundred and thirty-six square metres of walkable ground, and
    /// fifty-six metres of walk in it.
    ///
    /// Twelve waypoints rather than five, because the curve between them bows
    /// outward, and a sparse set bows far enough to cut a corner into a wall.
    /// At nine points the tightest clearance was zero. At these the curve
    /// keeps half a metre from anything, measured at six hundred points along
    /// it, which is what makes this a checked path rather than a third guess.
    /// </remarks>
    private static readonly IReadOnlyList<Vector3> Waypoints =
    [
        new(-4.0, 1.7, -12.0),
        new(-9.0, 1.7, -12.0),
        new(-10.0, 1.7, -8.0),
        new(-10.0, 1.7, -3.0),
        new(-10.0, 1.7, 2.0),
        new(-7.0, 1.7, 4.0),
        new(-6.0, 1.7, 8.0),
        new(-2.0, 1.7, 9.0),
        new(1.5, 1.7, 10.5),
        new(4.5, 1.7, 12.5),
        new(8.5, 1.7, 13.5),
        new(11.5, 1.7, 15.5),
    ];

    /// <summary>Declared after <see cref="Waypoints"/>, which it is measured from.</summary>
    private static readonly (List<Vector3> Stations, double Spacing) Measured = Measure();

    private readonly BistroFiles _files;

    /// <summary>Read once. It is a hundred entries and the scene is rebuilt sixty times a second.</summary>
    private readonly Lazy<List<BistroFixture>> _fixtures;

    private bool _night;
    private bool _festoon = true;

    /// <summary>The film speed, which at night is the dial that decides whether there is a picture at all.</summary>
    private double _iso = 1600;

    /// <summary>Walk the street, or stand still by the Vespa.</summary>
    /// <remarks>
    /// An orbit is the right camera for looking at an object and the wrong one
    /// for a place. A street is meant to be walked down. The lamps pass
    /// overhead one at a time, the shopfronts come alongside, and the light on
    /// a wall changes because you moved rather than because the wall did. None
    /// of that shows from a fixed point spinning around the middle. Off, it is
    /// a still of the Vespa: see <see cref="Still"/>.
    /// </remarks>
    private bool _walking = true;

    /// <summary>Finds the scene through <see cref="BistroFiles"/>.</summary>
    public BistroExteriorExample(Func<string, string?> environment, Func<string, bool> exists, Func<string, string> read)
    {
        _files = new BistroFiles(environment, exists, read);
        _fixtures = new(() => _files.Fixtures(Asset));
        Note = _files.Missing(Asset);
    }

    public override string Name => "Bistro exterior";

    public override ExampleSection Section => ExampleSection.Showcases;

    public override string Blurb => "Somebody else's street, lit by this engine. A hundred lights at night.";

    public override ViewPoint ViewPoint => new() { Distance = 22, Pitch = 0.20, Height = 3, Yaw = 2.2 };

    public override Downloadable? Needs => new()
    {
        What = "the Bistro exterior",
        Size = "about 700 MB",
        From = "Amazon Lumberyard Bistro, via ORCA",
        Licence = "CC BY 4.0",
        Command = ["tool/fetch_bistro.sh", "exterior"],
    };

    /// <summary>The length of the walk, in metres.</summary>
    private static double Length => (Measured.Stations.Count - 1) * Measured.Spacing;

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        var radiance = _files.Radiance;
        return new RenderScene
        {
            Camera = Camera(seconds),
            Objects = [_files.Model(Asset)],
            Lights = Lights(),
            // A photograph of a real sky, prefiltered: the reflection in its
            // mip chain and the diffuse in its harmonics. Only by day. It is a
            // bridge at noon, and at night it would light the street with sunshine.
            Environment = _night || radiance.Length == 0
                ? RenderEnvironment.None
                : new RenderEnvironment { Radiance = radiance, Skybox = _files.Skybox, Intensity = 30000 },
            Sky = Overhead(radiance.Length > 0),
            Pipeline = new Pipeline
            {
                // Holds the frame rate rather than the pixel count. What is in
                // view changes enormously as the walk turns, from a wall a
                // metre away to a hundred and seventy metres of street. A frame
                // arriving at an uneven rate judders however smooth the camera
                // is, and this example exists to be looked at while it moves.
                Resolution = new Resolution { Adaptive = true, MinScale = 0.6 },
                Shadows = new ShadowSettings
                {
                    Kind = ShadowKind.Soft,
                    // Four cascades of two thousand square is sixteen million
                    // shadow texels redrawn every frame. Looking down the
                    // street it came to thirty-eight milliseconds, and the
                    // fourth cascade covers ground nothing is close enough to.
                    Cascades = 3,
                    MapSize = 1024,
                    // A hundred and seventy metres of street, so the shadows
                    // are told to reach across it.
                    Distance = 120,
                    // Contact shadows are off. On their own they were
                    // twenty-four milliseconds, a third of the frame, for chair
                    // legs on cobbles in a street nobody stands still in. The
                    // interior keeps them.
                    Softness = 1.2,
                },
                // A street of railings, shutters and thin lamp posts is nothing
                // but edges, and edges are what a single sample makes a mess of.
                Samples = 4,
            },
            Post = Post(),
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Toggle("Walk", _walking, value => _walking = value)
        {
            Note = _walking ? "On foot, looking around" : "Still, by the Vespa",
        },
        new Toggle("Night", _night, value => _night = value)
        {
            Note = _night ? $"{_fixtures.Value.Count} point lights" : "One sun at 100,000 lux",
        },
        new Toggle("Festoon", _festoon, value => _festoon = value) { Note = "The small coloured bulbs", Enabled = _night },
        .. _night ? FilmSpeed() : [],
    ];

    public override string Code => """
        // The fixtures come out of the scene's own emissive geometry, so the
        // lights stand where the artist put the lamps.
        Lights =
        [
            .. fixtures.Select((fixture, i) => new RenderLight
            {
                Key = 100 + i,
                Kind = RenderLightKind.Point,
                Position = fixture.At,
                Intensity = 5200,       // lumens, a street lamp
                FalloffRadius = 28,     // metres
                CastShadows = false,    // a hundred shadow casters is not a thing
            }),
        ],

        // And the sky is still a light, even at night.
        Sky = new Sky { Zenith = Tint.Hex(0x0B1224).Linear, Horizon = Tint.Hex(0x243046).Linear, Ambient = 12 },
        """;

    private List<ExampleSetting> FilmSpeed() =>
    [
        new Setting("Film speed", _iso, 100, 6400, value => _iso = value) { Unit = " ISO", Decimals = 0 },
        new Caption("At ISO 100 this street is black. The lamps have not changed. The camera has."),
    ];

    /// <summary>
    /// The gallery's camera is framed on sunny 16: f/16 at a
    /// hundred-and-twenty-fifth, ISO 100. That is right for daylight and about
    /// seventeen stops too dark for night. On one fixed exposure either the
    /// night is black or the day is white, which is why these are a real
    /// camera's three numbers rather than a brightness.
    /// </summary>
    private RenderCamera Camera(double seconds)
    {
        if (_walking)
        {
            var (eye, look) = Walk(seconds);
            return new RenderCamera
            {
                Position = eye,
                Target = look,
                // Wider on foot. Fifty degrees is a portrait lens, and a street
                // seen through one feels like a corridor.
                FieldOfView = 65,
                Aperture = _night ? 2.0 : 16,
                ShutterSpeed = _night ? 1.0 / 30 : 1.0 / 125,
                Sensitivity = _night ? _iso : 100,
            };
        }

        var (stillEye, stillLook, _) = Still();
        return new RenderCamera
        {
            Position = stillEye,
            Target = stillLook,
            // The long lens the still is framed for.
            FieldOfView = 34,
            // Wide open by day as well, for a background out of focus. f/2.8
            // at a four-thousandth lets in what f/16 at a hundred-and-twenty-
            // fifth does, so only the depth of field changes.
            Aperture = _night ? 2.0 : 2.8,
            ShutterSpeed = _night ? 1.0 / 30 : 1.0 / 4000,
            Sensitivity = _night ? _iso : 100,
        };
    }

    /// <summary>
    /// The lamps and the moon at night. By day it is one light, which is the
    /// point of stating these in lux: a hundred thousand of them is what the
    /// sun is.
    /// </summary>
    private List<RenderLight> Lights() => _night
        ?
        [
            .. BistroFiles.Lamps(_fixtures.Value, _festoon ? 0 : 100),
            new RenderLight
            {
                Key = 1,
                Kind = RenderLightKind.Directional,
                Direction = new Vector3(-0.3, -1, 0.4).Normalized(),
                Colour = Tint.Hex(0x9FB4D8).Linear,
                Intensity = Moon,
            },
        ]
        :
        [
            new RenderLight
            {
                Key = 1,
                Kind = RenderLightKind.Directional,
                Direction = new Vector3(-0.45, -0.82, -0.35).Normalized(),
                Colour = Tint.Hex(0xFFF6E8).Linear,
                Intensity = 100000,
            },
        ];

    /// <summary>The sky, or only its light when the photographed one is there.</summary>
    private Sky Overhead(bool photographed) => _night
        ? new Sky
        {
            Zenith = Tint.Hex(0x0B1224).Linear,
            Horizon = Tint.Hex(0x243046).Linear,
            // Not zero. A night sky still casts light, and shadows with nothing
            // in them read as holes. But close to it: at anything like a
            // hundred lux it stops being night.
            Ambient = 12,
            ShowBody = false,
        }
        : new Sky
        {
            Zenith = Tint.Hex(0x4E86C8).Linear,
            Horizon = Tint.Hex(0xBFD4E8).Linear,
            // Both off when there is a photograph. The procedural sky would win,
            // hiding the very thing that was fetched, and light the scene twice.
            Ambient = photographed ? 0 : 22000,
            Drawn = !photographed,
        };

    private PostProcess Post() => new()
    {
        // It resolves an edge by sampling it in different places on successive
        // frames, which is why it smears when something moves fast. A camera
        // walking at a metre and a third a second is the case it is good at.
        AntiAliasing = AntiAliasing.Temporal,
        // Night only. It makes a small bright bulb read as a light rather than
        // a white dot, and in daylight it only fogs the image.
        Bloom = new Bloom { Enabled = _night, Strength = 0.22, Levels = 7 },
        // The cheapest stand-in for the contact darkening bounced light would
        // give. Without it everything sits on the ground rather than in it.
        Occlusion = new Occlusion { Enabled = true, Quality = 2, Radius = 0.4 },
        // Wet cobbles and shop glass. Screen space reflects only what is on
        // screen, which is most of what a street reflects anyway.
        Reflections = new Reflections { Enabled = true, MaxDistance = 6 },
        // For the still only: the Vespa sharp and the street behind it soft.
        // The caps stop the far end of the street dissolving. At the default
        // of one pixel they stopped the blur from showing at all.
        DepthOfField = new DepthOfField
        {
            Enabled = !_walking,
            FocusDistance = _walking ? 10 : Still().Focus,
            BlurScale = 3,
            MaxForeground = 12,
            MaxBackground = 16,
        },
        // ACES for more contrast and saturation. It matters most at night,
        // where the gap between a lamp and the dark it stands in is the picture.
        Grading = new Grading { Enabled = true, ToneMapping = ToneMapping.Aces, Contrast = _night ? 1.06 : 1.0 },
    };

    /// <summary>
    /// The camera when not walking: a still of the Vespa, and how far away it
    /// is focused.
    /// </summary>
    /// <remarks>
    /// Still, so the temporal anti-aliasing settles into a clean picture.
    /// Framed the way a scooter is photographed: from the front three-quarters,
    /// low, on a long lens a few metres off, so the shopfronts behind stay
    /// large and fall out of focus. Straight down the headlight's line it is
    /// all shield. Side on it is a diagram. Just over forty degrees off its
    /// nose shows the shield, the curve of the body and both wheels at once.
    /// </remarks>
    private static (Vector3 Eye, Vector3 Look, double Focus) Still()
    {
        const double Bearing = -168 * Math.PI / 180;
        const double Distance = 6.0;
        // About knee height: 0.7 m above the pavement.
        const double Height = 1.05;
        const double Lead = 0.5;

        var away = new Vector3(Math.Cos(Bearing), 0, Math.Sin(Bearing));
        var eye = Vespa + (away * Distance);
        eye.Y = Height;
        // Aimed half a metre to the side it faces, which puts it right of
        // centre with the room in front of it rather than behind.
        var look = Vespa + (new Vector3(-away.Z, 0, away.X) * Lead);
        return (eye, look, (Vespa - eye).Length);
    }

    private static (Vector3 Eye, Vector3 Look) Walk(double seconds)
    {
        var legTime = (Length / Pace) + Gather;
        var (distance, extraTurn) = Leg(seconds % ((legTime + TurnTime) * 2), legTime);
        var eye = At(distance);

        // The way the body faces: along the line from a couple of metres back
        // to a couple of metres on. The direction of the piece of path
        // underfoot changes in a step at every piece, and eight metres out a
        // step of a few degrees is a jump of half a metre. The line across
        // five metres turns smoothly and starts into a bend a little early, as
        // somebody walking does. On the way back it still points the way the
        // path was drawn, so the half-turns carry the direction.
        var heading = At(distance + Reach) - At(distance - Reach);
        var yaw = Math.Atan2(heading.Z, heading.X) + (Math.PI * extraTurn);

        // Looking about on top of that, in two slow waves so it never settles
        // into a rhythm. No bob: a bob read as the picture shaking.
        var sweep = (Math.Sin(seconds * 0.19) * 0.32) + (Math.Sin(seconds * 0.081) * 0.14);
        var look = yaw + sweep;

        // A little up. The lamps, the signs and the balconies are above eye level.
        var rise = 0.14 + (Math.Sin(seconds * 0.13) * 0.09);
        return (eye, eye + new Vector3(Math.Cos(look) * 8, rise * 8, Math.Sin(look) * 8));
    }

    /// <summary>
    /// How far along the walk is <paramref name="t"/> into a cycle, and how
    /// many half-turns it has made. The turn only grows through a cycle,
    /// because a yaw that jumps back is a snap.
    /// </summary>
    private static (double Distance, double ExtraTurn) Leg(double t, double legTime)
    {
        if (t < legTime)
        {
            return (Along(t, legTime), 0);
        }

        if (t < legTime + TurnTime)
        {
            return (Length, Ease((t - legTime) / TurnTime));
        }

        if (t < (legTime * 2) + TurnTime)
        {
            return (Length - Along(t - legTime - TurnTime, legTime), 1);
        }

        return (0, 1 + Ease((t - (legTime * 2) - TurnTime) / TurnTime));
    }

    /// <summary>
    /// How far along the path a leg is, <paramref name="t"/> seconds after
    /// setting off. Each end eases over <see cref="Gather"/> seconds with the
    /// speed on a smoothstep, so not even the acceleration jumps. An eased
    /// start covers the smoothstep's integral, x³ − x⁴/2, which is half of
    /// full pace. That is why a leg takes <see cref="Gather"/> seconds longer
    /// than the path at full pace.
    /// </summary>
    private static double Along(double t, double legTime)
    {
        if (t < Gather)
        {
            return Eased(t);
        }

        return t > legTime - Gather ? Length - Eased(legTime - t) : Pace * (t - (Gather / 2));
    }

    private static double Eased(double t)
    {
        var x = Math.Clamp(t / Gather, 0.0, 1.0);
        return Pace * Gather * ((x * x * x) - (x * x * x * x / 2));
    }

    /// <summary>Smoothstep: starts and stops at zero speed.</summary>
    private static double Ease(double t)
    {
        var c = Math.Clamp(t, 0.0, 1.0);
        return c * c * (3 - (2 * c));
    }

    /// <summary>
    /// The path as a Catmull-Rom curve, which passes through every waypoint
    /// with a continuous tangent. A polyline turns sharply at each one.
    /// </summary>
    private static Vector3 Spline(double u)
    {
        var n = Waypoints.Count;
        var scaled = Math.Clamp(u, 0.0, 1.0) * (n - 1);
        var i = Math.Clamp((int)Math.Floor(scaled), 0, n - 2);
        var f = scaled - i;
        // The ends are doubled up so the curve starts and finishes where the
        // waypoints do rather than overshooting past them.
        var p0 = Waypoints[Math.Clamp(i - 1, 0, n - 1)];
        var p1 = Waypoints[i];
        var p2 = Waypoints[Math.Clamp(i + 1, 0, n - 1)];
        var p3 = Waypoints[Math.Clamp(i + 2, 0, n - 1)];
        return ((p1 * 2.0)
            + ((p2 - p0) * f)
            + (((p0 * 2.0) - (p1 * 5.0) + (p2 * 4.0) - p3) * (f * f))
            + (((p1 * 3.0) - p0 - (p2 * 3.0) + p3) * (f * f * f))) * 0.5;
    }

    /// <summary>
    /// The curve staked out about five centimetres apart, and the distance
    /// between stakes. A spline's parameter is not its length, so stepping it
    /// evenly speeds up through the straights and dawdles round the bends. It
    /// is measured once, finely enough that a chord is the curve.
    /// </summary>
    private static (List<Vector3> Stations, double Spacing) Measure()
    {
        const int Fine = 4000;
        var points = Enumerable.Range(0, Fine + 1).Select(i => Spline((double)i / Fine)).ToList();
        var run = new List<double> { 0 };
        for (var i = 1; i <= Fine; i++)
        {
            run.Add(run[^1] + (points[i] - points[i - 1]).Length);
        }

        var count = (int)Math.Ceiling(run[^1] / 0.05);
        var spacing = run[^1] / count;
        var stations = new List<Vector3>();
        var j = 0;
        for (var k = 0; k <= count; k++)
        {
            var want = k * spacing;
            while (j < Fine - 1 && run[j + 1] < want)
            {
                j++;
            }

            var span = run[j + 1] - run[j];
            var f = span > 0 ? Math.Clamp((want - run[j]) / span, 0.0, 1.0) : 0.0;
            stations.Add(points[j] + ((points[j + 1] - points[j]) * f));
        }

        return (stations, spacing);
    }

    /// <summary>
    /// Where the walk is <paramref name="distance"/> metres along the path.
    /// Off either end the path carries straight on, so a heading taken near an
    /// end has somewhere to look.
    /// </summary>
    private static Vector3 At(double distance)
    {
        var (stations, spacing) = Measured;
        var last = stations.Count - 1;
        if (distance <= 0)
        {
            var start = stations[0];
            return start + ((stations[1] - start).Normalized() * distance);
        }

        if (distance >= Length)
        {
            var end = stations[last];
            return end + ((end - stations[last - 1]).Normalized() * (distance - Length));
        }

        var along = distance / spacing;
        var i = Math.Clamp((int)Math.Floor(along), 0, last - 1);
        return stations[i] + ((stations[i + 1] - stations[i]) * (along - i));
    }
}
