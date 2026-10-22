using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Json;
using Orblit.Parity;
using static Orblit.Parity.Dump;

namespace Orblit.Scene.Tests;

public class SceneParityTests
{
    private static readonly Fixture Expected = Fixture.Load("orblit_scene", "scene");

    [Fact]
    public void ComponentsReadAndWriteAsTheFixtureSays() => Expected.Check("read", Read);

    [Fact]
    public void RotationsMatchTheFixture() => Expected.Check("rotation", Rotation);

    [Fact]
    public void DocumentsReadAndWriteAsTheFixtureSays() => Expected.Check("document", Document);

    [Fact]
    public void DiffsMatchTheFixture() => Expected.Check("diff", Diff);

    [Fact]
    public void OperationsReadAsTheFixtureSays() => Expected.Check("operations", Operations);

    [Fact]
    public void SingleEntitiesMigrateAsTheFixtureSays() => Expected.Check("entity", Entity);

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

    private static JsonNode? Document(JsonElement c) => Attempt(() =>
    {
        var load = SceneDocument.Decode(c.GetProperty("in")[0].GetString()!);
        return new JsonObject
        {
            ["problems"] = Arr(load.Problems, Text),
            ["text"] = Text(load.Document.Encode()),
        };
    });

    private static JsonNode? Diff(JsonElement c)
    {
        var input = c.GetProperty("in");
        var a = SceneDocument.Decode(input[0].GetString()!).Document;
        var b = SceneDocument.Decode(input[1].GetString()!).Document;
        var diff = SceneDiff.Between(a, b);
        var ops = diff.ToJson();
        return new JsonObject
        {
            ["ops"] = Tree(ops),
            ["forward"] = Text(diff.ApplyTo(a).Encode()),
            ["back"] = Text(diff.Inverse.ApplyTo(b).Encode()),
            ["reread"] = Text(SceneDiff.FromJson(ops).ApplyTo(a).Encode()),
        };
    }

    private static JsonNode? Operations(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var document = SceneDocument.Decode(input[0].GetString()!).Document;
        var diff = SceneDiff.FromJson(DartJson.Decode(input[1].GetRawText()));
        return new JsonObject
        {
            ["ops"] = Tree(diff.ToJson()),
            ["applied"] = Text(diff.ApplyTo(document).Encode()),
        };
    });

    private static JsonNode? Entity(JsonElement c) => Attempt(() =>
    {
        var input = c.GetProperty("in");
        var notes = new List<string>();
        var moved = SceneMigrations.Entity((JsonMap)DartJson.Decode(input[0].GetRawText())!, input[1].GetInt64(), notes);
        return new JsonObject { ["entity"] = Tree(moved), ["notes"] = Arr(notes, Text) };
    });
}
