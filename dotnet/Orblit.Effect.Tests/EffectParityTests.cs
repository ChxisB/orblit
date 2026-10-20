using System.Text.Json;
using Orblit.Parity;
using Orblit.VectorMath;

namespace Orblit.Effect.Tests;

public class EffectParityTests
{
    private static readonly Dictionary<string, Ease> Curves = new()
    {
        ["linear"] = Eases.Linear,
        ["inQuad"] = Eases.InQuad,
        ["outQuad"] = Eases.OutQuad,
        ["inCubic"] = Eases.InCubic,
        ["outCubic"] = Eases.OutCubic,
        ["inOutQuad"] = Eases.InOutQuad,
        ["inOutCubic"] = Eases.InOutCubic,
        ["outBack"] = Eases.OutBack,
        ["outElastic"] = Eases.OutElastic,
        ["outBounce"] = Eases.OutBounce,
    };

    private static List<JsonElement> Cases(Fixture fixture, string op)
    {
        var cases = fixture.Cases.Where(c => c.GetProperty("op").GetString() == op).ToList();
        Assert.True(cases.Count > 0, $"the fixture has no {op} cases");
        return cases;
    }

    private static double[] Numbers(JsonElement array) => [.. array.EnumerateArray().Select(Fixture.Number)];

    private static Vector3 Vector(JsonElement array)
    {
        var v = Numbers(array);
        return new Vector3(v[0], v[1], v[2]);
    }

    private static void AssertClose(Fixture fixture, string what, double[] expected, double[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.True(
                Tolerance.Close(expected[i], actual[i]),
                $"{what}, value {i}: Dart {expected[i]:R}, C# {actual[i]:R}, from Dart commit {fixture.DartCommit}");
        }
    }

    private static void AssertClose(Fixture fixture, string what, JsonElement expected, double actual) =>
        AssertClose(fixture, what, [Fixture.Number(expected)], [actual]);

    /// <summary>A curve by name, with <c>reversed:</c> and <c>thereAndBack:</c> wrapping another.</summary>
    private static Ease EaseNamed(string name)
    {
        var colon = name.IndexOf(':', StringComparison.Ordinal);
        if (colon < 0)
        {
            return Curves[name];
        }

        var inner = EaseNamed(name[(colon + 1)..]);
        return name[..colon] switch
        {
            "reversed" => Eases.Reversed(inner),
            "thereAndBack" => Eases.ThereAndBack(inner),
            _ => throw new InvalidDataException($"unknown ease {name}"),
        };
    }

    private static Ease? EaseOf(JsonElement spec) =>
        spec.GetProperty("ease").ValueKind == JsonValueKind.Null ? null : EaseNamed(spec.GetProperty("ease").GetString()!);

    private static double Read(JsonElement spec, string name) => Fixture.Number(spec.GetProperty(name));

    private static long? OptionalLong(JsonElement spec, string name) =>
        spec.GetProperty(name).ValueKind == JsonValueKind.Null ? null : spec.GetProperty(name).GetInt64();

    /// <summary>Builds the effect a spec describes. A null ease leaves the effect's own default in place.</summary>
    private static TimedEffect Build(JsonElement spec)
    {
        var kind = spec.GetProperty("k").GetString();
        return kind switch
        {
            "wait" => new Wait(Read(spec, "duration")),
            "move" => new MoveBy(Vector(spec.GetProperty("offset")), Read(spec, "duration"), EaseOf(spec)),
            "turn" => BuildTurn(spec),
            "grow" => new GrowTo(Vector(spec.GetProperty("factor")), Read(spec, "duration"), EaseOf(spec)),
            "fade" => new FadeTo(Read(spec, "opacity"), Read(spec, "duration"), EaseOf(spec)),
            "tint" => BuildTint(spec),
            "shake" => BuildShake(spec),
            "then" => new Then(BuildAll(spec.GetProperty("steps"))),
            "both" => new Both(BuildAll(spec.GetProperty("parts"))),
            "again" => new Again(Build(spec.GetProperty("inner")), OptionalLong(spec, "times")),
            "outAndBack" => new OutAndBack(Build(spec.GetProperty("inner"))),
            "after" => new After(Read(spec, "delay"), Build(spec.GetProperty("inner"))),
            _ => throw new InvalidDataException($"unknown effect {kind}"),
        };
    }

    private static List<TimedEffect> BuildAll(JsonElement specs) => [.. specs.EnumerateArray().Select(Build)];

    private static TurnBy BuildTurn(JsonElement spec)
    {
        var axis = spec.GetProperty("axis");
        return new TurnBy(
            Read(spec, "angle"),
            Read(spec, "duration"),
            axis.ValueKind == JsonValueKind.Null ? null : Vector(axis),
            EaseOf(spec));
    }

    private static TintTo BuildTint(JsonElement spec)
    {
        var c = Numbers(spec.GetProperty("colour"));
        return new TintTo(new Vector4(c[0], c[1], c[2], c[3]), Read(spec, "duration"), EaseOf(spec));
    }

    private static Shake BuildShake(JsonElement spec)
    {
        var rate = spec.GetProperty("rate");
        return new Shake(
            Read(spec, "strength"),
            Read(spec, "duration"),
            rate.ValueKind == JsonValueKind.Null ? 24 : Fixture.Number(rate),
            OptionalLong(spec, "seed") ?? 0,
            EaseOf(spec));
    }

    private static void AssertChange(Fixture fixture, string what, JsonElement expected, Change actual)
    {
        AssertClose(fixture, $"{what} move", Numbers(expected.GetProperty("move")), [actual.Move.X, actual.Move.Y, actual.Move.Z]);
        AssertClose(
            fixture,
            $"{what} turn",
            Numbers(expected.GetProperty("turn")),
            [actual.Turn.X, actual.Turn.Y, actual.Turn.Z, actual.Turn.W]);
        AssertClose(fixture, $"{what} grow", Numbers(expected.GetProperty("grow")), [actual.Grow.X, actual.Grow.Y, actual.Grow.Z]);
        AssertClose(
            fixture,
            $"{what} tint",
            Numbers(expected.GetProperty("tint")),
            [actual.Tint.X, actual.Tint.Y, actual.Tint.Z, actual.Tint.W]);
        AssertClose(fixture, $"{what} fade", expected.GetProperty("fade"), actual.Fade);
        Assert.True(
            expected.GetProperty("isNothing").GetBoolean() == actual.IsNothing,
            $"{what} isNothing: Dart {expected.GetProperty("isNothing").GetBoolean()}, from Dart commit {fixture.DartCommit}");
    }

    [Fact]
    public void CurvesMatchTheDartTwin()
    {
        var fixture = Fixture.Load("orblit_effect", "effect");

        foreach (var c in Cases(fixture, "ease"))
        {
            var name = c.GetProperty("name").GetString()!;
            var t = Fixture.Number(c.GetProperty("t"));
            AssertClose(fixture, $"{name}({t:R})", c.GetProperty("expect"), EaseNamed(name)(t));
        }
    }

    [Fact]
    public void EffectsMatchTheDartTwinAtEverySampledTime()
    {
        var fixture = Fixture.Load("orblit_effect", "effect");

        foreach (var c in Cases(fixture, "effect"))
        {
            var spec = c.GetProperty("spec");
            var effect = Build(spec);
            var label = spec.GetRawText();
            AssertClose(fixture, $"{label} duration", c.GetProperty("duration"), effect.Duration);

            foreach (var sample in c.GetProperty("samples").EnumerateArray())
            {
                var at = Fixture.Number(sample.GetProperty("at"));
                var what = $"{label} at {at:R}";
                if (sample.TryGetProperty("throws", out _))
                {
                    // A shake floors its clock, and a floor of NaN or infinity throws.
                    Assert.ThrowsAny<ArgumentOutOfRangeException>(() => effect.At(at));
                    continue;
                }

                AssertChange(fixture, what, sample.GetProperty("expect"), effect.At(at));
                Assert.True(sample.GetProperty("done").GetBoolean() == effect.DoneBy(at), $"{what} doneBy");
            }
        }
    }

    [Fact]
    public void PlayingMatchesTheDartTwinStepByStep()
    {
        var fixture = Fixture.Load("orblit_effect", "effect");

        var scripts = Cases(fixture, "playing");
        for (var script = 0; script < scripts.Count; script++)
        {
            var c = scripts[script];
            var playing = new Playing();
            var ids = new Dictionary<TimedEffect, int>(ReferenceEqualityComparer.Instance);
            var steps = c.GetProperty("steps").EnumerateArray().ToList();
            var expected = c.GetProperty("expect").EnumerateArray().ToList();
            Assert.Equal(steps.Count, expected.Count);

            for (var i = 0; i < steps.Count; i++)
            {
                var what = $"script {script} step {i}";
                var want = expected[i];
                switch (steps[i].GetProperty("do").GetString())
                {
                    case "start":
                        var effect = Build(steps[i].GetProperty("spec"));
                        ids[effect] = ids.Count;
                        playing.Start(effect);
                        break;
                    case "advance":
                        var finished = playing.Advance(Read(steps[i], "by"));
                        Assert.Equal(
                            want.GetProperty("finished").EnumerateArray().Select(e => e.GetInt32()),
                            finished.Select(e => ids[e]));
                        break;
                    case "bake":
                        AssertChange(fixture, $"{what} baked", want.GetProperty("baked"), playing.Bake());
                        break;
                    case "clear":
                        playing.Clear();
                        break;
                    default:
                        throw new InvalidDataException("unknown step");
                }

                AssertClose(fixture, $"{what} now", want.GetProperty("now"), playing.Now);
                Assert.True(want.GetProperty("length").GetInt32() == playing.Length, $"{what} length");
                AssertChange(fixture, $"{what} settled", want.GetProperty("settled"), playing.Settled);
                AssertChange(fixture, $"{what} change", want.GetProperty("change"), playing.Change);
            }
        }
    }

    [Fact]
    public void AppliedMatchesTheDartTwin()
    {
        var fixture = Fixture.Load("orblit_effect", "effect");

        foreach (var c in Cases(fixture, "applied"))
        {
            var start = new Matrix4();
            var to = Numbers(c.GetProperty("to"));
            for (var i = 0; i < 16; i++)
            {
                start[i] = to[i];
            }

            var turn = Numbers(c.GetProperty("turn"));
            var change = new Change(
                Vector(c.GetProperty("move")),
                new Quaternion(turn[0], turn[1], turn[2], turn[3]),
                Vector(c.GetProperty("grow")));
            var result = Effects.Applied(start, change);
            AssertClose(
                fixture,
                $"applied({string.Join(", ", to)}) by {change}",
                Numbers(c.GetProperty("expect")),
                [.. Enumerable.Range(0, 16).Select(i => result[i])]);
        }
    }

    [Fact]
    public void HelpersMatchTheDartTwin()
    {
        var fixture = Fixture.Load("orblit_effect", "effect");

        foreach (var c in Cases(fixture, "shortestTurn"))
        {
            var from = Read(c, "from");
            var to = Read(c, "to");
            AssertClose(fixture, $"shortestTurn({from:R}, {to:R})", c.GetProperty("expect"), Effects.ShortestTurn(from, to));
        }

        foreach (var c in Cases(fixture, "lerp"))
        {
            var from = Read(c, "from");
            var to = Read(c, "to");
            var t = Read(c, "t");
            AssertClose(fixture, $"lerp({from:R}, {to:R}, {t:R})", c.GetProperty("expect"), Effects.Lerp(from, to, t));
        }
    }
}
