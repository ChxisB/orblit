using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Orblit.Parity;
using Orblit.VectorMath;

namespace Orblit.Audio.Tests;

public class AudioParityTests
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

    private static JsonElement? Optional(JsonElement spec, string name) =>
        spec.GetProperty(name).ValueKind == JsonValueKind.Null ? null : spec.GetProperty(name);

    private static string? OptionalString(JsonElement spec, string name) => Optional(spec, name)?.GetString();

    private static void AssertClose(Fixture fixture, string what, double[] expected, double[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.True(
                Tolerance.Close(expected[i], actual[i]),
                $"{what}, value {i}: the fixture {expected[i]:R}, the port {actual[i]:R}, from fixture commit {fixture.SourceCommit}");
        }
    }

    private static double[] Triple(Level level) => [level.Gain, level.Pitch, level.Pan];

    private static Emitter EmitterFrom(JsonElement s)
    {
        var falloff = Optional(s, "falloff");
        var cone = Optional(s, "cone");
        var forward = Optional(s, "forward");
        return new Emitter(
            Vec(s.GetProperty("pos")),
            falloff: falloff is { } f
                ? new Falloff(Read(f, "near"), Read(f, "far"), (FalloffCurve)f.GetProperty("curve").GetInt32())
                : null,
            cone: cone is { } c ? new Cone(Read(c, "inner"), Read(c, "outer"), Read(c, "gain")) : null,
            forward: forward is { } direction ? Vec(direction) : null,
            doppler: Read(s, "doppler"))
        {
            Velocity = Vec(s.GetProperty("vel")),
        };
    }

    private static void Place(Listener listener, JsonElement s)
    {
        listener.Position = Vec(s.GetProperty("pos"));
        listener.Velocity = Vec(s.GetProperty("vel"));
        listener.Forward = Vec(s.GetProperty("forward"));
        listener.Up = Vec(s.GetProperty("up"));
    }

    [Fact]
    public void FalloffAndConeCurvesMatchTheFixture()
    {
        var fixture = Fixture.Load("orblit_audio", "audio");

        foreach (var c in Cases(fixture, "falloff"))
        {
            var falloff = new Falloff(Read(c, "near"), Read(c, "far"), (FalloffCurve)c.GetProperty("curve").GetInt32());
            var distance = Read(c, "distance");
            AssertClose(fixture, $"falloff {c.GetRawText()}", [Read(c, "expect")], [falloff.GainAt(distance)]);
        }

        foreach (var c in Cases(fixture, "cone"))
        {
            var cone = new Cone(Read(c, "inner"), Read(c, "outer"), Read(c, "gain"));
            AssertClose(fixture, $"cone {c.GetRawText()}", [Read(c, "expect")], [cone.GainAt(Read(c, "angle"))]);
        }
    }

    [Fact]
    public void ConstructorsRefuseWhatTheFixtureRefuses()
    {
        var fixture = Fixture.Load("orblit_audio", "audio");

        foreach (var c in Cases(fixture, "refuses"))
        {
            var a = Read(c, "a");
            var b = Read(c, "b");
            var gain = Read(c, "c");
            Action build = c.GetProperty("what").GetString() switch
            {
                "falloff" => () => _ = new Falloff(a, b),
                "cone" => () => _ = new Cone(a, b, gain),
                "emitter" => () => _ = new Emitter(Vector3.Zero(), doppler: a),
                "track" => () => _ = new MusicTrack("a.ogg", a, (long)b),
                "contacts" => () => _ = new ContactSounds(new AudioPlayer(new RecordedAudio()), [], quiet: a, loud: b),
                _ => throw new InvalidDataException("unknown constructor"),
            };

            var threw = false;
            try
            {
                build();
            }
            catch (ArgumentException)
            {
                threw = true;
            }

            Assert.True(
                c.GetProperty("throws").GetBoolean() == threw,
                $"{c.GetRawText()}: the fixture throws {c.GetProperty("throws").GetBoolean()}, from fixture commit {fixture.SourceCommit}");
        }
    }

    [Fact]
    public void HeardByMatchesTheFixture()
    {
        var fixture = Fixture.Load("orblit_audio", "audio");

        foreach (var c in Cases(fixture, "heardBy"))
        {
            var listener = new Listener();
            Place(listener, c.GetProperty("listener"));
            var level = EmitterFrom(c.GetProperty("emitter")).HeardBy(listener);
            AssertClose(fixture, $"heardBy {c.GetRawText()}", Numbers(c.GetProperty("expect")), Triple(level));
        }
    }

    [Fact]
    public void AudioScriptsMatchTheFixtureStepByStep() => RunScripts("audio");

    [Fact]
    public void MusicScriptsMatchTheFixtureStepByStep() => RunScripts("music");

    [Fact]
    public void CueScriptsMatchTheFixtureStepByStep() => RunScripts("cues");

    [Fact]
    public void ContactScriptsMatchTheFixtureStepByStep() => RunScripts("contacts");

    private static void RunScripts(string kind)
    {
        var fixture = Fixture.Load("orblit_audio", "audio");

        var scripts = Cases(fixture, "script").Where(c => c.GetProperty("kind").GetString() == kind).ToList();
        Assert.True(scripts.Count > 0, $"the fixture has no {kind} scripts");
        for (var n = 0; n < scripts.Count; n++)
        {
            var script = scripts[n];
            var world = new World(kind, script);
            var steps = script.GetProperty("steps").EnumerateArray().ToList();
            var expected = script.GetProperty("expect").EnumerateArray().ToList();
            Assert.Equal(steps.Count, expected.Count);

            for (var i = 0; i < steps.Count; i++)
            {
                var what = $"{kind} script {n} step {i} {steps[i].GetRawText()}";
                var want = expected[i];
                var threw = world.Run(steps[i], out var calls);

                Assert.True(
                    want.GetProperty("throws").GetBoolean() == threw,
                    $"{what}: the fixture throws {want.GetProperty("throws").GetBoolean()}, the port {threw}, from fixture commit {fixture.SourceCommit}");
                AssertCalls(fixture, what, want.GetProperty("calls"), calls);
                world.AssertState(fixture, what, want);
            }
        }
    }

    private static void AssertCalls(Fixture fixture, string what, JsonElement expected, List<AudioCall> actual)
    {
        var want = expected.EnumerateArray().ToList();
        Assert.True(
            want.Count == actual.Count,
            $"{what}: the fixture made {want.Count} backend calls, the port {actual.Count}, from fixture commit {fixture.SourceCommit}");
        for (var i = 0; i < want.Count; i++)
        {
            var call = want[i];
            Assert.True(call.GetProperty("v").GetInt64() == actual[i].Voice, $"{what}, call {i}: voice");
            switch (actual[i])
            {
                case AudioStarted started:
                    Assert.Equal("start", call.GetProperty("t").GetString());
                    Assert.Equal(call.GetProperty("clip").GetString(), started.Clip);
                    Assert.Equal(call.GetProperty("loop").GetBoolean(), started.Loop);
                    AssertClose(fixture, $"{what}, call {i} offset", [Read(call, "offset")], [started.Offset]);
                    AssertClose(fixture, $"{what}, call {i} level", Numbers(call.GetProperty("level")), Triple(started.Level));
                    break;
                case AudioUpdated updated:
                    Assert.Equal("update", call.GetProperty("t").GetString());
                    AssertClose(fixture, $"{what}, call {i} level", Numbers(call.GetProperty("level")), Triple(updated.Level));
                    break;
                default:
                    Assert.Equal("stop", call.GetProperty("t").GetString());
                    break;
            }
        }
    }

    /// <summary>What a script runs against. It mirrors the exporter's world.</summary>
    private sealed class World
    {
        private readonly string _kind;
        private readonly RecordedAudio _backend = new();
        private readonly AudioPlayer _audio;
        private readonly Dictionary<string, Bus> _buses;
        private readonly List<Emitter> _emitters;
        private readonly List<Voice> _voices = [];
        private readonly Music? _music;
        private readonly CueBoard? _cues;
        private readonly ContactSounds? _contacts;

        public World(string kind, JsonElement script)
        {
            _kind = kind;
            _emitters = [.. script.GetProperty("emitters").EnumerateArray().Select(EmitterFrom)];
            _audio = new AudioPlayer(_backend);
            Place(_audio.Listener, script.GetProperty("listener"));
            var sfx = _audio.Mixer.Add("sfx");
            _buses = new Dictionary<string, Bus>
            {
                ["master"] = _audio.Mixer.Master,
                ["sfx"] = sfx,
                ["fx"] = _audio.Mixer.Add("fx", under: sfx),
                ["foreign"] = new Mixer().Add("sfx"),
            };
            var setup = script.GetProperty("setup");
            switch (kind)
            {
                case "music":
                    _music = BuildMusic(setup);
                    break;
                case "cues":
                    _cues = BuildCues(setup);
                    break;
                case "contacts":
                    _contacts = BuildContacts(setup);
                    break;
            }
        }

        private Bus? BusNamed(string? name) => name is null ? null : _buses[name];

        private Emitter? EmitterAt(JsonElement spec, string name) =>
            Optional(spec, name) is { } index ? _emitters[index.GetInt32()] : null;

        private Music BuildMusic(JsonElement setup) =>
            new(
                _audio,
                new Dictionary<string, MusicTrack>
                {
                    ["calm"] = new("calm.ogg"),
                    ["fight"] = new("fight.ogg", bpm: 60, beatsPerBar: 3),
                    ["drift"] = new("drift.ogg", bpm: 90.5, beatsPerBar: 5),
                },
                moves:
                [
                    .. setup.GetProperty("moves").EnumerateArray().Select(m => new MusicMove(
                        m.GetProperty("to").GetString()!,
                        OptionalString(m, "from"),
                        (MusicWhen)m.GetProperty("when").GetInt32(),
                        Read(m, "fade"))),
                ],
                bus: BusNamed(OptionalString(setup, "bus")),
                duck: Read(setup, "duck"));

        private CueBoard BuildCues(JsonElement setup) =>
            new(
                _audio,
                bus: BusNamed(OptionalString(setup, "bus")),
                beds: new HashSet<string>(setup.GetProperty("beds").EnumerateArray().Select(b => b.GetString()!)),
                emitterFor: binding =>
                {
                    var index = binding switch
                    {
                        "a" => 0,
                        "b" => 1,
                        _ => -1,
                    };
                    return index >= 0 && index < _emitters.Count ? _emitters[index] : null;
                },
                release: Read(setup, "release"));

        private ContactSounds BuildContacts(JsonElement setup) =>
            new(
                _audio,
                [
                    .. setup.GetProperty("rules").EnumerateArray().Select(rule => new ContactRule(
                        rule.GetProperty("a").GetString()!,
                        OptionalString(rule, "b"),
                        rule.GetProperty("clip").GetString()!)),
                ],
                bus: BusNamed(OptionalString(setup, "bus")),
                quiet: Read(setup, "quiet"),
                loud: Read(setup, "loud"),
                limit: setup.GetProperty("limit").GetInt64());

        /// <summary>Runs a step and says whether it threw. The calls it made come out in <paramref name="calls"/>.</summary>
        public bool Run(JsonElement step, out List<AudioCall> calls)
        {
            var from = _backend.Calls.Count;
            var threw = false;
            try
            {
                Do(step);
            }
            catch (ArgumentException)
            {
                threw = true;
            }

            calls = [.. _backend.Calls.Skip(from)];
            return threw;
        }

        private void Do(JsonElement step)
        {
            switch (_kind)
            {
                case "audio":
                    AudioStep(step);
                    break;
                case "music":
                    MusicStep(step);
                    break;
                case "cues":
                    CueStep(step);
                    break;
                default:
                    ContactStep(step);
                    break;
            }
        }

        private Voice VoiceAt(JsonElement step) => _voices[step.GetProperty("voice").GetInt32()];

        [SuppressMessage(
            "Maintainability", "CA1502:Avoid excessive complexity",
            Justification = "F2: a list of cases is data.")]
        private void AudioStep(JsonElement s)
        {
            switch (s.GetProperty("do").GetString())
            {
                case "play":
                    _voices.Add(_audio.Play(
                        s.GetProperty("clip").GetString()!,
                        bus: BusNamed(OptionalString(s, "bus")),
                        loop: s.GetProperty("loop").GetBoolean(),
                        volume: Read(s, "volume"),
                        pitch: Read(s, "pitch"),
                        offset: Read(s, "offset"),
                        at: EmitterAt(s, "at")));
                    break;
                case "fade":
                    VoiceAt(s).FadeTo(Read(s, "volume"), seconds: Read(s, "seconds"));
                    break;
                case "stop":
                    VoiceAt(s).Stop(fadeSeconds: Read(s, "fade"));
                    break;
                case "volume":
                    VoiceAt(s).Volume = Read(s, "value");
                    break;
                case "pitch":
                    VoiceAt(s).Pitch = Read(s, "value");
                    break;
                case "advance":
                    _audio.Advance(Read(s, "by"));
                    break;
                case "busVolume":
                    _buses[s.GetProperty("bus").GetString()!].Volume = Read(s, "value");
                    break;
                case "mute":
                    _buses[s.GetProperty("bus").GetString()!].Muted = s.GetProperty("on").GetBoolean();
                    break;
                case "moveEmitter":
                    _emitters[s.GetProperty("at").GetInt32()].MoveTo(Vec(s.GetProperty("to")), Read(s, "seconds"));
                    break;
                case "moveListener":
                    _audio.Listener.MoveTo(Vec(s.GetProperty("to")), Read(s, "seconds"));
                    break;
                case "teleportListener":
                    _audio.Listener.Teleport(Vec(s.GetProperty("to")));
                    break;
                case "finish":
                    _backend.Finish(VoiceAt(s).Id);
                    break;
                default:
                    throw new InvalidDataException("unknown step");
            }
        }

        private void MusicStep(JsonElement s)
        {
            switch (s.GetProperty("do").GetString())
            {
                case "go":
                    _music!.Go(s.GetProperty("track").GetString()!);
                    break;
                case "stop":
                    _music!.Stop(fade: Read(s, "fade"));
                    break;
                case "stinger":
                    _music!.Stinger("sting.ogg");
                    break;
                case "musicAdvance":
                    _music!.Advance(Read(s, "by"));
                    break;
                case "audioAdvance":
                    _audio.Advance(Read(s, "by"));
                    break;
                case "finishStingers":
                    Finish(call => call.Clip == "sting.ogg");
                    break;
                case "finishAll":
                    Finish(_ => true);
                    break;
                default:
                    throw new InvalidDataException("unknown step");
            }
        }

        private void CueStep(JsonElement s)
        {
            switch (s.GetProperty("do").GetString())
            {
                case "update":
                    _cues!.Update(
                        [
                            .. s.GetProperty("cues").EnumerateArray().Select(c => new Cue(
                                c.GetProperty("clip").GetString()!,
                                Read(c, "at"),
                                Read(c, "gain"),
                                OptionalString(c, "binding"))),
                        ],
                        seeked: s.GetProperty("seeked").GetBoolean());
                    break;
                case "advance":
                    _audio.Advance(Read(s, "by"));
                    break;
                case "stop":
                    _cues!.Stop();
                    break;
                case "finishAll":
                    Finish(_ => true);
                    break;
                default:
                    throw new InvalidDataException("unknown step");
            }
        }

        private void ContactStep(JsonElement s)
        {
            switch (s.GetProperty("do").GetString())
            {
                case "hit":
                    _contacts!.Hit(
                        s.GetProperty("a").GetString()!,
                        s.GetProperty("b").GetString()!,
                        Read(s, "strength"),
                        EmitterAt(s, "at"));
                    break;
                case "advance":
                    _audio.Advance(Read(s, "by"));
                    break;
                case "finishAll":
                    Finish(_ => true);
                    break;
                default:
                    throw new InvalidDataException("unknown step");
            }
        }

        private void Finish(Func<AudioStarted, bool> which)
        {
            foreach (var call in _backend.Calls.OfType<AudioStarted>().ToList())
            {
                if (which(call) && _backend.IsPlaying(call.Voice))
                {
                    _backend.Finish(call.Voice);
                }
            }
        }

        public void AssertState(Fixture fixture, string what, JsonElement want)
        {
            switch (_kind)
            {
                case "audio":
                    var voices = want.GetProperty("voices").EnumerateArray().ToList();
                    Assert.True(voices.Count == _voices.Count, $"{what}: voice count");
                    for (var i = 0; i < voices.Count; i++)
                    {
                        var v = voices[i].EnumerateArray().ToList();
                        Assert.True(v[0].GetBoolean() == _voices[i].IsPlaying, $"{what}: voice {i} isPlaying");
                        AssertClose(
                            fixture,
                            $"{what}: voice {i}",
                            [Fixture.Number(v[1]), Fixture.Number(v[2])],
                            [_voices[i].Volume, _voices[i].Pitch]);
                    }

                    Assert.True(want.GetProperty("live").GetInt32() == _audio.Voices.Count, $"{what}: live voices");
                    break;
                case "music":
                    Assert.True(OptionalString(want, "playing") == _music!.Playing, $"{what}: playing");
                    Assert.True(OptionalString(want, "waiting") == _music.WaitingFor, $"{what}: waiting for");
                    break;
                default:
                    Assert.True(want.GetProperty("live").GetInt32() == _audio.Voices.Count, $"{what}: live voices");
                    break;
            }
        }
    }
}
