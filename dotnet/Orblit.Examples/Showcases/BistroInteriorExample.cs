using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// The Bistro's room inside, which is the harder case.
/// </summary>
/// <remarks>
/// An interior is where a real-time renderer is most plainly not a path
/// tracer. Almost none of the light in a room like this arrives straight from
/// a bulb. It arrives off the walls and the ceiling. The renderer has
/// image-based lighting and screen-space occlusion, and it does not have
/// bounced light. What that costs shows here and nowhere else in the gallery,
/// which is the reason to have it. <see cref="BistroFiles"/> says where the
/// scene comes from.
/// </remarks>
public sealed class BistroInteriorExample : Example
{
    private readonly BistroFiles _files;

    /// <summary>Read once, from whichever variant was showing the first time they were needed.</summary>
    private readonly Lazy<List<BistroFixture>> _fixtures;

    private bool _wine;

    /// <summary>
    /// Standing in for the light this renderer will not bounce. In a real room
    /// the walls are half the lighting. Here the ambient is the only thing
    /// filling a shadow, so it is a dial rather than a constant.
    /// </summary>
    private double _ambient = 400;

    /// <summary>Finds the scene through <see cref="BistroFiles"/>.</summary>
    public BistroInteriorExample(Func<string, string?> environment, Func<string, bool> exists, Func<string, string> read)
    {
        _files = new BistroFiles(environment, exists, read);
        _fixtures = new(() => _files.Fixtures(Asset));
        Note = _files.Missing(Asset);
    }

    public override string Name => "Bistro interior";

    public override ExampleSection Section => ExampleSection.Showcases;

    public override string Blurb => "The room, and the one place where missing bounced light shows.";

    public override ViewPoint ViewPoint => new() { Distance = 9, Pitch = 0.06, Height = 1.7, Yaw = 0.4 };

    public override Downloadable? Needs => new()
    {
        What = "the Bistro interior",
        Size = "about 1.4 GB",
        From = "Amazon Lumberyard Bistro, via ORCA",
        Licence = "CC BY 4.0",
        Command = ["tool/fetch_bistro.sh", "interior"],
    };

    private string Asset => _wine ? "BistroInterior_Wine" : "BistroInterior";

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        var lamps = BistroFiles.Lamps(_fixtures.Value, 0);
        return new RenderScene
        {
            Camera = camera,
            Objects = [_files.Model(Asset)],
            Lights = lamps.Count > 0 ? lamps : Pendants(),
            Sky = new Sky
            {
                Zenith = Tint.Hex(0x2A1D18).Linear,
                Horizon = Tint.Hex(0x3A2A20).Linear,
                Ambient = _ambient,
                ShowBody = false,
                Drawn = false,
            },
            Pipeline = new Pipeline
            {
                // A room rather than a street, so the shadows only have to
                // reach across it. Not zero, which covers nothing at all.
                Shadows = new ShadowSettings { Kind = ShadowKind.Soft, MapSize = 2048, Distance = 30, Contact = true },
            },
            Post = new PostProcess
            {
                AntiAliasing = AntiAliasing.Temporal,
                Bloom = new Bloom { Enabled = true, Strength = 0.1 },
                // Occlusion earns its place indoors more than anywhere. It is
                // the cheapest stand-in for the contact darkening bounced light
                // would give, and indoors bounced light is most of the lighting.
                Occlusion = new Occlusion { Enabled = true, Quality = 2, Radius = 0.4 },
                Grading = new Grading { Enabled = true, ToneMapping = ToneMapping.Aces },
            },
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Toggle("Wine", _wine, Pour) { Note = "The variant with the bottles and glasses" },
        new Setting("Ambient", _ambient, 0, 2000, value => _ambient = value) { Unit = " lux", Decimals = 0 },
    ];

    public override string Code => """
        // Indoors, the ambient is doing the job bounced light would do. Drag it
        // to nothing and the shadows go black, which is what a renderer without
        // global illumination looks like when nothing stands in for it.
        Sky = new Sky
        {
            Zenith = Tint.Hex(0x2A1D18).Linear,
            Ambient = 400,    // lux
            Drawn = false,    // lighting only, with no sky to see from in here
        },

        // And occlusion, the cheapest stand-in for contact darkening.
        Post = new PostProcess { Occlusion = new Occlusion { Enabled = true } },
        """;

    /// <summary>
    /// When the room has no fixtures of its own, it is lit the way the
    /// reference is: warm pendants over the tables, at the height they hang.
    /// </summary>
    private static List<RenderLight> Pendants() =>
    [
        .. Enumerable.Range(0, 6).Select(i => new RenderLight
        {
            Key = 100 + i,
            Kind = RenderLightKind.Point,
            Position = new Vector3(-4.0 + (i * 1.8), 2.6, i % 2 == 0 ? -1.2 : 1.4),
            Colour = Tint.Hex(0xFFD9A8).Linear,
            Intensity = 900,
            FalloffRadius = 6,
            CastShadows = i == 0,
        }),
    ];

    private void Pour(bool wine)
    {
        _wine = wine;
        Note = _files.Missing(Asset);
    }
}
