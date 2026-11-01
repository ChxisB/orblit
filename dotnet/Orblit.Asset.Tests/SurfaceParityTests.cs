using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Parity;
using static Orblit.Asset.Tests.AssetCases;

namespace Orblit.Asset.Tests;

public class SurfaceParityTests
{
    private const string Pulse = "materials/pulse.oshader";

    private static readonly Fixture Golden = Fixture.Load("orblit_asset", "surface");

    [Fact]
    public void TranslateMatchesTheFixture() => Golden.Check("translate", c => Try(() =>
        TranslationOut(SurfaceTranslator.Translate(Text(c, 0), Text(c, 1), Text(c, 2), Reader(In(c, 3))))));

    [Fact]
    public void SplitMatchesTheFixture() => Golden.Check("split", c =>
        Dump.Attempt(() => Dump.Arr(SurfaceLiterals.SplitTopLevel(Text(c, 0)), Dump.Text)));

    [Fact]
    public void HintsMatchTheFixture() => Golden.Check("hints", c =>
        Try(() => new JsonObject(SurfaceLiterals.ParseHints(Text(c, 0), new SurfaceOrigin("f", 3))
            .Select(hint => KeyValuePair.Create(hint.Key, (JsonNode?)Dump.Arr(hint.Value, Dump.Text))))));

    [Fact]
    public void DefaultsMatchTheFixture() => Golden.Check("default", c => Try(() =>
    {
        var value = SurfaceLiterals.ParseDefault(SurfaceTypeTools.OfWord(Text(c, 0))!.Value, Text(c, 1), new SurfaceOrigin("f", 3));
        return new JsonObject { ["value"] = Dump.Tree(value), ["kinds"] = Kinds(value) };
    }));

    [Fact]
    public void IncludePathsMatchTheFixture() => Golden.Check("include_paths", c =>
        Try(() => Dump.Arr(SurfaceLines.IncludePaths(Text(c, 0), Text(c, 1)), Dump.Text)));

    [Fact]
    public void DiagnosticsMatchTheFixture() => Golden.Check("diagnostics", c => Try(() => Dump.Arr(
        SurfaceDiagnostics.Of(Text(c, 2), SurfaceTranslator.Translate("pulse", Pulse, Text(c, 0), Reader(In(c, 1)))),
        Dump.Text)));

    [Fact]
    public void OriginOfMatchesTheFixture() => Golden.Check("origin_of", c => Try(() => Dump.Text(
        SurfaceTranslator.Translate("pulse", Pulse, Text(c, 0), Reader(In(c, 1))).OriginOf(In(c, 2).GetInt64()).ToString())));

    [Fact]
    public void TypesMatchTheFixture() => Golden.Check("type", c => Dump.Attempt(() => SurfaceTypeTools.OfWord(Text(c, 0)) is { } type
        ? new JsonObject
        {
            ["index"] = (int)type,
            ["word"] = type.Word(),
            ["matType"] = type.MatType(),
            ["width"] = type.Width(),
        }
        : null));

    // A refusal is part of the answer. Any other exception is only that it threw.
    private static JsonObject Try(Func<JsonNode?> body) => Dump.Attempt(() =>
    {
        try
        {
            return body();
        }
        catch (SurfaceException e)
        {
            return new JsonObject
            {
                ["refused"] = new JsonObject
                {
                    ["file"] = e.Origin.File,
                    ["line"] = e.Origin.Line,
                    ["message"] = e.Reason,
                    ["text"] = e.Message,
                },
            };
        }
    });

    private static Func<string, string?> Reader(JsonElement files) =>
        path => files.TryGetProperty(path, out var text) ? text.GetString() : null;

    private static JsonObject TranslationOut(SurfaceTranslation t) => new()
    {
        ["material"] = t.Material,
        ["params"] = Dump.Arr(t.Params, ParamOut),
        ["origins"] = Dump.Arr(t.Origins, o => Dump.Text(o.ToString())),
        ["includes"] = Dump.Arr(t.Includes, Dump.Text),
    };

    private static JsonNode ParamOut(SurfaceParam p)
    {
        var json = (JsonObject)Dump.Tree(p.ToJson())!;
        json["line"] = p.Line;
        json["isColor"] = p.IsColor;
        json["kinds"] = Kinds(p.DefaultValue);
        return json;
    }

    // Which of the numbers were whole and which were fractions. A fixture holds 2 and 2.0 as one number.
    private static string Kinds(object? value) => value switch
    {
        null => string.Empty,
        bool => "b",
        long => "i",
        double => "d",
        IReadOnlyList<object?> list => string.Concat(list.Select(Kinds)),
        _ => "?",
    };
}
