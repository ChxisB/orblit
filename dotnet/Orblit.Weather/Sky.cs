using System.Diagnostics;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Weather;

/// <summary>What is lighting a scene from above. One at a time, because a renderer draws one directional light.</summary>
public enum CelestialBody
{
    Sun,
    Moon,
}

public static class CelestialBodyExtensions
{
    /// <summary>What it is called, which is also the name of a light of this body until somebody renames it.</summary>
    public static string Label(this CelestialBody body) => body switch
    {
        CelestialBody.Sun => "Sun",
        CelestialBody.Moon => "Moon",
        _ => throw new UnreachableException(),
    };
}

/// <summary>
/// The sky at one moment: where the light comes from, what colour it is, and
/// what a camera has to do to see by it.
/// </summary>
/// <param name="Body">Which body is up.</param>
/// <param name="Direction">The direction light travels: from the body towards the scene.</param>
/// <param name="Altitude">How high the body sits, in radians. Only the sun is ever below the horizon.</param>
/// <param name="LightColour">The colour of the light.</param>
/// <param name="Power">Watts per square metre, the unit a sun's strength is stated in.</param>
/// <param name="SkyColour">The colour of the sky itself.</param>
/// <param name="Ambient">How much light the sky itself casts, in lux.</param>
/// <param name="Exposure">What the camera has to be set to for any of this to be visible.</param>
// F2: eight values, all required and all called by name.
public sealed record SkyState(
    CelestialBody Body,
    Vector3 Direction,
    double Altitude,
    Tint LightColour,
    double Power,
    Tint SkyColour,
    double Ambient,
    CameraExposure Exposure);

/// <summary>
/// A camera's three settings, which together decide how much light reaches it.
/// A day cycle spans about seventeen stops, so one fixed exposure leaves either
/// the night black or the day white.
/// </summary>
/// <param name="Aperture">F-number.</param>
/// <param name="ShutterSpeed">Seconds.</param>
/// <param name="Sensitivity">ISO.</param>
public sealed record CameraExposure(double Aperture, double ShutterSpeed, double Sensitivity)
{
    private const double Ln2 = 0.6931471805599453;
    private const double DefaultShutter = 1.0 / 125;
    private const double SlowestShutter = 1.0 / 30;
    private const double FastestShutter = 1.0 / 4000;
    private const double FastestAperture = 1.4;
    private const double WidestAperture = 22;

    /// <summary>Sunny sixteen: what a camera is set to outdoors at midday.</summary>
    public static readonly CameraExposure Daylight = new(16, 1.0 / 125, 100);

    /// <summary>
    /// The exposure for a scene with this much light falling on it, in lux.
    /// Incident metering, as a hand-held meter works, so the light and the lens
    /// cannot disagree.
    /// </summary>
    public static CameraExposure ForIlluminance(double lux)
    {
        // Puts sunny sixteen at EV 15.
        const double calibration = 250;
        var wanted = Log2(Math.Max(lux, 1e-5) * 100 / calibration);
        var light = Math.Pow(2, wanted);

        // A photographer's order: stop down, then open up, then hold the shutter
        // open, and raise the sensitivity last because grain is the price.
        var aperture = Math.Sqrt(light * DefaultShutter);
        var shutter = DefaultShutter;
        var sensitivity = 100.0;

        if (aperture > WidestAperture)
        {
            aperture = WidestAperture;
            shutter = Doubles.Clamp(aperture * aperture / light, FastestShutter, 1.0);
        }
        else if (aperture < FastestAperture)
        {
            aperture = FastestAperture;
            shutter = aperture * aperture / light;
            if (shutter > SlowestShutter)
            {
                shutter = SlowestShutter;
                sensitivity = Doubles.Clamp(100 * aperture * aperture / (shutter * light), 50, 25600);
            }
        }

        return new CameraExposure(aperture, shutter, sensitivity);
    }

    /// <summary>The exposure value these three add up to, at ISO 100.</summary>
    public double Ev100 => Log2(Aperture * Aperture / ShutterSpeed) - Log2(Sensitivity / 100);

    // This divides by an ln2 constant. Math.Log2 rounds differently.
    private static double Log2(double value) => Math.Log(value) / Ln2;
}

/// <summary>
/// Where the sun and moon are through a day, and what that does to everything.
/// Not an ephemeris. It rises, crosses and sets, repeats, and has midnight and
/// midday opposite.
/// </summary>
public static class DayCycle
{
    /// <summary>How high the sun climbs at noon: sixty-five degrees.</summary>
    public const double PeakAltitude = 65 * Math.PI / 180;

    /// <summary>Direct sun at noon, in watts per square metre.</summary>
    public const double NoonPower = 110;

    /// <summary>
    /// Moonlight in the same unit. It is brighter than the real moon, because a
    /// night has to be dark and legible at once.
    /// </summary>
    public const double MoonPower = 0.0015;

    /// <summary>
    /// What the sun is down to at the horizon. It is the moon's, exactly, so the
    /// swap between them does not flicker.
    /// </summary>
    public const double TwilightPower = MoonPower;

    /// <summary>How much of what the body gives comes back off the sky.</summary>
    public const double SkyShare = 0.35;

    /// <summary>Starlight, in lux, so a night is dark and not a hole.</summary>
    public const double Starlight = 0.2;

    private static readonly Tint LowSun = Tint.Hex(0xFF8A3D);
    private static readonly Tint HighSun = Tint.Hex(0xFFF4E5);
    private static readonly Tint Moonlight = Tint.Hex(0xC3D4FF);

    private static readonly Tint NightSky = Tint.Hex(0x05070F);
    private static readonly Tint DuskSky = Tint.Hex(0x2A2438);
    private static readonly Tint DawnSky = Tint.Hex(0x7A5A63);
    private static readonly Tint DaySky = Tint.Hex(0x6E96C8);

    /// <summary>The sky at <paramref name="hour"/>, which runs from zero to twenty-four and wraps.</summary>
    public static SkyState At(double hour)
    {
        var time = Doubles.Mod(hour, 24);

        // Zero at six in the morning, pi at six in the evening.
        var swing = Math.Sin((time - 6) / 12 * Math.PI);
        var sunAltitude = swing * PeakAltitude;

        // Turns once a day, so it rises on one side and sets on the other.
        var azimuth = (time / 24) * 2 * Math.PI;

        var isDay = sunAltitude > 0;
        var altitude = isDay ? sunAltitude : -sunAltitude;
        var facing = isDay ? azimuth : azimuth + Math.PI;

        var toBody = new Vector3(
            Math.Cos(altitude) * Math.Sin(facing),
            Math.Sin(altitude),
            Math.Cos(altitude) * Math.Cos(facing));

        // Low sun is red because its light has come a long way through the air.
        var horizon = Smooth(Doubles.Clamp(swing, 0.0, 1.0) / 0.18);

        var power = isDay ? TwilightPower + ((NoonPower - TwilightPower) * swing * swing) : MoonPower;
        var bodyLux = Photometry.IrradianceToLux(power);
        var ambient = Math.Max(Starlight, SkyShare * bodyLux);

        // Light travels from the body to the scene, the way the body is not.
        var direction = -toBody;
        direction.Normalize();

        return new SkyState(
            isDay ? CelestialBody.Sun : CelestialBody.Moon,
            direction,
            altitude,
            isDay ? Tint.Lerp(LowSun, HighSun, horizon) : Moonlight,
            power,
            SkyAt(swing),
            ambient,
            // Metered off what falls on the scene: the body, angled by how high
            // it is, plus the sky.
            CameraExposure.ForIlluminance((bodyLux * Math.Max(0, Math.Sin(altitude))) + ambient));
    }

    // Four stops and not a formula. The dawn stop is what reads as a sunrise.
    private static Tint SkyAt(double swing)
    {
        if (swing <= 0)
        {
            return Tint.Lerp(DuskSky, NightSky, Smooth(-swing / 0.25));
        }

        if (swing < 0.18)
        {
            return Tint.Lerp(DuskSky, DawnSky, swing / 0.18);
        }

        return Tint.Lerp(DawnSky, DaySky, Smooth((swing - 0.18) / 0.5));
    }

    /// <summary>Smoothstep, clamped. NaN clamps to one.</summary>
    private static double Smooth(double t)
    {
        var x = Doubles.Clamp(t, 0.0, 1.0);
        return x * x * (3 - (2 * x));
    }
}
