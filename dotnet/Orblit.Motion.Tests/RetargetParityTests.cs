using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Json;
using Orblit.Parity;
using static Orblit.Motion.Tests.NamesParityTests;
using static Orblit.Parity.Dump;

namespace Orblit.Motion.Tests;

public class RetargetParityTests
{
    private static readonly Fixture Expected = Fixture.Load("orblit_motion", "retarget_clip");

    // The skeletons are the library of the retarget_names fixture. This fixture's own library case holds the
    // text of each clip its cases name.
    private static readonly Dictionary<string, ClipDocument> Clips = Expected.Cases
        .Single(static c => c.GetProperty("op").GetString() == "library")
        .GetProperty("expect").GetProperty("clips").EnumerateObject()
        .ToDictionary(static one => one.Name, static one => ClipDocument.Decode(one.Value.GetString()!).Clip);

    [Fact]
    public void ClipsRetargetAsTheFixtureSays() => Expected.Check("retarget", Retargeted);

    [Fact]
    public void BoneMapsReadAndWriteAsTheFixtureSays() => Expected.Check("boneMapDecode", Decoded);

    [Fact]
    public void BoneMapsEditAsTheFixtureSays() => Expected.Check("boneMapEdit", Edited);

    [Fact]
    public void BoneMapsNameWhatIsStaleAsTheFixtureSays() => Expected.Check("boneMapStale", Stale);

    [Fact]
    public void BoneMapsFitAsTheFixtureSays() => Expected.Check("boneMapFitted", Fitted);

    [Fact]
    public void BoneMapsPairBonesAsTheFixtureSays() => Expected.Check("boneMapPairing", Paired);

    [Fact]
    public void BoneMapsRetargetClipsAsTheFixtureSays() => Expected.Check("boneMapApply", Applied);

    private static BoneMap MapOf(JsonElement text) => BoneMap.Decode(text.GetString()!).Map;

    private static OrderedDictionary<string, string> Bones(JsonElement pairs) =>
        new(pairs.EnumerateObject().Select(static pair => KeyValuePair.Create(pair.Name, pair.Value.GetString()!)));

    private static HashSet<string> Names(JsonElement names) => [.. names.EnumerateArray().Select(static one => one.GetString()!)];

    private static JsonObject Carried(ClipRetargeted result) => new()
    {
        ["clip"] = Tree(JsonText.Decode(result.Clip.Encode())),
        ["problems"] = Arr(result.Problems, Text),
    };

    private static JsonObject Mapped(BoneMap map, IReadOnlyList<string> problems) => new()
    {
        ["map"] = Tree(map.ToJson()),
        ["text"] = Text(map.Encode()),
        ["problems"] = Arr(problems, Text),
    };

    private static JsonNode? Retargeted(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var options = input[3];
        return Carried(ClipRetarget.Of(
            Clips[input[2].GetString()!],
            Named(input[0]),
            Named(input[1]),
            Bones(options.GetProperty("bones")),
            Names(options.GetProperty("unpaired")),
            options.GetProperty("target").GetString()!,
            options.GetProperty("humanoid").GetBoolean(),
            options.GetProperty("fixRest").GetBoolean()));
    });

    private static JsonNode? Decoded(JsonElement c) => Attempt(() =>
    {
        var load = BoneMap.Decode(c.GetProperty("in")[0].GetString()!);
        return Mapped(load.Map, load.Problems);
    });

    private static JsonNode? Edited(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var map = MapOf(input[0]);
        var edit = input[1].EnumerateArray().Select(static one => one.GetString()!).ToList();
        var edited = edit[0] switch
        {
            "withPair" => map.WithPair(edit[1], edit[2]),
            "withUnpaired" => map.WithUnpaired(edit[1]),
            "withAuto" => map.WithAuto(edit[1]),
            _ => throw new InvalidOperationException($"no edit {edit[0]}"),
        };
        return new JsonObject { ["text"] = Text(edited.Encode()) };
    });

    private static JsonNode? Stale(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        return Arr(MapOf(input[0]).Stale(Named(input[1]), Named(input[2])), Text);
    });

    private static JsonNode? Fitted(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        return new JsonObject { ["text"] = Text(MapOf(input[0]).Fitted(Named(input[1]), Named(input[2])).Encode()) };
    });

    private static JsonNode? Paired(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var (drivers, problems) = MapOf(input[0]).Pairing(Named(input[1]), Named(input[2]));
        return new JsonObject
        {
            ["drivers"] = Arr(drivers, static pair => new JsonArray(Text(pair.Key), Text(pair.Value))),
            ["problems"] = Arr(problems, Text),
        };
    });

    private static JsonNode? Applied(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        return Carried(MapOf(input[0]).Apply(Clips[input[3].GetString()!], Named(input[1]), Named(input[2]), input[4].GetString()!));
    });
}
