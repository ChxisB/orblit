namespace Orblit.Ui.Tests;

public class DocumentTests
{
    [Fact]
    public void ANewOneIsResponsiveFromTheFirstFrame() =>
        Assert.Equal(CanvasFit.Responsive, UiDocument.Blank("Menu").Canvas.Fit);

    [Fact]
    public void SurvivesARoundTrip()
    {
        var document = new UiDocument
        {
            Name = "Main menu",
            Canvas = new UiCanvas { Width = 1280, Height = 720, Fit = CanvasFit.Width },
            Root = new UiNode
            {
                Type = "column",
                Children = [new UiNode { Type = "text", Text = "Title" }, new UiNode { Type = "button", Text = "Two" }],
            },
        };

        var back = UiDocument.Read(document.ToText())!;

        Assert.Equal("Main menu", back.Name);
        Assert.Equal(1280, back.Canvas.Width);
        Assert.Equal(CanvasFit.Width, back.Canvas.Fit);
        Assert.Equal("Two", back.Root.At([1])!.Text);
    }

    [Fact]
    public void IsNotReadFromSomethingThatIsNotOne()
    {
        Assert.Null(UiDocument.Read("nonsense"));
        Assert.Null(UiDocument.Read("""{"kind":"orblit.data"}"""));
    }

    [Fact]
    public void ANewOneHasSomethingOnIt()
    {
        var made = UiDocument.Blank("menu");

        Assert.NotEmpty(made.Root.Children);
        Assert.Equal("menu", made.Name);
    }

    [Fact]
    public void ANewCanvasIsFreePositioned()
    {
        var made = UiDocument.Blank("menu");

        Assert.Equal("stack", made.Root.Type);
        Assert.NotNull(made.Root.Children[0].Placed);
    }
}
