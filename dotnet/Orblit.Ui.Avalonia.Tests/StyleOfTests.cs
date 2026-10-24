namespace Orblit.Ui.Avalonia.Tests;

public class StyleOfTests
{
    [Fact]
    public void CssIsLaidOverTheClassesRatherThanReplacingThem()
    {
        var style = new UiBuilder(new UiBuilderOptions()).StyleOf(
            new UiNode { Type = "box", Classes = "p-4 bg-slate-800 rounded-lg", Css = "padding: 24px" });

        // The one thing the CSS said wins. Everything else the classes said survives.
        Assert.Equal(24, style.PaddingTop);
        Assert.NotNull(style.Background);
        Assert.Equal(10, style.Radius);
    }
}
