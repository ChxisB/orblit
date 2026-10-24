using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Json;
using Orblit.Parity;

namespace Orblit.Ui.Tests;

public class CanvasParityTests
{
    private static readonly Fixture Golden = Fixture.Load("orblit_ui", "canvas");

    private static double Read(JsonElement owner, string name) => Fixture.Number(owner.GetProperty(name));

    private static UiCanvas CanvasOf(JsonElement c)
    {
        var spec = c.GetProperty("canvas");
        return new UiCanvas
        {
            Width = Read(spec, "width"),
            Height = Read(spec, "height"),
            Fit = CanvasFits.Named(spec.GetProperty("fit").GetString()),
            SafeArea = Read(spec, "safeArea"),
            Columns = spec.GetProperty("columns").GetInt32(),
            Gutter = Read(spec, "gutter"),
            MinScale = Read(spec, "minScale"),
            MaxScale = Read(spec, "maxScale"),
        };
    }

    private static JsonArray Edges((double Left, double Right) span) =>
        new(ParityJson.Number(span.Left), ParityJson.Number(span.Right));

    private static JsonNode Check(JsonElement c, Func<UiCanvas, JsonNode?> run) =>
        ParityJson.Attempt(() => run(CanvasOf(c)));

    // The fields a canvas holds, as the exporter lists them.
    private static JsonObject Fields(UiCanvas canvas) => new()
    {
        ["width"] = ParityJson.Number(canvas.Width),
        ["height"] = ParityJson.Number(canvas.Height),
        ["fit"] = canvas.Fit.Name(),
        ["label"] = canvas.Fit.Label(),
        ["safeArea"] = ParityJson.Number(canvas.SafeArea),
        ["columns"] = canvas.Columns,
        ["gutter"] = ParityJson.Number(canvas.Gutter),
        ["minScale"] = ParityJson.Number(canvas.MinScale),
        ["maxScale"] = ParityJson.Number(canvas.MaxScale),
        ["json"] = ParityJson.Tree(canvas.ToJson()),
    };

    [Fact]
    public void FluidScaleMatchesTheFixture() => Golden.Check("canvas-fluid", c =>
        Check(c, canvas => ParityJson.Number(canvas.FluidScale(Read(c, "width")))));

    [Fact]
    public void ColumnsMatchTheFixture() => Golden.Check("canvas-columns", c =>
        Check(c, canvas =>
        {
            var width = Read(c, "width");
            return new JsonObject
            {
                ["span"] = Edges(canvas.GridSpanAcross(width)),
                ["at"] = canvas.ColumnsAt(width),
                ["across"] = new JsonArray([.. canvas.ColumnsAcross(width).Select(column => (JsonNode?)Edges(column))]),
            };
        }));

    [Fact]
    public void ScaleMatchesTheFixture() => Golden.Check("canvas-scale", c =>
        Check(c, canvas => new JsonObject
        {
            ["scale"] = ParityJson.Number(canvas.ScaleFor(Read(c, "width"), Read(c, "height"))),
            ["aspect"] = ParityJson.Number(canvas.Aspect),
        }));

    [Fact]
    public void SnappingMatchesTheFixture() => Golden.Check("canvas-snap", c =>
        Check(c, canvas =>
            canvas.SnapAcross(Read(c, "x"), Read(c, "width"), Read(c, "within")) is { } edge
                ? ParityJson.Number(edge)
                : null));

    [Fact]
    public void ACanvasReadFromTextMatchesTheFixture() => Golden.Check("canvas-json", c =>
        ParityJson.Attempt(() => Fields(UiCanvas.FromJson(JsonText.Decode(c.GetProperty("json").GetString()!)))));

    [Fact]
    public void AFitNamedInAFileMatchesTheFixture() => Golden.Check("fit-named", c =>
        ParityJson.Attempt(() =>
        {
            var fit = CanvasFits.Named(c.GetProperty("name").GetString());
            return new JsonObject { ["name"] = fit.Name(), ["label"] = fit.Label() };
        }));
}
