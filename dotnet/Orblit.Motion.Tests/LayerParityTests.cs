using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Json;
using Orblit.Parity;
using static Orblit.Motion.Tests.MotionParityTests;
using static Orblit.Parity.Dump;

namespace Orblit.Motion.Tests;

public class LayerParityTests
{
    private static readonly Fixture Expected = Fixture.Load("orblit_motion", "layer");

    // The fixture's one library case holds the skeletons, frames, masks and clip texts every other case names.
    private static readonly JsonElement Library = Expected.Cases.Single(c => c.GetProperty("op").GetString() == "library").GetProperty("expect");

    [Fact]
    public void RestPosesComeFromTheirSkeletonsAsTheFixtureSays() => Expected.Check("rest", Rested);

    [Fact]
    public void FramesFillFromARestPoseAsTheFixtureSays() => Expected.Check("complete", Completed);

    [Fact]
    public void MasksBelowABoneWeighAsTheFixtureSays() => Expected.Check("below", MaskedBelow);

    [Fact]
    public void MasksFromWeightsWeighAsTheFixtureSays() => Expected.Check("mask", MaskedBy);

    [Fact]
    public void FramesLayerAsTheFixtureSays() => Expected.Check("layer", Layered);

    [Fact]
    public void FramesAddAsTheFixtureSays() => Expected.Check("add", Added);

    [Fact]
    public void OneShotsWeighAsTheFixtureSays() => Expected.Check("weight", Weighed);

    [Fact]
    public void OneShotsAdvanceAsTheFixtureSays() => Expected.Check("advance", Advanced);

    [Fact]
    public void OneShotsSampleAsTheFixtureSays() => Expected.Check("sample", Sampled);

    [Fact]
    public void LayerPlacesReadAsTheFixtureSays() => Expected.Check("place", Placed);

    [Fact]
    public void LayerPlacesConstructAsTheFixtureSays() => Expected.Check("placeAt", PlacedAt);

    private static JsonElement Named(string kind, string name) => Library.GetProperty(kind).GetProperty(name);

    private static ClipFrame Frame(JsonElement name) => FrameOf(Named("frames", name.GetString()!));

    private static ClipDocument ClipOf(JsonElement name) => Clip(Named("clips", name.GetString()!));

    private static ClipFrame? ReferenceOf(JsonElement name) => name.ValueKind == JsonValueKind.Null ? null : Frame(name);

    private static ClipFrame FrameOf(JsonElement spec)
    {
        var frame = new ClipFrame(Fixture.Number(spec.GetProperty("at")));
        foreach (var target in spec.GetProperty("values").EnumerateObject())
        {
            frame.Values[target.Name] = new OrderedDictionary<string, object>(
                target.Value.EnumerateObject().Select(static property => KeyValuePair.Create(property.Name, ValueOf(property.Value))));
        }

        foreach (var target in spec.GetProperty("bones").EnumerateObject())
        {
            frame.Bones[target.Name] = new OrderedDictionary<string, BoneLocal>(
                target.Value.EnumerateObject().Select(static bone => KeyValuePair.Create(bone.Name, LocalOf(bone.Value))));
        }

        return frame;
    }

    // A flag, a number, a vector of three numbers or a turn of four.
    private static object ValueOf(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => value.GetDouble(),
        _ when value.GetArrayLength() == 3 => Vector(value),
        _ => Turn(value),
    };

    private static BoneLocal LocalOf(JsonElement parts) => new(Maybe(parts[0], Vector), Maybe(parts[1], Turn), Maybe(parts[2], Vector));

    private static T? Maybe<T>(JsonElement part, Func<JsonElement, T> read)
        where T : class =>
        part.ValueKind == JsonValueKind.Null ? null : read(part);

    private static RestSkeleton SkeletonOf(string name)
    {
        var spec = Named("skeletons", name);
        return new RestSkeleton(
            [.. spec.GetProperty("names").EnumerateArray().Select(static one => one.GetString()!)],
            [.. spec.GetProperty("parents").EnumerateArray().Select(static one => one.GetInt32())],
            [.. spec.GetProperty("local").EnumerateArray().Select(static parts => Matrix4.Compose(Vector(parts[0]), Turn(parts[1]), Vector(parts[2])))]);
    }

    private static BoneMask? MaskOf(JsonElement spec) => spec.ValueKind == JsonValueKind.Null
        ? null
        : new BoneMask(
            new Dictionary<string, double>(spec.GetProperty("weights").EnumerateArray()
                .Select(static pair => KeyValuePair.Create(pair[0].GetString()!, Fixture.Number(pair[1])))),
            spec.GetProperty("target").GetString()!);

    private static JsonObject MaskDump(BoneMask mask) => new JsonObject
    {
        ["target"] = Text(mask.Target),
        ["weights"] = Arr(mask.Weights, static one => new JsonArray(Text(one.Key), Num(one.Value))),
        ["probe"] = Arr(
            new[] { ("", "arm"), ("", "leg"), ("prop", "arm"), ("", "nope"), ("", "hips") },
            probe => Num(mask.WeightOf(probe.Item1, probe.Item2))),
    };

    private static JsonNode? Rested(JsonElement c) => Attempt(() =>
    {
        var skeletons = c.GetProperty("in")[0].EnumerateArray().Select(static name => name.GetString()!)
            .ToDictionary(static name => name, static name => SkeletonOf(name));
        return Framed(FrameLayers.RestOf(skeletons));
    });

    private static JsonNode? Completed(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        return Framed(FrameLayers.Complete(Frame(input[0]), Frame(input[1])));
    });

    private static JsonNode? MaskedBelow(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        return MaskDump(BoneMask.Below(SkeletonOf(input[0].GetString()!), input[1].GetString()!, input[2].GetString()!, Fixture.Number(input[3])));
    });

    private static JsonNode? MaskedBy(JsonElement c) => Attempt(() => MaskDump(MaskOf(c.GetProperty("in")[0])!));

    private static JsonNode? Layered(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        return Framed(FrameLayers.Layer(Frame(input[0]), Frame(input[1]), Frame(input[2]), MaskOf(input[3]), Fixture.Number(input[4])));
    });

    private static JsonNode? Added(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        return Framed(FrameLayers.Add(
            Frame(input[0]),
            Frame(input[1]),
            Frame(input[2]),
            reference: ReferenceOf(input[3]),
            mask: MaskOf(input[4]),
            weight: Fixture.Number(input[5])));
    });

    private static JsonNode? Weighed(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var layer = new ClipLayer(ClipOf(input[0]), fadeIn: Fixture.Number(input[1]), fadeOut: Fixture.Number(input[2]), influence: Fixture.Number(input[3]));
        return Num(layer.WeightAt(new LayerPlace(Fixture.Number(input[4]))));
    });

    private static JsonNode? Advanced(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var step = new ClipLayer(ClipOf(input[0])).Advance(new LayerPlace(Fixture.Number(input[1])), Fixture.Number(input[2]));
        return new JsonObject
        {
            ["at"] = Num(step.Place.At),
            ["finished"] = step.Finished,
            ["marks"] = Arr(step.Marks, static mark => new JsonArray(Num(mark.At), Text(mark.Name))),
        };
    });

    private static JsonNode? Sampled(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var layer = new ClipLayer(
            ClipOf(input[0]),
            MaskOf(input[1]),
            Fixture.Number(input[2]),
            Fixture.Number(input[3]),
            Fixture.Number(input[4]),
            input[5].GetBoolean(),
            ReferenceOf(input[6]));
        return Framed(layer.SampleAt(Frame(input[7]), new LayerPlace(Fixture.Number(input[9])), Frame(input[8])));
    });

    private static JsonNode? Placed(JsonElement c) => Attempt(() =>
    {
        if (LayerPlace.FromJson(JsonText.Decode(c.GetProperty("in")[0].GetRawText())) is not { } place)
        {
            return null;
        }

        return new JsonObject
        {
            ["json"] = Tree(place.ToJson()),
            ["at"] = Num(place.At),
            ["back"] = Tree(LayerPlace.FromJson(place.ToJson())!.ToJson()),
        };
    });

    private static JsonNode? PlacedAt(JsonElement c) => Attempt(() => Num(new LayerPlace(Fixture.Number(c.GetProperty("in")[0])).At));
}
