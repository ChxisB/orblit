using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Orblit.Filament;
using Orblit.Sprite;
using Orblit.VectorMath;

namespace Orblit.Stage.Avalonia.Tests;

public sealed class ControlPanelTests
{
    private static Border Fill(uint colour) => new() { Background = new SolidColorBrush(Color.FromUInt32(colour)) };

    // The first frame lays a new panel out and the second lets a pointer find what is in it.
    private static void Settle()
    {
        Headless.Render();
        Headless.Render();
    }

    private static (PanelHost Host, ControlPanel Panel) Open(Control content, Size size, double scale = 1)
    {
        var host = new PanelHost();
        var panel = host.Open(content, size, scale);
        Settle();
        return (host, panel);
    }

    private static Button Pad(Action? clicked = null, double width = double.NaN, double height = double.NaN)
    {
        var button = new Button
        {
            Width = width,
            Height = height,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        if (width is double.NaN)
        {
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
            button.VerticalAlignment = VerticalAlignment.Stretch;
        }

        button.Click += (_, _) => clicked?.Invoke();
        return button;
    }

    private static byte[] PixelOf(DecodedPng picture, int x, int y) => picture.Pixels[(((y * (int)picture.Width) + x) * 4)..][..4];

    private static void Tap(ControlPanel panel, double x, double y)
    {
        panel.Send(new PanelPointer(PointerPhase.Down, 1, new Point(x, y)));
        panel.Send(new PanelPointer(PointerPhase.Up, 1, new Point(x, y)));
    }

    [Fact]
    public Task ThePictureIsAsBigAsTheSizeTimesTheScale() => Headless.Run(() =>
    {
        var (host, panel) = Open(Fill(0xFFFF0000), new Size(64, 32), scale: 2);

        var picture = PngCodec.Decode(panel.Paint());

        Assert.Equal(128, picture.Width);
        Assert.Equal(64, picture.Height);
        Assert.Equal([255, 0, 0, 255], PixelOf(picture, 10, 10));
        Assert.Equal([255, 0, 0, 255], PixelOf(picture, 127, 63));
        host.Dispose();
    });

    [Fact]
    public Task AChangeMarksItOutOfDateUntilItIsDrawnAgain() => Headless.Run(() =>
    {
        var fill = Fill(0xFFFF0000);
        var (host, panel) = Open(fill, new Size(8, 8));
        Assert.True(panel.IsDirty);

        panel.Paint();
        Assert.False(panel.IsDirty);

        fill.Background = new SolidColorBrush(Color.FromUInt32(0xFF0000FF));
        panel.Invalidate();
        Assert.True(panel.IsDirty);

        Assert.Equal([0, 0, 255, 255], PixelOf(PngCodec.Decode(panel.Paint()), 4, 4));
        Assert.False(panel.IsDirty);
        host.Dispose();
    });

    [Fact]
    public Task APanelThatNothingChangedStaysDrawn() => Headless.Run(() =>
    {
        var (host, panel) = Open(Fill(0xFFFF0000), new Size(8, 8));
        panel.Paint();

        Headless.Wait(100);

        Assert.False(panel.IsDirty);
        host.Dispose();
    });

    [Fact]
    public Task ALayoutThatRanMarksItOutOfDate() => Headless.Run(() =>
    {
        var words = new TextBlock { Text = "one" };
        var (host, panel) = Open(words, new Size(80, 20));
        panel.Paint();

        words.Text = "a good deal longer";
        Headless.Render();

        Assert.True(panel.IsDirty);
        host.Dispose();
    });

    [Fact]
    public Task APressAndReleaseOnAButtonIsItsClick() => Headless.Run(() =>
    {
        var clicks = 0;
        var (host, panel) = Open(Pad(() => clicks++), new Size(100, 50));

        panel.Send(new PanelPointer(PointerPhase.Down, 1, new Point(50, 25)));
        Assert.Equal(0, clicks);
        panel.Send(new PanelPointer(PointerPhase.Up, 1, new Point(50, 25)));

        Assert.Equal(1, clicks);
        host.Dispose();
    });

    [Fact]
    public Task APressThatLandsOnNothingDoesNothing() => Headless.Run(() =>
    {
        var clicks = 0;
        var (host, panel) = Open(Pad(() => clicks++, 20, 20), new Size(100, 50));

        Tap(panel, 90, 40);
        panel.Send(new PanelPointer(PointerPhase.Move, 1, new Point(10, 10)));

        Assert.Equal(0, clicks);
        host.Dispose();
    });

    [Fact]
    public Task AMoveOrAReleaseWithNothingPressedIsIgnored() => Headless.Run(() =>
    {
        var clicks = 0;
        var (host, panel) = Open(Pad(() => clicks++), new Size(100, 50));
        panel.Paint();

        panel.Send(new PanelPointer(PointerPhase.Move, 1, new Point(50, 25)));
        panel.Send(new PanelPointer(PointerPhase.Up, 1, new Point(50, 25)));
        panel.Send(new PanelPointer(PointerPhase.Cancel, 1, new Point(50, 25)));

        Assert.Equal(0, clicks);
        Assert.False(panel.IsDirty);
        host.Dispose();
    });

    [Fact]
    public Task ASliderFollowsADrag() => Headless.Run(() =>
    {
        var slider = new Slider { Minimum = 0, Maximum = 1, Value = 0.5 };
        var (host, panel) = Open(slider, new Size(200, 40));

        panel.Send(new PanelPointer(PointerPhase.Down, 1, new Point(100, 20)));
        panel.Send(new PanelPointer(PointerPhase.Move, 1, new Point(180, 20)));
        panel.Send(new PanelPointer(PointerPhase.Up, 1, new Point(180, 20)));

        Assert.True(slider.Value > 0.8, $"the slider is at {slider.Value}");
        host.Dispose();
    });

    [Fact]
    public Task ADragKeepsReportingAfterItLeavesThePanel() => Headless.Run(() =>
    {
        var reached = new List<double>();
        var grip = Fill(0x00000000);
        grip.PointerPressed += (_, e) => e.Pointer.Capture(grip);
        grip.PointerMoved += (_, e) => reached.Add(e.GetPosition(grip).X);
        var (host, panel) = Open(grip, new Size(100, 50));

        panel.Send(new PanelPointer(PointerPhase.Down, 1, new Point(10, 25)));
        foreach (var x in new[] { 40.0, 70, 160 })
        {
            panel.Send(new PanelPointer(PointerPhase.Move, 1, new Point(x, 25)));
        }

        panel.Send(new PanelPointer(PointerPhase.Up, 1, new Point(160, 25)));

        Assert.Equal([40, 70, 160], reached);
        host.Dispose();
    });

    [Fact]
    public Task ACancelledPressIsNotAClick() => Headless.Run(() =>
    {
        var clicks = 0;
        var (host, panel) = Open(Pad(() => clicks++), new Size(100, 50));

        panel.Send(new PanelPointer(PointerPhase.Down, 1, new Point(50, 25)));
        panel.Send(new PanelPointer(PointerPhase.Cancel, 1, new Point(50, 25)));
        panel.Send(new PanelPointer(PointerPhase.Up, 1, new Point(50, 25)));

        Assert.Equal(0, clicks);
        host.Dispose();
    });

    [Fact]
    public Task APointerAfterTakingThePanelDownDoesNothing() => Headless.Run(() =>
    {
        var clicks = 0;
        var (host, panel) = Open(Pad(() => clicks++), new Size(100, 50));
        panel.Send(new PanelPointer(PointerPhase.Down, 1, new Point(50, 25)));
        panel.Dispose();

        panel.Send(new PanelPointer(PointerPhase.Up, 1, new Point(50, 25)));
        Tap(panel, 50, 25);
        panel.Dispose();

        Assert.Equal(0, clicks);
        host.Dispose();
    });

    [Fact]
    public Task TwoPanelsOnTheSameSpotDoNotShareAPress() => Headless.Run(() =>
    {
        var (first, second) = (0, 0);
        var host = new PanelHost();
        var one = host.Open(Pad(() => first++), new Size(100, 50));
        var two = host.Open(Pad(() => second++), new Size(100, 50));
        Settle();

        Tap(two, 50, 25);
        Assert.Equal((0, 1), (first, second));

        Tap(one, 50, 25);
        Assert.Equal((1, 1), (first, second));
        host.Dispose();
    });

    [Fact]
    public Task APointerIsInLogicalPixelsWhateverTheScale() => Headless.Run(() =>
    {
        var clicks = 0;
        var (host, panel) = Open(Pad(() => clicks++, 40, 40), new Size(100, 50), scale: 3);

        Tap(panel, 90, 40);
        Assert.Equal(0, clicks);
        Tap(panel, 20, 20);

        Assert.Equal(1, clicks);
        host.Dispose();
    });

    [Fact]
    public Task AWorldPanelPressesTheButtonItsRayLandsOn() => Headless.Run(() =>
    {
        var clicks = 0;
        var (host, panel) = Open(Pad(() => clicks++), new Size(200, 100));
        var resources = new RecordedResources();
        var wall = new WorldPanel(
            panel,
            Matrix4.Compose(new Vector3(0, 0, 0), Quaternion.AxisAngle(new Vector3(0, 1, 0), 0), new Vector3(2, 1, 0.05)),
            new WorldPanelOptions { Id = "wall", ObjectKey = 1, MaterialKey = 2, Provide = resources.Provide, Release = resources.Release });
        var ray = new PointerRay(new Vector3(0, 0, 10), new Vector3(0, 0, -1));

        wall.Sync();
        wall.Sync();
        wall.Down(ray);
        wall.Up(ray);
        wall.Sync();

        Assert.Equal(1, clicks);
        Assert.Equal(2, resources.Provided.Count());
        wall.Dispose();
        host.Dispose();
    });
}
