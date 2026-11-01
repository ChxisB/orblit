using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// Every shadow setting Filament has, on one set of objects.
/// </summary>
/// <remarks>
/// <para>
/// A sun, a spot and a bare bulb all cast through the same machinery. It is a
/// depth map drawn from the light and compared against by every surface. They
/// differ in its shape. A sun's map is split into cascades running away from
/// the camera, a spot's is one perspective map, and a bulb's is six of them,
/// one per face of a cube. So the settings that matter differ too, and the
/// only way to learn which is which is to move them with the light that reads
/// them.
/// </para>
/// <para>
/// What each does is easiest seen at an edge. The stick lying on the floor is
/// for contact shadows. Its shadow map is too coarse to put anything between a
/// centimetre-thick stick and the ground under it, and a short march through
/// the depth buffer is what does. The colonnade is for cascades. Its nearest
/// shadow is a metre from the camera and its furthest fifty, and a single map
/// across that distance is a centimetre per texel at best.
/// </para>
/// <para>
/// Two things this shows are Filament's behaviour and not a fault here. Both
/// are better met in an example than in a bug report. Soft and Area draw the
/// same picture, byte-identical over a whole frame, because Filament 1.76
/// marks its DPCF path deprecated and serves it with PCSS. And contact shadows
/// put a grain over the far half of this floor. The march finds the floor
/// itself where the floor is nearly edge-on, and nothing exposed tightens it.
/// The floor here is a hundred and twenty metres across on purpose, which is
/// the worst case for it.
/// </para>
/// </remarks>
public sealed class ShadowsExample : Example
{
    /// <summary>Which of Filament's own lights is casting.</summary>
    private static readonly string[] Lights = ["Sun", "Spot", "Point"];

    /// <summary>
    /// The stick first. It is a centimetre and a half thick and lies on the
    /// floor, so its shadow is the contact shadow and nothing else. Then
    /// something in the middle, with an overhang to shadow its own base.
    /// </summary>
    private static readonly IReadOnlyList<(long Key, Vector3 At, double Turn, Vector3 Size, int Rgb)> Props =
    [
        (2, new(0.6, -1.465, 2.6), 0.4, new(2.2, 0.02, 0.05), 0x3A3F47),
        (3, new(-0.8, -0.6, 0.6), 0.6, new(1.2, 1.8, 1.2), 0xD9634F),
        (4, new(-0.8, 0.55, 0.6), 0.2, new(2.2, 0.12, 2.2), 0xE8E4DC),
    ];

    private string _light = "Sun";

    /// <summary>The pipeline's shadow settings, shared by whichever light casts.</summary>
    private ShadowSettings _shadows = new() { Cascades = 3, Distance = 60 };

    /// <summary>Whether the cascades are placed by hand.</summary>
    private bool _handSplits;

    /// <summary>Where the first cascade ends, when they are placed by hand.</summary>
    private double _firstSplit = 0.08;

    /// <summary>
    /// How big the light is. The area kind makes its penumbra from this, and
    /// nothing else reads it. Metres for a spot or a bulb. For the sun it is
    /// degrees of sky, because a sun is an angle and not a size.
    /// </summary>
    private double _lightSize = 0.4;

    public override string Name => "Shadows";

    public override string Blurb =>
        "Filament's own shadows: sun, spot and point, with every setting. Map size, cascades and splits, bias, " +
        "distance, contact shadows, and the four kinds of edge.";

    public override ExampleSection Section => ExampleSection.Lighting;

    public override ViewPoint ViewPoint => new() { Distance = 14, Pitch = 0.32, Yaw = 0.55, Height = 1.0 };

    public override RenderScene Scene(RenderCamera camera, double seconds) => new()
    {
        Objects = [Floor(), .. Props.Select(Prop), .. Enumerable.Range(0, 12).Select(Column)],
        Camera = camera,
        Pipeline = new Pipeline { Shadows = _shadows with { Splits = Splits() } },
        Lights = [Light()],
        Sky = new Sky { Colour = new Vector3(0.24, 0.33, 0.46), Ambient = 9000 },
    };

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Choice("Light", Lights, _light, light => _light = light),
        new Toggle("Shadows", _shadows.Enabled, value => _shadows = _shadows with { Enabled = value }),
        .. _shadows.Enabled ? ShadowControls() : [],
    ];

    public override string Code => """
        // Shadow settings belong to the pipeline, not to one light. Every light
        // that casts reads them. Filament keeps them on each light inside, and
        // the renderer copies them onto every one whenever they change.
        new RenderScene
        {
            Pipeline = new Pipeline
            {
                Shadows = new ShadowSettings
                {
                    // Sharp is PCF, Soft is DPCF, Area is PCSS and Variance is VSM.
                    Kind = ShadowKind.Area,
                    MapSize = 2048,
                    // A sun's map split into cascades running away from the camera.
                    // The splits are fractions of the distance. Null lets Lambda
                    // place them.
                    Cascades = 3,
                    Splits = [0.08, 0.3],
                    Distance = 60,
                    ConstantBias = 0.001,
                    NormalBias = 1.0,
                    // Locked to the world, so an edge does not crawl as the camera turns.
                    Stable = true,
                    // A short march through the depth buffer, for what is too small
                    // for the map: a stick on the floor, a foot on the ground.
                    Contact = true,
                    ContactDistance = 0.3,
                    // For Area: how wide, and how fast it widens with distance.
                    Softness = 1.0,
                    SoftnessFalloff = 1.0,
                    // Only Variance reads these.
                    Variance = new VarianceShadows { Blur = 4, LightBleedReduction = 0.3 },
                },
            },
            Lights =
            [
                new RenderLight
                {
                    Key = 1,
                    Kind = RenderLightKind.Spot,
                    // The light's real size, which an Area edge is made from.
                    SourceRadius = 0.4,
                    CastShadows = true,
                    // ...
                },
            ],
            // ...
        }

        // Not reachable: caching a shadow map between frames. Filament redraws
        // every map every frame and has no API to hold one still.
        """;

    private static RenderObject Floor()
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(0, -1.5, 0));
        transform.ScaleByDouble(120, 0.05, 120, 1);
        return new RenderObject
        {
            Key = 1,
            Transform = transform,
            Colour = Tint.Hex(0xB9B4AA).Linear,

            // A floor that casts is a plane shadowing itself, which is acne.
            CastShadows = false,
        };
    }

    private static RenderObject Prop((long Key, Vector3 At, double Turn, Vector3 Size, int Rgb) prop)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(prop.At);
        transform.RotateY(prop.Turn);
        transform.ScaleByDouble(prop.Size.X, prop.Size.Y, prop.Size.Z, 1);
        return new RenderObject { Key = prop.Key, Transform = transform, Colour = Tint.Hex(prop.Rgb).Linear };
    }

    /// <summary>The colonnade, running away from the camera.</summary>
    private static RenderObject Column(int i)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(3.0, 0.1, 2.0 - (i * 4.0)));
        transform.ScaleByDouble(0.4, 3.2, 0.4, 1);
        return new RenderObject { Key = 100 + i, Transform = transform, Colour = Tint.Hex(0x7FA8B8).Linear };
    }

    /// <summary>
    /// The first split where it was asked for, and the rest spread evenly
    /// between it and the far end. Null leaves the placing to the lambda.
    /// </summary>
    private IReadOnlyList<double>? Splits() => _handSplits
        ?
        [
            .. Enumerable.Range(1, _shadows.Cascades - 1)
                .Select(i => _firstSplit + ((1 - _firstSplit) * (i - 1) / (_shadows.Cascades - 1))),
        ]
        : null;

    private RenderLight Light() => _light switch
    {
        "Spot" => new RenderLight
        {
            Key = 901,
            Kind = RenderLightKind.Spot,
            Position = new Vector3(3.5, 5, 5),
            Direction = new Vector3(-0.5, -0.75, -0.6).Normalized(),

            // Lumens, and a lot of them. This has to hold its own against the
            // sky's ambient, or its shadow is a shade of a shade.
            Intensity = 2000000,
            FalloffRadius = 30,
            InnerConeAngle = 0.45,
            OuterConeAngle = 0.7,
            SourceRadius = _lightSize,
        },
        "Point" => new RenderLight
        {
            Key = 902,
            Kind = RenderLightKind.Point,
            Position = new Vector3(1.2, 2.6, 2.4),
            Intensity = 3000000,
            FalloffRadius = 25,
            SourceRadius = _lightSize,
        },
        _ => new RenderLight
        {
            Key = 900,
            Kind = RenderLightKind.Directional,

            // Low and across, so shadows are long and the far ones are where
            // the cascades run out.
            Direction = new Vector3(-0.55, -0.5, -0.67).Normalized(),
            Intensity = 95000,

            // A sun's size is an angle, not a length, and the Area edge makes
            // its penumbra from it. The real one is about a quarter of a degree.
            SunAngularRadius = _lightSize,
            SourceRadius = _lightSize,
        },
    };

    private IReadOnlyList<ExampleSetting> ShadowControls() =>
    [
        new Choice("Edge", Enum.GetNames<ShadowKind>(), _shadows.Kind.ToString(), Edge),
        new Setting("Map size", Math.Log2(_shadows.MapSize / 256.0), 0, 4, MapSize) { Decimals = 0 },
        .. _light == "Sun" ? CascadeSettings() : [],
        new Setting("Distance", _shadows.Distance, 0, 200, value => _shadows = _shadows with { Distance = value })
        {
            Unit = " m",
        },
        new Setting("Constant bias", _shadows.ConstantBias, 0, 0.05, value => _shadows = _shadows with { ConstantBias = value })
        {
            Decimals = 3,
        },
        new Setting("Normal bias", _shadows.NormalBias, 0, 4, value => _shadows = _shadows with { NormalBias = value }),
        new Toggle("Contact shadows", _shadows.Contact, value => _shadows = _shadows with { Contact = value }),
        .. ContactSettings(),
        .. EdgeSettings(),
    ];

    private void Edge(string kind) => _shadows = _shadows with { Kind = Enum.Parse<ShadowKind>(kind) };

    private void MapSize(double value) =>
        _shadows = _shadows with { MapSize = 256 << (int)Math.Round(value, MidpointRounding.AwayFromZero) };

    private IReadOnlyList<ExampleSetting> ContactSettings() => _shadows.Contact
        ?
        [
            new Setting("Contact distance", _shadows.ContactDistance, 0.05, 2, value => _shadows = _shadows with { ContactDistance = value })
            {
                Unit = " m",
            },
        ]
        : [];

    private IReadOnlyList<ExampleSetting> CascadeSettings() =>
    [
        new Setting("Cascades", _shadows.Cascades, 1, 4, Cascades) { Decimals = 0 },
        new Toggle("Place splits by hand", _handSplits, value => _handSplits = value),
        _handSplits
            ? new Setting("First split", _firstSplit, 0.02, 0.6, value => _firstSplit = value)
            : new Setting("Split lambda", _shadows.Lambda, 0, 1, value => _shadows = _shadows with { Lambda = value }),
        new Toggle("Stable", _shadows.Stable, value => _shadows = _shadows with { Stable = value }),
    ];

    private void Cascades(double value) =>
        _shadows = _shadows with { Cascades = (int)Math.Round(value, MidpointRounding.AwayFromZero) };

    private IReadOnlyList<ExampleSetting> EdgeSettings() => _shadows.Kind switch
    {
        ShadowKind.Soft => [Softness()],
        ShadowKind.Area =>
        [
            Softness(),
            new Setting("Light size", _lightSize, 0.02, 2, value => _lightSize = value)
            {
                Unit = _light == "Sun" ? "°" : " m",
            },
            new Setting("Penumbra falloff", _shadows.SoftnessFalloff, 0.2, 4, value => _shadows = _shadows with { SoftnessFalloff = value })
            {
                Decimals = 1,
            },
        ],
        ShadowKind.Variance => VarianceSettings(),
        _ => [],
    };

    private Setting Softness() =>
        new("Softness", _shadows.Softness, 0.1, 8, value => _shadows = _shadows with { Softness = value }) { Decimals = 1 };

    private IReadOnlyList<ExampleSetting> VarianceSettings() =>
    [
        new Setting("Blur", _shadows.Variance.Blur, 0, 20, value => Variance(_shadows.Variance with { Blur = value }))
        {
            Unit = " px",
            Decimals = 1,
        },
        new Setting(
            "Light bleed reduction",
            _shadows.Variance.LightBleedReduction,
            0,
            1,
            value => Variance(_shadows.Variance with { LightBleedReduction = value })),
        new Toggle("High precision", _shadows.Variance.HighPrecision, value => Variance(_shadows.Variance with { HighPrecision = value })),
    ];

    private void Variance(VarianceShadows variance) => _shadows = _shadows with { Variance = variance };
}
