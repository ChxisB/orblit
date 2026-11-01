using static Orblit.Input.Tests.ActionFixture;

namespace Orblit.Input.Tests;

public class NextInputTests
{
    [Fact]
    public void ItWaitsForTheNextControlPressed()
    {
        var next = new NextInput(ActionKind.Button);

        next.Update(Frame());
        Assert.Null(next.Captured);
        next.Update(Frame(keys: ["KeyQ"]));

        Assert.Equal<Control>(Key("KeyQ"), next.Captured);
    }

    [Fact]
    public void ItIgnoresWhatWasAlreadyDownUntilItComesUp()
    {
        var next = new NextInput(ActionKind.Button);

        next.Update(Frame(keys: ["Enter"]));
        next.Update(Frame(keys: ["Enter"]));
        Assert.Null(next.Captured);
        next.Update(Frame());
        Assert.Null(next.Captured);
        next.Update(Frame(keys: ["Enter"]));

        Assert.Equal<Control>(Key("Enter"), next.Captured);
    }

    [Fact]
    public void ItIgnoresAHeldControlButTakesANewOneBesideIt()
    {
        var next = new NextInput(ActionKind.Button);

        next.Update(Frame(keys: ["Enter"]));
        next.Update(Frame(keys: ["Enter", "KeyQ"]));

        Assert.Equal<Control>(Key("KeyQ"), next.Captured);
    }

    [Fact]
    public void ItKeepsItsFirstAnswer()
    {
        var next = new NextInput(ActionKind.Button);
        next.Update(Frame());
        next.Update(Frame(keys: ["KeyQ"]));

        next.Update(Frame(keys: ["KeyW"]));
        next.Update(Frame());

        Assert.Equal<Control>(Key("KeyQ"), next.Captured);
    }

    [Fact]
    public void ItGivesAButtonTheHalfOfAStickThatWasPushed()
    {
        var next = new NextInput(ActionKind.Button);
        next.Update(Frame());

        next.Update(Frame(axes: Axes((PadAxis.LeftX, 0.9))));

        Assert.Equal<Control>(new PadAxisControl(PadAxis.LeftX, AxisSide.Positive), next.Captured);
    }

    [Fact]
    public void ItGivesTheOtherHalfForAPushTheOtherWay()
    {
        var next = new NextInput(ActionKind.Axis);
        next.Update(Frame());

        next.Update(Frame(axes: Axes((PadAxis.LeftX, -0.9))));

        Assert.Equal<Control>(new PadAxisControl(PadAxis.LeftX, AxisSide.Negative), next.Captured);
    }

    [Fact]
    public void ItTakesTheOtherWayOfAStickThatWasHeldOneWay()
    {
        var next = new NextInput(ActionKind.Axis);
        next.Update(Frame(axes: Axes((PadAxis.LeftX, 0.9))));
        next.Update(Frame(axes: Axes((PadAxis.LeftX, 0.9))));
        Assert.Null(next.Captured);

        next.Update(Frame(axes: Axes((PadAxis.LeftX, -0.9))));

        Assert.Equal<Control>(new PadAxisControl(PadAxis.LeftX, AxisSide.Negative), next.Captured);
    }

    [Fact]
    public void ItGivesAnAxisActionATriggerAsItsAxis()
    {
        var next = new NextInput(ActionKind.Axis);
        next.Update(Frame());

        next.Update(Frame(down: [PadButton.RightTrigger], axes: Axes((PadAxis.RightTrigger, 0.9))));

        Assert.Equal<Control>(new PadAxisControl(PadAxis.RightTrigger, AxisSide.Positive), next.Captured);
    }

    [Fact]
    public void ItGivesATwoAxisActionTheWholeStick()
    {
        var next = new NextInput(ActionKind.Axis2);
        next.Update(Frame());

        next.Update(Frame(axes: Axes((PadAxis.LeftX, 0.9))));

        Assert.Equal<Control>(new PadStickControl(PadStick.Left), next.Captured);
    }

    [Fact]
    public void ItDoesNotTakeAPadThatIsBarelyTouched()
    {
        var next = new NextInput(ActionKind.Axis);
        next.Update(Frame());

        next.Update(Frame(axes: Axes((PadAxis.LeftX, 0.3))));

        Assert.Null(next.Captured);
    }
}
