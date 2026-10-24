using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Orblit.Ui.Avalonia.Tests;

public class DecoratorTests
{
    private static readonly UiNode Tree = new()
    {
        Type = "column",
        Classes = "p-4",
        Children =
        [
            new UiNode { Type = "text", Text = "Title" },
            new UiNode
            {
                Type = "row",
                Children = [new UiNode { Type = "button", Text = "One" }, new UiNode { Type = "button", Text = "Two" }],
            },
        ],
    };

    [Fact]
    public Task ADecoratorSeesEveryElementWithItsPath() => Headless.Run(() =>
    {
        Dictionary<string, IReadOnlyList<int>> seen = [];

        TestWindow.Show(new UiSurface
        {
            Description = Tree,
            Decorate = (node, path, built) =>
            {
                seen[node.Type + node.Text] = path;
                return built;
            },
        });

        Assert.Empty(seen["column"]);
        Assert.Equal([1, 1], seen["buttonTwo"]);
        Assert.Equal(5, seen.Count);
    });

    [Fact]
    public Task AWrapperDoesNotComeBetweenAStackAndItsPlacement() => Headless.Run(() =>
    {
        // Growing and placing are read by the parent that lays the child out, so they go on the
        // outermost control, which is the wrapper a host hands back.
        var window = TestWindow.Show(new UiSurface
        {
            Description = new UiNode
            {
                Type = "stack",
                Children =
                [
                    new UiNode { Type = "text", Css = "left: 10px; top: 20px", Text = "A" },
                    new UiNode { Type = "row", Children = [new UiNode { Type = "text", Css = "flex: 1", Text = "B" }] },
                ],
            },
            Decorate = (_, _, built) => new Border { Background = new SolidColorBrush(Color.FromArgb(0x11, 255, 255, 255)), Child = built },
        });

        var a = Probe.Words(window, "A");
        var wrapper = Assert.IsType<Border>(a.Parent);
        Assert.Equal(new Point(10, 20), wrapper.Bounds.Position);
        Assert.NotNull(Probe.Words(window, "B"));
    });

    [Fact]
    public Task WithoutOneNothingIsWrapped() => Headless.Run(() =>
    {
        // The hook is null in a game: the chrome is not stripped out, it was never built.
        var surface = new UiSurface { Description = Tree };
        var window = TestWindow.Show(surface);

        Assert.Equal("One", Probe.Words(window, "One").Text);
        var box = Assert.IsType<Border>(surface.Child);
        Assert.IsType<FlexPanel>(box.Child);
    });
}
