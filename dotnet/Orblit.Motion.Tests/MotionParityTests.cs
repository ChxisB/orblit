using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Parity;
using static Orblit.Parity.Dump;

namespace Orblit.Motion.Tests;

public class MotionParityTests
{
    private static readonly Fixture Expected = Fixture.Load("orblit_motion", "motion");

    [Fact]
    public void ClipsReadAndWriteAsTheFixtureSays() => Expected.Check("decode", Decoded);

    [Fact]
    public void ClipsSampleAsTheFixtureSays() => Expected.Check("sample", Sampled);

    [Fact]
    public void RootsCarryTheBodyAsTheFixtureSays() => Expected.Check("root", Rooted);

    [Fact]
    public void MarksFallBetweenTimesAsTheFixtureSays() => Expected.Check("marks", Marked);

    [Fact]
    public void FramesMixAsTheFixtureSays() => Expected.Check("mix", Mixed);

    [Fact]
    public void RootStepsComposeAsTheFixtureSays() => Expected.Check("then", Composed);

    internal static ClipDocument Clip(JsonElement text) => ClipDocument.Decode(text.GetString()!).Clip;

    private static JsonNode? Decoded(JsonElement c) => Attempt(() =>
    {
        var load = ClipDocument.Decode(c.GetProperty("in")[0].GetString()!);
        return new JsonObject
        {
            ["problems"] = Arr(load.Problems, Text),
            ["text"] = Text(load.Clip.Encode()),
        };
    });

    private static JsonNode? Sampled(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        return Dumped(Clip(input[0]).SampleAt(Fixture.Number(input[1]), input[2].GetBoolean()));
    });

    private static JsonNode? Rooted(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        return Dumped(Clip(input[0]).RootStepBetween(Fixture.Number(input[1]), Fixture.Number(input[2])));
    });

    private static JsonNode? Marked(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var marks = Clip(input[0]).MarksBetween(Fixture.Number(input[1]), Fixture.Number(input[2]));
        return Arr(marks, mark => new JsonArray(Num(mark.At), Text(mark.Name), Tree(mark.Payload)));
    });

    private static JsonNode? Mixed(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var ats = input[1].EnumerateArray().Select(Fixture.Number).ToList();
        var frames = input[0].EnumerateArray().Select((text, i) => ClipFrame.Sample(Clip(text), ats[i])).ToList();
        var weights = input[2].EnumerateArray().Select(Fixture.Number).ToList();
        return Dumped(ClipFrame.Mix(frames, weights, 0.5));
    });

    private static JsonNode? Composed(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        return Dumped(Step(input[0]).Then(Step(input[1])));
    });

    private static RootStep Step(JsonElement step) => new(Vector(step[0]), Turn(step[1]));

    internal static Vector3 Vector(JsonElement parts) =>
        new(Fixture.Number(parts[0]), Fixture.Number(parts[1]), Fixture.Number(parts[2]));

    internal static Quaternion Turn(JsonElement parts) =>
        new(Fixture.Number(parts[0]), Fixture.Number(parts[1]), Fixture.Number(parts[2]), Fixture.Number(parts[3]));

    internal static JsonArray? Dumped(Vector3? v) => v is null ? null : new JsonArray(Num(v.X), Num(v.Y), Num(v.Z));

    internal static JsonArray? Dumped(Quaternion? q) => q is null ? null : new JsonArray(Num(q.X), Num(q.Y), Num(q.Z), Num(q.W));

    internal static JsonObject Dumped(RootStep step) => new()
    {
        ["position"] = Dumped(step.Position),
        ["rotation"] = Dumped(step.Rotation),
        ["none"] = step.IsNone,
    };

    private static JsonNode? Dumped(object value) => value switch
    {
        double number => Num(number),
        Vector3 v => Dumped(v),
        Quaternion q => Dumped(q),
        bool flag => flag,
        _ => throw new InvalidOperationException($"no dump for {value.GetType()}"),
    };

    internal static JsonObject Dumped(ClipFrame frame) => new()
    {
        ["values"] = new JsonObject(frame.Values.Select(target => KeyValuePair.Create<string, JsonNode?>(
            target.Key,
            new JsonObject(target.Value.Select(property => KeyValuePair.Create(property.Key, Dumped(property.Value))))))),
        ["bones"] = new JsonObject(frame.Bones.Select(target => KeyValuePair.Create<string, JsonNode?>(
            target.Key,
            new JsonObject(target.Value.Select(bone => KeyValuePair.Create<string, JsonNode?>(bone.Key, Dumped(bone.Value))))))),
    };

    // A frame as some callers read it also says which moment it was taken at.
    internal static JsonObject Framed(ClipFrame frame)
    {
        var dump = Dumped(frame);
        dump["at"] = Num(frame.At);
        return dump;
    }

    internal static JsonObject Dumped(BoneLocal local) => new()
    {
        ["position"] = Dumped(local.Position),
        ["rotation"] = Dumped(local.Rotation),
        ["scale"] = Dumped(local.Scale),
    };
}
