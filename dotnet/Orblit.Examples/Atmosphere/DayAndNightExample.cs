using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples;

/// <summary>
/// A day, and the camera that makes it visible.
/// </summary>
/// <remarks>
/// The arithmetic is the interesting part. A sun at noon lays about ninety
/// thousand lux on the ground and a moon lays one. That is seventeen stops, and
/// no single camera setting covers both. So the exposure is metered off the
/// light the scene has, the way a hand-held meter works, and every hour of the
/// day lands in the middle of the range.
/// </remarks>
public sealed class DayAndNightExample : Example
{
    private const double Moonlight = 1;

    private double _hour = 10;
    private bool _running = true;
    private double _hoursPerSecond = 0.6;
    private double _cover = 0.4;

    public override string Name => "Day and night";

    public override string Blurb => "A sun and a moon crossing the sky, with the camera metered for both.";

    public override ExampleSection Section => ExampleSection.Atmosphere;

    public override ViewPoint ViewPoint => new() { Distance = 14, Pitch = 0.18 };

    public override RenderScene Scene(RenderCamera camera, double seconds)
    {
        var now = _running ? (_hour + (seconds * _hoursPerSecond)) % 24 : _hour;
        // Zero at six in the morning and pi at six in the evening, so the sun is
        // up for half of it and highest at noon.
        var swing = Math.Sin((now - 6) / 12 * Math.PI);
        var isDay = swing > 0;
        var altitude = (isDay ? swing : -swing) * (65 * Math.PI / 180);
        var azimuth = (now / 24 * 2 * Math.PI) + (isDay ? 0 : Math.PI);
        var toBody = new Vector3(
            Math.Cos(altitude) * Math.Sin(azimuth),
            Math.Sin(altitude),
            Math.Cos(altitude) * Math.Cos(azimuth));
        // The sun falls to the moon's own strength as it reaches the horizon, so
        // the handover is a change of direction and not a step in how much light
        // there is.
        var lux = isDay ? Moonlight + (75000 * swing * swing) : Moonlight;
        // A clear sky gives back about a third of what lands on it.
        var ambient = Math.Max(0.2, lux * 0.35);
        var incident = (lux * Math.Max(0, Math.Sin(altitude))) + ambient;
        var bodyColour = (isDay
            ? Tint.Lerp(Tint.Hex(0xFF8A3D), Tint.Hex(0xFFF4E5), Math.Clamp(swing / 0.18, 0.0, 1.0))
            : Tint.Hex(0xC3D4FF)).Linear;
        // Deep overhead and pale along the ground. The lower the body sits, the
        // more the horizon takes its colour, which is the whole of a sunset.
        // Worked out from where the body is, so it happens on both sides of the
        // day without either being written down.
        var flat = SkyAt(swing).Linear;
        var glow = Math.Clamp(1 - (altitude / 0.45), 0.0, 1.0);
        return new RenderScene
        {
            Objects = [.. Enumerable.Range(0, 6).Select(Stone), Ground()],
            Lights =
            [
                new RenderLight
                {
                    Key = 310,
                    Kind = RenderLightKind.Directional,
                    Intensity = lux,
                    // Light travels from the body towards the scene, which is the
                    // way the body is not.
                    Direction = (-toBody).Normalized(),
                    Colour = bodyColour,
                    SunAngularRadius = 0.53,
                    // A wide soft halo reads as a sun through air, and a tight one
                    // as a moon on a clear night. It is most of what tells them apart.
                    HaloSize = isDay ? 12 : 3,
                    HaloFalloff = isDay ? 70 : 240,
                },
            ],
            Sky = new Sky
            {
                Colour = flat,
                Zenith = flat * 0.82,
                Horizon = Mix(Mix(flat, new Vector3(0.72, 0.80, 0.92), 0.28), bodyColour, glow * 0.55),
                Ambient = ambient,
                // The same direction the light comes from, which is why the cloud
                // lights up on the side the sun is on and goes to shadow on the
                // other, through the whole day.
                BodyDirection = toBody,
                BodyColour = bodyColour * (isDay ? 1.0 : 0.30),
                BodySize = 0.011,
                Clouds = Clouds.Cumulus with { Cover = _cover, Wind = new Vector2(5, 2) },
            },
            Camera = Metered(camera, incident),
        };
    }

    public override IReadOnlyList<ExampleSetting> Settings() =>
    [
        new Toggle("Running", _running, value => _running = value),
        new Setting("Hour", _hour, 0, 24, value => _hour = value),
        new Setting("Cloud cover", _cover, 0, 1, value => _cover = value),
        .. _running
            ? [new Setting("Speed", _hoursPerSecond, 0.05, 4, value => _hoursPerSecond = value) { Unit = " h/s" }]
            : Array.Empty<ExampleSetting>(),
    ];

    public override string Code => """
        // Where the body is, at this hour.
        var swing = Math.Sin((hour - 6) / 12 * Math.PI);    // up for half the day
        var isDay = swing > 0;
        var altitude = (isDay ? swing : -swing) * (65 * Math.PI / 180);
        var azimuth = (hour / 24 * 2 * Math.PI) + (isDay ? 0 : Math.PI);   // the moon opposes

        // The sun falls to the moon's own strength at the horizon, so the swap is a
        // change of direction and not a step in how much light there is.
        var lux = isDay ? 1.0 + (75000 * swing * swing) : 1.0;
        var ambient = Math.Max(0.2, lux * 0.35);    // a clear sky returns a third

        new RenderScene
        {
            Lights =
            [
                new RenderLight
                {
                    Key = 310,
                    Kind = RenderLightKind.Directional,
                    Intensity = lux,
                    Direction = (-toBody).Normalized(),
                    SunAngularRadius = 0.53,    // the real sun's, and the real moon's
                    HaloSize = isDay ? 12 : 3,  // glare, or none: what tells them apart
                },
            ],
            Sky = new Sky { Colour = SkyAt(swing), Ambient = ambient },
            // Seventeen stops between noon and moonlight. Meter it, or one of the
            // two is a solid colour.
            Camera = camera with { Aperture = aperture, ShutterSpeed = shutter, Sensitivity = iso },
        }
        """;

    private static RenderObject Stone(int i)
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(((i % 3) - 1) * 2.6, -0.5, ((i / 3) - 0.5) * 2.8));
        transform.RotateY(i * 0.5);
        transform.ScaleByDouble(0.7, 0.9 + ((i % 3) * 0.4), 0.7, 1);
        return new RenderObject { Key = 300 + i, Transform = transform, Colour = Tint.Hex(0xCFC7BB).Linear };
    }

    private static RenderObject Ground()
    {
        var transform = Matrix4.Identity();
        transform.SetTranslation(new Vector3(0, -1.5, 0));
        transform.ScaleByDouble(14.0, 0.06, 14.0, 1);
        return new RenderObject
        {
            Key = 290,
            Transform = transform,
            Colour = Tint.Hex(0x43494F).Linear,
            CastShadows = false,
        };
    }

    private static Vector3 Mix(Vector3 from, Vector3 to, double t) => new(
        from.X + ((to.X - from.X) * t),
        from.Y + ((to.Y - from.Y) * t),
        from.Z + ((to.Z - from.Z) * t));

    /// <summary>
    /// The sky through the day: night, the minute before dawn, an hour after it,
    /// and midday. Three stops would read as a filter being turned.
    /// </summary>
    private static Tint SkyAt(double swing)
    {
        var night = Tint.Hex(0x070C18);
        var dusk = Tint.Hex(0x2A2438);
        var dawn = Tint.Hex(0x7A5A63);
        var day = Tint.Hex(0x6E96C8);
        if (swing <= 0)
        {
            return Tint.Lerp(dusk, night, Math.Clamp(-swing / 0.25, 0.0, 1.0));
        }
        return swing < 0.18
            ? Tint.Lerp(dusk, dawn, swing / 0.18)
            : Tint.Lerp(dawn, day, Math.Clamp((swing - 0.18) / 0.5, 0.0, 1.0));
    }

    /// <summary>
    /// The camera, set for the light that is falling on the scene.
    /// </summary>
    /// <remarks>
    /// Incident metering. The calibration puts sunny sixteen at EV 15, where a
    /// century of film boxes says it goes. Then the three settings are chosen in
    /// a photographer's order. Stop down while there is light to spare, then
    /// open up, then hold the shutter, and only then raise the sensitivity,
    /// because grain is the price paid last.
    /// </remarks>
    private static RenderCamera Metered(RenderCamera camera, double lux)
    {
        var wanted = Math.Log(Math.Max(lux, 1e-5) * 100 / 250) / Math.Log(2);
        var light = Math.Pow(2, wanted);
        var aperture = Math.Sqrt(light / 125);
        var shutter = 1 / 125.0;
        var sensitivity = 100.0;
        if (aperture > 22)
        {
            aperture = 22;
            shutter = Math.Clamp(484 / light, 1 / 4000.0, 1.0);
        }
        else if (aperture < 1.4)
        {
            aperture = 1.4;
            shutter = 1.96 / light;
            if (shutter > 1 / 30.0)
            {
                shutter = 1 / 30.0;
                sensitivity = Math.Clamp(100 * 1.96 * 30 / light, 50, 25600);
            }
        }
        return camera with { Aperture = aperture, ShutterSpeed = shutter, Sensitivity = sensitivity };
    }
}
