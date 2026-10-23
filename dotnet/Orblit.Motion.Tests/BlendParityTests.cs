using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Json;
using Orblit.Parity;
using static Orblit.Motion.Tests.MotionParityTests;
using static Orblit.Parity.Dump;

namespace Orblit.Motion.Tests;

public class BlendParityTests
{
    private static readonly Fixture Expected = Fixture.Load("orblit_motion", "blend");

    // The fixture's one library case holds the clip and blend texts every other case names.
    private static readonly Dictionary<string, ClipDocument> Clips = Library("clips", static text => ClipDocument.Decode(text).Clip);

    private static readonly Dictionary<string, BlendDocument> Blends = Library("blends", static text => BlendDocument.Decode(text).Blend);

    [Fact]
    public void BlendsReadAndWriteAsTheFixtureSays() => Expected.Check("decode", Decoded);

    [Fact]
    public void PlacesReadAndGoToNumbersAsTheFixtureSays() => Expected.Check("place", Placed);

    [Fact]
    public void PlacesComeBackFromNumbersAsTheFixtureSays() => Expected.Check("numbers", Numbered);

    [Fact]
    public void ClipsGetTheirWeightsAsTheFixtureSays() => Expected.Check("weights", Weighed);

    [Fact]
    public void ChangesFireAsTheFixtureSays() => Expected.Check("changeFor", Changed);

    [Fact]
    public void PosesBlendAsTheFixtureSays() => Expected.Check("sample", Sampled);

    [Fact]
    public void PosesBlendOverARestPoseAsTheFixtureSays() => Expected.Check("rested", Rested);

    [Fact]
    public void StatesHaveALengthAsTheFixtureSays() => Expected.Check("length", Lengthened);

    [Fact]
    public void RoutesRunAsTheFixtureSays() => Expected.Check("route", Routed);

    [Fact]
    public void PlayersPlayAsTheFixtureSays() => Expected.Check("player", Played);

    private static Dictionary<string, T> Library<T>(string kind, Func<string, T> read)
    {
        var library = Expected.Cases.Single(c => c.GetProperty("op").GetString() == "library");
        return library.GetProperty("expect").GetProperty(kind).EnumerateObject()
            .ToDictionary(one => one.Name, one => read(one.Value.GetString()!));
    }

    private static BlendDocument BlendOf(JsonElement name) => Blends[name.GetString()!];

    private static object? Raw(JsonElement json) => JsonText.Decode(json.GetRawText());

    private static BlendPlace PlaceOf(JsonElement blend, JsonElement json) =>
        BlendPlace.FromJson(Raw(json), BlendOf(blend)) ?? throw new InvalidOperationException("no place");

    private static Dictionary<string, double> Readings(JsonElement map) =>
        map.EnumerateObject().ToDictionary(static one => one.Name, static one => Fixture.Number(one.Value));

    private static (BlendDocument Blend, BlendPlace Place, Dictionary<string, double> Readings) Reading(JsonElement c)
    {
        var input = c.GetProperty("in");
        return (BlendOf(input[0]), PlaceOf(input[0], input[1]), Readings(input[2]));
    }

    private static JsonNode? Decoded(JsonElement c) => Attempt(() =>
    {
        var load = BlendDocument.Decode(c.GetProperty("in")[0].GetString()!);
        var blend = load.Blend;
        return new JsonObject
        {
            ["problems"] = Arr(load.Problems, static text => Text(text)),
            ["text"] = Text(blend.Encode()),
            ["states"] = Arr(blend.States, static state => Text(state.Name)),
            ["entries"] = new JsonObject(blend.Entries.Select(static one => KeyValuePair.Create<string, JsonNode?>(one.Key, Text(one.Value)))),
            ["clips"] = Arr(blend.ClipNames.Order(StringComparer.Ordinal), static text => Text(text)),
            ["reads"] = Arr(blend.InputsRead.Order(StringComparer.Ordinal), static text => Text(text)),
        };
    });

    private static JsonNode? Placed(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var blend = BlendOf(input[0]);
        if (BlendPlace.FromJson(Raw(input[1]), blend) is not { } read)
        {
            return new JsonObject { ["read"] = false };
        }

        var back = BlendPlace.FromNumbers(blend, read.ToNumbers(blend));
        return new JsonObject
        {
            ["read"] = true,
            ["json"] = Tree(read.ToJson()),
            ["weight"] = Num(read.Weight),
            ["depth"] = read.Depth,
            ["route"] = Arr(read.Route, static text => Text(text)),
            ["shares"] = Arr(read.Shares, static share => new JsonArray(Text(share.Place.State), Num(share.Share))),
            ["numbers"] = Arr(read.ToNumbers(blend), static number => Num(number)),
            ["back"] = back is null ? null : Tree(back.ToJson()),
            ["same"] = back is not null && back.Equals(read),
        };
    });

    private static JsonNode? Numbered(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var numbers = input[1].EnumerateArray().Select(Fixture.Number).ToList();
        return BlendPlace.FromNumbers(BlendOf(input[0]), numbers) is { } placed ? Tree(placed.ToJson()) : null;
    });

    private static JsonNode? Weighed(JsonElement c) => Attempt(() =>
    {
        var (blend, place, readings) = Reading(c);
        return Arr(
            blend.WeightsAt(place, readings),
            static weight => new JsonArray(Text(weight.State), Text(weight.Clip), Num(weight.Weight), Num(weight.Lap)));
    });

    private static JsonNode? Changed(JsonElement c) => Attempt(() =>
    {
        var (blend, place, readings) = Reading(c);
        return blend.ChangeFor(place, readings) is { } change ? Tree(change.ToJson()) : null;
    });

    private static JsonNode? Sampled(JsonElement c) => Attempt(() =>
    {
        var (blend, place, readings) = Reading(c);
        return Framed(blend.SampleAt(place, readings, Clips));
    });

    private static JsonNode? Rested(JsonElement c) => Attempt(() =>
    {
        var (blend, place, readings) = Reading(c);
        var pose = PoseSnapshot.FromJson(Raw(c.GetProperty("in")[3])) ?? throw new InvalidOperationException("no pose");
        return Framed(blend.SampleAt(place, readings, Clips, pose.Sample()));
    });

    private static JsonNode? Lengthened(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        return Num(BlendOf(input[0]).LengthOf(input[1].GetString()!, Readings(input[2]), Clips));
    });

    private static JsonNode? Routed(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var route = BlendOf(input[0]).RouteTo(input[1].GetString()!, input[2].GetString()!);
        return route is null ? null : Arr(route, static text => Text(text));
    });

    // Each command is its own attempt, since a player keeps going after one that throws.
    private static JsonNode? Played(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var place = input[1].ValueKind == JsonValueKind.Null ? null : PlaceOf(input[0], input[1]);
        var player = new BlendPlayer(BlendOf(input[0]), Clips, place, Readings(input[2]));
        return Arr(input[3].EnumerateArray().ToList(), command => Attempt(() => Commanded(player, command)));
    });

    private static JsonObject Commanded(BlendPlayer player, JsonElement command)
    {
        switch (command[0].GetString())
        {
            case "advance":
                return Stepped(player.Advance(Fixture.Number(command[1])));
            case "enter":
                return Entered(player, command);
            case "travel":
                player.Travel(command[1].GetString()!);
                return new JsonObject { ["place"] = Tree(player.Place.ToJson()) };
            case "set":
                player.Inputs[command[1].GetString()!] = Fixture.Number(command[2]);
                return new JsonObject
                {
                    ["inputs"] = new JsonObject(player.Inputs.Select(static one => KeyValuePair.Create<string, JsonNode?>(one.Key, Num(one.Value)))),
                };
            case "sample":
                return new JsonObject { ["frame"] = Framed(player.Sample()) };
            default:
                throw new InvalidOperationException($"no command {command[0]}");
        }
    }

    private static JsonObject Entered(BlendPlayer player, JsonElement command)
    {
        player.Enter(
            command[1].GetString()!,
            fade: Fixture.Number(command[2]),
            shape: FileNames.Named<Easing>(command[5].GetString()) ?? throw new InvalidOperationException("no shape"),
            inStep: command[3].GetBoolean(),
            fromPose: command[4].GetBoolean());
        return new JsonObject { ["place"] = Tree(player.Place.ToJson()) };
    }

    private static JsonObject Stepped(BlendStep step) => new()
    {
        ["place"] = Tree(step.Place.ToJson()),
        ["marks"] = Arr(step.Marks, static mark => new JsonArray(Num(mark.At), Text(mark.Name))),
        ["moved"] = Dumped(step.Moved),
        ["change"] = step.Change is { } change ? Tree(change.ToJson()) : null,
        ["frame"] = Framed(step.Frame),
    };
}
