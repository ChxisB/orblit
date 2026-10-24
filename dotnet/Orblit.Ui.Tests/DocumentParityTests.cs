using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Parity;

namespace Orblit.Ui.Tests;

public class DocumentParityTests
{
    private static readonly Fixture Golden = Fixture.Load("orblit_ui", "document");

    private static double Read(JsonElement owner, string name) => Fixture.Number(owner.GetProperty(name));

    private static UiCanvas CanvasOf(JsonElement spec) => new()
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

    private static JsonObject Canvas(UiCanvas canvas) => new()
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

    private static JsonObject Fields(UiDocument document) => new()
    {
        ["name"] = ParityJson.Tree(document.Name),
        ["canvas"] = Canvas(document.Canvas),
        ["root"] = ParityJson.Tree(document.Root.ToJson()),
    };

    [Fact]
    public void ReadingMatchesTheFixture() => Golden.Check("read", c =>
        ParityJson.Attempt(() => UiDocument.Read(c.GetProperty("text").GetString()!) is { } document ? Fields(document) : null));

    [Fact]
    public void ReadingThenWritingMatchesTheFixture() => Golden.Check("read-text", c =>
        ParityJson.Attempt(() => UiDocument.Read(c.GetProperty("text").GetString()!)?.ToText() is { } text ? JsonValue.Create(text) : null));

    [Fact]
    public void ANewOneMatchesTheFixture() => Golden.Check("blank", c =>
        ParityJson.Attempt(() =>
        {
            var made = UiDocument.Blank(c.GetProperty("name").GetString()!);
            var fields = Fields(made);
            fields["text"] = made.ToText();
            return fields;
        }));

    [Fact]
    public void WritingMatchesTheFixture() => Golden.Check("write", c =>
        ParityJson.Attempt(() => JsonValue.Create(new UiDocument
        {
            Name = c.GetProperty("name").GetString()!,
            Canvas = CanvasOf(c.GetProperty("canvas")),
            Root = UiNode.Decode(c.GetProperty("root").GetString()!),
        }.ToText())));
}
