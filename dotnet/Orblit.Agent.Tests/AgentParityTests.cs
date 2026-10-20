using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Orblit.Parity;
using Orblit.VectorMath;

namespace Orblit.Agent.Tests;

public class AgentParityTests
{
    private static List<JsonElement> Cases(Fixture fixture, string op)
    {
        var cases = fixture.Cases.Where(c => c.GetProperty("op").GetString() == op).ToList();
        Assert.True(cases.Count > 0, $"the fixture has no {op} cases");
        return cases;
    }

    private static double Read(JsonElement spec, string name) => Fixture.Number(spec.GetProperty(name));

    private static double[] Numbers(JsonElement array) => [.. array.EnumerateArray().Select(Fixture.Number)];

    private static Vector3 Vec(JsonElement array)
    {
        var v = Numbers(array);
        return new Vector3(v[0], v[1], v[2]);
    }

    private static long? OptionalLong(JsonElement spec, string name) =>
        spec.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt64()
            : null;

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

    private static void AssertSame<T>(Fixture fixture, string what, T expected, T actual) =>
        Assert.True(
            EqualityComparer<T>.Default.Equals(expected, actual),
            $"{what}: Dart {expected}, C# {actual}, from Dart commit {fixture.DartCommit}");

    private static double[] Triple(Vector3 v) => [v.X, v.Y, v.Z];

    private static Steerable SteerableFrom(JsonElement s) =>
        new(
            position: Vec(s.GetProperty("p")),
            velocity: Vec(s.GetProperty("v")),
            maxSpeed: Read(s, "maxSpeed"),
            maxForce: Read(s, "maxForce"),
            radius: Read(s, "radius"),
            priority: s.GetProperty("priority").GetInt64());

    private static List<Steerable> NeighboursFrom(JsonElement list, Steerable self) =>
    [
        .. list.EnumerateArray().Select(s => s.TryGetProperty("self", out _) ? self : SteerableFrom(s)),
    ];

    [SuppressMessage("Maintainability", "CA1502:Avoid excessive complexity", Justification = "F2: a list of cases is data.")]
    private static Steering Build(JsonElement b, Steerable self)
    {
        JsonElement Get(string name) => b.GetProperty(name);
        return b.GetProperty("t").GetString() switch
        {
            "seek" => new Seek(Vec(Get("target"))),
            "flee" => new Flee(Vec(Get("from")), within: Read(b, "within")),
            "arrive" => new Arrive(Vec(Get("target")), slowingRadius: Read(b, "slowingRadius")),
            "pursue" => new Pursue(SteerableFrom(Get("quarry")), lookAhead: Read(b, "lookAhead")),
            "evade" => new Evade(
                SteerableFrom(Get("hunter")),
                lookAhead: Read(b, "lookAhead"),
                within: Read(b, "within")),
            "wander" => new Wander(
                seed: Get("seed").GetInt64(),
                at: Read(b, "at"),
                strength: Read(b, "strength"),
                rate: Read(b, "rate")),
            "separate" => new Separate(NeighboursFrom(Get("neighbours"), self), radius: Read(b, "radius")),
            "align" => new Align(NeighboursFrom(Get("neighbours"), self), radius: Read(b, "radius")),
            "cohere" => new Cohere(NeighboursFrom(Get("neighbours"), self), radius: Read(b, "radius")),
            "avoid" => new Avoid(
                Vec(Get("preferred")),
                NeighboursFrom(Get("neighbours"), self),
                horizon: Read(b, "horizon"),
                turns: Get("turns").GetInt64(),
                weight: Read(b, "weight")),
            "path" => new FollowPath(
                [.. Get("points").EnumerateArray().Select(Vec)],
                index: Get("index").GetInt64(),
                reached: Read(b, "reached"),
                loop: Get("loop").GetBoolean()),
            "blend" => new Blend(
            [
                .. Get("parts").EnumerateArray().Select(p => (Build(p.GetProperty("b"), self), Read(p, "w"))),
            ]),
            var other => throw new InvalidOperationException($"unknown behaviour {other}"),
        };
    }

    [Fact]
    public void EveryForceAgreesWithDart()
    {
        var fixture = Fixture.Load("orblit_agent", "agent");
        foreach (var spec in Cases(fixture, "force"))
        {
            var self = SteerableFrom(spec.GetProperty("self"));
            var behaviour = Build(spec.GetProperty("b"), self);
            var kind = spec.GetProperty("b").GetProperty("t").GetString();
            if (spec.TryGetProperty("throws", out _))
            {
                Assert.ThrowsAny<ArgumentException>(() => behaviour.Force(self));
                continue;
            }

            AssertClose(fixture, $"{kind} force", Numbers(spec.GetProperty("force")), Triple(behaviour.Force(self)));
            if (behaviour is FollowPath path)
            {
                AssertSame(fixture, "path next", spec.GetProperty("next").GetInt64(), path.NextIndex(self));
                AssertSame(fixture, "path done", spec.GetProperty("done").GetBoolean(), path.IsDone(self));
            }
        }
    }

    [Fact]
    public void WhatAvoidRefusesIsWhatDartRefuses()
    {
        var fixture = Fixture.Load("orblit_agent", "agent");
        foreach (var spec in Cases(fixture, "avoidNew"))
        {
            var ok = true;
            try
            {
                _ = new Avoid(
                    Vector3.Zero(),
                    [],
                    horizon: Read(spec, "horizon"),
                    turns: spec.GetProperty("turns").GetInt64(),
                    weight: Read(spec, "weight"));
            }
            catch (ArgumentException)
            {
                ok = false;
            }

            AssertSame(
                fixture,
                $"avoid horizon {Read(spec, "horizon")} turns {spec.GetProperty("turns")} weight {Read(spec, "weight")} accepted",
                spec.GetProperty("ok").GetBoolean(),
                ok);
        }
    }

    [Fact]
    public void IntegratingMovesAnAgentAsDartDoes()
    {
        var fixture = Fixture.Load("orblit_agent", "agent");
        foreach (var spec in Cases(fixture, "integrate"))
        {
            var self = SteerableFrom(spec.GetProperty("self"));
            foreach (var step in spec.GetProperty("steps").EnumerateArray())
            {
                self.Integrate(Vec(step.GetProperty("force")), Read(step, "seconds"));
                AssertClose(fixture, "position", Numbers(step.GetProperty("p")), Triple(self.Position));
                AssertClose(fixture, "velocity", Numbers(step.GetProperty("v")), Triple(self.Velocity));
                AssertClose(fixture, "speed", [Read(step, "speed")], [self.Speed]);
            }
        }
    }

    [Fact]
    public void HeadingIsNullWhereDartsIs()
    {
        var fixture = Fixture.Load("orblit_agent", "agent");
        foreach (var spec in Cases(fixture, "heading"))
        {
            var heading = new Steerable(velocity: Vec(spec.GetProperty("v"))).Heading;
            var expected = spec.GetProperty("heading");
            if (expected.ValueKind == JsonValueKind.Null)
            {
                Assert.Null(heading);
            }
            else
            {
                Assert.NotNull(heading);
                AssertClose(fixture, "heading", Numbers(expected), Triple(heading));
            }
        }
    }

    /// <summary>Builds a tree from a spec, and records what each leaf is asked.</summary>
    private sealed class TreeBuild(List<int> calls)
    {
        private readonly Dictionary<int, int> _counts = [];

        /// <summary>Every label, children before their parent.</summary>
        public List<string> Labels { get; } = [];

        private JsonElement Next(int id, JsonElement script)
        {
            var n = _counts.GetValueOrDefault(id);
            _counts[id] = n + 1;
            return script[n % script.GetArrayLength()];
        }

        public Node Build(JsonElement s)
        {
            var node = Make(s);
            Labels.Add(node.Label);
            return node;
        }

        [SuppressMessage("Maintainability", "CA1502:Avoid excessive complexity", Justification = "F2: a list of cases is data.")]
        private Node Make(JsonElement s)
        {
            var id = s.TryGetProperty("id", out var found) ? found.GetInt32() : 0;
            List<Node> Children() => [.. s.GetProperty("c").EnumerateArray().Select(Build)];
            Node Child() => Build(s.GetProperty("child"));
            double Seconds() => Read(s, "seconds");
            return s.GetProperty("t").GetString() switch
            {
                "seq" => new Sequence(Children()),
                "sel" => new Selector(Children()),
                "par" => new Parallel(
                    Children(),
                    succeedAfter: OptionalLong(s, "succeedAfter"),
                    failAfter: s.GetProperty("failAfter").GetInt64()),
                "invert" => new Invert(Child()),
                "succeed" => new Succeed(Child()),
                "repeat" => new Repeat(Child(), times: OptionalLong(s, "times")),
                "wait" => new Wait(Seconds()),
                "cooldown" => new Cooldown(Child(), Seconds()),
                "deadline" => new Deadline(Child(), Seconds()),
                "do" => new Do($"do{id}", tick =>
                {
                    calls.Add(id);
                    return (Status)Next(id, s.GetProperty("script")).GetInt32();
                }),
                "check" => new Check($"check{id}", tick =>
                {
                    calls.Add(id);
                    return Next(id, s.GetProperty("script")).GetBoolean();
                }),
                "act" => new Act($"act{id}", tick =>
                {
                    calls.Add(id);
                    tick.Write("acts", tick.Read<long>("acts") + 1);
                }),
                var other => throw new InvalidOperationException($"unknown node {other}"),
            };
        }
    }

    [Fact]
    public void EveryTreeRunsAsDartsDoes()
    {
        var fixture = Fixture.Load("orblit_agent", "agent");
        var index = 0;
        foreach (var spec in Cases(fixture, "tree"))
        {
            index++;
            var calls = new List<int>();
            var tree = new TreeBuild(calls);
            var brain = new Brain(tree.Build(spec.GetProperty("spec")));
            Assert.Equal(spec.GetProperty("labels").EnumerateArray().Select(l => l.GetString()), tree.Labels);

            var blackboard = new Dictionary<string, object?>();
            var tick = 0;
            foreach (var expected in spec.GetProperty("ticks").EnumerateArray())
            {
                tick++;
                var where = $"tree {index} tick {tick}";
                calls.Clear();
                if (expected.GetProperty("interrupt").GetBoolean())
                {
                    brain.Interrupt();
                }

                var status = brain.Tick(Read(expected, "seconds"), blackboard);

                AssertSame(fixture, $"{where} status", expected.GetProperty("status").GetInt32(), (int)status);
                Assert.True(
                    expected.GetProperty("calls").EnumerateArray().Select(c => c.GetInt32()).SequenceEqual(calls),
                    $"{where} calls: Dart {expected.GetProperty("calls")}, C# [{string.Join(",", calls)}], from Dart commit {fixture.DartCommit}");
                AssertSame(fixture, $"{where} memory", expected.GetProperty("memory").GetInt32(), brain.Memory.Length);
                AssertSame(fixture, $"{where} acts", OptionalLong(expected, "acts"), blackboard.TryGetValue("acts", out var acts) ? (long?)acts : null);
            }
        }
    }

    [Fact]
    public void LabelsWriteTheirSecondsAsDartDoes()
    {
        var fixture = Fixture.Load("orblit_agent", "agent");
        foreach (var spec in Cases(fixture, "label"))
        {
            var seconds = Read(spec, "seconds");
            var wait = new Wait(1);
            AssertSame(fixture, $"wait {seconds:R}", spec.GetProperty("wait").GetString(), new Wait(seconds).Label);
            AssertSame(fixture, $"cooldown {seconds:R}", spec.GetProperty("cooldown").GetString(), new Cooldown(wait, seconds).Label);
            AssertSame(fixture, $"deadline {seconds:R}", spec.GetProperty("deadline").GetString(), new Deadline(wait, seconds).Label);
        }
    }

    [Fact]
    public void ACrowdCrossingEndsWhereDartsDoes()
    {
        var fixture = Fixture.Load("orblit_agent", "agent");
        var spec = Cases(fixture, "plaza").Single();

        var end = AvoidTests.Cross(AvoidTests.Plaza(), seconds: Read(spec, "seconds")).End;

        var expected = spec.GetProperty("end").EnumerateArray().ToList();
        Assert.Equal(expected.Count, end.Count);
        for (var i = 0; i < end.Count; i++)
        {
            AssertClose(fixture, $"walker {i}", Numbers(expected[i]), Triple(end[i]));
        }
    }
}
