using System.Text.Json;
using Orblit.Parity;
using Orblit.VectorMath;

namespace Orblit.Light.Tests;

public class LightParityTests
{
    private static List<JsonElement> Cases(Fixture fixture, string op)
    {
        var cases = fixture.Cases.Where(c => c.GetProperty("op").GetString() == op).ToList();
        Assert.True(cases.Count > 0, $"the fixture has no {op} cases");
        return cases;
    }

    private static double[] Triple(JsonElement array) => [.. array.EnumerateArray().Select(Fixture.Number)];

    private static double[] Triple(Vector3 v) => [v.X, v.Y, v.Z];

    private static double[] Triple(Tint t) => [t.Red, t.Green, t.Blue];

    private static void AssertClose(Fixture fixture, string what, double[] expected, double[] actual)
    {
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.True(
                Tolerance.Close(expected[i], actual[i]),
                $"{what}, channel {i}: the fixture {expected[i]:R}, the port {actual[i]:R}, from fixture commit {fixture.SourceCommit}");
        }
    }

    private static void AssertClose(Fixture fixture, string what, JsonElement expected, double actual) =>
        AssertClose(fixture, what, [Fixture.Number(expected)], [actual]);

    [Fact]
    public void PhotometryMatchesTheFixture()
    {
        var fixture = Fixture.Load("orblit_light", "light");

        foreach (var c in Cases(fixture, "photometry"))
        {
            var fn = c.GetProperty("fn").GetString();
            var input = Triple(c.GetProperty("in"));
            var actual = fn switch
            {
                "wattsToLumens" => Photometry.WattsToLumens(input[0]),
                "irradianceToLux" => Photometry.IrradianceToLux(input[0]),
                "lumensToCandela" => Photometry.LumensToCandela(input[0]),
                "candelaToLumens" => Photometry.CandelaToLumens(input[0]),
                "influenceRadius" => Photometry.InfluenceRadius(input[0], input[1]),
                _ => throw new InvalidDataException($"unknown function {fn}"),
            };
            AssertClose(fixture, $"{fn}({string.Join(", ", input)})", c.GetProperty("expect"), actual);
        }
    }

    [Fact]
    public void TintsMatchTheFixture()
    {
        var fixture = Fixture.Load("orblit_light", "light");

        foreach (var c in Cases(fixture, "tint"))
        {
            var input = Triple(c.GetProperty("in"));
            var tint = new Tint(input[0], input[1], input[2]);
            AssertClose(fixture, $"linear of {tint}", Triple(c.GetProperty("linear")), Triple(tint.Linear));
            Assert.Equal(c.GetProperty("string").GetString(), tint.ToString());
        }

        foreach (var c in Cases(fixture, "fromLinear"))
        {
            var input = Triple(c.GetProperty("in"));
            var actual = Tint.FromLinear(new Vector3(input[0], input[1], input[2]));
            AssertClose(fixture, $"fromLinear({string.Join(", ", input)})", Triple(c.GetProperty("expect")), Triple(actual));
        }

        foreach (var c in Cases(fixture, "hex"))
        {
            var value = c.GetProperty("in").GetInt32();
            AssertClose(fixture, $"hex {value:X6}", Triple(c.GetProperty("expect")), Triple(Tint.Hex(value)));
        }

        foreach (var c in Cases(fixture, "lerp"))
        {
            var t = Fixture.Number(c.GetProperty("t"));
            var mixed = Tint.Lerp(new Tint(0.1, 0.5, 0.9), new Tint(0.9, 0.4, 0), t);
            AssertClose(fixture, $"lerp at {t}", Triple(c.GetProperty("expect")), Triple(mixed));
        }
    }

    [Fact]
    public void ARenderedLightMatchesTheFixture()
    {
        var fixture = Fixture.Load("orblit_light", "light");

        foreach (var c in Cases(fixture, "render"))
        {
            double Value(string name) => Fixture.Number(c.GetProperty(name));
            double? Optional(string name) =>
                c.GetProperty(name).ValueKind == JsonValueKind.Null ? null : Value(name);

            var light = new LightSource(
                (LightType)c.GetProperty("type").GetInt32(),
                new Vector3(1, 0.5, 0.2),
                Optional("power"),
                Value("radius"),
                Value("sunAngle"),
                Value("spotSize"),
                Value("spotBlend"),
                (AreaShape)c.GetProperty("shape").GetInt32(),
                Value("sizeX"),
                Value("sizeY"),
                castShadows: c.GetProperty("castShadows").GetBoolean(),
                customDistance: Optional("customDistance"));
            var rendered = light.ToRenderer();
            var expect = c.GetProperty("expect");
            var what = $"light {c}";

            Assert.Equal((RendererLightKind)expect.GetProperty("kind").GetInt32(), rendered.Kind);
            Assert.Equal(expect.GetProperty("castShadows").GetBoolean(), rendered.CastShadows);
            Assert.Equal(expect.GetProperty("approximated").GetBoolean(), rendered.Approximated);
            AssertClose(fixture, what, Triple(expect.GetProperty("color")), Triple(rendered.Color));
            AssertClose(fixture, what, expect.GetProperty("intensity"), rendered.Intensity);
            AssertClose(fixture, what, expect.GetProperty("falloffRadius"), rendered.FalloffRadius);
            AssertClose(fixture, what, expect.GetProperty("innerConeAngle"), rendered.InnerConeAngle);
            AssertClose(fixture, what, expect.GetProperty("outerConeAngle"), rendered.OuterConeAngle);
            AssertClose(fixture, what, expect.GetProperty("sunAngularRadius"), rendered.SunAngularRadius);
            AssertClose(fixture, what, expect.GetProperty("sourceRadius"), rendered.SourceRadius);
            AssertClose(fixture, what, expect.GetProperty("width"), rendered.Width);
            AssertClose(fixture, what, expect.GetProperty("height"), rendered.Height);
            AssertClose(fixture, what, expect.GetProperty("emittingArea"), light.EmittingArea);
        }
    }
}
