using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Parity;
using Orblit.VectorMath;

namespace Orblit.Particles.Tests;

public class ParticleParityTests
{
    private static readonly Fixture Dart = Fixture.Load("orblit_particles", "particles");

    [Fact]
    public void UnitsMatchTheDartTwin() =>
        Dart.Check("unit", c => Dump.Attempt(() => Dump.Num(UnitRandom.At(Long(c, 0), Long(c, 1), Long(c, 2)))));

    [Fact]
    public void RampsMatchTheDartTwin() =>
        Dart.Check("ramp", c => Dump.Attempt(() => Dump.Num(Ramp.At(Numbers(In(c, 0)), Int(c, 1), Int(c, 2), Num(c, 3)))));

    [Fact]
    public void ShapesMatchTheDartTwin() => Dart.Check("shape", c => Dump.Attempt(() => Pick(c)));

    [Fact]
    public void EmittersMatchTheDartTwinOnWhatTheyRefuse() =>
        Dart.Check("make", c => Dump.Attempt(() => JsonValue.Create(EmitterOf(In(c, 0)) is not null)));

    [Fact]
    public void FramesMatchTheDartTwin() => Dart.Check("sample", c => Dump.Attempt(() => Sample(c)));

    [Fact]
    public void BirthsMatchTheDartTwin() => Dart.Check("birth", c => Dump.Attempt(() => Birth(c)));

    [Fact]
    public void BurstIdsMatchTheDartTwin() =>
        Dart.Check("burstId", c => Dump.Attempt(() => Dump.Big(Emitter.BurstId(Long(c, 0), Long(c, 1)))));

    [Fact]
    public void LooksMatchTheDartTwin() => Dart.Check("look", c => Dump.Attempt(() => Look(c)));

    [Fact]
    public void SimulationsMatchTheDartTwin() => Dart.Check("sim", c => Dump.Attempt(() => Simulate(c)));

    [Fact]
    public void TrailsMatchTheDartTwin() => Dart.Check("trail", c => Dump.Attempt(() => MadeTrail(c)));

    [Fact]
    public void StripsMatchTheDartTwin() => Dart.Check("strip", c => Dump.Attempt(() => MadeStrip(c)));

    [Fact]
    public void TrailsAlongAKnownPathMatchTheDartTwin() => Dart.Check("along", c => Dump.Attempt(() => Along(c)));

    [Fact]
    public void TrailsThatRememberMatchTheDartTwin() => Dart.Check("ring", c => Dump.Attempt(() => Remember(c)));

    private static JsonElement In(JsonElement c, int index) => c.GetProperty("in")[index];

    private static double Num(JsonElement c, int index) => Fixture.Number(In(c, index));

    private static int Int(JsonElement c, int index) => In(c, index).GetInt32();

    private static long Long(JsonElement c, int index) =>
        In(c, index) is { ValueKind: JsonValueKind.String } text ? long.Parse(text.GetString()!) : In(c, index).GetInt64();

    private static double[] Numbers(JsonElement list) => [.. list.EnumerateArray().Select(Fixture.Number)];

    private static Vector3 Vector(JsonElement list) => new(Fixture.Number(list[0]), Fixture.Number(list[1]), Fixture.Number(list[2]));

    private static Range RangeOf(JsonElement pair) => new(Fixture.Number(pair[0]), Fixture.Number(pair[1]));

    private static JsonArray Triple(Vector3 v) => new(Dump.Num(v.X), Dump.Num(v.Y), Dump.Num(v.Z));

    private static JsonArray Floats(IEnumerable<float> values) => Dump.Arr(values, f => Dump.Num((double)f));

    private static Shape ShapeOf(JsonElement spec) => spec[0].GetString() switch
    {
        "point" => new PointShape(),
        "sphere" => new SphereShape(Fixture.Number(spec[1])),
        "cone" => new ConeShape(Fixture.Number(spec[1]), Fixture.Number(spec[2])),
        _ => new BoxShape(new Vector3(Fixture.Number(spec[1]), Fixture.Number(spec[2]), Fixture.Number(spec[3]))),
    };

    private static T? Val<T>(JsonElement spec, string key, Func<JsonElement, T> read)
        where T : struct => spec.TryGetProperty(key, out var value) ? read(value) : null;

    private static T? Ref<T>(JsonElement spec, string key, Func<JsonElement, T> read)
        where T : class => spec.TryGetProperty(key, out var value) ? read(value) : null;

    private static Burst BurstOf(JsonElement pair) => new(Fixture.Number(pair[0]), pair[1].GetInt32());

    // Keys the case leaves out keep what a new Emitter has, so the defaults
    // are compared with Dart's too.
    private static Emitter EmitterOf(JsonElement spec) => new Emitter().CopyWith(
        seed: Val(spec, "seed", e => e.GetInt64()),
        shape: Ref(spec, "shape", ShapeOf),
        rate: Val(spec, "rate", Fixture.Number),
        bursts: Ref(spec, "bursts", e => e.EnumerateArray().Select(BurstOf).ToList()),
        duration: Val(spec, "duration", Fixture.Number),
        lifetime: Ref(spec, "lifetime", RangeOf),
        speed: Ref(spec, "speed", RangeOf),
        size: Ref(spec, "size", Numbers),
        colour: Ref(spec, "colour", Numbers),
        gravity: Ref(spec, "gravity", Vector),
        drag: Val(spec, "drag", Fixture.Number),
        noiseStrength: Val(spec, "noiseStrength", Fixture.Number),
        noiseScale: Val(spec, "noiseScale", Fixture.Number),
        spin: Ref(spec, "spin", RangeOf),
        space: Val(spec, "space", e => e.GetString() == "local" ? ParticleSpace.Local : ParticleSpace.World),
        maxParticles: Val(spec, "max", e => e.GetInt32()));

    private static Placement? PlacementOf(JsonElement spec)
    {
        if (spec.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var speed = Fixture.Number(spec[1]);
        var roll = spec.GetArrayLength() > 2 ? Fixture.Number(spec[2]) : 0.0;
        var turned = spec[0].GetString() != "slide";
        return t =>
        {
            var matrix = Matrix4.TranslationValues(t * speed, 1, 2);
            if (turned)
            {
                matrix.Multiply(Matrix4.RotationZ(roll));
            }

            return matrix;
        };
    }

    private static HitTest? HitOf(JsonElement spec)
    {
        if (spec.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var at = Fixture.Number(spec[1]);
        return spec[0].GetString() switch
        {
            "floor" => (_, to) => to.Y < at ? new Hit(new Vector3(to.X, at, to.Z), new Vector3(0, 1, 0)) : null,
            "wall" => (_, to) => to.X > at ? new Hit(new Vector3(at, to.Y, to.Z), new Vector3(-1, 0, 0)) : null,
            _ => (_, to) => Slope(to, at),
        };
    }

    private static Hit? Slope(Vector3 to, double at)
    {
        var normal = new Vector3(0.6, 0.8, 0);
        var depth = to.Dot(normal) - at;
        return depth < 0 ? new Hit(to - (normal * depth), normal) : null;
    }

    private static JsonObject Buffer(ParticleBuffer b) => new()
    {
        ["count"] = b.Count,
        ["positions"] = Floats(b.Positions.Take(b.Count * 3)),
        ["sizes"] = Floats(b.Sizes.Take(b.Count)),
        ["colours"] = Floats(b.Colours.Take(b.Count * 3)),
        ["angles"] = Floats(b.Angles.Take(b.Count)),
    };

    private static JsonObject Strip(TrailStrip s) => new()
    {
        ["count"] = s.Count,
        ["positions"] = Dump.Arr(s.Positions.Take(s.Count * 3), Dump.Num),
        ["widths"] = Floats(s.Widths.Take(s.Count)),
        ["head"] = Triple(s.Head),
    };

    private static JsonObject Pick(JsonElement c)
    {
        var position = Vector3.Zero();
        var direction = Vector3.Zero();
        ShapeOf(In(c, 0)).Pick(Long(c, 1), Long(c, 2), position, direction);
        return new JsonObject { ["position"] = Triple(position), ["direction"] = Triple(direction) };
    }

    private static JsonObject Sample(JsonElement c)
    {
        var output = new ParticleBuffer(Int(c, 3));
        int? limit = In(c, 4).ValueKind == JsonValueKind.Null ? null : Int(c, 4);
        EmitterOf(In(c, 0)).Sample(Num(c, 1), output, PlacementOf(In(c, 2)), limit);
        return Buffer(output);
    }

    private static JsonObject Birth(JsonElement c)
    {
        var emitter = EmitterOf(In(c, 0));
        var id = Long(c, 1);
        var position = Vector3.Zero();
        var velocity = Vector3.Zero();
        emitter.Birth(id, position, velocity);
        return new JsonObject
        {
            ["life"] = Dump.Num(emitter.LifeOf(id)),
            ["position"] = Triple(position),
            ["velocity"] = Triple(velocity),
        };
    }

    private static JsonObject Look(JsonElement c)
    {
        var output = new ParticleBuffer(2);
        EmitterOf(In(c, 0)).Look(output, 1, Long(c, 1), Num(c, 2), Num(c, 3));
        return new JsonObject
        {
            ["size"] = Dump.Num((double)output.Sizes[1]),
            ["colours"] = Floats(output.Colours.Skip(3)),
            ["angle"] = Dump.Num((double)output.Angles[1]),
        };
    }

    private static JsonObject Simulate(JsonElement c)
    {
        double? bounce = In(c, 3).ValueKind == JsonValueKind.Null ? null : Num(c, 3);
        var sim = new ParticleSim(EmitterOf(In(c, 0)), HitOf(In(c, 2)), bounce, Num(c, 4));
        var place = PlacementOf(In(c, 5));
        var counts = new JsonArray();
        foreach (var dt in Numbers(In(c, 1)))
        {
            sim.Step(dt, place);
            counts.Add(sim.Count);
        }

        var output = new ParticleBuffer(Int(c, 6));
        sim.Fill(output, place);
        return new JsonObject { ["counts"] = counts, ["buffer"] = Buffer(output) };
    }

    private static Trail TrailOf(JsonElement spec)
    {
        var made = new Trail();
        return new Trail(
            Val(spec, "life", Fixture.Number) ?? made.Life,
            Val(spec, "rate", Fixture.Number) ?? made.Rate,
            Ref(spec, "width", Numbers) ?? made.Width);
    }

    private static JsonObject MadeTrail(JsonElement c)
    {
        var trail = TrailOf(In(c, 0));
        return new JsonObject
        {
            ["life"] = Dump.Num(trail.Life),
            ["rate"] = Dump.Num(trail.Rate),
            ["width"] = Dump.Arr(trail.Width, Dump.Num),
            ["points"] = trail.Points,
        };
    }

    private static JsonObject MadeStrip(JsonElement c)
    {
        var strip = new TrailStrip(Int(c, 0));
        return new JsonObject { ["capacity"] = strip.Capacity, ["count"] = strip.Count };
    }

    private static PathAt PathOf(JsonElement spec)
    {
        var a = Fixture.Number(spec[1]);
        var b = Fixture.Number(spec[2]);
        return spec[0].GetString() == "line"
            ? t => new Vector3(t * a, t * b, 1)
            : t => new Vector3(a * Math.Cos(b * t), t, a * Math.Sin(b * t));
    }

    private static JsonObject Along(JsonElement c)
    {
        var strip = new TrailStrip(Int(c, 3));
        TrailOf(In(c, 0)).SampleAlong(Num(c, 2), PathOf(In(c, 1)), strip);
        return Strip(strip);
    }

    private static JsonArray Remember(JsonElement c)
    {
        var ring = new TrailRing(TrailOf(In(c, 0)));
        var strip = new TrailStrip(Int(c, 2));
        var fills = new JsonArray();
        foreach (var call in In(c, 1).EnumerateArray())
        {
            Apply(ring, strip, call, fills);
        }

        return fills;
    }

    private static void Apply(TrailRing ring, TrailStrip strip, JsonElement call, JsonArray fills)
    {
        switch (call[0].GetString())
        {
            case "record":
                ring.Record(Fixture.Number(call[1]), new Vector3(Fixture.Number(call[2]), Fixture.Number(call[3]), Fixture.Number(call[4])));
                break;
            case "clear":
                ring.Clear();
                break;
            default:
                ring.Fill(strip);
                fills.Add(Strip(strip));
                break;
        }
    }
}
