using System.Text.Json.Nodes;

namespace Orblit.Ui.Tests;

// The shape the fixture exporter writes for a case: its value, or that it threw.
internal static class ParityJson
{
    public static JsonNode Attempt(Func<JsonNode?> body)
    {
        try
        {
            return new JsonObject { ["value"] = body() };
        }
        catch (Exception error) when (error is ArgumentException or FormatException)
        {
            return new JsonObject { ["throws"] = true };
        }
    }
}
