using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;
using Orblit.Weather;

namespace Orblit.Examples;

/// <summary>
/// Air with something in it.
/// </summary>
/// <remarks>
/// Three separate things that read as one: an even haze for distance, banks of
/// cloud for shape, and a curtain of drops for what is coming down. The first
/// cannot look like anything in particular. The second cannot tell you how far
/// away something is. The third is nothing without either.
/// </remarks>
public sealed class WeatherExample : Example
{
    /// <summary>
    /// The shapes on offer, each a real one and not a preset of the same one.
    /// They differ in how high the base sits, how deep the layer is, how large
    /// its lumps are and how far the noise is folded.
    /// </summary>
    private static readonly IReadOnlyList<(string Name, Clouds Clouds)> Shapes =
    [
        ("Cumulus", Clouds.Cumulus),
        ("Stratocumulus", Clouds.Stratocumulus),
        ("Stratus", Clouds.Stratus),
        ("Cirrus", Clouds.Cirrus),
        ("Cumulonimbus", Clouds.Cumulonimbus),
    ];

    /// <summary>The conditions on offer, in the order the panel lists them.</summary>
    private static readonly IReadOnlyList<(string Name, double Haze, double Mist, double MistSize, double Height,
        double Wind, double Rain, double Snow, double Cover)> Conditions =
    [
        ("Clear", 0.004, 0.0, 40.0, 0.0, 1.5, 0.0, 0.0, 0.06),
        ("Fair", 0.006, 0.05, 50.0, 0.0, 4.0, 0.0, 0.0, 0.38),
        ("Storm", 0.07, 0.9, 45.0, 0.0, 12.0, 0.95, 0.0, 1.0),
        ("Misty", 0.055, 0.75, 22.0, -1.5, 2.5, 0.0, 0.0, 0.45),
        ("Rain", 0.035, 0.2, 70.0, 0.0, 5.0, 0.65, 0.0, 0.85),
        ("Snow", 0.04, 0.25, 60.0, 0.0, 2.2, 0.0, 0.75, 0.8),
    ];

    private static readonly IReadOnlyList<(string Name, SkyQuality Quality)> Details =
    [
        ("Lean", SkyQuality.Lean),
        ("Fair", SkyQuality.Fair),
        ("Full", SkyQuality.Full),
    ];

    private string _condition = "Fair";
    private double _density = 0.006;
    private double _structure = 0.05;
    private double _cloudSize = 50;
    private double _height;
    private double _windSpeed = 4;
    private double _windBearing = 135;
    private double _rain;
    private double _snow;
    private double _cover = 0.38;
    private double _cloudAltitude = 900;
    private string _shape = "Cumulus";
    private double _lightning;
    private string _detail = "Fair";

    public override string Name => "Weather";

    public override string Blurb => "Haze, banks of cloud and falling weather, carried by one wind.";

    public override ExampleSection Section => ExampleSection.Atmosphere;

    public override ViewPoint ViewPoint => new() { Distance = 16, Pitch = -0.06, Height = 1.5 };

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        var bearing = _windBearing * Math.PI / 180;
        var wind = new Vector2(Math.Sin(bearing), Math.Cos(bearing)) * _windSpeed;
        var strike = Strike.At(seconds, _lightning);
        return new RenderScene
        {
            Objects = [.. Enumerable.Range(0, 7).Select(Rock), Ground()],
            Lights =
            [
                new RenderLight
                {
                    Key = 410,
                    Kind = RenderLightKind.Directional,
                    // Dimmer than a clear day, because there is cloud in the way of it.
                    Intensity = 30000,
                    Direction = new Vector3(-0.42, -0.36, -0.52).Normalized(),
                    Colour = Tint.Hex(0xEFEFEA).Linear,
                    SunAngularRadius = 6,
                },
            ],
            // The sky, the body in it and the cloud are one object because they
            // are one shader on one dome. The cloud covers the sun, the sun lights
            // the cloud, and a strike would light both.
            Sky = new Sky
            {
                Colour = Tint.Hex(0x6E757D).Linear,
                Zenith = Tint.Hex(0x3E6FA8).Linear,
                Horizon = Tint.Hex(0xBCCBD8).Linear,
                Ambient = 22000,
                Quality = Details.First(detail => detail.Name == _detail).Quality,
                // The way the light comes from, which is the way the light goes,
                // the other way round.
                BodyDirection = new Vector3(0.42, 0.36, 0.52).Normalized(),
                BodyColour = Tint.Hex(0xFFF6E8).Linear,
                BodySize = 0.011,
                // Worked out from the clock and not rolled, so the same second of
                // the same storm strikes the same way twice.
                Flash = strike.Flash,
                FlashDirection = strike.Direction,
                FlashSeed = strike.Seed,
                // Cloud is a layer overhead and has nothing to do with the fog. A
                // scene can have either without the other.
                Clouds = _cover <= 0.01
                    ? Clouds.None
                    : ShapeOf(_shape) with { Cover = _cover, Wind = wind, Altitude = _cloudAltitude },
            },
            Fog = new Fog
            {
                Colour = Tint.Hex(0xD3D8DD).Linear,
                Density = _density,
                Height = _height,
                HeightFalloff = 0.4,
                // Above zero, the same air is also drawn as banks of cloud.
                Structure = _structure,
                Wind = wind,
                // Turns of the noise per metre: the reciprocal of how big a cloud is.
                FeatureSize = 1 / Math.Max(_cloudSize, 0.5),
                Thickness = 6,
            },
            Precipitation = Falling(wind),
            Camera = camera,
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Choice("Condition", [.. Conditions.Select(condition => condition.Name)], _condition, Apply),
        new Choice("Sky detail", [.. Details.Select(detail => detail.Name)], _detail, detail => _detail = detail),
        new Setting("Lightning", _lightning, 0, 1, value => _lightning = value),
        new Setting("Cloud cover", _cover, 0, 1, value => _cover = value),
        .. _cover > 0.01 ? CloudControls() : [],
        new Setting("Haze", _density, 0, 0.2, value => _density = value) { Decimals = 3 },
        new Setting("Ground mist", _structure, 0, 1, value => _structure = value),
        .. _structure > 0
            ? [new Setting("Mist size", _cloudSize, 4, 120, value => _cloudSize = value) { Unit = " m", Decimals = 0 }]
            : Array.Empty<ExampleSetting>(),
        new Setting("Height", _height, -8, 8, value => _height = value) { Unit = " m", Decimals = 1 },
        new Setting("Rain", _rain, 0, 1, value => _rain = value),
        new Setting("Snow", _snow, 0, 1, value => _snow = value),
        new Setting("Wind", _windSpeed, 0, 20, value => _windSpeed = value) { Unit = " m/s", Decimals = 1 },
        new Setting("Bearing", _windBearing, 0, 360, value => _windBearing = value) { Unit = "°", Decimals = 0 },
    ];

    public override string Code => """
        // Haze for distance, banks for shape. One setting, because weather with no
        // haze behind it reads as cut-outs hanging in clear air.
        Fog = new Fog
        {
            Colour = new Vector3(0.65, 0.69, 0.73),
            Density = 0.055,                 // per metre
            Height = -1.5,                   // where the layer lies
            HeightFalloff = 0.4,             // how fast it thins going up
            Structure = 0.75,                // above zero, banks of cloud are drawn as well
            Wind = new Vector2(1.8, -1.8),   // metres a second, across the ground
            FeatureSize = 1 / 22.0,          // one over how big a cloud is, in metres
            Thickness = 6,                   // how deep the bank is
        },

        // Rain and snow are one curtain at different settings. What separates them
        // is how far a drop travels while the shutter is open.
        Precipitation = new Precipitation
        {
            Amount = 0.65,
            Fall = 9,                        // metres a second. Snow is under one
            Wind = wind,
            DropsPerMetre = 6,
            Stretch = 30,                    // a streak. 1.2 is a flake
        },

        // The renderer moves both on its own clock, so a still scene keeps raining
        // without the host sending another frame.
        """;

    private static Clouds ShapeOf(string name) => Shapes.First(shape => shape.Name == name).Clouds;

    private static RenderObject Rock(int i)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(
            Math.Cos(i * 1.9) * (3 + (i * 0.7)),
            -0.9 + ((i % 3) * 0.4),
            Math.Sin(i * 1.9) * (3 + (i * 0.7))));
        transform.RotateY(i * 0.6);
        transform.ScaleByDouble(0.8, 1.4 + ((i % 3) * 0.7), 0.8, 1);
        return new RenderObject
        {
            Key = 400 + i,
            Transform = transform,
            Colour = Tint.Hex(i % 2 == 0 ? 0x6E6A63 : 0x8A8279).Linear,
        };
    }

    private static RenderObject Ground()
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(0, -1.9, 0));
        transform.ScaleByDouble(40.0, 0.06, 40.0, 1);
        return new RenderObject
        {
            Key = 390,
            Transform = transform,
            Colour = Tint.Hex(0x4A4E52).Linear,
            CastShadows = false,
        };
    }

    /// <summary>
    /// Sets every slider to one named condition, as picking it in the panel
    /// does. Lightning comes with a storm, and so does its own shape of cloud.
    /// </summary>
    private void Apply(string name)
    {
        var condition = Conditions.First(one => one.Name == name);
        _condition = name;
        _density = condition.Haze;
        _structure = condition.Mist;
        _cloudSize = condition.MistSize;
        _height = condition.Height;
        _windSpeed = condition.Wind;
        _rain = condition.Rain;
        _snow = condition.Snow;
        _cover = condition.Cover;
        _lightning = name == "Storm" ? 0.85 : 0;
        if (name == "Storm")
        {
            _shape = "Cumulonimbus";
            _cloudAltitude = Clouds.Cumulonimbus.Altitude;
        }
    }

    private IReadOnlyList<ExampleSetting> CloudControls() =>
    [
        new Choice("Cloud shape", [.. Shapes.Select(shape => shape.Name)], _shape, shape =>
        {
            _shape = shape;
            _cloudAltitude = ShapeOf(shape).Altitude;
        }),
        new Setting("Cloud height", _cloudAltitude, 40, 600, value => _cloudAltitude = value) { Unit = " m", Decimals = 0 },
    ];

    /// <summary>
    /// Rain and snow as one curtain, mixed by how much of each is falling.
    /// </summary>
    private Precipitation Falling(Vector2 wind)
    {
        var falling = _rain + _snow;
        if (falling <= 0)
        {
            return Precipitation.None;
        }
        var asSnow = Math.Clamp(_snow / falling, 0.0, 1.0);
        double Between(double wet, double white) => wet + ((white - wet) * asSnow);
        return new Precipitation
        {
            Colour = Tint.Lerp(Tint.Hex(0xB8C6D6), Tint.Hex(0xF2F5F8), asSnow).Linear,
            Amount = Math.Clamp(falling, 0.0, 1.0),
            // Nine metres a second for rain, under one for snow.
            Fall = Between(9, 0.8),
            // Snow is taken by the wind far more. It weighs nothing and it has all day.
            Wind = wind * Between(0.6, 1.6),
            DropsPerMetre = Between(8, 3.5),
            // How far a drop travels while the shutter is open. A streak, or a flake.
            Stretch = Between(30, 5),
            Threshold = Between(0.7, 0.55),
        };
    }
}
