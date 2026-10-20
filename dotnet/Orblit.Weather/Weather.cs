using System.Collections.Frozen;
using System.Diagnostics;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Weather;

/// <summary>
/// The kinds of weather a scene can be put into. A condition is a place to
/// start from and not a cage, because the values underneath stay free.
/// </summary>
public enum WeatherCondition
{
    Clear,
    Fair,
    Hazy,
    Misty,
    Overcast,
    Rain,
    Storm,
    Snow,
}

public static class WeatherConditionExtensions
{
    public static string Label(this WeatherCondition condition) => condition switch
    {
        WeatherCondition.Clear => "Clear",
        WeatherCondition.Fair => "Fair",
        WeatherCondition.Hazy => "Hazy",
        WeatherCondition.Misty => "Misty",
        WeatherCondition.Overcast => "Overcast",
        WeatherCondition.Rain => "Rain",
        WeatherCondition.Storm => "Storm",
        WeatherCondition.Snow => "Snow",
        _ => throw new UnreachableException(),
    };
}

/// <summary>
/// The shapes a sky's cloud can take. It is a choice apart from the weather,
/// because one condition can make very different skies.
/// </summary>
public enum CloudKind
{
    None,

    /// <summary>Flat bases at the condensation level, cauliflower tops, blue between.</summary>
    Cumulus,

    /// <summary>The lumps run together into a layer with breaks in it.</summary>
    Stratocumulus,

    /// <summary>The grey lid: low, shallow, and with almost no shape.</summary>
    Stratus,

    /// <summary>Ice seven kilometres up, drawn into streaks.</summary>
    Cirrus,

    /// <summary>Deep enough that its own base is in its own shadow.</summary>
    Cumulonimbus,
}

public static class CloudKindExtensions
{
    public static string Label(this CloudKind kind) => kind switch
    {
        CloudKind.None => "None",
        CloudKind.Cumulus => "Cumulus",
        CloudKind.Stratocumulus => "Stratocumulus",
        CloudKind.Stratus => "Stratus",
        CloudKind.Cirrus => "Cirrus",
        CloudKind.Cumulonimbus => "Cumulonimbus",
        _ => throw new UnreachableException(),
    };

    /// <summary>What a condition puts in the sky when nobody has said otherwise.</summary>
    public static CloudKind ForCondition(WeatherCondition condition) => condition switch
    {
        WeatherCondition.Clear => CloudKind.None,
        WeatherCondition.Fair => CloudKind.Cumulus,
        WeatherCondition.Hazy => CloudKind.Cirrus,
        WeatherCondition.Misty => CloudKind.Stratus,
        WeatherCondition.Overcast => CloudKind.Stratocumulus,
        WeatherCondition.Rain => CloudKind.Stratus,
        WeatherCondition.Storm => CloudKind.Cumulonimbus,
        WeatherCondition.Snow => CloudKind.Stratus,
        _ => throw new UnreachableException(),
    };
}

/// <summary>
/// What the air is doing. One value for the whole of it, so a change of weather
/// is one thing that can be interpolated and not twelve kept in step.
/// </summary>
/// <param name="CloudCover">How much of the sky is covered, from nothing to everything.</param>
/// <param name="FogColour">The colour of the fog.</param>
/// <param name="FogDensity">How thick the air is, per metre.</param>
/// <param name="FogHeight">Where the fog layer sits.</param>
/// <param name="FogFalloff">How quickly the fog thins going up.</param>
/// <param name="Mist">How much shape the air has.</param>
/// <param name="MistSize">How large that shape is, in metres.</param>
/// <param name="WindSpeed">Metres a second.</param>
/// <param name="Rain">How much rain is coming down, from nothing to a downpour.</param>
/// <param name="Snow">How much snow is coming down. Rain and snow are two amounts so one can cross the other.</param>
/// <param name="Lightning">How often it strikes, from never to every few seconds.</param>
/// <param name="CloudHeight">How high the cloud hangs, in metres.</param>
// F2: twelve values, eight required and all called by name, as the Dart constructor is.
public sealed record WeatherState(
    double CloudCover,
    Tint FogColour,
    double FogDensity,
    double FogHeight,
    double FogFalloff,
    double Mist,
    double MistSize,
    double WindSpeed,
    double Rain = 0,
    double Snow = 0,
    double Lightning = 0,
    double CloudHeight = 900)
{
    /// <summary>What each condition is made of. They are tuned as a set.</summary>
    public static readonly IReadOnlyDictionary<WeatherCondition, WeatherState> Presets = new Dictionary<WeatherCondition, WeatherState>
    {
        [WeatherCondition.Clear] = new(
            CloudCover: 0,
            FogColour: Tint.Hex(0xAFC2D6),
            FogDensity: 0.004,
            FogHeight: 0,
            FogFalloff: 0.15,
            Mist: 0,
            MistSize: 40,
            WindSpeed: 1.5),
        [WeatherCondition.Fair] = new(
            CloudCover: 0.25,
            FogColour: Tint.Hex(0xB6C4D2),
            FogDensity: 0.008,
            FogHeight: 0,
            FogFalloff: 0.2,
            Mist: 0.15,
            MistSize: 55,
            WindSpeed: 2.5,
            CloudHeight: 900),
        [WeatherCondition.Hazy] = new(
            CloudCover: 0.35,
            FogColour: Tint.Hex(0xC8CDD3),
            FogDensity: 0.022,
            FogHeight: 1,
            FogFalloff: 0.12,
            Mist: 0.3,
            MistSize: 70,
            WindSpeed: 2,
            CloudHeight: 7000),
        [WeatherCondition.Misty] = new(
            CloudCover: 0.55,
            FogColour: Tint.Hex(0xDCE0E4),
            FogDensity: 0.055,
            FogHeight: -1.5,
            FogFalloff: 0.4,
            Mist: 0.75,
            MistSize: 22,
            WindSpeed: 1.2,
            CloudHeight: 320),
        [WeatherCondition.Overcast] = new(
            CloudCover: 0.92,
            FogColour: Tint.Hex(0xA8AEB6),
            FogDensity: 0.03,
            FogHeight: 0,
            FogFalloff: 0.1,
            Mist: 0.4,
            MistSize: 90,
            WindSpeed: 4,
            CloudHeight: 700),
        [WeatherCondition.Rain] = new(
            CloudCover: 0.85,
            FogColour: Tint.Hex(0x9AA4AE),
            FogDensity: 0.035,
            FogHeight: 0,
            FogFalloff: 0.12,
            Mist: 0.35,
            MistSize: 70,
            WindSpeed: 5,
            Rain: 0.65,
            CloudHeight: 450),
        [WeatherCondition.Storm] = new(
            CloudCover: 1,
            FogColour: Tint.Hex(0x7C838C),
            FogDensity: 0.07,
            FogHeight: 0,
            FogFalloff: 0.14,
            Mist: 0.9,
            MistSize: 45,
            WindSpeed: 12,
            Rain: 0.95,
            Lightning: 0.6,
            CloudHeight: 600),
        [WeatherCondition.Snow] = new(
            CloudCover: 0.82,
            FogColour: Tint.Hex(0xD3D8DD),
            FogDensity: 0.04,
            FogHeight: 0,
            FogFalloff: 0.1,
            Mist: 0.4,
            MistSize: 60,
            CloudHeight: 500,
            WindSpeed: 2.2,
            Snow: 0.75),
    }.ToFrozenDictionary();

    /// <summary>Whether anything is falling.</summary>
    public bool IsWet => Rain + Snow > 0;

    /// <summary>How much of what is above the scene still reaches it. Heavy cloud still passes a tenth.</summary>
    public double Transmitted => 1 - (0.88 * CloudCover);

    /// <summary>How much wider the source becomes, as a multiplier on its angle. Cloud turns a disc into the sky.</summary>
    public double Spread => 1 + (CloudCover * CloudCover * 60);

    /// <summary>How much more of the light arrives from everywhere and not from one direction.</summary>
    public double Scattered => 1 + (CloudCover * 2.5);

    /// <summary>How far towards a flat grey the light and the sky are dragged.</summary>
    public double Greying => CloudCover * 0.7;

    public static WeatherState Of(WeatherCondition condition) =>
        Presets.GetValueOrDefault(condition) ?? Presets[WeatherCondition.Clear];

    /// <summary>
    /// Part of the way from one weather to another, in straight lines through
    /// every value. Weather has no business easing.
    /// </summary>
    public static WeatherState Lerp(WeatherState from, WeatherState to, double t)
    {
        var at = Doubles.Clamp(t, 0.0, 1.0);
        double Mix(double a, double b) => a + ((b - a) * at);

        return new WeatherState(
            CloudCover: Mix(from.CloudCover, to.CloudCover),
            FogColour: Tint.Lerp(from.FogColour, to.FogColour, at),
            FogDensity: Mix(from.FogDensity, to.FogDensity),
            FogHeight: Mix(from.FogHeight, to.FogHeight),
            FogFalloff: Mix(from.FogFalloff, to.FogFalloff),
            Mist: Mix(from.Mist, to.Mist),
            MistSize: Mix(from.MistSize, to.MistSize),
            WindSpeed: Mix(from.WindSpeed, to.WindSpeed),
            Rain: Mix(from.Rain, to.Rain),
            Snow: Mix(from.Snow, to.Snow),
            Lightning: Mix(from.Lightning, to.Lightning),
            CloudHeight: Mix(from.CloudHeight, to.CloudHeight));
    }

    /// <summary>How bright a flash of lightning is this instant, from nothing to one.</summary>
    public static double FlashAt(double clock, double frequency) => Strike.At(clock, frequency).Flash;

    /// <summary>The whole strike: how bright, which way, and what shape.</summary>
    public static Strike StrikeAt(double clock, double frequency) => Strike.At(clock, frequency);

    /// <summary>Which way the wind is blowing, as a direction on the ground.</summary>
    public static (double X, double Z) WindFrom(double degrees)
    {
        var radians = degrees * Math.PI / 180;
        return (Math.Sin(radians), Math.Cos(radians));
    }
}
