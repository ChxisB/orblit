using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Json;
using Orblit.Parity;

namespace Orblit.Remote.Tests;

public class RemoteParityTests
{
    private static readonly Fixture Diff = Fixture.Load("orblit_remote", "diff");

    private static JsonNode? Node(object? value) => JsonNode.Parse(JsonText.Encode(value));

    [Fact]
    public void TwoDocumentsDifferWhereDartSaysTheyDo() =>
        Diff.Check("diff", c =>
        {
            var found = JsonDiff.Find(
                JsonText.Decode(c.GetProperty("a").GetRawText()),
                JsonText.Decode(c.GetProperty("b").GetRawText()));
            return new JsonArray([.. found.Select(d => (JsonNode?)new JsonObject
            {
                ["path"] = d.Path,
                ["from"] = Node(d.From),
                ["to"] = Node(d.To),
            })]);
        });
}
