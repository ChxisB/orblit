using System.Text.Json;
using Orblit.Parity;
using Orblit.VectorMath;

namespace Orblit.Camera.Tests;

public class CameraParityTests
{
    private const int CaseTextLimit = 600;

    private static List<JsonElement> Cases(Fixture fixture, string op)
    {
        var cases = fixture.Cases.Where(c => c.GetProperty("op").GetString() == op).ToList();
        Assert.True(cases.Count > 0, $"the fixture has no {op} cases");
        return cases;
    }

    /// <summary>
    /// Runs every case of one op. A case the fixture exporter saw refused has to be refused here
    /// too, and every other case has to give the same tree of values.
    /// </summary>
    private static void Check(string op, Func<JsonElement, object?> run)
    {
        var fixture = Fixture.Load("orblit_camera", "camera");
        foreach (var c in Cases(fixture, op))
        {
            var spec = c.GetProperty("spec");
            var expected = c.GetProperty("result");
            var where = $"{op} {Clip(c.GetRawText())}";
            if (expected.ValueKind == JsonValueKind.Object && expected.TryGetProperty("error", out _))
            {
                var error = Record.Exception(() => run(spec));
                Assert.True(
                    error is ArgumentException,
                    $"{where}: the fixture refused it, the port gave {error?.GetType().Name ?? "an answer"}, from fixture commit {fixture.SourceCommit}");
                continue;
            }

            AssertTree(fixture, where, expected, run(spec));
        }
    }

    private static string Clip(string text) => text.Length <= CaseTextLimit ? text : text[..CaseTextLimit] + "...";

    private static void AssertTree(Fixture fixture, string where, JsonElement expected, object? actual)
    {
        switch (actual)
        {
            case IReadOnlyDictionary<string, object?> map:
                AssertMap(fixture, where, expected, map);
                break;
            case double[] numbers:
                AssertList(fixture, where, expected, [.. numbers.Cast<object?>()]);
                break;
            case IReadOnlyList<object?> list:
                AssertList(fixture, where, expected, list);
                break;
            default:
                Assert.True(
                    Leaf(expected, actual),
                    $"{where}: the fixture {expected.GetRawText()}, the port {actual ?? "null"}, from fixture commit {fixture.SourceCommit}");
                break;
        }
    }

    private static void AssertMap(Fixture fixture, string where, JsonElement expected, IReadOnlyDictionary<string, object?> map)
    {
        Assert.True(
            expected.ValueKind == JsonValueKind.Object,
            $"{where}: the fixture {expected.GetRawText()}, the port gave a record, from fixture commit {fixture.SourceCommit}");
        var names = expected.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToList();
        Assert.True(
            names.SequenceEqual(map.Keys.Order(StringComparer.Ordinal)),
            $"{where}: the fixture has [{string.Join(", ", names)}], the port has [{string.Join(", ", map.Keys)}], from fixture commit {fixture.SourceCommit}");
        foreach (var property in expected.EnumerateObject())
        {
            AssertTree(fixture, $"{where}.{property.Name}", property.Value, map[property.Name]);
        }
    }

    private static void AssertList(Fixture fixture, string where, JsonElement expected, IReadOnlyList<object?> list)
    {
        Assert.True(
            expected.ValueKind == JsonValueKind.Array && expected.GetArrayLength() == list.Count,
            $"{where}: the fixture {Clip(expected.GetRawText())}, the port gave {list.Count} items, from fixture commit {fixture.SourceCommit}");
        var i = 0;
        foreach (var item in expected.EnumerateArray())
        {
            AssertTree(fixture, $"{where}[{i}]", item, list[i]);
            i++;
        }
    }

    private static bool Leaf(JsonElement expected, object? actual) => actual switch
    {
        null => expected.ValueKind == JsonValueKind.Null,
        bool flag => expected.ValueKind is JsonValueKind.True or JsonValueKind.False && expected.GetBoolean() == flag,
        string text => expected.ValueKind == JsonValueKind.String && expected.GetString() == text,
        double number => expected.ValueKind is JsonValueKind.Number or JsonValueKind.String
            && Tolerance.Close(Fixture.Number(expected), number),
        long integer => expected.ValueKind == JsonValueKind.Number && expected.GetInt64() == integer,
        _ => false,
    };

    private static Dictionary<string, object?> Tree(params (string Name, object? Value)[] pairs) =>
        pairs.ToDictionary(p => p.Name, p => p.Value);

    private static double N(JsonElement spec, string name) => Fixture.Number(spec.GetProperty(name));

    private static bool Flag(JsonElement spec, string name) =>
        spec.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static bool IsNull(JsonElement spec, string name) => spec.GetProperty(name).ValueKind == JsonValueKind.Null;

    private static double[] Numbers(JsonElement array) => [.. array.EnumerateArray().Select(Fixture.Number)];

    private static Vector3 Vec(JsonElement array)
    {
        var v = Numbers(array);
        return new Vector3(v[0], v[1], v[2]);
    }

    private static Vector3? VecOrNull(JsonElement spec, string name) => IsNull(spec, name) ? null : Vec(spec.GetProperty(name));

    private static Quaternion Quat(JsonElement array)
    {
        var v = Numbers(array);
        return new Quaternion(v[0], v[1], v[2], v[3]);
    }

    private static double[] Write(Vector3 v) => [v.X, v.Y, v.Z];

    private static double[] Write(Quaternion q) => [q.X, q.Y, q.Z, q.W];

    /// <summary>A name as the fixture spells it, <c>easeInOut</c>, read as the C# member.</summary>
    private static TEnum Word<TEnum>(JsonElement name)
        where TEnum : struct, Enum => Enum.Parse<TEnum>(name.GetString()!, ignoreCase: true);

    private static string Spell(Enum member)
    {
        var name = member.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static Lens ReadLens(JsonElement s) => new(
        FieldOfView: N(s, "fov"),
        Near: N(s, "near"),
        Far: N(s, "far"),
        Dutch: N(s, "dutch"),
        Orthographic: s.GetProperty("ortho").GetBoolean(),
        Height: N(s, "height"));

    private static Dictionary<string, object?> Write(Lens l) => Tree(
        ("fov", l.FieldOfView),
        ("near", l.Near),
        ("far", l.Far),
        ("dutch", l.Dutch),
        ("ortho", l.Orthographic),
        ("height", l.Height));

    private static Blend ReadBlend(JsonElement s) => new(Word<BlendStyle>(s.GetProperty("style")), N(s, "duration"));

    private static Dictionary<string, object?> Write(Blend b) => Tree(("style", Spell(b.Style)), ("duration", b.Duration));

    private static double[] Write(ScreenRect r) => [r.Left, r.Top, r.Width, r.Height];

    private static Dictionary<string, object?>? Write(CameraGuides? g) => g is null
        ? null
        : Tree(("screenX", g.ScreenX), ("screenY", g.ScreenY), ("dead", Write(g.Dead)), ("soft", Write(g.Soft)));

    private static Dictionary<string, object?>? Write(FixedTarget? t) =>
        t is null ? null : Tree(("position", Write(t.Position)), ("rotation", Write(t.Rotation)));

    private static FixedTarget? ReadTarget(JsonElement value) =>
        value.ValueKind == JsonValueKind.Null
            ? null
            : new FixedTarget(Vec(value.GetProperty("position")), Quat(value.GetProperty("rotation")));

    // the pure functions

    [Fact]
    public void DampingMatchesTheFixture() => Check("damping", spec =>
    {
        var (current, target, damping, delta) = (N(spec, "current"), N(spec, "target"), N(spec, "damping"), N(spec, "delta"));
        return Tree(
            ("factor", Smoothing.Factor(damping, delta)),
            ("damped", Smoothing.Damp(current, target, damping, delta)));
    });

    [Fact]
    public void LensBlendsAndTextMatchTheFixture() => Check("lens", spec =>
    {
        var a = ReadLens(spec.GetProperty("a"));
        var b = ReadLens(spec.GetProperty("b"));
        return Tree(("lerp", Write(Lens.Lerp(a, b, N(spec, "t")))), ("a", a.ToString()), ("b", b.ToString()));
    });

    [Fact]
    public void FlatLensesMatchTheFixture() => Check("flat", spec =>
    {
        var height = N(spec, "height");
        var lens = IsNull(spec, "near")
            ? Lens.Flat(height)
            : Lens.Flat(
                height,
                near: N(spec, "near"),
                far: IsNull(spec, "far") ? 100 : N(spec, "far"),
                dutch: IsNull(spec, "dutch") ? 0 : N(spec, "dutch"));
        return Tree(("lens", Write(lens)), ("text", lens.ToString()));
    });

    [Fact]
    public void TheDefaultLensMatchesTheFixture() => Check("defaultLens", _ =>
    {
        var lens = new Lens();
        return Tree(("lens", Write(lens)), ("text", lens.ToString()));
    });

    [Fact]
    public void ScreenRectsMatchTheFixture() => Check("rect", spec =>
    {
        var rect = new ScreenRect(N(spec, "left"), N(spec, "top"), N(spec, "width"), N(spec, "height"));
        return Tree(
            ("right", rect.Right),
            ("bottom", rect.Bottom),
            ("contains", rect.Contains(N(spec, "x"), N(spec, "y"))),
            ("text", rect.ToString()));
    });

    [Fact]
    public void BlendEasingMatchesTheFixture() => Check("blend", spec =>
    {
        var blend = Flag(spec, "ctor") ? Blend.Cut() : ReadBlend(spec);
        return Tree(("blend", Write(blend)), ("ease", blend.Ease(N(spec, "t"))));
    });

    private static BlendTable ReadTable(JsonElement spec)
    {
        var table = IsNull(spec, "default") ? new BlendTable() : new BlendTable(ReadBlend(spec.GetProperty("default")));
        foreach (var rule in spec.GetProperty("rules").EnumerateArray())
        {
            table.Set(Name(rule, "from"), Name(rule, "to"), ReadBlend(rule.GetProperty("blend")));
        }

        return table;
    }

    private static string? Name(JsonElement spec, string name) => IsNull(spec, name) ? null : spec.GetProperty(name).GetString();

    [Fact]
    public void BlendTablesMatchTheFixture() => Check("table", spec =>
    {
        var table = ReadTable(spec);
        return Tree(
            ("default", Write(table.DefaultBlend)),
            ("found", spec.GetProperty("queries").EnumerateArray().Select(q => (object?)Write(table.Between(Name(q, "from"), Name(q, "to")))).ToList()));
    });

    [Fact]
    public void RotatingAVectorMatchesTheFixture() =>
        Check("rotate", spec => Write(Rotations.RotateVector(Quat(spec.GetProperty("q")), Vec(spec.GetProperty("v")))));

    [Fact]
    public void ShortestSlerpMatchesTheFixture() => Check(
        "slerp",
        spec => Write(Rotations.SlerpShortest(Quat(spec.GetProperty("a")), Quat(spec.GetProperty("b")), N(spec, "t"))));

    [Fact]
    public void LookRotationMatchesTheFixture() =>
        Check("look", spec => Write(Rotations.LookRotation(Vec(spec.GetProperty("forward")), VecOrNull(spec, "up"))));

    [Fact]
    public void ProjectionMatchesTheFixture() => Check("project", spec =>
    {
        var seen = Framing.Project(
            Vec(spec.GetProperty("position")),
            Quat(spec.GetProperty("rotation")),
            Vec(spec.GetProperty("point")),
            ReadLens(spec.GetProperty("lens")),
            N(spec, "aspect"));
        return Tree(("x", seen.X), ("y", seen.Y), ("inFront", seen.InFront));
    });

    [Fact]
    public void RotationPlacingMatchesTheFixture() => Check("placing", spec => Write(Framing.RotationPlacing(
        Vec(spec.GetProperty("position")),
        Vec(spec.GetProperty("point")),
        N(spec, "ndcX"),
        N(spec, "ndcY"),
        ReadLens(spec.GetProperty("lens")),
        N(spec, "aspect"),
        VecOrNull(spec, "up"))));

    // aims

    private static ICameraAim ReadAim(JsonElement s) => s.GetProperty("kind").GetString() switch
    {
        "static" => new StaticAim(Quat(s.GetProperty("rotation"))),
        "hard" => new HardLookAt(VecOrNull(s, "up")),
        "composer" => ReadComposer(s),
        "head" => ReadHead(s),
        _ => ReadPov(s),
    };

    private static ComposerAim ReadComposer(JsonElement s) => Flag(s, "defaults")
        ? new ComposerAim()
        : new ComposerAim(
            screenX: N(s, "screenX"),
            screenY: N(s, "screenY"),
            deadZoneWidth: N(s, "deadZoneWidth"),
            deadZoneHeight: N(s, "deadZoneHeight"),
            softZoneWidth: N(s, "softZoneWidth"),
            softZoneHeight: N(s, "softZoneHeight"),
            damping: N(s, "damping"),
            up: VecOrNull(s, "up"));

    private static HeadAim ReadHead(JsonElement s) =>
        Flag(s, "defaults") ? new HeadAim() : new HeadAim(damping: N(s, "damping"), tilt: N(s, "tilt"));

    private static PovAim ReadPov(JsonElement s) => Flag(s, "defaults")
        ? new PovAim()
        : new PovAim(
            yaw: N(s, "yaw"),
            pitch: N(s, "pitch"),
            minimumPitch: N(s, "minimumPitch"),
            maximumPitch: N(s, "maximumPitch"),
            damping: N(s, "damping"));

    [Fact]
    public void AimsMatchTheFixture() => Check("aim", spec =>
    {
        var aim = ReadAim(spec.GetProperty("aim"));
        var lookAt = ReadTarget(spec.GetProperty("lookAt"));
        var current = Quat(spec.GetProperty("current"));
        var position = Vec(spec.GetProperty("position"));
        var lens = ReadLens(spec.GetProperty("lens"));
        var rotations = new List<object?>();
        double[]? currentAfter = null;
        var turn = current;
        for (var i = 0; i < spec.GetProperty("frames").GetInt32(); i++)
        {
            turn = aim.Solve(turn, position, lookAt, lens, N(spec, "aspect"), N(spec, "delta"));
            currentAfter ??= Write(current);
            rotations.Add(Write(turn));
        }

        return Tree(
            ("rotations", rotations),
            ("guides", Write(aim.Guides)),
            ("currentAfter", currentAfter),
            ("lookAtAfter", Write(lookAt)));
    });

    // bodies

    private static ArmProbe ReadProbe(JsonElement s, List<object?> log) => (from, to, radius) =>
    {
        log.Add(new object?[] { Write(from), Write(to), radius });
        return s.GetProperty("kind").GetString() switch
        {
            "wall" => ArmTests.WallAt(N(s, "z"), N(s, "minX"), N(s, "maxX"))(from, to, radius),
            "fixed" => IsNull(s, "distance") ? null : N(s, "distance"),
            _ => null,
        };
    };

    private static ICameraBody ReadBody(JsonElement s, List<object?> log) => s.GetProperty("kind").GetString() switch
    {
        "static" => new StaticBody(Vec(s.GetProperty("position"))),
        "follow" => new FollowBody(
            Vec(s.GetProperty("offset")),
            Word<FollowBinding>(s.GetProperty("binding")),
            VecOrNull(s, "damping")),
        "orbit" => ReadOrbit(s),
        "framing" => ReadFraming(s),
        "screen" => ReadScreen(s),
        _ => ReadArm(s, log),
    };

    private static OrbitBody ReadOrbit(JsonElement s) => Flag(s, "defaults")
        ? new OrbitBody()
        : new OrbitBody(
            radius: N(s, "radius"),
            azimuth: N(s, "azimuth"),
            elevation: N(s, "elevation"),
            damping: N(s, "damping"),
            height: N(s, "height"));

    private static FramingBody ReadFraming(JsonElement s) => Flag(s, "defaults")
        ? new FramingBody(Vec(s.GetProperty("viewDirection")))
        : new FramingBody(
            Vec(s.GetProperty("viewDirection")),
            distance: N(s, "distance"),
            minimum: N(s, "minimum"),
            maximum: N(s, "maximum"),
            damping: N(s, "damping"));

    private static ScreenFollowBody ReadScreen(JsonElement s)
    {
        if (Flag(s, "defaults"))
        {
            return new ScreenFollowBody();
        }

        var box = s.GetProperty("bounds");
        (Vector3, Vector3)? bounds = box.ValueKind == JsonValueKind.Null
            ? null
            : (Vec(box.GetProperty("minimum")), Vec(box.GetProperty("maximum")));
        return new ScreenFollowBody(
            distance: N(s, "distance"),
            screenX: N(s, "screenX"),
            screenY: N(s, "screenY"),
            deadZoneWidth: N(s, "deadZoneWidth"),
            deadZoneHeight: N(s, "deadZoneHeight"),
            softZoneWidth: N(s, "softZoneWidth"),
            softZoneHeight: N(s, "softZoneHeight"),
            damping: N(s, "damping"),
            bounds: bounds);
    }

    private static ArmBody ReadArm(JsonElement s, List<object?> log)
    {
        var inner = ReadBody(s.GetProperty("inner"), log);
        var probe = ReadProbe(s.GetProperty("probe"), log);
        return Flag(s, "defaults")
            ? new ArmBody(inner, probe)
            : new ArmBody(inner, probe, radius: N(s, "radius"), height: N(s, "height"));
    }

    [Fact]
    public void BodiesMatchTheFixture() => Check("body", spec =>
    {
        var log = new List<object?>();
        var body = ReadBody(spec.GetProperty("body"), log);
        var follow = ReadTarget(spec.GetProperty("follow"));
        var rotation = Quat(spec.GetProperty("rotation"));
        var lens = ReadLens(spec.GetProperty("lens"));
        var current = Vec(spec.GetProperty("current"));
        var positions = new List<object?>();
        double[]? currentAfter = null;
        var place = current;
        for (var i = 0; i < spec.GetProperty("frames").GetInt32(); i++)
        {
            place = body.Solve(place, follow, rotation, lens, N(spec, "aspect"), N(spec, "delta"));
            currentAfter ??= Write(current);
            positions.Add(Write(place));
        }

        return Tree(
            ("positions", positions),
            ("guides", Write(body.Guides)),
            ("probes", log),
            ("currentAfter", currentAfter),
            ("followAfter", Write(follow)));
    });

    // noise

    private static CameraNoise ReadNoise(JsonElement s) => new(
        positionAmplitude: VecOrNull(s, "positionAmplitude"),
        positionFrequency: VecOrNull(s, "positionFrequency"),
        rotationAmplitude: VecOrNull(s, "rotationAmplitude"),
        rotationFrequency: VecOrNull(s, "rotationFrequency"),
        seed: s.GetProperty("seed").GetInt64());

    [Fact]
    public void NoiseMatchesTheFixture() => Check("noise", spec =>
    {
        var noise = ReadNoise(spec.GetProperty("noise"));
        return Tree(
            (
                "fields",
                Tree(
                    ("positionAmplitude", Write(noise.PositionAmplitude)),
                    ("positionFrequency", Write(noise.PositionFrequency)),
                    ("rotationAmplitude", Write(noise.RotationAmplitude)),
                    ("rotationFrequency", Write(noise.RotationFrequency)),
                    ("seed", noise.Seed))),
            (
                "samples",
                Numbers(spec.GetProperty("times"))
                    .Select(time => (object?)Tree(
                        ("position", Write(noise.PositionAt(time))),
                        ("rotation", Write(noise.RotationAt(time)))))
                    .ToList()));
    });

    // the brain

    private static VirtualCamera ReadCamera(JsonElement s, FixedTarget[] targets)
    {
        FixedTarget? Pick(string name) => IsNull(s, name) ? null : targets[s.GetProperty(name).GetInt32()];

        return new VirtualCamera(
            s.GetProperty("name").GetString()!,
            priority: s.GetProperty("priority").GetInt64(),
            enabled: s.GetProperty("enabled").GetBoolean(),
            lens: IsNull(s, "lens") ? null : ReadLens(s.GetProperty("lens")),
            follow: Pick("follow"),
            lookAt: Pick("lookAt"),
            body: IsNull(s, "body") ? null : ReadBody(s.GetProperty("body"), []),
            aim: IsNull(s, "aim") ? null : ReadAim(s.GetProperty("aim")),
            noise: IsNull(s, "noise") ? null : ReadNoise(s.GetProperty("noise")),
            position: VecOrNull(s, "position"),
            rotation: IsNull(s, "rotation") ? null : Quat(s.GetProperty("rotation")));
    }

    private static void Apply(CameraBrain brain, JsonElement step, VirtualCamera[] cameras, FixedTarget[] targets)
    {
        VirtualCamera Camera() => cameras[step.GetProperty("camera").GetInt32()];

        switch (step.GetProperty("do").GetString())
        {
            case "snap":
                brain.Snap();
                break;
            case "priority":
                Camera().Priority = step.GetProperty("value").GetInt64();
                break;
            case "enable":
                Camera().Enabled = step.GetProperty("value").GetBoolean();
                break;
            case "remove":
                brain.Remove(Camera());
                break;
            case "target":
                targets[step.GetProperty("index").GetInt32()].Position = Vec(step.GetProperty("position"));
                break;
            case "aspect":
                brain.Aspect = N(step, "value");
                break;
            default:
                brain.Update(N(step, "delta"));
                break;
        }
    }

    private static Dictionary<string, object?> Snapshot(CameraBrain brain) => Tree(
        ("live", brain.Live?.Name),
        ("progress", brain.BlendProgress),
        ("blending", brain.IsBlending),
        ("aspect", brain.Aspect),
        ("position", Write(brain.State.Position)),
        ("rotation", Write(brain.State.Rotation)),
        ("lens", Write(brain.State.Lens)),
        (
            "cameras",
            brain.Cameras
                .Select(camera => (object?)Tree(
                    ("name", camera.Name),
                    ("position", Write(camera.State.Position)),
                    ("rotation", Write(camera.State.Rotation)),
                    ("guides", Write(camera.Guides))))
                .ToList()));

    [Fact]
    public void TheBrainMatchesTheFixture() => Check("brain", spec =>
    {
        FixedTarget[] targets = [.. spec.GetProperty("targets").EnumerateArray().Select(t => ReadTarget(t)!)];
        VirtualCamera[] cameras = [.. spec.GetProperty("cameras").EnumerateArray().Select(c => ReadCamera(c, targets))];
        var table = ReadTable(spec);
        var brain = IsNull(spec, "aspect") ? new CameraBrain(blends: table) : new CameraBrain(N(spec, "aspect"), table);
        foreach (var camera in cameras.Take(spec.GetProperty("initial").GetInt32()))
        {
            brain.Add(camera);
        }

        var start = Snapshot(brain);
        var steps = new List<object?>();
        foreach (var step in spec.GetProperty("steps").EnumerateArray())
        {
            Apply(brain, step, cameras, targets);
            steps.Add(Snapshot(brain));
        }

        return Tree(("start", start), ("steps", steps));
    });
}
