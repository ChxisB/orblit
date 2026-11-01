using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// What the engine costs, with the load in your hands.
/// </summary>
/// <remarks>
/// Every other example is arranged to look like something. This one is
/// arranged to be measured: each dial adds work of one particular kind, and
/// the readout says what a frame costs the GPU. Not how often a frame is
/// presented, which is the display's business and looks identical whether the
/// engine has ten per cent of headroom or two hundred.
///
/// The three loads are separated because they are not the same cost at all.
/// A member of a population is a row in a buffer and sixty-four of them share
/// a draw. An object is tracked one at a time: it has a key, it is compared
/// against last frame, and it gets its own entity. The sky is neither. It is
/// a volume marched per pixel, and it is the only one of the three whose cost
/// does not depend on how much of anything there is.
/// </remarks>
public sealed class BenchmarkExample : Example
{
    private static readonly IReadOnlyList<(string Name, SkyQuality Quality)> Skies =
    [
        ("Lean", SkyQuality.Lean),
        ("Fair", SkyQuality.Fair),
        ("Full", SkyQuality.Full),
    ];

    // Members of a population: a row in a buffer, sixty-four to a draw.
    private double _members = 50000;
    private double _wantedMembers = 50000;

    // Objects tracked one at a time, each with its own entity and draw.
    private double _objects = 200;
    private double _wantedObjects = 200;

    private string _sky = "Fair";
    private bool _shadows = true;
    private bool _moving;

    private float[] _transforms = [];
    private float[] _colours = [];
    private int _revision;
    private int _built = -1;

    public override string Name => "Benchmark";

    public override string Blurb => "Turn the load up and watch what a frame costs. Nothing here is staged.";

    public override ExampleSection Section => ExampleSection.Performance;

    public override ViewPoint ViewPoint => new() { Distance = 70, Pitch = 0.36, Height = 4 };

    /// <summary>The load being drawn, for a readout beside the cost. A slider moves it when it is let go.</summary>
    public string Load =>
        $"{Math.Round(_members, MidpointRounding.AwayFromZero)} members · {Math.Round(_objects, MidpointRounding.AwayFromZero)} objects · sky {_sky.ToLowerInvariant()}";

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        var total = (int)Math.Round(_members, MidpointRounding.AwayFromZero);
        if (_built != total)
        {
            Fill(total);
            _built = total;
            _revision++;
        }

        // Only when something is asked to move, because the point of a
        // population is that standing still costs nothing at all.
        if (_moving)
        {
            _revision++;
        }

        var individually = (int)Math.Round(_objects, MidpointRounding.AwayFromZero);
        var drift = _moving ? seconds : 0.0;
        return new RenderScene
        {
            Populations = total > 0
                ? [new Population(1, _transforms, _colours, new Bounds(new Vector3(-60, -2, -60), new Vector3(60, 8, 60))) { Revision = _revision, CastShadows = _shadows }]
                : [],
            Objects =
            [
                // The other kind of load: each of these is compared against
                // last frame, gets an entity of its own, and costs a draw.
                .. Enumerable.Range(0, individually).Select(i => Box(i, drift)),
                Floor(),
            ],
            Lights =
            [
                new RenderLight
                {
                    Key = 1980,
                    Kind = RenderLightKind.Directional,
                    Intensity = 78000,
                    Direction = new Vector3(-0.4, -0.9, -0.35).Normalized(),
                    Colour = Tint.Hex(0xFFF3E0).Linear,
                    SunAngularRadius = 0.6,
                    CastShadows = _shadows,
                },
            ],
            Fog = new Fog { Colour = Tint.Hex(0xB9CBDD).Linear, Density = 0.004, Distance = 25, MaximumOpacity = 1 },
            Sky = new Sky
            {
                Colour = Tint.Hex(0x6E8DB4).Linear,
                Zenith = Tint.Hex(0x2F5F97).Linear,
                Horizon = Tint.Hex(0xB9CBDD).Linear,
                Ambient = 24000,
                BodyDirection = new Vector3(0.4, 0.9, 0.35).Normalized(),
                BodyColour = Tint.Hex(0xFFF6E8).Linear,
                BodySize = 0.011,
                Quality = Skies.First(sky => sky.Name == _sky).Quality,
                Clouds = Clouds.Cumulus with { Cover = 0.42, Wind = new Vector2(4, 1.5) },
            },
            Camera = camera,
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Setting("Members", _wantedMembers, 0, 200000, value => _wantedMembers = value)
        {
            // Laying two hundred thousand out takes long enough that doing it
            // on every pixel of a drag is the only thing you would be measuring.
            Settle = value => _members = value,
            Decimals = 0,
        },
        new Setting("Objects", _wantedObjects, 0, 3000, value => _wantedObjects = value)
        {
            Settle = value => _objects = value,
            Decimals = 0,
        },
        new Choice("Sky", [.. Skies.Select(sky => sky.Name)], _sky, sky => _sky = sky),
        new Toggle("Shadows", _shadows, value => _shadows = value),
        new Toggle("Moving", _moving, value => _moving = value),
    ];

    public override string Code => """
        // The three loads, and why they are separate dials.
        //
        // A member of a population is a row in a buffer. Sixty-four share a draw, and
        // standing still costs nothing at all. The buffer is only sent when its
        // revision moves.
        new Population(1, transforms, colours, bounds) { Revision = revision }

        // An object is tracked one at a time: its own key, its own entity, its own
        // draw, compared against last frame every frame.
        new RenderObject { Key = 2000 + i, Transform = ..., Colour = ... }

        // And the sky is neither. It is a volume marched per pixel, so its cost has
        // nothing to do with how much of anything is in the scene.
        new Sky { Quality = SkyQuality.Fair, Clouds = Clouds.Cumulus with { Cover = 0.42 } }

        // What a frame cost, from Filament's own frame history: the median of the
        // last handful, because a mean is dragged about by the one frame in thirty
        // that hits a hitch.
        var ms = renderer.GetStats().GpuMilliseconds;
        """;

    private static RenderObject Floor()
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(0, -1.2, 0));
        transform.ScaleByDouble(90.0, 0.06, 90.0, 1);
        return new RenderObject
        {
            Key = 1990,
            Transform = transform,
            Colour = Tint.Hex(0x39404A).Linear,
            CastShadows = false,
        };
    }

    private RenderObject Box(int i, double drift)
    {
        var angle = i * 2.399963;
        var radius = 1.6 * Math.Sqrt(i + 1);
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(
            Math.Cos(angle) * radius,
            0.6 + (Math.Sin((drift * 0.9) + i) * (_moving ? 0.5 : 0)),
            Math.Sin(angle) * radius));
        transform.RotateY((i * 0.7) + (drift * 0.3));
        transform.ScaleByDouble(0.6, 1.2, 0.6, 1);
        return new RenderObject
        {
            Key = 2000 + i,
            Transform = transform,
            Colour = Tint.Lerp(Tint.Hex(0xD9634F), Tint.Hex(0x5FA8D3), (i % 17) / 16.0).Linear,
            CastShadows = _shadows,
        };
    }

    private void Fill(int total)
    {
        _transforms = new float[total * 16];
        _colours = new float[total * 3];

        var random = new Random(11);
        for (var i = 0; i < total; i++)
        {
            var x = ((random.NextDouble() * 2) - 1) * 58;
            var z = ((random.NextDouble() * 2) - 1) * 58;
            var height = 0.4 + (random.NextDouble() * 2.6);
            var width = 0.25 + (random.NextDouble() * 0.4);
            var turn = random.NextDouble() * Math.PI * 2;
            var c = (float)(Math.Cos(turn) * width);
            var s = (float)(Math.Sin(turn) * width);
            ReadOnlySpan<float> columns =
            [
                c, 0, -s, 0,
                0, (float)(height * 0.5), 0, 0,
                s, 0, c, 0,
                (float)x, (float)((height * 0.5) - 1.2), (float)z, 1,
            ];
            columns.CopyTo(_transforms.AsSpan(i * 16));

            var colour = Tint.Lerp(Tint.Hex(0x6F8F55), Tint.Hex(0xB86B3C), random.NextDouble()).Linear;
            _colours[i * 3] = (float)colour.X;
            _colours[(i * 3) + 1] = (float)colour.Y;
            _colours[(i * 3) + 2] = (float)colour.Z;
        }
    }
}
