using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Parity;
using Orblit.Scene;
using static Orblit.Motion.Tests.MotionParityTests;
using static Orblit.Parity.Dump;

namespace Orblit.Motion.Tests;

public class PlayParityTests
{
    private static readonly Fixture Expected = Fixture.Load("orblit_motion", "play");

    [Fact]
    public void PlayersStepAsTheFixtureSays() => Expected.Check("play", Played);

    [Fact]
    public void LapsFallOnClipTimeAsTheFixtureSays() => Expected.Check("timeAt", TimedAt);

    [Fact]
    public void PassesCarryTheBodyAndTheMarksAsTheFixtureSays() => Expected.Check("passOver", PassedOver);

    [Fact]
    public void LapsSyncAsTheFixtureSays() => Expected.Check("sync", Synced);

    [Fact]
    public void KeysReduceAsTheFixtureSays() => Expected.Check("reduce", Reduced);

    [Fact]
    public void RestSkeletonsPoseAsTheFixtureSays() => Expected.Check("rest", Rested);

    [Fact]
    public void FramesBecomeSceneChangesAsTheFixtureSays() => Expected.Check("scene", Scened);

    private static WhenDone? Ending(JsonElement name) => FileNames.Named<WhenDone>(name.GetString());

    private static JsonNode? Played(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var clip = Clip(input[0]);
        var speed = Fixture.Number(input[2]);
        var player = new ClipPlayer(clip, Ending(input[1]), speed);
        if (speed < 0)
        {
            player.Seek(clip.Duration);
        }

        player.Play();
        var seconds = input[3].EnumerateArray().Select(Fixture.Number).ToList();
        var steps = new JsonArray();
        for (var i = 0; i < seconds.Count; i++)
        {
            steps.Add(Stepped(player, seconds[i], i == seconds.Count - 1));
        }

        return steps;
    });

    private static JsonObject Stepped(ClipPlayer player, double seconds, bool last)
    {
        var step = player.Advance(seconds);
        var dumped = new JsonObject
        {
            ["marks"] = Marks(step.Marks),
            ["moved"] = Dumped(step.Moved),
            ["at"] = Num(player.At),
            ["playing"] = player.Playing,
            ["finished"] = player.Finished,
            ["released"] = player.Released,
        };
        if (last)
        {
            dumped["frame"] = Dumped(step.Frame);
        }

        return dumped;
    }

    private static JsonArray Marks(IReadOnlyList<Mark> marks) =>
        Arr(marks, mark => new JsonArray(Num(mark.At), Text(mark.Name)));

    private static JsonNode? TimedAt(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        return Num(ClipPass.TimeAt(Clip(input[0]), Ending(input[1])!.Value, Fixture.Number(input[2])));
    });

    private static JsonNode? PassedOver(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var passed = ClipPass.Over(
            Clip(input[0]),
            Ending(input[1])!.Value,
            Fixture.Number(input[2]),
            Fixture.Number(input[3]),
            input[4].GetBoolean());
        return new JsonObject { ["moved"] = Dumped(passed.Moved), ["marks"] = Marks(passed.Marks) };
    });

    private static JsonNode? Synced(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var names = input[1].EnumerateArray().Select(name => name.GetString()!).ToList();
        return Num(ClipSync.SyncedLap(Clip(input[0]), Fixture.Number(input[2]), names));
    });

    private static JsonNode? Reduced(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var reduced = KeyReducer.Reduce(Clip(input[0]), Fixture.Number(input[1]), Fixture.Number(input[2]));
        return Text(reduced.Encode());
    });

    private static JsonNode? Rested(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var skeleton = SkeletonOf(input[0]);
        var bones = new Dictionary<string, BoneLocal>();
        foreach (var bone in input[1].EnumerateObject())
        {
            bones[bone.Name] = new BoneLocal(
                position: Maybe(bone.Value[0], Vector),
                rotation: Maybe(bone.Value[1], Turn),
                scale: Maybe(bone.Value[2], Vector));
        }

        var each = Enumerable.Range(0, skeleton.Length).ToList();
        var first = input[0].GetProperty("names").EnumerateArray().Select(name => name.GetString()!).FirstOrDefault() ?? "";
        return new JsonObject
        {
            ["length"] = skeleton.Length,
            ["reach"] = Num(skeleton.Reach),
            ["index"] = new JsonArray(
                Position(skeleton.IndexOf(first)),
                Position(skeleton.IndexOf("upper")),
                Position(skeleton.IndexOf("nope"))),
            ["origins"] = Arr(each, i => Dumped(skeleton.OriginOf(i))),
            ["posed"] = Arr(skeleton.PosedOrigins(bones), v => Dumped(v)),
            ["positions"] = Arr(each, i => Dumped(skeleton.PositionOf(i))),
            ["rotations"] = Arr(each, i => Dumped(skeleton.RotationOf(i))),
            ["scales"] = Arr(each, i => Dumped(skeleton.ScaleOf(i))),
            ["worlds"] = Arr(each, i => Dumped(skeleton.WorldRotationOf(i))),
            ["parentWorlds"] = Arr(each, i => Dumped(skeleton.ParentWorldRotationOf(i))),
            ["parentBodies"] = Arr(each, i => Arr(Enumerable.Range(0, 16), at => Num(skeleton.ParentWorldOf(i)[at]))),
        };
    });

    private static RestSkeleton SkeletonOf(JsonElement spec)
    {
        var above = spec.GetProperty("above");
        return new RestSkeleton(
            [.. spec.GetProperty("names").EnumerateArray().Select(name => name.GetString()!)],
            [.. spec.GetProperty("parents").EnumerateArray().Select(parent => parent.GetInt32())],
            [.. spec.GetProperty("local").EnumerateArray().Select(Composed)],
            above.ValueKind == JsonValueKind.Null ? null : Composed(above));
    }

    private static Matrix4 Composed(JsonElement parts) => Matrix4.Compose(Vector(parts[0]), Turn(parts[1]), Vector(parts[2]));

    private static T? Maybe<T>(JsonElement part, Func<JsonElement, T> read)
        where T : class =>
        part.ValueKind == JsonValueKind.Null ? null : read(part);

    private static JsonValue? Position(int? at) => at is { } found ? JsonValue.Create(found) : null;

    private static JsonNode? Scened(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var document = SceneDocument.Decode(input[0].GetString()!).Document;
        var frame = Clip(input[2]).SampleAt(Fixture.Number(input[3]));
        var ops = ClipScene.OpsFor(frame, document, ClipScope.InScene(document, input[1].GetString()!));
        return Arr(ops, op => new JsonArray(Text(op.Id), Text(op.Type), Text(op.Field), Jsonish(op.From), Jsonish(op.To)));
    });

    // A scene value is a list of doubles as often as a list of objects, which Dump.Tree does not take.
    private static JsonNode? Jsonish(object? value) => value switch
    {
        IReadOnlyDictionary<string, object?> map => new JsonObject(
            map.Select(pair => KeyValuePair.Create(pair.Key, Jsonish(pair.Value)))),
        IEnumerable list and not string => new JsonArray([.. list.Cast<object?>().Select(Jsonish)]),
        _ => Tree(value),
    };
}
