using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Parity;
using static Orblit.Motion.Tests.MotionParityTests;
using static Orblit.Parity.Dump;

namespace Orblit.Motion.Tests;

public class NamesParityTests
{
    private static readonly Fixture Expected = Fixture.Load("orblit_motion", "retarget_names");

    // The fixture's one library case holds every skeleton and pose the other cases name.
    private static readonly JsonElement Library = Expected.Cases.Single(c => c.GetProperty("op").GetString() == "library").GetProperty("expect");

    // Built once, since the cases name the same few hundred skeletons over and over.
    private static readonly Dictionary<string, RestSkeleton> Skeletons = Library.GetProperty("skeletons").EnumerateObject()
        .ToDictionary(static skeleton => skeleton.Name, static skeleton => SkeletonOf(skeleton.Value));

    [Fact]
    public void BoneNamesCleanAsTheFixtureSays() => Expected.Check("clean", Cleaned);

    [Fact]
    public void BonesMatchByNameAsTheFixtureSays() => Expected.Check("match", Matched);

    [Fact]
    public void HumanoidBonesAreFoundAsTheFixtureSays() => Expected.Check("humanoid", Found);

    [Fact]
    public void BonesMatchByHumanoidSlotAsTheFixtureSays() => Expected.Check("matchHumanoid", MatchedBySlot);

    [Fact]
    public void LimbsReachAsTheFixtureSays() => Expected.Check("reach", Reached);

    private static RestSkeleton SkeletonOf(JsonElement spec) => new(
        [.. spec.GetProperty("names").EnumerateArray().Select(static one => one.GetString()!)],
        [.. spec.GetProperty("parents").EnumerateArray().Select(static one => one.GetInt32())],
        [.. spec.GetProperty("local").EnumerateArray().Select(Composed)],
        spec.GetProperty("above") is { ValueKind: not JsonValueKind.Null } above ? Composed(above) : null);

    private static Matrix4 Composed(JsonElement parts) => Matrix4.Compose(Vector(parts[0]), Turn(parts[1]), Vector(parts[2]));

    private static RestSkeleton Named(JsonElement name) => Skeletons[name.GetString()!];

    private static Vector3? MaybeVector(JsonElement part) => part.ValueKind == JsonValueKind.Null ? null : Vector(part);

    private static JsonArray Pairs(OrderedDictionary<string, string> pairs) =>
        Arr(pairs, static pair => new JsonArray(Text(pair.Key), Text(pair.Value)));

    private static JsonNode? Cleaned(JsonElement c) => Attempt(() =>
    {
        var (name, side) = BoneNames.CleanName(c.GetProperty("in")[0].GetString()!);
        return new JsonObject { ["base"] = Text(name), ["side"] = side is null ? null : Text(side) };
    });

    private static JsonNode? Matched(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        return Pairs(BoneNames.Match(Named(input[0]), Named(input[1])));
    });

    private static JsonNode? Found(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var found = HumanoidMap.Of(Named(input[0]), MaybeVector(input[1]), MaybeVector(input[2]));
        return new JsonObject
        {
            ["bones"] = Arr(found.Bones, static pair => new JsonArray(Text(SlotName(pair.Key)), Text(pair.Value))),
            ["problems"] = Arr(found.Problems, static problem => Text(problem)),
        };
    });

    private static JsonNode? MatchedBySlot(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        return Pairs(HumanoidMap.Match(Named(input[0]), Named(input[1])));
    });

    private static JsonNode? Reached(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var reach = LimbReach.Of(
            Named(input[0]),
            PoseOf(input[1].GetString()!),
            input[2].GetString()!,
            input[3].GetString()!,
            input[4].GetString()!,
            Vector(input[5]),
            MaybeVector(input[6]));
        return new JsonObject
        {
            ["root"] = Dumped(reach.Root),
            ["mid"] = Dumped(reach.Mid),
            ["end"] = Dumped(reach.End),
            ["reachable"] = reach.Reachable,
        };
    });

    private static Dictionary<string, BoneLocal> PoseOf(string name) => Library.GetProperty("poses").GetProperty(name).EnumerateObject()
        .ToDictionary(
            static bone => bone.Name,
            static bone => new BoneLocal(
                MaybeVector(bone.Value[0]),
                bone.Value[1].ValueKind == JsonValueKind.Null ? null : Turn(bone.Value[1]),
                MaybeVector(bone.Value[2])));

    // The fixture names a slot as it is written in a file: the enum's name, first letter lower case.
    private static string SlotName(HumanoidBone slot)
    {
        var name = slot.ToString();
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
