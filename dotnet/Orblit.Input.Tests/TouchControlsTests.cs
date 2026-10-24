using Orblit.VectorMath;
using static Orblit.Input.Tests.ActionFixture;

namespace Orblit.Input.Tests;

// What an on-screen interface says reaches the action layer. The payloads are the ones the stick,
// touch-button and swipe-pad elements send.
public class TouchControlsTests
{
    private const double Tolerance = 1e-12;

    private readonly TouchControls _touch = new();

    private FrameControls Controls(Dictionary<PadAxis, double>? axes = null)
    {
        var held = axes ?? [];
        return new FrameControls(
            touch: _touch.Held,
            sticks: _touch.Sticks,
            pad: new PadState(0, new PadIdentity("test"), [], [], [], held, held));
    }

    [Fact]
    public void ARegionIsHeldFromATruePayloadUntilAFalseOne()
    {
        _touch.Hold("jump", true);
        Assert.Equal(["jump"], _touch.Held);

        _touch.Hold("jump", false);
        Assert.Empty(_touch.Held);
    }

    [Fact]
    public void ARegionIgnoresAPayloadThatIsNotABool()
    {
        _touch.Hold("jump", true);

        _touch.Hold("jump", null);
        _touch.Hold("jump", 0L);
        _touch.Hold("jump", "false");

        Assert.Equal(["jump"], _touch.Held);
    }

    [Fact]
    public void AStickIsWhereThePayloadPutsIt()
    {
        _touch.MoveStick(PadStick.Left, new object?[] { 0.5, -0.25 });

        Assert.Equal(new Vector2(0.5, -0.25), _touch.Sticks[PadStick.Left]);
    }

    [Fact]
    public void AStickIsBroughtBackToTheEdgeOfTheCircle()
    {
        _touch.MoveStick(PadStick.Left, new object?[] { 3.0, 4.0 });

        var stick = _touch.Sticks[PadStick.Left];
        Assert.Equal(1, stick.Length, Tolerance);
        Assert.Equal(0.75, stick.X / stick.Y, Tolerance);
    }

    [Fact]
    public void AStickAtRestHasNoEntry()
    {
        _touch.MoveStick(PadStick.Left, new object?[] { 0.4, 0.4 });
        _touch.MoveStick(PadStick.Left, new object?[] { 0.0, 0.0 });

        Assert.Empty(_touch.Sticks);
    }

    [Fact]
    public void AStickKeepsTheLastGoodValueWhenAPayloadIsWrong()
    {
        _touch.MoveStick(PadStick.Left, new object?[] { 0.4, 0.2 });
        object?[] bad =
        [
            null,
            "up",
            new object?[] { 1.0 },
            new object?[] { 1.0, 2.0, 3.0 },
            new object?[] { "a", "b" },
            new object?[] { double.NaN, 0.0 },
            new object?[] { double.PositiveInfinity, 0.0 },
        ];

        foreach (var payload in bad)
        {
            _touch.MoveStick(PadStick.Left, payload);
        }

        Assert.Equal(new Vector2(0.4, 0.2), _touch.Sticks[PadStick.Left]);
    }

    [Fact]
    public void AStickTakesWholeNumbersAsWellAsFractions()
    {
        _touch.MoveStick(PadStick.Right, new object?[] { 1L, 0L });

        Assert.Equal(new Vector2(1, 0), _touch.Sticks[PadStick.Right]);
    }

    [Fact]
    public void ASwipeAddsUpUntilTheFrameEnds()
    {
        _touch.Swipe(new object?[] { 3.0, 1.0 });
        _touch.Swipe(new object?[] { 2.0, -4.0 });
        Assert.Equal(new Vector2(5, -3), _touch.Swiped);

        _touch.EndFrame();
        Assert.Equal(Vector2.Zero(), _touch.Swiped);
    }

    [Fact]
    public void ASwipeIgnoresAPayloadThatIsNotAPair()
    {
        _touch.Swipe(null);
        _touch.Swipe(new object?[] { 1.0 });
        _touch.Swipe(new object?[] { double.NaN, 1.0 });

        Assert.Equal(Vector2.Zero(), _touch.Swiped);
    }

    [Fact]
    public void ASwipeHandsOutACopySoAGameCannotChangeWhatIsHeld()
    {
        _touch.Swipe(new object?[] { 1.0, 1.0 });

        var copy = _touch.Swiped;
        copy.X = 0;
        copy.Y = 0;

        Assert.Equal(new Vector2(1, 1), _touch.Swiped);
    }

    [Fact]
    public void AScreenIsReadAsAStickWhenThePadIsAtRest()
    {
        _touch.MoveStick(PadStick.Left, new object?[] { 0.0, 0.8 });

        var now = Controls();
        Assert.Equal(new Vector2(0, 0.8), now.Stick(PadStick.Left));
        Assert.Equal(0.8, now.Read(new PadStickControl(PadStick.Left)), Tolerance);
        Assert.Equal(Vector2.Zero(), now.Stick(PadStick.Right));
    }

    [Fact]
    public void AScreenAndAPadSharingAStickReadTheLongerPush()
    {
        _touch.MoveStick(PadStick.Left, new object?[] { 0.0, 0.3 });
        Assert.Equal(new Vector2(0.9, 0), Controls(Axes((PadAxis.LeftX, 0.9))).Stick(PadStick.Left));

        _touch.MoveStick(PadStick.Left, new object?[] { 0.0, 1.0 });
        Assert.Equal(new Vector2(0, 1), Controls(Axes((PadAxis.LeftX, 0.9))).Stick(PadStick.Left));
    }

    [Fact]
    public void AScreenListsAStickPushedPastHalfAsPressed()
    {
        _touch.MoveStick(PadStick.Left, new object?[] { 0.0, 0.2 });
        Assert.Empty(Controls().Active);

        _touch.MoveStick(PadStick.Left, new object?[] { 0.0, 0.6 });
        Assert.Equal<Control>(
            [new PadStickControl(PadStick.Left), new PadAxisControl(PadAxis.LeftY, AxisSide.Positive)],
            Controls().Active);
    }

    [Fact]
    public void AScreenReadsAStickAxisToo()
    {
        _touch.MoveStick(PadStick.Left, new object?[] { -0.8, 0.0 });

        var now = Controls();
        Assert.Equal(0.8, now.Read(new PadAxisControl(PadAxis.LeftX, AxisSide.Negative)), Tolerance);
        Assert.Equal(0, now.Read(new PadAxisControl(PadAxis.RightX)));
        Assert.Equal<Control>(
            [new PadStickControl(PadStick.Left), new PadAxisControl(PadAxis.LeftX, AxisSide.Negative)],
            now.Active);
    }

    [Fact]
    public void AScreenHandsOutACopySoAGameCanScaleWhatItReads()
    {
        _touch.MoveStick(PadStick.Left, new object?[] { 0.0, 0.5 });

        var read = Controls().Stick(PadStick.Left);
        read.X *= 4;
        read.Y *= 4;

        Assert.Equal(new Vector2(0, 0.5), _touch.Sticks[PadStick.Left]);
    }

    [Fact]
    public void AScreenSeesLaterChangesSinceItHoldsViewsOfTheScreen()
    {
        var now = Controls();
        _touch.Hold("jump", true);
        _touch.MoveStick(PadStick.Left, new object?[] { 1.0, 0.0 });

        Assert.Equal(1, now.Read(new TouchControl("jump")));
        Assert.Equal(new Vector2(1, 0), now.Stick(PadStick.Left));
    }

    [Fact]
    public void TwoFingersDriveAMoveAndAJumpThroughTheActionLayer()
    {
        var input = new ActionMap(
            [InputAction.Axis2("move"), InputAction.Button("jump")],
            [
                new InputContext(
                    "play",
                    new Dictionary<string, IReadOnlyList<Binding>>
                    {
                        ["move"] = [new Binding(new PadStickControl(PadStick.Left))],
                        ["jump"] = [new Binding(new TouchControl("jump"))],
                    }),
            ],
            ["play"]);
        FrameControls Now() => new(touch: _touch.Held, sticks: _touch.Sticks);

        input.Update(Now(), Ms(0));
        Assert.False(input.WasPressed("jump"));

        _touch.MoveStick(PadStick.Left, new object?[] { 0.0, 1.0 });
        _touch.Hold("jump", true);
        input.Update(Now(), Ms(16));
        Assert.True(input.WasPressed("jump"));
        Assert.True(input.Vector("move").Y > 0.9);

        _touch.Hold("jump", false);
        input.Update(Now(), Ms(32));
        Assert.False(input.IsDown("jump"));
        Assert.True(input.Vector("move").Y > 0.9, "still pushing");
    }
}
