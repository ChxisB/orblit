using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Json;
using Orblit.Parity;
using Orblit.Sequence;
using static Orblit.Parity.Dump;

namespace Orblit.Motion.Tests;

public class CutsceneParityTests
{
    private static readonly Fixture Expected = Fixture.Load("orblit_motion", "cutscene");

    [Fact]
    public void CutscenesDecodeAsTheFixtureSays() => Expected.Check("decode", Decoded);

    [Fact]
    public void FramesLookAndSoundAsTheFixtureSays() => Expected.Check("frame", Framed);

    [Fact]
    public void MarksFireAsTheFixtureSays() => Expected.Check("marks", Marked);

    [Fact]
    public void DirectorsStepCutscenesAsTheFixtureSays() => Expected.Check("play", Played);

    [Fact]
    public void ShotsAreMadeAsTheFixtureSays() => Expected.Check("shot", Shot);

    [Fact]
    public void SoundsAreMadeAsTheFixtureSays() => Expected.Check("sound", Sound);

    private static CutsceneDocument Read(JsonElement text) => CutsceneDocument.Decode(text.GetString()!).Cutscene;

    private static JsonNode? Decoded(JsonElement c) => Attempt(() =>
    {
        var load = CutsceneDocument.Decode(c.GetProperty("in")[0].GetString()!);
        return new JsonObject
        {
            ["problems"] = Arr(load.Problems, Text),
            ["text"] = Text(load.Cutscene.Encode()),
        };
    });

    private static JsonNode? Framed(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var frame = Read(input[0]).Sequence.SampleAt(Fixture.Number(input[1]));
        return new JsonObject
        {
            ["shots"] = Arr(frame.Shots, static shot => new JsonArray(Text(shot.Camera), Num(shot.Weight))),
            ["sounds"] = Arr(frame.Sounds, static sound => new JsonArray(Text(sound.Sound), Num(sound.At), Num(sound.Gain))),
        };
    });

    private static JsonNode? Marked(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var marks = Read(input[0]).Sequence.MarksBetween(Fixture.Number(input[1]), Fixture.Number(input[2]));
        return Arr(marks, static mark => new JsonArray(Num(mark.At), Text(mark.Name), Tree(mark.Payload)));
    });

    private static JsonNode? Played(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var director = new Director(Read(input[0]).Sequence, FileNames.Named<WhenDone>(input[1].GetString())!.Value);
        director.Play();
        var marks = new JsonArray();
        for (var i = 0; i < input[2].GetInt32(); i++)
        {
            foreach (var mark in director.Advance(Fixture.Number(input[3])).Marks)
            {
                marks.Add(new JsonArray(Num(mark.At), Text(mark.Name)));
            }
        }

        return new JsonObject { ["marks"] = marks, ["at"] = Num(director.At), ["finished"] = director.Finished };
    });

    private static JsonNode? Shot(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var shot = new CutsceneShot(input[0].GetString()!, Fixture.Number(input[1]), Fixture.Number(input[2]));
        return new JsonArray(Text(shot.Camera), Num(shot.Start), Num(shot.Duration), Num(shot.End));
    });

    private static JsonNode? Sound(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var sound = new CutsceneSound(input[0].GetString()!, Fixture.Number(input[1]), Fixture.Number(input[2]));
        return new JsonArray(Text(sound.Sound), Num(sound.Start), Num(sound.Duration), Num(sound.End));
    });
}
