using Orblit.Json;

namespace Orblit.Ui.Tests;

public class UiAddedTests
{
    [Fact]
    public void ATreeNestedPastTheReaderLimitReadsAsAMessageNotAThrow()
    {
        var node = UiNode.Decode(new string('[', JsonText.MaxDepth + 1) + new string(']', JsonText.MaxDepth + 1));

        Assert.Equal("text", node.Type);
        Assert.Equal("The interface could not be read: Nested too deep", node.Text);
    }

    [Fact]
    public void ATreeSurvivesBeingWrittenAndReadBack()
    {
        var source = """
            {"type":"column","class":"p-4","style":"gap: 8px","key":"root","props":{"textKey":"title","n":2},
             "children":[{"type":"text","text":"Hi"},{"type":"button","props":{"onPressed":"go"}}]}
            """;
        var tree = UiNode.Decode(source);

        var again = UiNode.Decode(JsonText.Encode(tree.ToJson()));

        Assert.Equal(JsonText.Encode(tree.ToJson()), JsonText.Encode(again.ToJson()));
        Assert.Equal("go", again.At([1])!.HandlerFor("onPressed"));
        Assert.Equal(["title"], again.TextKeys);
    }

    [Fact]
    public void UnplacingDropsAllFourSidesAndKeepsTheRest()
    {
        var node = new UiNode { Type = "box", Css = "left: 1px; padding: 4px; right: 2px; TOP: 3px; bottom: 4px" };

        Assert.Equal("padding: 4px", node.Unplaced.Css);
        Assert.Null(node.Unplaced.Placed);
    }

    [Fact]
    public void AScaledThemeKeepsThePaletteAndTheBreakpoints()
    {
        var theme = new UiTheme { Breakpoints = new UiBreakpoints(1, 2, 3, 4) };
        var scaled = theme.Scaled(3);

        Assert.Same(theme.Palette, scaled.Palette);
        Assert.Same(theme.Breakpoints, scaled.Breakpoints);
    }
}
