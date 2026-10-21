using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Json;
using Orblit.Parity;
using static Orblit.Parity.Dump;

namespace Orblit.Scene.Tests;

public class SceneParityTests
{
    private static readonly Fixture Dart = Fixture.Load("orblit_scene", "scene");

    [Fact]
    public void ComponentsReadAndWriteLikeTheDartTwin() => Dart.Check("read", Read);

    [Fact]
    public void RotationsMatchTheDartTwin() => Dart.Check("rotation", Rotation);

    private static JsonNode? Read(JsonElement c)
    {
        var input = c.GetProperty("in");
        var type = input[0].GetString()!;
        var json = (JsonMap)DartJson.Decode(input[1].GetRawText())!;
        return Attempt(() => Tree(SceneComponents.Read(type, json).ToJson()));
    }

    private static JsonNode? Rotation(JsonElement c)
    {
        var input = c.GetProperty("in");
        var degrees = new Vector3(Fixture.Number(input[0]), Fixture.Number(input[1]), Fixture.Number(input[2]));
        var turn = TransformComponent.RotationOf(degrees);
        var back = TransformComponent.AnglesOf(turn);
        return new JsonObject
        {
            ["matrix"] = Arr(Enumerable.Range(0, 9).Select(i => turn[i]), v => Num(v)),
            ["angles"] = new JsonArray(Num(back.X), Num(back.Y), Num(back.Z)),
        };
    }
}
