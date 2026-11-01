using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// What a light is, stated in the units a light is sold in. A sun is lux, a
/// bulb is lumens, and the renderer takes both. What changes between them is
/// not brightness but shape: where the light comes from, how far it reaches,
/// and how wide the source is. The width decides whether a shadow has an edge.
/// </summary>
public sealed class LightsExample : Example
{
    private static readonly string[] Kinds = ["Sun", "Point", "Spot", "Panel"];

    private string _kind = "Sun";
    private double _intensity = 82000;
    private double _cone = 45;
    private double _sourceWidth = 0.5;
    private double _panelWidth = 3;
    private double _panelHeight = 1.4;
    private bool _shadows = true;
    private bool _orbiting = true;

    public override string Name => "Lights";

    public override string Blurb => "Sun, point and spot, in lux and lumens, with the shadows each casts.";

    public override ExampleSection Section => ExampleSection.Lighting;

    public override ViewPoint ViewPoint => new() { Distance = 13, Pitch = 0.28 };

    private bool Punctual => _kind != "Sun";

    public override RenderScene Scene(RenderCamera camera, double seconds) => new()
    {
        Objects = [.. Enumerable.Range(0, 5).Select(Piece), Floor()],
        Lights = [Light(_orbiting ? seconds * 0.4 : 1.1)],
        Sky = new Sky { Colour = Tint.Hex(0x161C25).Linear, Ambient = 6000, ShowBody = _kind == "Sun" },
        Camera = camera,
    };

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Choice("Kind", Kinds, _kind, Select),
        new Setting(Punctual ? "Lumens" : "Lux", _intensity, 0, MaxIntensity(), value => _intensity = value)
        {
            Decimals = 0,
        },
        .. ShapeSettings(),
        new Toggle("Circling", _orbiting, value => _orbiting = value),
    ];

    public override string Code => """
        // Sun: lux, and a width in degrees that decides how soft its shadows are.
        new RenderLight
        {
            Key = 200,
            Kind = RenderLightKind.Directional,
            Intensity = 82000,                  // lux
            Direction = new Vector3(-0.4, -1, -0.5).Normalized(),
            SunAngularRadius = 0.53,            // degrees, the real sun's
            CastShadows = true,
        }

        // Point: lumens, a position, and a distance it stops mattering past.
        new RenderLight
        {
            Key = 200,
            Kind = RenderLightKind.Point,
            Intensity = 12000,                  // lumens
            Position = new Vector3(3, 3.4, 3),
            FalloffRadius = 24,                 // metres
            SourceRadius = 0.5,                 // how wide the bulb is
        }

        // Spot: the same, aimed, with the cone it throws.
        new RenderLight
        {
            Key = 200,
            Kind = RenderLightKind.Spot,
            Intensity = 12000,
            Position = new Vector3(3, 3.4, 3),
            Direction = aim,
            InnerConeAngle = 25 * Math.PI / 180,   // full brightness inside this
            OuterConeAngle = 45 * Math.PI / 180,   // nothing outside it
        }

        // Watts, metres and degrees belong in Orblit.Light, which converts them once.
        """;

    private static RenderObject Piece(int i)
    {
        var even = i % 2 == 0;
        var around = i / 5.0 * Math.PI * 2;
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(Math.Cos(around) * 2.4, -0.4 + (even ? 0.35 : 0), Math.Sin(around) * 2.4));
        transform.RotateY(i * 0.7);
        transform.ScaleByDouble(0.55, even ? 0.9 : 0.55, 0.55, 1);
        return new RenderObject
        {
            Key = 100 + i,
            Transform = transform,
            Colour = Tint.Hex(even ? 0xD9634F : 0xE8E4DC).Linear,
        };
    }

    private static RenderObject Floor()
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(0, -1.4, 0));
        transform.ScaleByDouble(9.0, 0.06, 9.0, 1);
        return new RenderObject
        {
            Key = 90,
            Transform = transform,
            Colour = Tint.Hex(0x39404A).Linear,

            // A floor that casts is a floor casting a shadow onto itself, and
            // the acne that produces is most of what makes a scene look dirty.
            CastShadows = false,
        };
    }

    private RenderLight Light(double turn)
    {
        var from = new Vector3(Math.Sin(turn) * 4.5, 3.4, Math.Cos(turn) * 4.5);

        // A light points down its own -Z, and here it is aimed at the floor in
        // the middle, which is what makes the cone visible as it goes round.
        var aim = (new Vector3(0, 0.2, 0) - from).Normalized();
        return _kind switch
        {
            "Point" => Point(from),
            "Spot" => Spot(from, aim),
            "Panel" => Panel(from, aim, turn),
            _ => Sun(from),
        };
    }

    private RenderLight Point(Vector3 from) => new()
    {
        Key = 200,
        Kind = RenderLightKind.Point,

        // Lumens: the whole output of the bulb, spread over a sphere.
        Intensity = _intensity,
        Position = from,
        FalloffRadius = 24,
        SourceRadius = _sourceWidth,
        CastShadows = _shadows,
        Colour = Tint.Hex(0xFFE9C8).Linear,
    };

    private RenderLight Spot(Vector3 from, Vector3 aim) => new()
    {
        Key = 200,
        Kind = RenderLightKind.Spot,
        Intensity = _intensity,
        Position = from,
        Direction = aim,
        FalloffRadius = 24,

        // Full brightness inside the inner angle, gone by the outer.
        InnerConeAngle = Math.Max(_cone * 0.55, 1) * Math.PI / 180,
        OuterConeAngle = _cone * Math.PI / 180,
        SourceRadius = _sourceWidth,
        CastShadows = _shadows,
        Colour = Tint.Hex(0xFFF0D8).Linear,
    };

    private RenderLight Panel(Vector3 from, Vector3 aim, double turn) => new()
    {
        Key = 200,
        Kind = RenderLightKind.Area,

        // Lumens, like a bulb, but off a surface and not out of a point. A
        // bigger panel spreads the same light wider and softens its edge. It
        // does not brighten the room.
        Intensity = _intensity,
        Position = from,

        // The face it emits from. A rectangle is one-sided. Turn it round and
        // the scene goes dark.
        Direction = aim,

        // Which way the width runs. Without it the panel is free to spin in
        // its own plane, and a strip on its side is a different light.
        Tangent = new Vector3(Math.Cos(turn), 0, -Math.Sin(turn)),
        Width = _panelWidth,
        Height = _panelHeight,
        FalloffRadius = 24,
        Colour = Tint.Hex(0xFFF0D8).Linear,
    };

    private RenderLight Sun(Vector3 from) => new()
    {
        Key = 200,
        Kind = RenderLightKind.Directional,

        // Lux. A sun has no total to state, only a strength per square metre
        // of whatever it lands on.
        Intensity = _intensity,
        Direction = (new Vector3(0, -1.1, 0) + (from * -0.35)).Normalized(),

        // Half the width of the disc in the sky. It is why an outdoor shadow
        // is crisp at your feet and soft at its far end.
        SunAngularRadius = Math.Max(_sourceWidth, 0.25),
        CastShadows = _shadows,
        Colour = Tint.Hex(0xFFF3E0).Linear,
    };

    private void Select(string kind)
    {
        _kind = kind;

        // The units change with the kind, so the number does too. A hundred
        // thousand lumens is a floodlight. A hundred thousand lux is the sun. A
        // panel wants more than a bulb for the same effect in shot, because its
        // flux leaves a whole surface and lands as a gentler wash.
        _intensity = kind switch
        {
            "Sun" => 82000,
            "Panel" => 260000,
            _ => 12000,
        };
    }

    private double MaxIntensity() => _kind switch
    {
        "Panel" => 600000,
        "Sun" => 140000,
        _ => 60000,
    };

    /// <summary>
    /// A panel states its size as two edges, so the single "how wide is the
    /// source" slider has nothing to say about it.
    /// </summary>
    private IReadOnlyList<ExampleSetting> ShapeSettings() => _kind switch
    {
        "Panel" =>
        [
            new Setting("Panel width", _panelWidth, 0.1, 8, value => _panelWidth = value) { Unit = " m" },
            new Setting("Panel height", _panelHeight, 0.1, 8, value => _panelHeight = value) { Unit = " m" },
        ],
        "Spot" =>
        [
            new Setting("Cone", _cone, 5, 120, value => _cone = value) { Unit = "°", Decimals = 0 },
            .. SourceSettings(),
        ],
        _ => SourceSettings(),
    };

    private IReadOnlyList<ExampleSetting> SourceSettings() =>
    [
        new Setting(Punctual ? "Bulb radius" : "Sun size", _sourceWidth, 0.05, Punctual ? 2 : 8, value => _sourceWidth = value)
        {
            Unit = Punctual ? " m" : "°",
        },
        new Toggle("Shadows", _shadows, value => _shadows = value),
    ];
}
