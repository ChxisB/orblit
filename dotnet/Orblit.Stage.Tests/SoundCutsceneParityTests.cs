using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Audio;
using Orblit.Filament;
using Orblit.Motion;
using Orblit.Parity;
using Orblit.Scene;
using Orblit.Sequence;
using Orblit.VectorMath;
using static Orblit.Parity.Dump;

namespace Orblit.Stage.Tests;

public class SoundCutsceneParityTests
{
    private static readonly Fixture Expected = Fixture.Load("orblit_stage", "sound_cutscenes");

    [Fact]
    public void ACutsceneMatchesTheFixture() => Expected.Check("cutscene", c =>
    {
        var input = c.GetProperty("in");
        var view = new DocumentView(Read(input.GetProperty("document").GetString()!));
        var player = new Cutscenes(view, CutscenesIn(input));

        var frames = new List<JsonNode?>();
        foreach (var action in input.GetProperty("script").EnumerateArray())
        {
            CutsceneStep? step = null;
            switch (action[0].GetString())
            {
                case "start":
                    player.Start(action[1].GetString()!);
                    break;
                case "startFrom":
                    player.StartFrom(action[1].EnumerateArray().Select(name => new Mark(0, name.GetString()!)).ToList());
                    break;
                case "stop":
                    player.Stop();
                    break;
                case "advance":
                    step = player.Advance(action[1].GetDouble());
                    break;
            }

            frames.Add(CutFrame(player, view, step));
        }

        return new JsonArray([.. frames]);
    });

    [Fact]
    public void ABlendMatchesTheFixture() => Expected.Check("blend", c =>
    {
        var input = c.GetProperty("in");
        var view = new DocumentView(Read(input.GetProperty("document").GetString()!));
        var picks = input.GetProperty("cameras").EnumerateArray()
            .Select(pick => (view.CameraOf(pick[0].GetString()!)!, pick[1].GetDouble()))
            .ToList();

        return CameraDump(Cutscenes.BlendCameras(picks));
    });

    [Fact]
    public void TheSoundOfASceneMatchesTheFixture() => Expected.Check("sound", c =>
    {
        var input = c.GetProperty("in");
        var documents = input.GetProperty("documents").EnumerateArray().Select(text => Read(text.GetString()!)).ToList();
        var backend = new RecordedAudio();
        var audio = new AudioPlayer(backend);
        audio.Mixer.Add("sfx");
        var view = new DocumentView(documents[0]);
        var sound = new SoundView(view, audio);
        var player = new Cutscenes(view, CutscenesIn(input));

        var frames = new List<JsonNode?>();
        var seen = 0;
        foreach (var action in input.GetProperty("script").EnumerateArray())
        {
            switch (action[0].GetString())
            {
                case "replace":
                    view.Replace(documents[(int)action[1].GetDouble()]);
                    break;
                case "play":
                    sound.Play(action[1].GetString()!);
                    break;
                case "stop":
                    sound.Stop();
                    break;
                case "start":
                    player.Start(action[1].GetString()!);
                    break;
                case "frame":
                    var seconds = action[1].GetDouble();
                    sound.Advance(seconds, player.Advance(seconds));
                    audio.Advance(seconds);
                    break;
            }

            frames.Add(new JsonObject
            {
                ["calls"] = Arr(backend.Calls.Skip(seen), AudioCall),
                ["position"] = Point(audio.Listener.Position),
                ["forward"] = Point(audio.Listener.Forward),
                ["up"] = Point(audio.Listener.Up),
            });
            seen = backend.Calls.Count;
        }

        return new JsonArray([.. frames]);
    });

    private static SceneDocument Read(string text) => SceneDocument.Decode(text).Document;

    private static List<CutsceneDocument> CutscenesIn(JsonElement input) => input.GetProperty("cutscenes").EnumerateArray()
        .Select(text => CutsceneDocument.Decode(text.GetString()!).Cutscene)
        .ToList();

    private static JsonObject CutFrame(Cutscenes player, DocumentView view, CutsceneStep? step) => new()
    {
        ["playing"] = player.Playing,
        ["cube"] = Point(((TransformComponent)view.Document["cube"]![SceneComponents.Transform]!).Position),
        ["through"] = CameraDump(view.Through),
        ["marks"] = Arr(step?.Marks ?? [], mark => new JsonObject { ["at"] = Num(mark.At), ["name"] = mark.Name }),
        ["sounds"] = Arr(step?.Sounds ?? [], sound => new JsonObject
        {
            ["sound"] = sound.Sound,
            ["at"] = Num(sound.At),
            ["gain"] = Num(sound.Gain),
            ["binding"] = sound.Binding,
        }),
        ["ended"] = step?.Ended ?? false,
    };

    private static JsonObject? CameraDump(RenderCamera? camera) => camera is null ? null : new JsonObject
    {
        ["position"] = Point(camera.Position),
        ["target"] = Point(camera.Target),
        ["fieldOfView"] = Num(camera.FieldOfView),
        ["aperture"] = Num(camera.Aperture),
    };

    private static JsonObject AudioCall(AudioCall call) => call switch
    {
        AudioStarted started => new JsonObject
        {
            ["kind"] = "start",
            ["voice"] = Big(started.Voice),
            ["clip"] = started.Clip,
            ["loop"] = started.Loop,
            ["gain"] = Num(started.Level.Gain),
            ["pitch"] = Num(started.Level.Pitch),
            ["pan"] = Num(started.Level.Pan),
            ["offset"] = Num(started.Offset),
        },
        AudioUpdated updated => new JsonObject
        {
            ["kind"] = "update",
            ["voice"] = Big(updated.Voice),
            ["gain"] = Num(updated.Level.Gain),
            ["pitch"] = Num(updated.Level.Pitch),
            ["pan"] = Num(updated.Level.Pan),
        },
        _ => new JsonObject { ["kind"] = "stop", ["voice"] = Big(call.Voice) },
    };

    private static JsonArray Point(Vector3 point) => new(Num(point.X), Num(point.Y), Num(point.Z));
}
