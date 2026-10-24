using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace Orblit.Ui.Avalonia.Tests;

public class SurfaceTests
{
    private static readonly UiNode Card = new()
    {
        Type = "box",
        Classes = "col md:row gap-2",
        Children =
        [
            new UiNode { Type = "text", Classes = "text-base md:text-2xl", Text = "Play" },
            new UiNode { Type = "text", Text = "Continue" },
        ],
    };

    // The surface goes in a box of a given width, in a window with room for the widest case, so the
    // window is never the thing deciding the layout.
    private static (Window Window, Border Slot) Seat(UiSurface surface, double width)
    {
        var slot = new Border { Width = width, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Child = surface };
        return (Headless.Show(slot, 1400, 600), slot);
    }

    [Fact]
    public Task MeasuresItselfAndLaysOutForWhatItFound() => Headless.Run(() =>
    {
        // Nobody wired a width in. The surface asked. On a phone the base size, shrunk by the fluid
        // floor. On a laptop the md size, still shrunk, since 1200 is well under the 1920 it was drawn for.
        var surface = new UiSurface { Description = Card, Canvas = new UiCanvas { Fit = CanvasFit.Responsive } };
        var (window, slot) = Seat(surface, 390);
        Assert.Equal(14 * 0.8, Probe.Words(window, "Play").FontSize, 10);

        slot.Width = 1200;
        window.UpdateLayout();
        Assert.Equal(23 * 0.8, Probe.Words(window, "Play").FontSize, 10);
    });

    [Fact]
    public Task TheDirectionChangesWithTheWidth() => Headless.Run(() =>
    {
        var (window, slot) = Seat(new UiSurface { Description = Card }, 390);
        Assert.Equal(Orientation.Vertical, Assert.Single(Probe.All<FlexPanel>(window)).Orientation);

        slot.Width = 1200;
        window.UpdateLayout();
        Assert.Equal(Orientation.Horizontal, Assert.Single(Probe.All<FlexPanel>(window)).Orientation);
    });

    [Fact]
    public Task AToldWidthBeatsAMeasuredOne() => Headless.Run(() =>
    {
        // The editor shows a phone inside a panel: the width that decides the layout is the phone's.
        var (window, _) = Seat(new UiSurface { Description = Card, KnownWidth = 390 }, 1200);
        Assert.DoesNotContain(Probe.All<FlexPanel>(window), panel => panel.Orientation == Orientation.Horizontal);
    });

    [Fact]
    public Task AnUnboundedWidthFallsBackToTheWindow() => Headless.Run(() =>
    {
        // A surface inside a row that scrolls is offered no width. The window is the honest answer.
        var scroller = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Content = new UiSurface { Description = Card } };
        var window = Headless.Show(scroller, 1400, 600);
        Assert.Equal(Orientation.Horizontal, Assert.Single(Probe.All<FlexPanel>(window)).Orientation);
    });

    [Fact]
    public Task AnotherDescriptionIsBuiltAgain() => Headless.Run(() =>
    {
        var surface = new UiSurface { Description = new UiNode { Type = "text", Text = "One" } };
        var window = Headless.Show(surface);
        Assert.Equal("One", Probe.Words(window, "One").Text);

        surface.Description = new UiNode { Type = "text", Text = "Two" };
        window.UpdateLayout();
        Assert.Equal("Two", Probe.Words(window, "Two").Text);
        Assert.DoesNotContain(Probe.All<TextBlock>(window), block => block.Text == "One");
    });

    [Fact]
    public Task WithNoDescriptionItShowsNothingAndDoesNotThrow() => Headless.Run(() =>
    {
        var surface = new UiSurface();
        Headless.Show(surface);
        Assert.Null(surface.Child);
    });
}
