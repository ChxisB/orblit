using System.Text.Json;
using Orblit.Parity;
using Orblit.VectorMath;

namespace Orblit.Sequence.Tests;

public class SequenceParityTests
{
    private sealed record Kind<T>(string Name, IMixer<T> Mixer, Func<JsonElement, T> Read);

    private static readonly Kind<double> DoubleKind = new("double", Mixers.OfDouble, Fixture.Number);

    private static readonly Kind<Vector3> Vec3Kind = new("vec3", Mixers.OfVector3, Vec);

    private static readonly Kind<Quaternion> QuatKind = new("quat", Mixers.OfQuaternion, Quat);

    private static readonly Kind<bool> BoolKind = new("bool", Mixers.OfBool, e => e.GetBoolean());

    private static List<JsonElement> Cases(Fixture fixture, string op)
    {
        var cases = fixture.Cases.Where(c => c.GetProperty("op").GetString() == op).ToList();
        Assert.True(cases.Count > 0, $"the fixture has no {op} cases");
        return cases;
    }

    private static double Read(JsonElement spec, string name) => Fixture.Number(spec.GetProperty(name));

    private static double[] Numbers(JsonElement array) => [.. array.EnumerateArray().Select(Fixture.Number)];

    private static JsonElement.ArrayEnumerator Items(JsonElement spec, string name) => spec.GetProperty(name).EnumerateArray();

    private static string? Text(JsonElement spec, string name) =>
        spec.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetString() : null;

    private static Vector3 Vec(JsonElement array)
    {
        var v = Numbers(array);
        return new Vector3(v[0], v[1], v[2]);
    }

    private static Quaternion Quat(JsonElement array)
    {
        var v = Numbers(array);
        return new Quaternion(v[0], v[1], v[2], v[3]);
    }

    /// <summary>A name as Dart spells it, <c>in_</c> and <c>inOut</c>, read as the C# member.</summary>
    private static TEnum Word<TEnum>(JsonElement name)
        where TEnum : struct, Enum => Enum.Parse<TEnum>(name.GetString()!.Replace("_", ""), ignoreCase: true);

    private static void AssertClose(Fixture fixture, string what, double[] expected, double[] actual)
    {
        Assert.True(
            expected.Length == actual.Length,
            $"{what}: Dart gave {expected.Length} numbers, C# gave {actual.Length}, from Dart commit {fixture.DartCommit}");
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

    private static void AssertSameList<T>(Fixture fixture, string what, IEnumerable<T> expected, IEnumerable<T> actual) =>
        Assert.True(
            expected.SequenceEqual(actual),
            $"{what}: Dart [{string.Join(", ", expected)}], C# [{string.Join(", ", actual)}], from Dart commit {fixture.DartCommit}");

    private static double[] Flat(object? value) => value switch
    {
        double d => [d],
        Vector3 v => [v.X, v.Y, v.Z],
        Quaternion q => [q.X, q.Y, q.Z, q.W],
        _ => throw new InvalidOperationException($"{value?.GetType()} is not a value a track gives"),
    };

    /// <summary>A value Dart wrote with nothing, a flag, a number or a list of numbers.</summary>
    private static void AssertValue(Fixture fixture, string what, JsonElement expected, object? actual)
    {
        switch (expected.ValueKind)
        {
            case JsonValueKind.Null:
                Assert.True(actual is null, $"{what}: Dart gave nothing, C# gave {actual}, from Dart commit {fixture.DartCommit}");
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                Assert.True(
                    actual is bool flag && flag == expected.GetBoolean(),
                    $"{what}: Dart {expected.GetBoolean()}, C# {actual}, from Dart commit {fixture.DartCommit}");
                break;
            case JsonValueKind.Array:
                AssertClose(fixture, what, Numbers(expected), Flat(actual));
                break;
            default:
                AssertClose(fixture, what, [Fixture.Number(expected)], Flat(actual));
                break;
        }
    }

    private static Clip ClipFrom(JsonElement s) => new(
        start: Read(s, "start"),
        duration: Read(s, "duration"),
        easeIn: Read(s, "easeIn"),
        easeOut: Read(s, "easeOut"),
        shapeIn: Word<Easing>(s.GetProperty("shapeIn")),
        shapeOut: Word<Easing>(s.GetProperty("shapeOut")),
        clipIn: Read(s, "clipIn"),
        speed: Read(s, "speed"),
        name: Text(s, "name"));

    private static Slope<T>? SlopeFrom<T>(Kind<T> kind, JsonElement s, string name)
    {
        var value = s.GetProperty(name);
        return value.ValueKind == JsonValueKind.Null ? null : new Slope<T>(kind.Read(value));
    }

    private static Key<T> KeyFrom<T>(Kind<T> kind, JsonElement s)
    {
        var shape = s.GetProperty("shape");
        return new Key<T>(
            at: Read(s, "at"),
            value: kind.Read(s.GetProperty("value")),
            hold: Word<Hold>(s.GetProperty("hold")),
            shape: shape.ValueKind == JsonValueKind.Null ? null : Word<Easing>(shape),
            slopeIn: SlopeFrom(kind, s, "slopeIn"),
            slopeOut: SlopeFrom(kind, s, "slopeOut"));
    }

    private static Channel<T> ChannelFrom<T>(Kind<T> kind, JsonElement keys) =>
        new([.. keys.EnumerateArray().Select(k => KeyFrom(kind, k))], kind.Mixer);

    private static PropertyTrack<T> PropertyOf<T>(Kind<T> kind, JsonElement s) => new(
        property: s.GetProperty("property").GetString()!,
        clips: [.. Items(s, "clips").Select(c => new Keyed<T>(ClipFrom(c.GetProperty("clip")), ChannelFrom(kind, c.GetProperty("keys"))))],
        mixer: kind.Mixer,
        name: Text(s, "name"),
        binding: Text(s, "binding"),
        muted: s.GetProperty("muted").GetBoolean());

    private static object? PayloadOf(JsonElement payload) => payload.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => payload.GetString(),
        _ => Fixture.Number(payload),
    };

    private static Track TrackFrom(JsonElement s)
    {
        var name = Text(s, "name");
        var binding = Text(s, "binding");
        var muted = s.GetProperty("muted").GetBoolean();
        return s.GetProperty("kind").GetString() switch
        {
            "property" => s.GetProperty("type").GetString() switch
            {
                "double" => PropertyOf(DoubleKind, s),
                "vec3" => PropertyOf(Vec3Kind, s),
                "quat" => PropertyOf(QuatKind, s),
                "bool" => PropertyOf(BoolKind, s),
                var other => throw new InvalidOperationException($"unknown property type {other}"),
            },
            "activation" => new ActivationTrack(
                clips: [.. Items(s, "clips").Select(ClipFrom)], name: name, binding: binding, muted: muted),
            "marks" => new MarkTrack(
                marks: [.. Items(s, "marks").Select(m => new Mark(Read(m, "at"), m.GetProperty("name").GetString()!, PayloadOf(m.GetProperty("payload"))))],
                name: name,
                binding: binding,
                muted: muted),
            "sound" => new SoundTrack(
                clips: [.. Items(s, "clips").Select(c => (ClipFrom(c.GetProperty("clip")), c.GetProperty("sound").GetString()!))],
                gain: Read(s, "gain"),
                name: name,
                binding: binding,
                muted: muted),
            "shot" => new ShotTrack(
                shots: [.. Items(s, "clips").Select(c => (ClipFrom(c.GetProperty("clip")), c.GetProperty("camera").GetString()!))],
                name: name,
                muted: muted),
            var other => throw new InvalidOperationException($"unknown track {other}"),
        };
    }

    private static CutSequence SequenceFrom(JsonElement s)
    {
        var duration = s.GetProperty("duration");
        return new CutSequence(
            tracks: [.. Items(s, "tracks").Select(TrackFrom)],
            rate: Read(s, "rate"),
            duration: duration.ValueKind == JsonValueKind.Null ? null : Fixture.Number(duration));
    }

    private static object? ValueAt<T>(PropertyTrack<T> track, double at) => track.TryValueAt(at, out var value) ? value : null;

    /// <summary>What one track says at a moment, for the two kinds that can say nothing.</summary>
    private static object? Probe(Track track, double at) => track switch
    {
        PropertyTrack<double> t => ValueAt(t, at),
        PropertyTrack<Vector3> t => ValueAt(t, at),
        PropertyTrack<Quaternion> t => ValueAt(t, at),
        PropertyTrack<bool> t => ValueAt(t, at),
        ActivationTrack t => t.ContributionAt(at),
        _ => null,
    };

    private static void AssertMarks(Fixture fixture, string what, JsonElement expected, IReadOnlyList<Mark> actual)
    {
        var theirs = expected.EnumerateArray().ToList();
        Assert.True(
            theirs.Count == actual.Count,
            $"{what}: Dart fired {theirs.Count} marks, C# fired {actual.Count}, from Dart commit {fixture.DartCommit}");
        for (var i = 0; i < theirs.Count; i++)
        {
            AssertSame(fixture, $"{what}, mark {i} time", Fixture.Number(theirs[i][0]), actual[i].At);
            AssertSame(fixture, $"{what}, mark {i} name", theirs[i][1].GetString(), actual[i].Name);
            AssertSame(fixture, $"{what}, mark {i} payload", PayloadOf(theirs[i][2]), actual[i].Payload);
        }
    }

    private static void AssertValues(Fixture fixture, string what, JsonElement expected, SequenceFrame actual)
    {
        var bindings = expected.GetProperty("values").EnumerateObject().ToList();
        AssertSameList(fixture, $"{what}, bindings", bindings.Select(b => b.Name), actual.Values.Keys);
        foreach (var binding in bindings)
        {
            var properties = binding.Value.EnumerateObject().ToList();
            var theirs = actual.Values[binding.Name];
            AssertSameList(fixture, $"{what}, {binding.Name} properties", properties.Select(p => p.Name), theirs.Keys);
            foreach (var property in properties)
            {
                AssertValue(fixture, $"{what}, {binding.Name}.{property.Name}", property.Value, theirs[property.Name]);
            }
        }

        var active = expected.GetProperty("active").EnumerateObject().ToList();
        AssertSameList(fixture, $"{what}, active bindings", active.Select(a => a.Name), actual.Active.Keys);
        foreach (var one in active)
        {
            AssertSame(fixture, $"{what}, active {one.Name}", one.Value.GetBoolean(), actual.Active[one.Name]);
        }
    }

    private static void AssertSoundsAndShots(Fixture fixture, string what, JsonElement expected, SequenceFrame actual)
    {
        var sounds = Items(expected, "sounds").ToList();
        AssertSame(fixture, $"{what}, sound count", sounds.Count, actual.Sounds.Count);
        for (var i = 0; i < sounds.Count; i++)
        {
            AssertSame(fixture, $"{what}, sound {i}", sounds[i].GetProperty("sound").GetString(), actual.Sounds[i].Sound);
            AssertClose(fixture, $"{what}, sound {i} position", [Read(sounds[i], "at")], [actual.Sounds[i].At]);
            AssertClose(fixture, $"{what}, sound {i} gain", [Read(sounds[i], "gain")], [actual.Sounds[i].Gain]);
            AssertSame(fixture, $"{what}, sound {i} binding", Text(sounds[i], "binding"), actual.Sounds[i].Binding);
        }

        var shots = Items(expected, "shots").ToList();
        AssertSame(fixture, $"{what}, shot count", shots.Count, actual.Shots.Count);
        for (var i = 0; i < shots.Count; i++)
        {
            AssertSame(fixture, $"{what}, shot {i}", shots[i].GetProperty("camera").GetString(), actual.Shots[i].Camera);
            AssertClose(fixture, $"{what}, shot {i} weight", [Read(shots[i], "weight")], [actual.Shots[i].Weight]);
        }
    }

    private static void AssertFrame(Fixture fixture, string what, JsonElement expected, SequenceFrame actual)
    {
        AssertClose(fixture, $"{what}, moment", [Read(expected, "at")], [actual.At]);
        AssertValues(fixture, what, expected, actual);
        AssertSoundsAndShots(fixture, what, expected, actual);
        AssertSame(fixture, $"{what}, running count", Items(expected, "running").Count(), actual.Running.Count);
    }

    private static void AssertChannel<T>(Fixture fixture, Kind<T> kind, string what, JsonElement c)
    {
        var channel = ChannelFrom(kind, c.GetProperty("keys"));
        var ats = Numbers(c.GetProperty("ats"));
        var values = Items(c, "values").ToList();
        for (var i = 0; i < ats.Length; i++)
        {
            AssertValue(fixture, $"{what}, at {ats[i]:R}", values[i], channel.At(ats[i]));
        }

        var slopes = c.GetProperty("slopes");
        if (slopes.ValueKind == JsonValueKind.String)
        {
            Assert.Throws<InvalidOperationException>(() => channel.SlopeAt(-1));
            return;
        }

        var theirs = slopes.EnumerateArray().ToList();
        for (var i = 0; i < theirs.Count; i++)
        {
            AssertValue(fixture, $"{what}, slope of key {i - 1}", theirs[i], channel.SlopeAt(i - 1));
        }
    }

    private static void AssertLerp<T>(Fixture fixture, Kind<T> kind, string what, JsonElement c)
    {
        var a = kind.Read(c.GetProperty("a"));
        var b = kind.Read(c.GetProperty("b"));
        var ts = Numbers(c.GetProperty("ts"));
        var values = Items(c, "values").ToList();
        for (var i = 0; i < ts.Length; i++)
        {
            AssertValue(fixture, $"{what}, at {ts[i]:R}", values[i], kind.Mixer.Lerp(a, b, ts[i]));
        }
    }

    private static void AssertMix<T>(Fixture fixture, Kind<T> kind, string what, JsonElement c)
    {
        List<T> values = [.. Items(c, "values").Select(kind.Read)];
        var weights = Numbers(c.GetProperty("weights"));
        AssertValue(fixture, what, c.GetProperty("value"), kind.Mixer.Mix(values, weights));
    }

    [Fact]
    public void EveryEasingShapeMatchesDart()
    {
        var fixture = Fixture.Load("orblit_sequence", "sequence");
        foreach (var c in Cases(fixture, "ease"))
        {
            var shape = Word<Easing>(c.GetProperty("shape"));
            var ats = Numbers(c.GetProperty("ats"));
            var values = Numbers(c.GetProperty("values"));
            for (var i = 0; i < ats.Length; i++)
            {
                AssertClose(fixture, $"{shape} at {ats[i]:R}", [values[i]], [Easings.Ease(shape, ats[i])]);
            }
        }
    }

    [Fact]
    public void ClipWeightsPositionsAndCoverageMatchDart()
    {
        var fixture = Fixture.Load("orblit_sequence", "sequence");
        var n = 0;
        foreach (var c in Cases(fixture, "clip"))
        {
            var clip = ClipFrom(c.GetProperty("clip"));
            var ats = Numbers(c.GetProperty("ats"));
            var weights = Numbers(c.GetProperty("weights"));
            var locals = Numbers(c.GetProperty("locals"));
            var covers = Items(c, "covers").Select(e => e.GetBoolean()).ToList();
            for (var i = 0; i < ats.Length; i++)
            {
                var what = $"clip {n} at {ats[i]:R}";
                AssertClose(fixture, $"{what}, weight", [weights[i]], [clip.WeightAt(ats[i])]);
                AssertClose(fixture, $"{what}, local time", [locals[i]], [clip.LocalAt(ats[i])]);
                AssertSame(fixture, $"{what}, covers", covers[i], clip.Covers(ats[i]));
            }

            n++;
        }
    }

    [Fact]
    public void ChannelSamplesAndSlopesMatchDart()
    {
        var fixture = Fixture.Load("orblit_sequence", "sequence");
        var n = 0;
        foreach (var c in Cases(fixture, "channel"))
        {
            var type = c.GetProperty("type").GetString();
            var what = $"{type} channel {n++}";
            switch (type)
            {
                case "double":
                    AssertChannel(fixture, DoubleKind, what, c);
                    break;
                case "vec3":
                    AssertChannel(fixture, Vec3Kind, what, c);
                    break;
                case "quat":
                    AssertChannel(fixture, QuatKind, what, c);
                    break;
                default:
                    AssertChannel(fixture, BoolKind, what, c);
                    break;
            }
        }
    }

    [Fact]
    public void MixerLerpMatchesDart()
    {
        var fixture = Fixture.Load("orblit_sequence", "sequence");
        var n = 0;
        foreach (var c in Cases(fixture, "lerp"))
        {
            var type = c.GetProperty("type").GetString();
            var what = $"{type} lerp {n++}";
            switch (type)
            {
                case "double":
                    AssertLerp(fixture, DoubleKind, what, c);
                    break;
                case "vec3":
                    AssertLerp(fixture, Vec3Kind, what, c);
                    break;
                case "quat":
                    AssertLerp(fixture, QuatKind, what, c);
                    break;
                default:
                    AssertLerp(fixture, BoolKind, what, c);
                    break;
            }
        }
    }

    [Fact]
    public void MixerMixMatchesDart()
    {
        var fixture = Fixture.Load("orblit_sequence", "sequence");
        var n = 0;
        foreach (var c in Cases(fixture, "mix"))
        {
            var type = c.GetProperty("type").GetString();
            var what = $"{type} mix {n++}";
            switch (type)
            {
                case "double":
                    AssertMix(fixture, DoubleKind, what, c);
                    break;
                case "vec3":
                    AssertMix(fixture, Vec3Kind, what, c);
                    break;
                case "quat":
                    AssertMix(fixture, QuatKind, what, c);
                    break;
                default:
                    AssertMix(fixture, BoolKind, what, c);
                    break;
            }
        }
    }

    [Fact]
    public void SequenceFramesTracksMarksAndSnapsMatchDart()
    {
        var fixture = Fixture.Load("orblit_sequence", "sequence");
        var n = 0;
        foreach (var c in Cases(fixture, "sequence"))
        {
            var sequence = SequenceFrom(c.GetProperty("spec"));
            var what = $"sequence {n++}";
            AssertClose(fixture, $"{what}, duration", [Read(c, "duration")], [sequence.Duration]);
            AssertClose(fixture, $"{what}, track ends", Numbers(c.GetProperty("ends")), [.. sequence.Tracks.Select(t => t.End)]);

            var ats = Numbers(c.GetProperty("ats"));
            var frames = Items(c, "frames").ToList();
            var probes = Items(c, "probes").Select(p => p.EnumerateArray().ToList()).ToList();
            for (var i = 0; i < ats.Length; i++)
            {
                AssertFrame(fixture, $"{what} at {ats[i]:R}", frames[i], sequence.SampleAt(ats[i]));
                for (var t = 0; t < probes.Count; t++)
                {
                    AssertValue(fixture, $"{what}, track {t} at {ats[i]:R}", probes[t][i], Probe(sequence.Tracks[t], ats[i]));
                }
            }

            AssertWindowsAndSnaps(fixture, what, c, sequence);
        }
    }

    private static void AssertWindowsAndSnaps(Fixture fixture, string what, JsonElement c, CutSequence sequence)
    {
        foreach (var window in Items(c, "windows"))
        {
            var from = Fixture.Number(window[0]);
            var to = Fixture.Number(window[1]);
            AssertMarks(fixture, $"{what}, marks from {from:R} to {to:R}", window[2], sequence.MarksBetween(from, to));
        }

        var snapAts = Numbers(c.GetProperty("snapAts"));
        var snaps = Numbers(c.GetProperty("snaps"));
        for (var i = 0; i < snapAts.Length; i++)
        {
            AssertClose(fixture, $"{what}, snap {snapAts[i]:R}", [snaps[i]], [sequence.Snap(snapAts[i])]);
        }
    }

    private static (SequenceFrame? Frame, IReadOnlyList<Mark>? Marks) Run(Director director, JsonElement step)
    {
        switch (step[0].GetString())
        {
            case "play":
                director.Play();
                return (null, null);
            case "pause":
                director.Pause();
                return (null, null);
            case "stop":
                director.Stop();
                return (null, null);
            case "seek":
                director.Seek(Fixture.Number(step[1]));
                return (null, null);
            case "whenDone":
                director.WhenDone = Word<WhenDone>(step[1]);
                return (null, null);
            case "sample":
                return (director.Sample(), null);
            case "advance":
                var advanced = director.Advance(Fixture.Number(step[1]));
                return (advanced.Frame, advanced.Marks);
            default:
                throw new InvalidOperationException($"unknown step {step[0]}");
        }
    }

    [Fact]
    public void DirectorScriptsMatchDart()
    {
        var fixture = Fixture.Load("orblit_sequence", "sequence");
        var n = 0;
        foreach (var c in Cases(fixture, "director"))
        {
            var director = new Director(
                SequenceFrom(c.GetProperty("spec")),
                Word<WhenDone>(c.GetProperty("whenDone")),
                Read(c, "speed"));
            var script = Items(c, "script").ToList();
            var log = Items(c, "log").ToList();
            for (var i = 0; i < script.Count; i++)
            {
                var what = $"director {n}, step {i} ({script[i][0]})";
                var (frame, marks) = Run(director, script[i]);
                AssertClose(fixture, $"{what}, playhead", [Read(log[i], "at")], [director.At]);
                AssertSame(fixture, $"{what}, playing", log[i].GetProperty("playing").GetBoolean(), director.Playing);
                AssertSame(fixture, $"{what}, finished", log[i].GetProperty("finished").GetBoolean(), director.Finished);
                AssertStep(fixture, what, log[i], frame, marks);
            }

            n++;
        }
    }

    private static void AssertStep(Fixture fixture, string what, JsonElement expected, SequenceFrame? frame, IReadOnlyList<Mark>? marks)
    {
        if (expected.TryGetProperty("frame", out var theirFrame))
        {
            Assert.True(frame is not null, $"{what}: Dart gave a frame, C# gave none, from Dart commit {fixture.DartCommit}");
            AssertFrame(fixture, what, theirFrame, frame);
        }
        else
        {
            Assert.True(frame is null, $"{what}: Dart gave no frame, C# gave one, from Dart commit {fixture.DartCommit}");
        }

        if (expected.TryGetProperty("marks", out var theirMarks))
        {
            Assert.True(marks is not null, $"{what}: Dart gave marks, C# gave none, from Dart commit {fixture.DartCommit}");
            AssertMarks(fixture, what, theirMarks, marks);
        }
        else
        {
            Assert.True(marks is null, $"{what}: Dart gave no marks, C# gave some, from Dart commit {fixture.DartCommit}");
        }
    }
}
