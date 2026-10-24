using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Orblit.Ui.Avalonia.Tests;

public class BuilderTests
{
    private static UiNode Text(string words, string classes = "") => new() { Type = "text", Text = words, Classes = classes };

    [Fact]
    public Task TextArrivesAsText() => Headless.Run(() =>
    {
        var window = Probe.Show(Text("Health"));
        Assert.Equal("Health", Probe.Words(window, "Health").Text);
    });

    [Fact]
    public Task ARowIsHorizontalAndAColumnIsNot() => Headless.Run(() =>
    {
        UiNode[] pair = [Text("a"), Text("b")];

        var row = Probe.Show(new UiNode { Type = "row", Children = pair });
        Assert.Equal(Orientation.Horizontal, Assert.Single(Probe.All<FlexPanel>(row)).Orientation);

        var column = Probe.Show(new UiNode { Type = "column", Children = pair });
        Assert.Equal(Orientation.Vertical, Assert.Single(Probe.All<FlexPanel>(column)).Orientation);
    });

    [Fact]
    public Task AGapGoesBetweenChildrenNotAroundThem() => Headless.Run(() =>
    {
        // Boxes of a fixed width, so the positions are whole numbers whatever the font is.
        var block = new UiNode { Type = "box", Css = "width: 30px; height: 10px" };
        var window = Probe.Show(new UiNode { Type = "row", Classes = "gap-4", Children = [block, block, block] });

        var panel = Assert.Single(Probe.All<FlexPanel>(window));
        Assert.Equal(16, panel.Gap);
        Assert.Equal([0, 46, 92], panel.Children.Select(child => child.Bounds.Left));
    });

    [Fact]
    public Task AClassListReachesTheControlsItDescribes() => Headless.Run(() =>
    {
        var surface = new UiSurface
        {
            Description = new UiNode { Type = "box", Classes = "p-4 bg-slate-800 rounded-lg", Children = [Text("inside")] },
        };
        TestWindow.Show(surface);

        var box = Assert.IsType<Border>(surface.Child);
        Assert.Equal(new Thickness(16), box.Padding);
        Assert.Equal(new CornerRadius(10), box.CornerRadius);
        Assert.Equal(Color.FromUInt32(new UiTheme().Colour("slate-800")!.Value.Argb), Assert.IsAssignableFrom<ISolidColorBrush>(box.Background).Color);
    });

    [Fact]
    public Task LiningUpOnTheBaselineDoesNotNeedToBeToldTwice() => Headless.Run(() =>
    {
        // A script asking for a baseline should not be the thing that takes the frame down.
        var window = Probe.Show(new UiNode { Type = "row", Classes = "items-baseline", Children = [Text("Score"), Text("1840", "text-2xl")] });
        Assert.Equal(2, Assert.Single(Probe.All<FlexPanel>(window)).Children.Count);
    });

    [Fact]
    public Task NothingInADescriptionCanThrowAFrameAway() => Headless.Run(() =>
    {
        // Half written trees are the normal case: script is reloaded on save.
        var window = Probe.Show(UiNode.Decode("""{"type": "row", "children": [null, 3, {}]}"""));
        Assert.Equal(3, Assert.Single(Probe.All<FlexPanel>(window)).Children.Count);
    });

    [Fact]
    public Task AButtonSaysWhichHandlerWasPressed() => Headless.Run(() =>
    {
        List<string> fired = [];
        var window = Probe.Show(
            new UiNode { Type = "button", Text = "Start", Props = new Dictionary<string, object?> { ["onPressed"] = "startGame" } },
            (handler, _) => fired.Add(handler));

        Probe.Click(window, Probe.Words(window, "Start"));
        Assert.Equal(["startGame"], fired);
    });

    [Fact]
    public Task AnythingCanBeTappedNotOnlyButtons() => Headless.Run(() =>
    {
        List<string> fired = [];
        var window = Probe.Show(
            new UiNode
            {
                Type = "box",
                Classes = "p-4",
                Props = new Dictionary<string, object?> { ["onTap"] = "closePanel" },
                Children = [Text("x")],
            },
            (handler, _) => fired.Add(handler));

        Probe.Click(window, Probe.Words(window, "x"));
        Assert.Equal(["closePanel"], fired);
    });

    [Fact]
    public Task AHandlerNobodySuppliedIsNotAPress() => Headless.Run(() =>
    {
        List<string> fired = [];
        var window = Probe.Show(new UiNode { Type = "button", Text = "Quiet" }, (handler, _) => fired.Add(handler));

        Probe.Click(window, Probe.Words(window, "Quiet"));
        Assert.Empty(fired);
    });
}
