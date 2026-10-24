using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Parity;

namespace Orblit.Ui.Tests;

public class StyleParityTests
{
    private static readonly Fixture Golden = Fixture.Load("orblit_ui", "style");

    // Every property a style holds, by the name the fixture gives it. A field that is not set is left out.
    private static JsonObject StyleJson(UiStyle style)
    {
        var json = new JsonObject();
        foreach (var property in typeof(UiStyle).GetProperties())
        {
            if (property.GetValue(style) is { } value)
            {
                json[char.ToLowerInvariant(property.Name[0]) + property.Name[1..]] = Field(value);
            }
        }

        return json;
    }

    private static JsonNode Field(object value) => value switch
    {
        UiColour colour => JsonValue.Create((long)colour.Argb),
        double number => ParityJson.Number(number),
        long whole => ParityJson.Whole(whole),
        bool flag => JsonValue.Create(flag),
        string text => JsonValue.Create(text),
        _ => throw new InvalidDataException($"unexpected {value.GetType()}"),
    };

    private static double? Optional(JsonElement c, string name) =>
        c.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? Fixture.Number(value) : null;

    private static UiTheme ThemeOf(JsonElement c)
    {
        var theme = new UiTheme();
        if (c.TryGetProperty("bp", out var bp) && bp.ValueKind == JsonValueKind.Array)
        {
            var starts = bp.EnumerateArray().Select(Fixture.Number).ToArray();
            theme = new UiTheme { Breakpoints = new UiBreakpoints(starts[0], starts[1], starts[2], starts[3]) };
        }

        return Optional(c, "scale") is { } scale ? theme.Scaled(scale) : theme;
    }

    private static JsonArray Strings(IEnumerable<string> strings) => new JsonArray([.. strings.Select(s => (JsonNode?)JsonValue.Create(s))]);

    [Fact]
    public void ColoursMatchTheFixture() => Golden.Check("colour", c =>
        ParityJson.Attempt(() => UiColour.FromText(c.GetProperty("text").GetString()!) is { } colour ? JsonValue.Create((long)colour.Argb) : null));

    [Fact]
    public void ClassListsMatchTheFixture() => Golden.Check("classes", c =>
    {
        var utilities = new UiUtilities(ThemeOf(c));
        var classes = c.GetProperty("classes").GetString()!;
        return ParityJson.Attempt(() => new JsonObject
        {
            ["style"] = StyleJson(utilities.Parse(classes, Optional(c, "width"))),
            ["unknown"] = Strings(utilities.UnknownIn(classes)),
        });
    });

    [Fact]
    public void CssMatchesTheFixture() => Golden.Check("css", c =>
    {
        var css = new UiCss(ThemeOf(c));
        var source = c.GetProperty("css").GetString()!;
        return ParityJson.Attempt(() => new JsonObject
        {
            ["style"] = StyleJson(css.Parse(source)),
            ["unknown"] = Strings(css.UnknownIn(source)),
        });
    });

    [Fact]
    public void BreakpointsMatchTheFixture() => Golden.Check("breakpoints", c =>
    {
        var starts = c.GetProperty("bp").EnumerateArray().Select(Fixture.Number).ToArray();
        var points = new UiBreakpoints(starts[0], starts[1], starts[2], starts[3]);
        var width = Fixture.Number(c.GetProperty("width"));
        return ParityJson.Attempt(() => new JsonObject
        {
            ["active"] = Strings(points.ActiveAt(width)),
            ["label"] = points.LabelAt(width),
        });
    });

    [Fact]
    public void BreakpointStartsMatchTheFixture() => Golden.Check("starts", c =>
    {
        var points = new UiBreakpoints();
        var name = c.GetProperty("name").GetString()!;
        return ParityJson.Attempt(() => new JsonObject
        {
            ["start"] = points.StartOf(name) is { } start ? ParityJson.Number(start) : null,
            ["knows"] = points.Knows(name),
        });
    });

    private static JsonObject Table(IReadOnlyDictionary<string, double> table) =>
        new([.. table.Select(e => KeyValuePair.Create(e.Key, (JsonNode?)ParityJson.Number(e.Value)))]);

    [Fact]
    public void ScalingAThemeMatchesTheFixture() => Golden.Check("scaled", c =>
    {
        var theme = new UiTheme();
        var factor = Fixture.Number(c.GetProperty("factor"));
        return ParityJson.Attempt(() =>
        {
            var scaled = theme.Scaled(factor);
            return new JsonObject
            {
                ["same"] = ReferenceEquals(scaled, theme),
                ["step"] = ParityJson.Number(scaled.Step),
                ["text"] = Table(scaled.Text),
                ["radius"] = Table(scaled.Radius),
                ["palette"] = scaled.Palette.Count,
            };
        });
    });

    [Fact]
    public void TheDefaultPaletteMatchesTheFixture() => Golden.Check("palette", _ =>
        ParityJson.Attempt(() => new JsonObject
        {
            ["names"] = Strings(UiTheme.DefaultPalette.Keys),
            ["colours"] = new JsonArray([.. UiTheme.DefaultPalette.Values.Select(v => (JsonNode?)JsonValue.Create((long)v.Argb))]),
        }));

    [Fact]
    public void PaletteLookupsMatchTheFixture() => Golden.Check("palette-colour", c =>
        ParityJson.Attempt(() => new UiTheme().Colour(c.GetProperty("name").GetString()!) is { } colour ? JsonValue.Create((long)colour.Argb) : null));
}
