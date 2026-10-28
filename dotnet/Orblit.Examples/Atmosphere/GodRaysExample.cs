using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// A low sun behind a colonnade, and the shafts of light between the pillars.
/// </summary>
/// <remarks>
/// A shaft is not light itself but air. The air in a gap between two pillars
/// is lit and scatters some of that light towards the eye. The air in a
/// pillar's shadow is not, and does not. So the shafts are the colonnade's
/// shadows seen from inside the volume they fall through, and they point at
/// the sun because every shadow does. A low sun behind the subject shows it
/// most. Turn the bearing round and the shafts fade as the sun leaves the
/// frame. Raise the cloud and they thin.
/// </remarks>
public sealed class GodRaysExample : Example
{
    private const long FloorKey = 10;
    private const long LintelKey = 11;
    private const long PillarKey = 20;
    private const double Lux = 60000;

    private bool _on = true;
    private double _strength = 0.8;
    private double _decay = 0.97;
    private double _density = 0.9;
    private double _samples = 64;
    /// <summary>Degrees round from straight ahead. Nought is the sun behind the colonnade, 180 behind the camera.</summary>
    private double _bearing;

    /// <summary>Degrees above the horizon. Low enough that the sun sits in the gap under the lintel.</summary>
    private double _altitude = 5;
    private double _cover;

    public override string Name => "God rays";

    public override string Blurb =>
        "A low sun behind a row of pillars, and the shafts of light the air between them catches.";

    public override ExampleSection Section => ExampleSection.Atmosphere;

    public override ViewPoint ViewPoint => new() { Yaw = 0, Pitch = 0.04, Distance = 15, Height = 1.2 };

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        var up = _altitude * Math.PI / 180;
        var round = _bearing * Math.PI / 180;
        var toSun = new Vector3(Math.Sin(round) * Math.Cos(up), Math.Sin(up), -Math.Cos(round) * Math.Cos(up));
        var sunColour = Tint.Hex(0xFFC98A).Linear;
        const double ambient = Lux * 0.3;
        var incident = (Lux * Math.Max(0, Math.Sin(up))) + ambient;
        return new RenderScene
        {
            Objects =
            [
                Box(FloorKey, new Vector3(0, -1.6, 0), new Vector3(16, 0.1, 16), 0x8C8478),
                // Nine pillars and a lintel over them, so the sun is only seen
                // through the gaps. Every shaft is one gap.
                .. Enumerable.Range(0, 9).Select(i =>
                    Box(PillarKey + i, new Vector3((i - 4) * 1.5, 1.5, -2), new Vector3(0.24, 3, 0.24), 0xD8CFC0)),
                Box(LintelKey, new Vector3(0, 4.75, -2), new Vector3(7, 0.25, 0.4), 0xD8CFC0),
            ],
            Lights =
            [
                new RenderLight
                {
                    Key = 1,
                    Kind = RenderLightKind.Directional,
                    Intensity = Lux,
                    Direction = -toSun,
                    Colour = sunColour,
                    SunAngularRadius = 0.53,
                    HaloSize = 12,
                    HaloFalloff = 70,
                    CastShadows = true,
                },
            ],
            Sky = new Sky
            {
                Colour = Tint.Hex(0x7D8FAE).Linear,
                Zenith = Tint.Hex(0x4F6C98).Linear,
                Horizon = Tint.Hex(0xE9B889).Linear,
                Ambient = ambient,
                BodyDirection = toSun,
                BodyColour = sunColour,
                BodySize = 0.011,
                Clouds = _cover > 0.01 ? Clouds.Cumulus with { Cover = _cover, Wind = new Vector2(3, 1) } : Clouds.None,
            },
            // The only line the shafts need. With no graph of its own, the
            // renderer puts in the passes it needs and takes them out when
            // this is off.
            GodRays = _on
                ? new GodRays
                {
                    Strength = _strength,
                    Decay = _decay,
                    Density = _density,
                    Samples = (int)Math.Round(_samples, MidpointRounding.AwayFromZero),
                }
                : GodRays.Off,
            Camera = Metered(camera, incident),
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Toggle("God rays", _on, value => _on = value),
        new Setting("Strength", _strength, 0, 2, value => _strength = value),
        new Setting("Decay", _decay, 0.85, 1, value => _decay = value),
        new Setting("Density", _density, 0.2, 1.2, value => _density = value),
        new Setting("Samples", _samples, 8, 128, value => _samples = value),
        new Setting("Sun bearing", _bearing, -180, 180, value => _bearing = value) { Unit = "°" },
        new Setting("Sun altitude", _altitude, 2, 40, value => _altitude = value) { Unit = "°" },
        new Setting("Cloud cover", _cover, 0, 1, value => _cover = value),
    ];

    public override string Code => """
        // The scene's own directional light is the sun the shafts come from, so
        // they always point where the shadows do. Nothing else to wire up. With no
        // graph of its own, the scene gets the passes it needs.
        new RenderScene
        {
            Lights = [sun],
            GodRays = new GodRays
            {
                Strength = 0.8,  // how bright. Nought is off, and free
                Decay = 0.97,    // how far along its length a shaft fades
                Density = 0.9,   // how far towards the sun each pixel looks
                Samples = 64,    // smoothness against cost
            },
            // ...
        }
        """;

    private static RenderObject Box(long key, Vector3 at, Vector3 half, int rgb)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(at);
        transform.ScaleByDouble(half.X, half.Y, half.Z, 1);
        return new RenderObject { Key = key, Transform = transform, Colour = Tint.Hex(rgb).Linear, CastShadows = true };
    }

    /// <summary>
    /// The camera set for the light falling on the scene, as the day and night
    /// example does it: sunny sixteen at EV 15, stopping down first.
    /// </summary>
    private static RenderCamera Metered(RenderCamera camera, double lux)
    {
        var light = Math.Max(lux, 1e-5) * 100 / 250;
        var aperture = Math.Clamp(Math.Sqrt(light / 125), 1.4, 22.0);
        var shutter = Math.Clamp(aperture * aperture / light, 1 / 4000.0, 1 / 30.0);
        return camera with { Aperture = aperture, ShutterSpeed = shutter, Sensitivity = 100 };
    }
}
