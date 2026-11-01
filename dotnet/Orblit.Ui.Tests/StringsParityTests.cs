using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Json;
using Orblit.Parity;

namespace Orblit.Ui.Tests;

public class StringsParityTests
{
    private static readonly Fixture Golden = Fixture.Load("orblit_ui", "strings");

    // A count past 2^53 travels as text.
    private static long CountOf(JsonElement c)
    {
        var count = c.GetProperty("count");
        return count.ValueKind == JsonValueKind.String ? long.Parse(count.GetString()!) : count.GetInt64();
    }

    private static JsonArray Words(IEnumerable<string> words) => new([.. words.Select(word => (JsonNode?)word)]);

    private static string Text(JsonElement step, int index) => step[index].GetString()!;

    private static IReadOnlyDictionary<string, object?>? ArgsOf(JsonElement step)
    {
        var text = step.GetArrayLength() > 2 && step[2].ValueKind == JsonValueKind.String ? step[2].GetString() : null;
        return text is null ? null : (IReadOnlyDictionary<string, object?>)JsonText.Decode(text)!;
    }

    private static Dictionary<string, string> TableOf(JsonElement step) =>
        ((IReadOnlyDictionary<string, object?>)JsonText.Decode(Text(step, 2))!)
            .ToDictionary(entry => entry.Key, entry => (string)entry.Value!);

    private static JsonObject Gaps(UiStrings strings, JsonElement step) =>
        new([.. strings.Gaps(step[1].EnumerateArray().Select(key => key.GetString()!))
            .Select(entry => KeyValuePair.Create(entry.Key, (JsonNode?)Words(entry.Value)))]);

    // One verb of a script. A verb that changes state answers null.
    private static readonly Dictionary<string, Func<UiStrings, JsonElement, JsonNode?>> Verbs = new()
    {
        ["add"] = (strings, step) => Do(() => strings.Add(Text(step, 1), TableOf(step))),
        ["addJson"] = (strings, step) => Do(() => strings.AddJson(Text(step, 1), Text(step, 2))),
        ["use"] = (strings, step) => Do(() => strings.Use(Text(step, 1))),
        ["clear"] = (strings, _) => Do(strings.Clear),
        ["text"] = (strings, step) => strings.Text(Text(step, 1), ArgsOf(step)),
        ["find"] = (strings, step) => strings.Find(Text(step, 1), ArgsOf(step)),
        ["chain"] = (strings, step) => Words(strings.Chain(Text(step, 1))),
        ["gaps"] = Gaps,
        ["locale"] = (strings, _) => strings.Locale,
        ["locales"] = (strings, _) => Words(strings.Locales),
    };

    private static JsonNode? Do(Action change)
    {
        change();
        return null;
    }

    private static JsonNode? Run(UiStrings strings, JsonElement step) =>
        Verbs.TryGetValue(Text(step, 0), out var verb)
            ? verb(strings, step)
            : throw new InvalidDataException($"unknown step {step}");

    [Fact]
    public void PluralFormsMatchTheFixture() => Golden.Check("plural", c =>
        ParityJson.Attempt(() => JsonValue.Create(UiStrings.PluralForm(c.GetProperty("locale").GetString()!, CountOf(c)))));

    [Fact]
    public void TagsMatchTheFixture() => Golden.Check("tag", c =>
        ParityJson.Attempt(() => JsonValue.Create(UiStrings.TagOf(c.GetProperty("tag").GetString()!))));

    [Fact]
    public void ChainsMatchTheFixture() => Golden.Check("chain", c =>
        ParityJson.Attempt(() => Words(new UiStrings(fallback: c.GetProperty("fallback").GetString()!).Chain(c.GetProperty("locale").GetString()!))));

    [Fact]
    public void ScriptsOfTablesAndLookupsMatchTheFixture() => Golden.Check("script", c =>
    {
        var heard = 0;
        UiStrings? made = null;
        var init = ParityJson.Attempt(() =>
        {
            made = new UiStrings(c.GetProperty("locale").GetString()!, c.GetProperty("fallback").GetString()!);
            made.Changed += () => heard++;
            return null;
        });
        var results = new JsonArray();
        if (made is not null)
        {
            foreach (var step in c.GetProperty("steps").EnumerateArray())
            {
                results.Add(ParityJson.Attempt(() => Run(made, step)));
            }
        }

        return new JsonObject
        {
            ["init"] = init,
            ["results"] = results,
            ["heard"] = heard,
            ["locale"] = made?.Locale,
            ["locales"] = made is null ? null : Words(made.Locales),
        };
    });
}
