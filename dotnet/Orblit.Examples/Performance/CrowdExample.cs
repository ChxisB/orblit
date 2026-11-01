using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// A hundred thousand of them, in four draw calls.
/// </summary>
/// <remarks>
/// The object path in <see cref="ManyExample"/> is the right shape for a scene
/// somebody is arranging by hand: each thing has a key, is compared against
/// what was there last frame, and gets its own entity. Push that to six
/// figures and every one of those virtues becomes a cost: a hundred thousand
/// comparisons, a hundred thousand entities, a hundred thousand draw calls.
///
/// A population turns it around. One mesh, one buffer of transforms, and the
/// renderer submits it once per thirty-two thousand members, which is
/// Filament's own limit rather than ours.
///
/// The second half of it is the wire. Six megabytes of transforms is not
/// something to send sixty times a second in order to say nothing moved, so
/// the buffer is only sent when its revision changes. Leave Drifting off and
/// the scene costs nothing per member per frame at all: it is built once and
/// then only looked at. That is the difference between a world and a
/// slideshow, and it is the same trick every game that draws a forest uses.
/// </remarks>
public sealed class CrowdExample : Example
{
    // What is drawn, and what the sliders are showing.
    //
    // Two of each, because laying a hundred thousand members out takes fifty
    // milliseconds and a slider reports every pixel of a drag. Rebuilding on
    // each report is eighteen frames a second while dragging. So the drawn
    // numbers only catch up when the handle is let go.
    private double _count = 100000;
    private double _wantedCount = 100000;
    private bool _drifting;
    private double _spread = 700;
    private double _wantedSpread = 700;
    private double _range = 300;

    // The buffers, kept rather than rebuilt. This is the point. Allocating a
    // hundred thousand transforms every frame would cost more than drawing
    // them, so they are written in place and the revision says when that has
    // happened.
    private float[] _transforms = [];
    private float[] _colours = [];
    private int _revision;
    private int _built = -1;
    private double _builtSpread = -1;
    private double _driftedTo = -1;

    public override string Name => "A hundred thousand";

    public override string Blurb => "One buffer of transforms, sent once, drawn in a handful of calls.";

    public override ExampleSection Section => ExampleSection.Performance;

    public override ViewPoint ViewPoint => new() { Distance = 190, Pitch = 0.34, Height = 6 };

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        var total = (int)Math.Round(_count, MidpointRounding.AwayFromZero);
        if (_built != total || _builtSpread != _spread)
        {
            Fill(total);
            _built = total;
            _builtSpread = _spread;
            _revision++;
        }

        // Ten times a second rather than sixty, because the point being made
        // is that moving them is the expensive half and standing still is free.
        var beat = Math.Floor(seconds * 10);
        if (_drifting && beat != _driftedTo)
        {
            Drift(total, seconds);
            _driftedTo = beat;
            _revision++;
        }

        return new RenderScene
        {
            Populations =
            [
                // One box around the lot. Every member is culled by it, so it
                // has to cover all of them.
                new Population(1, _transforms, _colours, new Bounds(new Vector3(-_spread, -2, -_spread), new Vector3(_spread, 14, _spread)))
                {
                    Revision = _revision,
                    // How far a member is still drawn from. The whole map stays
                    // loaded in one buffer, uploaded once. What is too far to
                    // see costs a distance test rather than a draw.
                    Range = _range,
                },
            ],
            Objects = [Floor()],
            Lights =
            [
                new RenderLight
                {
                    Key = 910,
                    Kind = RenderLightKind.Directional,
                    Intensity = 82000,
                    Direction = new Vector3(-0.4, -0.86, -0.32).Normalized(),
                    Colour = Tint.Hex(0xFFF3E0).Linear,
                    SunAngularRadius = 0.6,
                },
            ],
            Fog = Air,
            Sky = Sky,
            Camera = camera,
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Setting("Members", _wantedCount, 1000, 100000, value => _wantedCount = value)
        {
            Settle = value => _count = value,
            Decimals = 0,
        },
        new Setting("Spread", _wantedSpread, 40, 2000, value => _wantedSpread = value)
        {
            Settle = value => _spread = value,
            Unit = " m",
            Decimals = 0,
        },
        new Setting("View range", _range, 0, 600, value => _range = value) { Unit = " m", Decimals = 0 },
        new Toggle("Drifting", _drifting, value => _drifting = value),
    ];

    public override string Code => """
        // One mesh, one buffer of transforms, one submission.
        //
        // The buffer is written once and kept. Revision is what tells the renderer
        // whether it has to be sent again. Leave it alone and a hundred thousand
        // members cost nothing per frame at all.
        var transforms = new float[count * 16];
        var colours = new float[count * 3];

        for (var i = 0; i < count; i++)
        {
            // Column major, straight into the buffer. Nothing is allocated per member.
            transforms[(i * 16) + 0] = width;
            transforms[(i * 16) + 5] = height;
            transforms[(i * 16) + 10] = width;
            transforms[(i * 16) + 12] = x;
            transforms[(i * 16) + 13] = y;
            transforms[(i * 16) + 14] = z;
            transforms[(i * 16) + 15] = 1;
        }

        new RenderScene
        {
            Populations =
            [
                // Every member is culled by this one box, so it must cover all of them.
                new Population(1, transforms, colours, new Bounds(
                    new Vector3(-spread, -2, -spread),
                    new Vector3(spread, 14, spread)))
                {
                    // Bump this when you write into the buffers. Do not, and nothing
                    // is sent.
                    Revision = revision,
                },
            ],
            Camera = camera,
        }
        """;

    // Air, so that the far end of the field goes into the distance rather than
    // stopping at a line. A range without it is honest but obvious: the
    // members sink, and what they sink into is a hard horizon.
    private static Fog Air => new()
    {
        Colour = Tint.Hex(0xB9CBDD).Linear,
        Density = 0.0038,
        // Not right in front of the lens: the thing being looked at should be
        // seen, and only what is far away should be veiled.
        Distance = 30,
        // All the way, so the far edge of the ground is not a line.
        MaximumOpacity = 1,
    };

    private static Sky Sky => new()
    {
        Colour = Tint.Hex(0x6E8DB4).Linear,
        Zenith = Tint.Hex(0x2F5F97).Linear,
        Horizon = Tint.Hex(0xB9CBDD).Linear,
        Ambient = 26000,
        BodyDirection = new Vector3(0.4, 0.86, 0.32).Normalized(),
        BodyColour = Tint.Hex(0xFFF6E8).Linear,
        BodySize = 0.011,
        Clouds = Clouds.Cumulus with { Cover = 0.3, Wind = new Vector2(4, 1.5) },
    };

    private RenderObject Floor()
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(0, -2.4, 0));
        transform.ScaleByDouble(_spread * 1.2, 0.06, _spread * 1.2, 1);
        return new RenderObject
        {
            Key = 900,
            Transform = transform,
            Colour = Tint.Hex(0x2E343C).Linear,
            CastShadows = false,
        };
    }

    /// <summary>Lays the crowd out. Called when the count or the spread changes, and not otherwise.</summary>
    private void Fill(int total)
    {
        _transforms = new float[total * 16];
        _colours = new float[total * 3];

        var random = new Random(7);
        for (var i = 0; i < total; i++)
        {
            var x = ((random.NextDouble() * 2) - 1) * _spread;
            var z = ((random.NextDouble() * 2) - 1) * _spread;
            var height = 0.6 + (random.NextDouble() * 3.4);
            var width = 0.3 + (random.NextDouble() * 0.5);
            Write(i, new Member(x, (height * 0.5) - 2.2, z, width, height, random.NextDouble()));

            // Something between grass and rust, so the field reads as a crowd
            // of different things rather than one thing repeated.
            var colour = Tint.Lerp(Tint.Hex(0x6F8F55), Tint.Hex(0xB86B3C), random.NextDouble()).Linear;
            _colours[i * 3] = (float)colour.X;
            _colours[(i * 3) + 1] = (float)colour.Y;
            _colours[(i * 3) + 2] = (float)colour.Z;
        }
    }

    /// <summary>Sways the lot, in place.</summary>
    /// <remarks>
    /// Writes into the buffer that is already there rather than making a new
    /// one. A hundred thousand new matrices a frame is the thing this whole
    /// arrangement exists to avoid.
    /// </remarks>
    private void Drift(int total, double seconds)
    {
        var random = new Random(7);
        for (var i = 0; i < total; i++)
        {
            var x = ((random.NextDouble() * 2) - 1) * _spread;
            var z = ((random.NextDouble() * 2) - 1) * _spread;
            var height = 0.6 + (random.NextDouble() * 3.4);
            var width = 0.3 + (random.NextDouble() * 0.5);
            var phase = random.NextDouble();
            Write(i, new Member(
                x + (Math.Sin((seconds * 0.9) + (phase * 6.28)) * 0.8),
                (height * 0.5) - 2.2,
                z + (Math.Cos((seconds * 0.7) + (phase * 6.28)) * 0.8),
                width,
                height,
                phase));
        }
    }

    /// <summary>One member, written straight into the flat buffer.</summary>
    /// <remarks>
    /// Column major, which is what the renderer's matrices already are, so
    /// nothing on either side has to convert anything.
    /// </remarks>
    private void Write(int i, in Member member)
    {
        var angle = member.Turn * Math.PI * 2;
        var c = (float)(Math.Cos(angle) * member.Width);
        var s = (float)(Math.Sin(angle) * member.Width);
        ReadOnlySpan<float> columns =
        [
            c, 0, -s, 0,
            0, (float)(member.Height * 0.5), 0, 0,
            s, 0, c, 0,
            (float)member.X, (float)member.Y, (float)member.Z, 1,
        ];
        columns.CopyTo(_transforms.AsSpan(i * 16));
    }

    /// <summary>Where a member stands, how big it is, and how far round it is turned, in turns.</summary>
    private readonly record struct Member(double X, double Y, double Z, double Width, double Height, double Turn);
}
