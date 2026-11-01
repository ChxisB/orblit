using static Orblit.Input.Tests.ActionFixture;

namespace Orblit.Input.Tests;

public class FrameControlsTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void ItReadsEachKindOfControl()
    {
        var controls = Frame(
            keys: ["KeyW"],
            touch: ["fire"],
            down: [PadButton.South],
            axes: Axes((PadAxis.LeftX, 0.6), (PadAxis.LeftY, 0.8), (PadAxis.RightTrigger, 1)));

        Assert.Equal(1, controls.Read(Key("KeyW")));
        Assert.Equal(0, controls.Read(Key("KeyS")));
        Assert.Equal(1, controls.Read(new TouchControl("fire")));
        Assert.Equal(0, controls.Read(new TouchControl("jump")));
        Assert.Equal(1, controls.Read(new PadButtonControl(PadButton.South)));
        Assert.Equal(0, controls.Read(new PadButtonControl(PadButton.East)));
        Assert.Equal(0.6, controls.Read(new PadAxisControl(PadAxis.LeftX)));
        Assert.Equal(1, controls.Read(new PadStickControl(PadStick.Left)), Tolerance);
        Assert.Equal(0.8, controls.Stick(PadStick.Left).Y);
    }

    [Fact]
    public void ItReadsOneHalfOfAnAxisAsZeroToOne()
    {
        var controls = Frame(axes: Axes((PadAxis.LeftX, 0.6), (PadAxis.LeftY, -0.7)));
        var rightOfX = new PadAxisControl(PadAxis.LeftX, AxisSide.Positive);
        var leftOfX = new PadAxisControl(PadAxis.LeftX, AxisSide.Negative);
        var upOfY = new PadAxisControl(PadAxis.LeftY, AxisSide.Positive);
        var downOfY = new PadAxisControl(PadAxis.LeftY, AxisSide.Negative);

        Assert.Equal(0.6, controls.Read(rightOfX));
        Assert.Equal(0, controls.Read(leftOfX));
        Assert.Equal(0, controls.Read(upOfY));
        Assert.Equal(0.7, controls.Read(downOfY));
        Assert.Equal(-0.7, controls.Read(new PadAxisControl(PadAxis.LeftY)));
    }

    [Fact]
    public void ItListsWhatIsPressedKeysButtonsSticksAxesTouch()
    {
        var controls = Frame(
            keys: ["KeyW"],
            touch: ["fire"],
            down: [PadButton.South],
            axes: Axes((PadAxis.LeftX, 0.8), (PadAxis.RightTrigger, 0.9), (PadAxis.RightX, 0.2)));

        Assert.Equal<Control>(
            [
                Key("KeyW"),
                new PadButtonControl(PadButton.South),
                new PadStickControl(PadStick.Left),
                new PadAxisControl(PadAxis.LeftX, AxisSide.Positive),
                new PadAxisControl(PadAxis.RightTrigger, AxisSide.Positive),
                new TouchControl("fire"),
            ],
            controls.Active);
    }

    [Fact]
    public void ItListsTheHalfOfAnAxisThatWasPushed()
    {
        var controls = Frame(axes: Axes((PadAxis.LeftX, -0.9)));

        Assert.Equal<Control>(
            [new PadStickControl(PadStick.Left), new PadAxisControl(PadAxis.LeftX, AxisSide.Negative)],
            controls.Active);
    }

    [Theory]
    [InlineData(PadButton.LeftTrigger, PadAxis.LeftTrigger)]
    [InlineData(PadButton.RightTrigger, PadAxis.RightTrigger)]
    public void ItListsATriggerOnceAsTheAxisThatTravels(PadButton button, PadAxis axis)
    {
        var travelled = Frame(down: [button], axes: Axes((axis, 0.9)));
        var clicked = Frame(down: [button]);

        Assert.Equal<Control>([new PadAxisControl(axis, AxisSide.Positive)], travelled.Active);
        Assert.Equal<Control>([new PadButtonControl(button)], clicked.Active);
    }

    [Fact]
    public void ItHasNothingPressedWhenNothingIs()
    {
        Assert.Empty(Frame().Active);
        Assert.Empty(new FrameControls().Active);
    }
}
