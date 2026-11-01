using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Parity;
using static Orblit.Motion.Tests.MotionParityTests;
using static Orblit.Parity.Dump;

namespace Orblit.Motion.Tests;

public class LiveParityTests
{
    private static readonly Fixture Expected = Fixture.Load("orblit_motion", "live");

    [Fact]
    public void FollowsAsTheFixtureSays() => Expected.Check("follow", Followed);

    [Fact]
    public void PointsAsTheFixtureSays() => Expected.Check("pointOf", Pointed);

    [Fact]
    public void PinsAsTheFixtureSays() => Expected.Check("pinned", Pinned);

    private static LiveRetarget Made(JsonElement input)
    {
        var options = input[2];
        return new LiveRetarget(
            NamesParityTests.Named(input[0]),
            NamesParityTests.Named(input[1]),
            options.GetProperty("bones").EnumerateObject().ToDictionary(static pair => pair.Name, static pair => pair.Value.GetString()!),
            options.GetProperty("unpaired").EnumerateArray().Select(static name => name.GetString()!).ToHashSet(StringComparer.Ordinal),
            options.GetProperty("humanoid").GetBoolean(),
            options.GetProperty("fixRest").GetBoolean());
    }

    private static T? Present<T>(JsonElement part, Func<JsonElement, T> read)
        where T : class =>
        part.ValueKind == JsonValueKind.Null ? null : read(part);

    private static Dictionary<string, BoneLocal> Source(JsonElement frame) =>
        frame.EnumerateObject().ToDictionary(
            static bone => bone.Name,
            static bone => new BoneLocal(
                position: Present(bone.Value[0], Vector),
                rotation: Present(bone.Value[1], Turn),
                scale: Present(bone.Value[2], Vector)));

    private static JsonNode? Followed(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var live = Made(input);
        var frame = live.Follow(Source(input[3]));
        var made = new JsonObject();
        foreach (var (bone, local) in frame)
        {
            made[bone] = Dumped(local);
        }

        return new JsonObject { ["problems"] = Arr(live.Problems, Text), ["frame"] = made };
    });

    private static JsonNode? Pointed(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        return Dumped(Made(input).PointOf(input[4].GetString()!, Source(input[3])));
    });

    private static JsonNode? Pinned(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        _ = Made(input).Pinned(input[4].GetString()!, static () => null);
        return JsonValue.Create(true);
    });
}
