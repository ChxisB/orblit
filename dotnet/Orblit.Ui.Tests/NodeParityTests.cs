using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Parity;

namespace Orblit.Ui.Tests;

public class NodeParityTests
{
    private static readonly Fixture Golden = Fixture.Load("orblit_ui", "node");

    private static readonly UiNode Fresh = new() { Type = "button", Text = "X", Key = "fresh" };

    private static JsonNode? Json(UiNode? node) => node is null ? null : ParityJson.Tree(node.ToJson());

    private static List<int> Path(JsonElement c) => [.. c.GetProperty("path").EnumerateArray().Select(i => i.GetInt32())];

    private static JsonArray? Placed((double Left, double Top)? placed) =>
        placed is { } at ? new JsonArray(ParityJson.Number(at.Left), ParityJson.Number(at.Top)) : null;

    [Fact]
    public void DecodedTextMatchesTheFixture() => Golden.Check("decode", c =>
        ParityJson.Attempt(() => Json(UiNode.Decode(c.GetProperty("text").GetString()!))));

    [Fact]
    public void EditsMatchTheFixture() => Golden.Check("edit", c =>
    {
        var root = UiNode.Decode(c.GetProperty("tree").GetRawText());
        var path = Path(c);
        var index = c.GetProperty("index").ValueKind == JsonValueKind.Null ? 0 : c.GetProperty("index").GetInt32();
        return ParityJson.Attempt(() => Json(c.GetProperty("action").GetString() switch
        {
            "at" => root.At(path),
            "replace" => root.ReplaceAt(path, Fresh),
            "remove" => root.RemoveAt(path),
            "insert" => root.InsertAt(path, index, Fresh),
            "move" => root.MoveAt(path, index),
            var other => throw new InvalidDataException($"unknown action {other}"),
        }));
    });

    [Fact]
    public void WalksMatchTheFixture() => Golden.Check("walk", c =>
    {
        var root = UiNode.Decode(c.GetProperty("tree").GetRawText());
        return ParityJson.Attempt(() => new JsonObject
        {
            ["seen"] = new JsonArray([.. root.Walk().Select(found => (JsonNode?)new JsonObject
            {
                ["path"] = ParityJson.Path(found.Path),
                ["type"] = found.Node.Type,
                ["text"] = found.Node.Text,
            })]),
            ["textKeys"] = new JsonArray([.. root.TextKeys.Order(StringComparer.Ordinal).Select(k => (JsonNode?)JsonValue.Create(k))]),
        });
    });

    [Fact]
    public void PropsMatchTheFixture() => Golden.Check("props", c =>
    {
        var node = UiNode.Decode("{\"type\":\"button\",\"props\":" + c.GetProperty("props").GetString() + "}");
        return ParityJson.Attempt(() => new JsonObject
        {
            ["textKey"] = node.TextKey,
            ["textArgs"] = ParityJson.Tree(node.TextArgs),
            ["handlers"] = new JsonObject(new[] { "onPressed", "onChanged", "onMissing" }
                .Select(e => KeyValuePair.Create(e, (JsonNode?)node.HandlerFor(e)))),
        });
    });

    [Fact]
    public void PlacingMatchesTheFixture() => Golden.Check("place", c =>
    {
        var node = new UiNode { Type = "box", Css = c.GetProperty("css").GetString()! };
        var left = Fixture.Number(c.GetProperty("left"));
        var top = Fixture.Number(c.GetProperty("top"));
        var round = c.GetProperty("round").GetBoolean();
        return ParityJson.Attempt(() =>
        {
            var moved = node.PlaceAt(left, top, round);
            return new JsonObject { ["css"] = moved.Css, ["placed"] = Placed(moved.Placed) };
        });
    });

    [Fact]
    public void ReadingAPlaceMatchesTheFixture() => Golden.Check("placed", c =>
    {
        var node = new UiNode { Type = "box", Css = c.GetProperty("css").GetString()! };
        return ParityJson.Attempt(() => new JsonObject { ["placed"] = Placed(node.Placed), ["unplaced"] = node.Unplaced.Css });
    });
}
