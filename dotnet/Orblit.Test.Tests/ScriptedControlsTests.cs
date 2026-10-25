using Orblit.Input;
using Orblit.VectorMath;

namespace Orblit.Test.Tests;

public sealed class ScriptedControlsTests
{
    private readonly ScriptedControls _controls = new();

    [Fact]
    public void NothingIsHeldToBeginWith()
    {
        Assert.Equal(0, _controls.Read(new KeyControl("Space")));
        Assert.Equal(Vector2.Zero(), _controls.Stick(PadStick.Left));
    }

    [Fact]
    public void AControlIsHeldFromPressToRelease()
    {
        _controls.Press(new KeyControl("Space"));
        Assert.Equal(1, _controls.Read(new KeyControl("Space")));

        _controls.Release(new KeyControl("Space"));
        Assert.Equal(0, _controls.Read(new KeyControl("Space")));
    }

    [Fact]
    public void AnAxisReadsTheValueItWasPushedToBySide()
    {
        _controls.PushAxis(PadAxis.LeftTrigger, 0.7);
        _controls.PushAxis(PadAxis.LeftX, -0.4);

        Assert.Equal(0.7, _controls.Read(new PadAxisControl(PadAxis.LeftTrigger)));
        Assert.Equal(0.4, _controls.Read(new PadAxisControl(PadAxis.LeftX, AxisSide.Negative)));
        Assert.Equal(0, _controls.Read(new PadAxisControl(PadAxis.LeftX, AxisSide.Positive)));
    }

    [Fact]
    public void AStickIsItsTwoAxesSoAnAxisBindingSeesAPush()
    {
        _controls.PushStick(PadStick.Right, new Vector2(-0.5, 0.25));

        Assert.Equal(0.5, _controls.Read(new PadAxisControl(PadAxis.RightX, AxisSide.Negative)));
        Assert.Equal(0.25, _controls.Read(new PadAxisControl(PadAxis.RightY)));
    }

    [Fact]
    public void AStickAndAnAxisArePushedWithTheirOwnCalls()
    {
        Assert.Throws<ArgumentException>(() => _controls.Press(new PadAxisControl(PadAxis.LeftX)));
        Assert.Throws<ArgumentException>(() => _controls.Press(new PadStickControl(PadStick.Left)));
    }

    [Fact]
    public void APushBeyondWhatAPadCanGiveIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _controls.PushAxis(PadAxis.LeftX, 1.5));
        Assert.Throws<ArgumentOutOfRangeException>(() => _controls.PushAxis(PadAxis.LeftX, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => _controls.PushStick(PadStick.Left, new Vector2(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => _controls.Press(new KeyControl("Space"), double.PositiveInfinity));
    }

    [Fact]
    public void AStickIsReadAsAVectorAndAsHowFarItIsPushed()
    {
        _controls.PushStick(PadStick.Left, new Vector2(0.6, 0.8));

        Assert.Equal(new Vector2(0.6, 0.8), _controls.Stick(PadStick.Left));
        Assert.Equal(1, _controls.Read(new PadStickControl(PadStick.Left)), 1e-9);
        Assert.Equal(Vector2.Zero(), _controls.Stick(PadStick.Right));
    }

    [Fact]
    public void AStickPushedToZeroIsLetGo()
    {
        _controls.PushStick(PadStick.Left, new Vector2(0.6, 0));
        _controls.PushStick(PadStick.Left, Vector2.Zero());

        Assert.Equal(0, _controls.Read(new PadStickControl(PadStick.Left)));
    }

    [Fact]
    public void AStickHandsOutACopySoAGameCanScaleWhatItReads()
    {
        _controls.PushStick(PadStick.Left, new Vector2(0, 0.5));

        var read = _controls.Stick(PadStick.Left);
        read.X *= 4;
        read.Y *= 4;

        Assert.Equal(new Vector2(0, 0.5), _controls.Stick(PadStick.Left));
    }
}
