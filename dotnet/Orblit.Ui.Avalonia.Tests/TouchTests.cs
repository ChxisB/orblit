using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Controls.Shapes;

namespace Orblit.Ui.Avalonia.Tests;

public class TouchTests
{
    private const string Size = "width: 160px; height: 160px";

    private static readonly double[] Zero = [0.0, 0.0];

    private static readonly UiNode Controls = new()
    {
        Type = "row",
        Children =
        [
            Node("stick", "move", $"{Size}; background: #333333; border-radius: 80px"),
            Node("touch-button", "jump", "padding: 24px; background: #aa3300", new UiNode { Type = "text", Text = "Jump" }),
            Node("swipe-pad", "look", Size),
        ],
    };

    private static UiNode Node(string type, string handler, string css, params UiNode[] children) => new()
    {
        Type = type,
        Css = css,
        Children = children,
        Props = new Dictionary<string, object?> { [type switch { "stick" => "onMove", "touch-button" => "onHold", _ => "onSwipe" }] = handler },
    };

    private static Point Middle(Window window, Visual target) =>
        target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;

    private static (Window Window, List<(string Handler, object? Payload)> Events, UiSurface Surface) Show(UiNode description)
    {
        List<(string, object?)> events = [];
        var surface = new UiSurface { Description = description, OnEvent = (handler, payload) => events.Add((handler, payload)) };
        return (TestWindow.Show(surface, 700, 300), events, surface);
    }

    private static double[] Last(List<(string Handler, object? Payload)> events) => Assert.IsType<double[]>(events[^1].Payload);

    [Fact]
    public Task AStickSendsWhereTheThumbIsUpPositiveAndZeroOnRelease() => Headless.Run(() =>
    {
        var (window, events, _) = Show(Controls);
        var middle = Middle(window, Probe.All<TouchStick>(window).Single());

        var thumb = window.TouchBegin(middle + new Vector(40, -20));
        Assert.Equal("move", events[^1].Handler);
        Assert.Equal(0.5, Last(events)[0], 1e-9);
        Assert.Equal(0.25, Last(events)[1], 1e-9);

        window.TouchMove(thumb, middle + new Vector(0, 40));
        Assert.Equal(0, Last(events)[0], 1e-9);
        Assert.Equal(-0.5, Last(events)[1], 1e-9);

        window.TouchEnd(thumb, middle + new Vector(0, 40));
        Assert.Equal(Zero, Last(events));
    });

    [Fact]
    public Task AStickHoldsAPushPastTheEdgeToTheCircle() => Headless.Run(() =>
    {
        var (window, events, _) = Show(Controls);

        window.TouchBegin(Middle(window, Probe.All<TouchStick>(window).Single()) + new Vector(70, 70));

        Assert.Equal(0.7071, Last(events)[0], 1e-4);
        Assert.Equal(-0.7071, Last(events)[1], 1e-4);
    });

    [Fact]
    public Task AStickLetsGoWhenTheSystemTakesTheFinger() => Headless.Run(() =>
    {
        var (window, events, _) = Show(Controls);
        var thumb = window.TouchBegin(Middle(window, Probe.All<TouchStick>(window).Single()) + new Vector(30, 0));

        thumb.Dispose();

        Assert.Equal(Zero, Last(events));
    });

    [Fact]
    public Task AStickLetsGoWhenItIsRebuiltAway() => Headless.Run(() =>
    {
        var (window, events, surface) = Show(Controls);
        window.TouchBegin(Middle(window, Probe.All<TouchStick>(window).Single()) + new Vector(30, 0));
        events.Clear();

        surface.Description = new UiNode { Type = "text", Text = "Paused" };
        window.UpdateLayout();

        var (handler, payload) = Assert.Single(events);
        Assert.Equal("move", handler);
        Assert.Equal(Zero, Assert.IsType<double[]>(payload));
    });

    [Fact]
    public Task AStickSendsAPlainZeroForYLevelWithTheMiddle() => Headless.Run(() =>
    {
        var (window, events, _) = Show(Controls);

        window.TouchBegin(Middle(window, Probe.All<TouchStick>(window).Single()) + new Vector(40, 0));

        Assert.Equal(0, Last(events)[1]);
        Assert.False(double.IsNegative(Last(events)[1]), "not -0.0");
    });

    [Fact]
    public Task AStickSaysNothingWhenTheThumbHasNotMoved() => Headless.Run(() =>
    {
        var (window, events, _) = Show(Controls);
        var at = Middle(window, Probe.All<TouchStick>(window).Single()) + new Vector(40, 0);
        var thumb = window.TouchBegin(at);
        var count = events.Count;

        window.TouchMove(thumb, at);

        Assert.Equal(count, events.Count);
    });

    [Fact]
    public Task AStickDrawsTheKnobUnderTheThumb() => Headless.Run(() =>
    {
        var (window, _, _) = Show(Controls);
        var stick = Probe.All<TouchStick>(window).Single();
        var middle = Middle(window, stick);

        window.TouchBegin(middle + new Vector(40, 0));
        window.UpdateLayout();

        var knob = Middle(window, Probe.All<Ellipse>(stick).Single());
        Assert.Equal(middle.X + 40, knob.X, 1e-6);
        Assert.Equal(middle.Y, knob.Y, 1e-6);
    });

    [Fact]
    public Task AStickFollowsOnlyItsFirstFinger() => Headless.Run(() =>
    {
        var (window, events, _) = Show(Controls);
        var middle = Middle(window, Probe.All<TouchStick>(window).Single());
        window.TouchBegin(middle + new Vector(40, 0));
        var count = events.Count;

        var second = window.TouchBegin(middle + new Vector(-40, 0));
        window.TouchMove(second, middle + new Vector(0, 40));
        window.TouchEnd(second, middle + new Vector(0, 40));

        Assert.Equal(count, events.Count);
    });

    [Fact]
    public Task ATouchButtonIsHeldFromTouchToLift() => Headless.Run(() =>
    {
        var (window, events, _) = Show(Controls);
        var at = Middle(window, Probe.All<TouchHold>(window).Single());

        var finger = window.TouchBegin(at);
        Assert.Equal(("jump", (object?)true), events[^1]);

        window.TouchEnd(finger, at);
        Assert.Equal(("jump", (object?)false), events[^1]);
        Assert.Single(Probe.All<TextBlock>(window), block => block.Text == "Jump");
    });

    [Fact]
    public Task ATouchButtonTakesATouchOnItsPaddingAndDimsItsWholeFace() => Headless.Run(() =>
    {
        var (window, events, _) = Show(Controls);
        var hold = Probe.All<TouchHold>(window).Single();
        var corner = hold.TranslatePoint(new Point(2, 2), window)!.Value;

        var finger = window.TouchBegin(corner);

        Assert.Equal(("jump", (object?)true), events[^1]);
        Assert.Equal(0.6, hold.Opacity);
        Assert.Contains(Probe.All<Border>(hold), box => box.Background is ISolidColorBrush { Color.A: 255 });

        window.TouchEnd(finger, corner);
        Assert.Equal(1, hold.Opacity);
    });

    [Fact]
    public Task ATouchButtonLetsGoWhenTheSystemTakesTheFinger() => Headless.Run(() =>
    {
        var (window, events, _) = Show(Controls);
        var finger = window.TouchBegin(Middle(window, Probe.All<TouchHold>(window).Single()));

        finger.Dispose();

        Assert.Equal(("jump", (object?)false), events[^1]);
    });

    [Fact]
    public Task ATouchButtonLetsGoWhenItIsRebuiltAway() => Headless.Run(() =>
    {
        var (window, events, surface) = Show(Controls);
        window.TouchBegin(Middle(window, Probe.All<TouchHold>(window).Single()));
        events.Clear();

        surface.Description = new UiNode { Type = "text", Text = "Paused" };
        window.UpdateLayout();

        Assert.Equal(("jump", (object?)false), Assert.Single(events));
    });

    [Fact]
    public Task ASwipePadSendsEachMoveUpPositive() => Headless.Run(() =>
    {
        var (window, events, _) = Show(Controls);
        var start = Middle(window, Probe.All<TouchSwipe>(window).Single());

        var finger = window.TouchBegin(start);
        Assert.Empty(events);

        window.TouchMove(finger, start + new Vector(6, -4));
        window.TouchMove(finger, start + new Vector(4, -1));
        window.TouchEnd(finger, start + new Vector(4, -1));

        Assert.Equal(2, events.Count);
        Assert.Equal(new[] { 6.0, 4.0 }, Assert.IsType<double[]>(events[0].Payload));
        Assert.Equal(new[] { -2.0, -3.0 }, Assert.IsType<double[]>(events[1].Payload));
    });

    [Fact]
    public Task ASwipePadAndAStickAreSquaresOfTheirOwnWhenNoSizeIsGiven() => Headless.Run(() =>
    {
        var (window, _, _) = Show(new UiNode { Type = "row", Children = [new UiNode { Type = "swipe-pad" }, new UiNode { Type = "stick" }] });

        Assert.Equal(new Size(120, 120), Probe.All<TouchSwipe>(window).Single().Bounds.Size);
        Assert.Equal(new Size(120, 120), Probe.All<TouchStick>(window).Single().Bounds.Size);
    });

    [Fact]
    public Task ASwipePadSaysNothingWhenTheFingerLandsElsewhere() => Headless.Run(() =>
    {
        var (window, events, _) = Show(Controls);
        var at = Middle(window, Probe.All<TouchStick>(window).Single());
        var finger = window.TouchBegin(at);

        window.TouchMove(finger, at + new Vector(5, 5));

        Assert.DoesNotContain(events, e => e.Handler == "look");
    });

    [Fact]
    public Task ThreeFingersDriveAStickAButtonAndASwipeAtOnce() => Headless.Run(() =>
    {
        var (window, events, _) = Show(Controls);
        var stick = Middle(window, Probe.All<TouchStick>(window).Single());
        var button = Middle(window, Probe.All<TouchHold>(window).Single());
        var swipe = Middle(window, Probe.All<TouchSwipe>(window).Single());

        var thumb = window.TouchBegin(stick + new Vector(40, 0));
        var jump = window.TouchBegin(button);
        var look = window.TouchBegin(swipe);
        window.TouchMove(look, swipe + new Vector(10, 0));
        window.TouchMove(thumb, stick + new Vector(40, -20));

        Assert.Equal(new[] { "jump", "look", "move" }, events.Select(e => e.Handler).Distinct().Order());
        Assert.Equal(("jump", (object?)true), Assert.Single(events, e => e.Handler == "jump"));

        window.TouchEnd(jump, button);
        Assert.Equal(("jump", (object?)false), events[^1]);
        Assert.True(Assert.IsType<double[]>(events.Last(e => e.Handler == "move").Payload)[0] > 0.2, "the thumb is still down");
    });

    [Fact]
    public Task AnElementWithNoHandlerNamedStillDrawsAndListens() => Headless.Run(() =>
    {
        var (window, events, _) = Show(new UiNode
        {
            Type = "column",
            Children = [new UiNode { Type = "stick", Css = Size }, new UiNode { Type = "swipe-pad", Css = Size }],
        });
        var at = Middle(window, Probe.All<TouchStick>(window).Single());

        var finger = window.TouchBegin(at);
        window.TouchMove(finger, at + new Vector(10, 0));
        window.TouchEnd(finger, at + new Vector(10, 0));

        Assert.Empty(events);
        Assert.Single(Probe.All<Ellipse>(window));
    });

    [Fact]
    public Task AMouseDragsAStickToo() => Headless.Run(() =>
    {
        var (window, events, _) = Show(Controls);
        var middle = Middle(window, Probe.All<TouchStick>(window).Single());

        window.MouseDown(middle + new Vector(40, 0), MouseButton.Left);
        window.MouseMove(middle + new Vector(200, 0));
        window.MouseUp(middle + new Vector(200, 0), MouseButton.Left);

        Assert.Equal(3, events.Count);
        Assert.Equal(new[] { 1.0, 0.0 }, Assert.IsType<double[]>(events[1].Payload));
        Assert.Equal(Zero, Last(events));
    });
}
