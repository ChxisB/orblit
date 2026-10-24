using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Orblit.Ui.Avalonia.Tests;

public class BoxTests
{
    private static readonly UiTheme Vocabulary = new();

    private static Control Built(UiNode node) => new UiBuilder(new UiBuilderOptions()).Build(node);

    private static UiNode Block(string classes, params UiNode[] children) => new() { Type = "box", Classes = classes, Children = children };

    private static UiNode Chip(string classes) => Block($"w-12 h-6 {classes}");

    private static Color ColourOf(string name) => Color.FromUInt32(Vocabulary.Colour(name)!.Value.Argb);

    [Fact]
    public Task AnElementThatAsksForNoBoxIsNotWrapped() => Headless.Run(() =>
    {
        Assert.IsType<TextBlock>(Built(new UiNode { Type = "text", Text = "plain" }));
        Assert.IsType<TextBlock>(Built(Block("", new UiNode { Type = "text", Text = "alone" })));
    });

    [Fact]
    public Task MarginSizeColourBorderCornersAndOpacityAreOneBox() => Headless.Run(() =>
    {
        var box = Assert.IsType<Border>(Built(Block("m-2 w-24 h-6 bg-slate-800 border border-slate-500 rounded-lg opacity-50 clip")));

        Assert.Equal(new Thickness(8), box.Margin);
        Assert.Equal((96, 24), (box.Width, box.Height));
        Assert.Equal(ColourOf("slate-800"), Assert.IsAssignableFrom<ISolidColorBrush>(box.Background).Color);
        Assert.Equal(new Thickness(1), box.BorderThickness);
        Assert.Equal(ColourOf("slate-500"), Assert.IsAssignableFrom<ISolidColorBrush>(box.BorderBrush).Color);
        Assert.Equal(new CornerRadius(10), box.CornerRadius);
        Assert.Equal(0.5, box.Opacity);
        Assert.True(box.ClipToBounds);
    });

    [Fact]
    public Task ABorderWithNoColourIsAFaintWhite() => Headless.Run(() =>
    {
        var box = Assert.IsType<Border>(Built(Block("border")));
        Assert.Equal(Color.FromUInt32(0x33FFFFFF), Assert.IsAssignableFrom<ISolidColorBrush>(box.BorderBrush).Color);
    });

    [Fact]
    public Task AShadowDropsByAThirdOfItsBlur() => Headless.Run(() =>
    {
        var box = Assert.IsType<Border>(Built(Block("shadow-lg")));
        Assert.Equal(1, box.BoxShadow.Count);
        var shadow = box.BoxShadow[0];
        Assert.Equal((20, 7, Color.FromUInt32(0x40000000)), (shadow.Blur, shadow.OffsetY, shadow.Color));
    });

    [Fact]
    public Task SmallestAndLargestSizesAreKept() => Headless.Run(() =>
    {
        var box = Assert.IsType<Border>(Built(Block("min-w-8 max-w-48 min-h-4 max-h-40")));
        Assert.Equal((32, 192, 16, 160), (box.MinWidth, box.MaxWidth, box.MinHeight, box.MaxHeight));
    });

    [Fact]
    public Task ASizeOfFullMeansAsMuchAsThereIs() => Headless.Run(() =>
    {
        var window = Probe.Show(Block("col", Chip("w-full"), Chip("")));
        var (wide, narrow) = (Probe.All<FlexPanel>(window).Single().Children[0], Probe.All<FlexPanel>(window).Single().Children[1]);
        Assert.Equal(400, wide.Bounds.Width);
        Assert.Equal(48, narrow.Bounds.Width);
    });

    [Fact]
    public Task AChildThatGrowsTakesWhatTheOthersLeave() => Headless.Run(() =>
    {
        var window = Probe.Show(Block("row", Chip(""), Block("flex-1 h-6")));
        var row = Probe.All<FlexPanel>(window).Single();
        Assert.Equal(352, row.Children[1].Bounds.Width);
    });

    [Fact]
    public Task ASpacerPushesWhatFollowsToTheEnd() => Headless.Run(() =>
    {
        var window = Probe.Show(Block("row", Chip(""), new UiNode { Type = "spacer" }, Chip("")));
        var row = Probe.All<FlexPanel>(window).Single();
        Assert.Equal([0, 48, 352], row.Children.Select(child => child.Bounds.Left));
    });

    [Fact]
    public Task AStackPutsEachChildAgainstTheEdgesItNames() => Headless.Run(() =>
    {
        var window = Probe.Show(Block("stack", Chip("left-4 top-2"), Chip("right-0 bottom-0")));
        var stack = Probe.All<Grid>(window).Single();

        Assert.Equal(new Point(16, 8), stack.Children[0].Bounds.Position);
        Assert.Equal(new Point(352, 276), stack.Children[1].Bounds.Position);
    });

    [Fact]
    public Task AStackChildWithBothEdgesStretchesBetweenThem() => Headless.Run(() =>
    {
        var window = Probe.Show(Block("stack", Block("left-10 right-10 h-6")));
        var child = Probe.All<Grid>(window).Single().Children.Single();
        Assert.Equal((40, 320), (child.Bounds.Left, child.Bounds.Width));
    });

    [Fact]
    public Task ABoxWithNothingInItIsAnEmptyBorder() => Headless.Run(() =>
    {
        Assert.Null(Assert.IsType<Border>(Built(new UiNode { Type = "row" })).Child);
    });

    [Fact]
    public Task AScrollingBoxScrollsTheWayItWasToldAndNoOther() => Headless.Run(() =>
    {
        var across = Assert.IsType<ScrollViewer>(Built(Block("row scroll-x", Chip(""))));
        Assert.Equal((global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled), (across.HorizontalScrollBarVisibility, across.VerticalScrollBarVisibility));

        var down = Assert.IsType<ScrollViewer>(Built(Block("col scroll-y", Chip(""))));
        Assert.Equal((global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled, global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto), (down.HorizontalScrollBarVisibility, down.VerticalScrollBarVisibility));
    });
}
