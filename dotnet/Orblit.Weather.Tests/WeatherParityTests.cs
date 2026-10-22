using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Light;
using Orblit.Parity;
using Orblit.VectorMath;

namespace Orblit.Weather.Tests;

public class WeatherParityTests
{
    private static readonly Fixture Golden = Fixture.Load("orblit_weather", "weather");

    [Fact]
    public void DaysMatchTheFixture() => Golden.Check("day", c => Sky(DayCycle.At(Input(c, 0))));

    [Fact]
    public void ExposuresMatchTheFixture() =>
        Golden.Check("exposure", c => Exposure(CameraExposure.ForIlluminance(Input(c, 0))));

    [Fact]
    public void DaylightMatchesTheFixture() => Golden.Check("daylight", _ => Exposure(CameraExposure.Daylight));

    [Fact]
    public void PresetsMatchTheFixture() => Golden.Check("preset", Preset);

    [Fact]
    public void LabelsMatchTheFixture() => Golden.Check("label", Label);

    [Fact]
    public void LerpsMatchTheFixture() => Golden.Check("lerp", Lerp);

    [Fact]
    public void StrikesMatchTheFixture() =>
        Golden.Check("strike", c => Dump.Attempt(() => Place(Strike.At(Input(c, 0), Input(c, 1)))));

    [Fact]
    public void StrikesAskedThroughTheWeatherMatchTheFixture() =>
        Golden.Check("strikeAt", c => Dump.Attempt(() => Place(WeatherState.StrikeAt(Input(c, 0), Input(c, 1)))));

    [Fact]
    public void FlashesMatchTheFixture() =>
        Golden.Check("flashAt", c => Dump.Attempt(() => Dump.Num(WeatherState.FlashAt(Input(c, 0), Input(c, 1)))));

    [Fact]
    public void WindsMatchTheFixture() => Golden.Check("wind", Wind);

    private static double Input(JsonElement c, int index) => Fixture.Number(c.GetProperty("in")[index]);

    private static string Name(JsonElement c, int index) => c.GetProperty("in")[index].GetString()!;

    private static WeatherCondition Condition(JsonElement c, int index) =>
        Enum.Parse<WeatherCondition>(Name(c, index), ignoreCase: true);

    private static JsonArray Triple(double a, double b, double c) => new(Dump.Num(a), Dump.Num(b), Dump.Num(c));

    private static JsonArray Triple(Vector3 v) => Triple(v.X, v.Y, v.Z);

    private static JsonArray Triple(Tint t) => Triple(t.Red, t.Green, t.Blue);

    private static JsonObject Exposure(CameraExposure e) => new()
    {
        ["aperture"] = Dump.Num(e.Aperture),
        ["shutterSpeed"] = Dump.Num(e.ShutterSpeed),
        ["sensitivity"] = Dump.Num(e.Sensitivity),
        ["ev100"] = Dump.Num(e.Ev100),
    };

    private static JsonObject Sky(SkyState s) => new()
    {
        ["body"] = s.Body.Label(),
        ["direction"] = Triple(s.Direction),
        ["altitude"] = Dump.Num(s.Altitude),
        ["lightColour"] = Triple(s.LightColour),
        ["power"] = Dump.Num(s.Power),
        ["skyColour"] = Triple(s.SkyColour),
        ["ambient"] = Dump.Num(s.Ambient),
        ["exposure"] = Exposure(s.Exposure),
    };

    private static JsonObject Weather(WeatherState w) => new()
    {
        ["cloudCover"] = Dump.Num(w.CloudCover),
        ["fogColour"] = Triple(w.FogColour),
        ["fogDensity"] = Dump.Num(w.FogDensity),
        ["fogHeight"] = Dump.Num(w.FogHeight),
        ["fogFalloff"] = Dump.Num(w.FogFalloff),
        ["mist"] = Dump.Num(w.Mist),
        ["mistSize"] = Dump.Num(w.MistSize),
        ["windSpeed"] = Dump.Num(w.WindSpeed),
        ["rain"] = Dump.Num(w.Rain),
        ["snow"] = Dump.Num(w.Snow),
        ["lightning"] = Dump.Num(w.Lightning),
        ["cloudHeight"] = Dump.Num(w.CloudHeight),
        ["isWet"] = w.IsWet,
        ["transmitted"] = Dump.Num(w.Transmitted),
        ["spread"] = Dump.Num(w.Spread),
        ["scattered"] = Dump.Num(w.Scattered),
        ["greying"] = Dump.Num(w.Greying),
    };

    private static JsonObject Place(Strike s) => new()
    {
        ["flash"] = Dump.Num(s.Flash),
        ["direction"] = Triple(s.Direction),
        ["seed"] = Dump.Num(s.Seed),
    };

    private static JsonNode Preset(JsonElement c)
    {
        var condition = Condition(c, 0);
        return new JsonObject
        {
            ["label"] = condition.Label(),
            ["cloud"] = CloudKindExtensions.ForCondition(condition).Label(),
            ["state"] = Weather(WeatherState.Of(condition)),
        };
    }

    private static JsonNode Label(JsonElement c) => Name(c, 0) switch
    {
        "body" => Enum.Parse<CelestialBody>(Name(c, 1), ignoreCase: true).Label(),
        "kind" => Enum.Parse<CloudKind>(Name(c, 1), ignoreCase: true).Label(),
        var other => throw new InvalidDataException($"unknown label set {other}"),
    };

    private static JsonNode Lerp(JsonElement c) =>
        Weather(WeatherState.Lerp(WeatherState.Of(Condition(c, 0)), WeatherState.Of(Condition(c, 1)), Input(c, 2)));

    private static JsonNode Wind(JsonElement c)
    {
        var (x, z) = WeatherState.WindFrom(Input(c, 0));
        return new JsonObject { ["x"] = Dump.Num(x), ["z"] = Dump.Num(z) };
    }
}
