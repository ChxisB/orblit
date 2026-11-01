using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Json;
using Orblit.Parity;
using static Orblit.Motion.Tests.MotionParityTests;
using static Orblit.Parity.Dump;

namespace Orblit.Motion.Tests;

public class ImportParityTests
{
    private static readonly Fixture Expected = Fixture.Load("orblit_motion", "import");

    [Fact]
    public void ClipsImportAsTheFixtureSays() => Expected.Check("clips", Clips);

    [Fact]
    public void SkeletonsImportAsTheFixtureSays() => Expected.Check("skeletons", Skeletons);

    private static byte[] Bytes(JsonElement text) => Convert.FromBase64String(text.GetString()!);

    private static Dictionary<string, byte[]> Files(JsonElement files) =>
        files.EnumerateObject().ToDictionary(static file => file.Name, static file => Bytes(file.Value));

    private static JsonNode? Clips(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var result = GltfImport.Clips(Bytes(input[0]), Files(input[1]));
        return new JsonObject
        {
            ["clips"] = Arr(result.Clips, static clip => Tree(JsonText.Decode(clip.Encode()))),
            ["problems"] = Arr(result.Problems, Text),
        };
    });

    private static JsonNode? Skeletons(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        return Arr(GltfImport.Skeletons(Bytes(input[0]), Files(input[1])), Standing);
    });

    private static JsonNode? Standing(RestSkeleton skeleton)
    {
        var each = Enumerable.Range(0, skeleton.Length).ToList();
        return new JsonObject
        {
            ["names"] = Arr(skeleton.Names, Text),
            ["parents"] = Arr(skeleton.Parents, static parent => Big(parent)),
            ["reach"] = Num(skeleton.Reach),
            ["positions"] = Arr(each, i => Dumped(skeleton.PositionOf(i))),
            ["rotations"] = Arr(each, i => Dumped(skeleton.RotationOf(i))),
            ["scales"] = Arr(each, i => Dumped(skeleton.ScaleOf(i))),
            ["parentBodies"] = Arr(each, i => Arr(Enumerable.Range(0, 16), at => Num(skeleton.ParentWorldOf(i)[at]))),
        };
    }
}
