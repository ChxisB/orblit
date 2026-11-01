using Orblit.Light;
using Orblit.Weather;

namespace Orblit.Scene;

/// <summary>
/// A light, stated the way a fixture is.
/// </summary>
/// <remarks>
/// Power in watts for anything with a bulb in it, and watts per square metre for a sun, because a sun
/// is not a bulb a long way off: it is a sheet of light arriving parallel, and the number on it is an
/// irradiance. Keeping the two in the same field under the same name is what version one of this
/// format did, and every scene written then had to be converted.
/// </remarks>
public sealed class LightComponent(
    LightType kind = LightType.Sun,
    double power = 1000,
    Tint? colour = null,
    double spotSize = 45,
    double spotBlend = 0.15,
    double sourceRadius = 0.1,
    double sunAngle = 0.526,
    CelestialBody body = CelestialBody.Sun,
    bool castShadows = true) : SceneComponent
{
    public LightType Kind { get; } = kind;

    /// <summary>Watts, or watts per square metre when this is a sun.</summary>
    public double Power { get; } = power;

    public Tint Colour { get; } = colour ?? Tint.Hex(0xD9634F);

    /// <summary>The full cone angle of a spot, in degrees, and how much of its edge is soft.</summary>
    public double SpotSize { get; } = spotSize;

    public double SpotBlend { get; } = spotBlend;

    /// <summary>
    /// How big the thing emitting is, in metres. What decides how soft a shadow edge is: a bare
    /// filament gives a hard one and a softbox does not.
    /// </summary>
    public double SourceRadius { get; } = sourceRadius;

    /// <summary>How wide a sun is from the ground, in degrees. Half a degree is ours.</summary>
    public double SunAngle { get; } = sunAngle;

    public CelestialBody Body { get; } = body;

    public bool CastShadows { get; } = castShadows;

    public override string Type => SceneComponents.Light;

    public static LightComponent FromJson(JsonMap json) => new(
        Values.Named<LightType>(Values.At(json, "kind")) ?? LightType.Sun,
        Values.Number(json, "power", 1000),
        Values.ReadTint(Values.At(json, "colour")),
        Values.Number(json, "spotSize", 45),
        Values.Number(json, "spotBlend", 0.15),
        Values.Number(json, "sourceRadius", 0.1),
        Values.Number(json, "sunAngle", 0.526),
        Values.Named<CelestialBody>(Values.At(json, "body")) ?? CelestialBody.Sun,
        Values.Flag(json, "castShadows", true));

    /// <summary>
    /// Every field, every time, including the ones this kind of light does not use. The alternative,
    /// writing only what applies, loses a spot's cone angle the moment somebody flips it to a point
    /// light and back, which is exactly the kind of quiet data loss that makes people stop trusting a
    /// tool.
    /// </summary>
    public override JsonMap ToJson() => new OrderedDictionary<string, object?>
    {
        ["kind"] = EnumNames.Of(Kind),
        ["power"] = Power,
        ["colour"] = Values.TintToJson(Colour),
        ["spotSize"] = SpotSize,
        ["spotBlend"] = SpotBlend,
        ["sourceRadius"] = SourceRadius,
        ["sunAngle"] = SunAngle,
        ["body"] = EnumNames.Of(Body),
        ["castShadows"] = CastShadows,
    };
}

/// <summary>
/// A point of view, and the lens at it.
/// </summary>
/// <remarks>
/// The angle is across the shorter axis of the frame, which is the convention the renderer measures
/// by: stating it the other way makes a scene reframe itself when somebody turns a phone sideways.
/// </remarks>
public sealed class CameraComponent(double fieldOfView = 50, double near = 0.1, double far = 1000) : SceneComponent
{
    /// <summary>Degrees across the shorter axis.</summary>
    public double FieldOfView { get; } = fieldOfView;

    public double Near { get; } = near;

    public double Far { get; } = far;

    public override string Type => SceneComponents.Camera;

    public static CameraComponent FromJson(JsonMap json) => new(
        Values.Number(json, "fieldOfView", 50),
        Values.Number(json, "near", 0.1),
        Values.Number(json, "far", 1000));

    public override JsonMap ToJson() => new OrderedDictionary<string, object?>
    {
        ["fieldOfView"] = FieldOfView,
        ["near"] = Near,
        ["far"] = Far,
    };
}

/// <summary>
/// What the air is doing, and what it is on its way to doing.
/// </summary>
/// <remarks>
/// An entity rather than a set of fields on the scene, because weather changes: a change has two ends
/// and a set of fields can only hold one. The <see cref="Condition"/> is where it is heading,
/// <see cref="Air"/> is where it is now, and <see cref="TransitionSeconds"/> is how long it takes to
/// get there.
/// </remarks>
public sealed class WeatherComponent(
    WeatherState air,
    WeatherCondition condition = WeatherCondition.Clear,
    CloudKind? cloudKind = null,
    double windDirection = 135,
    double transitionSeconds = 8) : SceneComponent
{
    public WeatherCondition Condition { get; } = condition;

    /// <summary>
    /// The cloud somebody chose, or null to follow the condition's own. Null is not a missing value
    /// here: it is "whatever this condition means by cloud", so a scene that has never been given one
    /// keeps following the mapping when the mapping improves rather than being frozen at whatever it
    /// happened to be the day it was saved.
    /// </summary>
    public CloudKind? CloudKind { get; } = cloudKind;

    public WeatherState Air { get; } = air;

    /// <summary>Where the wind comes from, in degrees clockwise from north.</summary>
    public double WindDirection { get; } = windDirection;

    public double TransitionSeconds { get; } = transitionSeconds;

    public override string Type => SceneComponents.Weather;

    /// <summary>Weather with nothing set, which is a clear sky.</summary>
    public static WeatherComponent Clear() => new(WeatherState.Of(WeatherCondition.Clear));

    public static WeatherComponent FromJson(JsonMap json)
    {
        var condition = Values.Named<WeatherCondition>(Values.At(json, "condition")) ?? WeatherCondition.Clear;
        return new WeatherComponent(
            AirFromJson(Values.At(json, "air"), condition),
            condition,
            Values.Named<CloudKind>(Values.At(json, "cloudKind")),
            Values.Number(json, "windDirection", 135),
            Values.Number(json, "transition", 8));
    }

    public override JsonMap ToJson() => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["condition"] = EnumNames.Of(Condition),
        ["cloudKind"] = CloudKind is { } kind ? EnumNames.Of(kind) : null,
        ["windDirection"] = WindDirection,
        ["transition"] = TransitionSeconds,
        ["air"] = AirToJson(Air),
    });

    /// <summary>
    /// The air as JSON. Static and public because the 2 to 3 migration writes one of these out of a
    /// scene's old fog fields, and it should produce exactly what a saved scene does rather than its
    /// own nearly identical shape.
    /// </summary>
    public static OrderedDictionary<string, object?> AirToJson(WeatherState air) => Values.Pruned(new OrderedDictionary<string, object?>
    {
        ["cover"] = air.CloudCover,
        ["colour"] = Values.TintToJson(air.FogColour),
        ["density"] = air.FogDensity,
        ["height"] = air.FogHeight,
        ["falloff"] = air.FogFalloff,
        ["mist"] = air.Mist,
        ["size"] = air.MistSize,
        ["wind"] = air.WindSpeed,

        // Left out when there is none, so a dry scene's file says nothing about rain.
        ["rain"] = air.Rain > 0 ? air.Rain : null,
        ["snow"] = air.Snow > 0 ? air.Snow : null,
        ["lightning"] = air.Lightning > 0 ? air.Lightning : null,
        ["cloudHeight"] = air.CloudHeight,
    });

    /// <summary>And back, falling through to the condition's own values for anything the file does not say.</summary>
    public static WeatherState AirFromJson(object? raw, WeatherCondition condition)
    {
        var preset = WeatherState.Of(condition);
        if (raw is not JsonMap air)
        {
            return preset;
        }

        return new WeatherState(
            CloudCover: Values.Number(air, "cover", preset.CloudCover),
            FogColour: Values.ReadTint(Values.At(air, "colour"), preset.FogColour),
            FogDensity: Values.Number(air, "density", preset.FogDensity),
            FogHeight: Values.Number(air, "height", preset.FogHeight),
            FogFalloff: Values.Number(air, "falloff", preset.FogFalloff),
            Mist: Values.Number(air, "mist", preset.Mist),
            MistSize: Values.Number(air, "size", preset.MistSize),
            WindSpeed: Values.Number(air, "wind", preset.WindSpeed),
            Rain: Values.Number(air, "rain", 0),
            Snow: Values.Number(air, "snow", 0),
            Lightning: Values.Number(air, "lightning", 0),
            CloudHeight: Values.Number(air, "cloudHeight", preset.CloudHeight));
    }
}
