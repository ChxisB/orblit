using System.Text;
using System.Text.Json.Nodes;

namespace Orblit.Ui.Tests;

// The shapes the fixture exporter writes. A double that JSON cannot hold travels as text, and so
// does a whole number a reader would hand back as a double.
internal static class ParityJson
{
    private const long ExactLimit = 9007199254740992;

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

    public static JsonNode Number(double value) =>
        double.IsFinite(value)
            ? JsonValue.Create(value)
            : JsonValue.Create(double.IsNaN(value) ? "NaN" : value > 0 ? "Infinity" : "-Infinity");

    public static JsonNode Whole(long value) =>
        value is > ExactLimit or < -ExactLimit ? JsonValue.Create(value.ToString()) : JsonValue.Create(value);

    public static JsonNode? Tree(object? value) => value switch
    {
        null => null,
        bool flag => JsonValue.Create(flag),
        int whole => Whole(whole),
        long whole => Whole(whole),
        double fraction => Number(fraction),
        string text => Text(text),
        IReadOnlyDictionary<string, object?> map => Map(map),
        IReadOnlyList<object?> list => new JsonArray([.. list.Select(Tree)]),
        _ => throw new InvalidDataException($"unexpected {value.GetType()}"),
    };

    public static JsonArray Path(IEnumerable<int> path) => new([.. path.Select(i => (JsonNode?)JsonValue.Create(i))]);

    // A string with a lone surrogate cannot be compared as JSON text, so it travels as its UTF-16 code
    // units. UTF-8 turns a lone surrogate into U+FFFD, which is how one is told apart.
    private static JsonNode Text(string text) =>
        Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(text)) == text
            ? JsonValue.Create(text)
            : new JsonObject { ["units"] = new JsonArray([.. text.Select(c => (JsonNode?)JsonValue.Create((int)c))]) };

    private static JsonObject Map(IReadOnlyDictionary<string, object?> map)
    {
        var json = new JsonObject();
        foreach (var (key, value) in map)
        {
            json[key] = Tree(value);
        }

        return json;
    }
}
