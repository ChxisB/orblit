using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Shapes;

namespace Orblit.Ui.Avalonia.Tests;

public class TouchTests
{
    private static readonly IBrush Knob = new SolidColorBrush(Color.FromUInt32(0xB3FFFFFF));

    // A face that draws nothing, so the control's own fill is what takes a touch.
    private static Border Square() => new() { Width = 160, Height = 160 };

    private static Point Middle(Window window, Visual target) =>
        target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;

    [Fact]
    public Task AStickSendsWhereTheThumbIsUpPositiveAndZeroOnRelease() => Headless.Run(() =>
    {
        var row = new TouchRow();
        var middle = Middle(row.Window, row.Stick);

        var thumb = row.Window.TouchBegin(middle + new Vector(40, -20));
        Assert.Equal("move", row.Events[^1].Control);
        Assert.Equal(0.5, row.Last.X, 1e-9);
        Assert.Equal(0.25, row.Last.Y, 1e-9);

        row.Window.TouchMove(thumb, middle + new Vector(0, 40));
        Assert.Equal(0, row.Last.X, 1e-9);
        Assert.Equal(-0.5, row.Last.Y, 1e-9);

        row.Window.TouchEnd(thumb, middle + new Vector(0, 40));
        Assert.Equal(default, row.Last);
    });

    [Fact]
    public Task AStickHoldsAPushPastTheEdgeToTheCircle() => Headless.Run(() =>
    {
        var row = new TouchRow();

        row.Window.TouchBegin(Middle(row.Window, row.Stick) + new Vector(70, 70));

        Assert.Equal(0.7071, row.Last.X, 1e-4);
        Assert.Equal(-0.7071, row.Last.Y, 1e-4);
    });

    [Fact]
    public Task AStickLetsGoWhenTheSystemTakesTheFinger() => Headless.Run(() =>
    {
        var row = new TouchRow();
        var thumb = row.Window.TouchBegin(Middle(row.Window, row.Stick) + new Vector(30, 0));

        thumb.Dispose();

        Assert.Equal(default, row.Last);
    });

    [Fact]
    public Task AStickLetsGoWhenItIsTakenAway() => Headless.Run(() =>
    {
        var row = new TouchRow();
        row.Window.TouchBegin(Middle(row.Window, row.Stick) + new Vector(30, 0));
        row.Events.Clear();

        row.Panel.Children.Remove(row.Stick);
        row.Window.UpdateLayout();

        Assert.Equal(("move", (object)default(Vector)), Assert.Single(row.Events));
    });

    [Fact]
    public Task AStickSendsAPlainZeroForYLevelWithTheMiddle() => Headless.Run(() =>
    {
        var row = new TouchRow();

        row.Window.TouchBegin(Middle(row.Window, row.Stick) + new Vector(40, 0));

        Assert.Equal(0, row.Last.Y);
        Assert.False(double.IsNegative(row.Last.Y), "not -0.0");
    });

    [Fact]
    public Task AStickSaysNothingWhenTheThumbHasNotMoved() => Headless.Run(() =>
    {
        var row = new TouchRow();
        var at = Middle(row.Window, row.Stick) + new Vector(40, 0);
        var thumb = row.Window.TouchBegin(at);
        var count = row.Events.Count;

        row.Window.TouchMove(thumb, at);

        Assert.Equal(count, row.Events.Count);
    });

    [Fact]
    public Task AStickDrawsTheKnobUnderTheThumbAndBackInTheMiddle() => Headless.Run(() =>
    {
        var row = new TouchRow();
        var middle = Middle(row.Window, row.Stick);

        var thumb = row.Window.TouchBegin(middle + new Vector(40, 0));
        row.Window.UpdateLayout();

        var knob = row.Stick.Children.OfType<Ellipse>().Single();
        Assert.Equal(middle.X + 40, Middle(row.Window, knob).X, 1e-6);
        Assert.Equal(middle.Y, Middle(row.Window, knob).Y, 1e-6);

        row.Window.TouchEnd(thumb, middle + new Vector(40, 0));
        row.Window.UpdateLayout();
        Assert.Equal(middle, Middle(row.Window, knob));
    });

    [Fact]
    public Task AStickFollowsOnlyItsFirstFinger() => Headless.Run(() =>
    {
        var row = new TouchRow();
        var middle = Middle(row.Window, row.Stick);
        row.Window.TouchBegin(middle + new Vector(40, 0));
        var count = row.Events.Count;

        var second = row.Window.TouchBegin(middle + new Vector(-40, 0));
        row.Window.TouchMove(second, middle + new Vector(0, 40));
        row.Window.TouchEnd(second, middle + new Vector(0, 40));

        Assert.Equal(count, row.Events.Count);
    });

    [Fact]
    public Task ATouchButtonIsHeldFromTouchToLift() => Headless.Run(() =>
    {
        var row = new TouchRow();
        var at = Middle(row.Window, row.Hold);

        var finger = row.Window.TouchBegin(at);
        Assert.Equal(("jump", (object)true), row.Events[^1]);

        row.Window.TouchEnd(finger, at);
        Assert.Equal(("jump", (object)false), row.Events[^1]);
        Assert.Equal("Jump", Assert.IsType<TextBlock>(row.Face.Child).Text);
    });

    [Fact]
    public Task ATouchButtonTakesATouchOnItsPaddingAndDimsItsWholeFace() => Headless.Run(() =>
    {
        var row = new TouchRow();
        var corner = row.Hold.TranslatePoint(new Point(2, 2), row.Window)!.Value;

        var finger = row.Window.TouchBegin(corner);

        Assert.Equal(("jump", (object)true), row.Events[^1]);
        Assert.Equal(0.6, row.Hold.Opacity);
        Assert.Equal(1, row.Face.Opacity);

        row.Window.TouchEnd(finger, corner);
        Assert.Equal(1, row.Hold.Opacity);
    });

    [Fact]
    public Task ATouchButtonLetsGoWhenTheSystemTakesTheFinger() => Headless.Run(() =>
    {
        var row = new TouchRow();
        var finger = row.Window.TouchBegin(Middle(row.Window, row.Hold));

        finger.Dispose();

        Assert.Equal(("jump", (object)false), row.Events[^1]);
    });

    [Fact]
    public Task ATouchButtonLetsGoWhenItIsTakenAway() => Headless.Run(() =>
    {
        var row = new TouchRow();
        row.Window.TouchBegin(Middle(row.Window, row.Hold));
        row.Events.Clear();

        row.Panel.Children.Remove(row.Hold);
        row.Window.UpdateLayout();

        Assert.Equal(("jump", (object)false), Assert.Single(row.Events));
    });

    [Fact]
    public Task ASwipePadSendsEachMoveUpPositive() => Headless.Run(() =>
    {
        var row = new TouchRow();
        var start = Middle(row.Window, row.Swipe);

        var finger = row.Window.TouchBegin(start);
        Assert.Empty(row.Events);

        row.Window.TouchMove(finger, start + new Vector(6, -4));
        row.Window.TouchMove(finger, start + new Vector(4, -1));
        row.Window.TouchMove(finger, start + new Vector(4, -1));
        row.Window.TouchEnd(finger, start + new Vector(4, -1));

        Assert.Equal([("look", (object)new Vector(6, 4)), ("look", new Vector(-2, -3))], row.Events);
    });

    [Fact]
    public Task AStickAndASwipePadAreTheSizeOfTheirFace() => Headless.Run(() =>
    {
        var stick = new TouchStick(new Border { Width = 120, Height = 90 }, Knob);
        var swipe = new TouchSwipe(new Border { Width = 100, Height = 70 });
        TestWindow.Show(new StackPanel { Orientation = Orientation.Horizontal, Children = { stick, swipe } }, 700, 300);

        Assert.Equal(new Size(120, 90), stick.DesiredSize);
        Assert.Equal(new Size(100, 70), swipe.DesiredSize);
    });

    [Fact]
    public Task ASwipePadSaysNothingWhenTheFingerLandsElsewhere() => Headless.Run(() =>
    {
        var row = new TouchRow();
        var at = Middle(row.Window, row.Stick);
        var finger = row.Window.TouchBegin(at);

        row.Window.TouchMove(finger, at + new Vector(5, 5));

        Assert.DoesNotContain(row.Events, e => e.Control == "look");
    });

    [Fact]
    public Task ThreeFingersDriveAStickAButtonAndASwipeAtOnce() => Headless.Run(() =>
    {
        var row = new TouchRow();
        var stick = Middle(row.Window, row.Stick);
        var button = Middle(row.Window, row.Hold);
        var swipe = Middle(row.Window, row.Swipe);

        var thumb = row.Window.TouchBegin(stick + new Vector(40, 0));
        var jump = row.Window.TouchBegin(button);
        var look = row.Window.TouchBegin(swipe);
        row.Window.TouchMove(look, swipe + new Vector(10, 0));
        row.Window.TouchMove(thumb, stick + new Vector(40, -20));

        Assert.Equal(new[] { "jump", "look", "move" }, row.Events.Select(e => e.Control).Distinct().Order());
        Assert.Equal(("jump", (object)true), Assert.Single(row.Events, e => e.Control == "jump"));

        row.Window.TouchEnd(jump, button);
        Assert.Equal(("jump", (object)false), row.Events[^1]);
        Assert.True(Assert.IsType<Vector>(row.Events.Last(e => e.Control == "move").Payload).X > 0.2, "the thumb is still down");
    });

    [Fact]
    public Task AControlNobodyListensToStillDrawsAndFollows() => Headless.Run(() =>
    {
        var stick = new TouchStick(Square(), Knob);
        var swipe = new TouchSwipe(Square());
        var window = TestWindow.Show(new StackPanel { Children = { stick, swipe } }, 300, 400);
        var at = Middle(window, stick);

        var finger = window.TouchBegin(at);
        window.TouchMove(finger, at + new Vector(10, 0));
        window.UpdateLayout();
        var look = window.TouchBegin(Middle(window, swipe));
        window.TouchMove(look, Middle(window, swipe) + new Vector(10, 0));

        Assert.Equal(at.X + 10, Middle(window, stick.Children.OfType<Ellipse>().Single()).X, 1e-6);
    });

    [Fact]
    public Task AMouseDragsAStickToo() => Headless.Run(() =>
    {
        var row = new TouchRow();
        var middle = Middle(row.Window, row.Stick);

        row.Window.MouseDown(middle + new Vector(40, 0), MouseButton.Left);
        row.Window.MouseMove(middle + new Vector(200, 0));
        row.Window.MouseUp(middle + new Vector(200, 0), MouseButton.Left);

        Assert.Equal(3, row.Events.Count);
        Assert.Equal(new Vector(1, 0), row.Events[1].Payload);
        Assert.Equal(default, row.Last);
    });

    /// <summary>A stick, a button and a swipe pad side by side, and every event they raise in order.</summary>
    private sealed class TouchRow
    {
        public TouchRow()
        {
            Face = new Border { Padding = new Thickness(24), Background = new SolidColorBrush(Color.FromUInt32(0xFFAA3300)), Child = new TextBlock { Text = "Jump" } };
            Stick = new TouchStick(new Border { Width = 160, Height = 160, Background = new SolidColorBrush(Color.FromUInt32(0xFF333333)), CornerRadius = new CornerRadius(80) }, Knob);
            Hold = new TouchHold(Face);
            Swipe = new TouchSwipe(Square());
            Stick.Tilted += at => Events.Add(("move", at));
            Hold.Held += held => Events.Add(("jump", held));
            Swipe.Swiped += step => Events.Add(("look", step));
            Panel = new StackPanel { Orientation = Orientation.Horizontal, Children = { Stick, Hold, Swipe } };
            Window = TestWindow.Show(Panel, 700, 300);
        }

        public List<(string Control, object Payload)> Events { get; } = [];

        public Border Face { get; }

        public TouchStick Stick { get; }

        public TouchHold Hold { get; }

        public TouchSwipe Swipe { get; }

        public StackPanel Panel { get; }

        public Window Window { get; }

        public Vector Last => Assert.IsType<Vector>(Events[^1].Payload);
    }
}
