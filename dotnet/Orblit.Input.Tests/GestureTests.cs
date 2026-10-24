using static Orblit.Input.Tests.ActionFixture;

namespace Orblit.Input.Tests;

public class GestureTests
{
    [Fact]
    public void AShortPressIsATapWhenItComesUp()
    {
        var map = Map();

        map.Update(Frame(keys: ["Space"]), Ms(0));
        Assert.False(map.WasTapped("jump"));
        map.Update(Frame(), Ms(100));
        Assert.True(map.WasTapped("jump"));
        map.Update(Frame(), Ms(116));
        Assert.False(map.WasTapped("jump"));
    }

    [Fact]
    public void ALongPressIsNotATap()
    {
        var map = Map();

        map.Update(Frame(keys: ["Space"]), Ms(0));
        map.Update(Frame(), Ms(300));

        Assert.False(map.WasTapped("jump"));
        Assert.True(map.WasReleased("jump"));
    }

    [Fact]
    public void APressThatLastsIsHeldOnce()
    {
        var map = Map();
        map.Update(Frame(keys: ["Space"]), Ms(0));

        map.Update(Frame(keys: ["Space"]), Ms(399));
        Assert.False(map.WasHeld("jump"));
        map.Update(Frame(keys: ["Space"]), Ms(400));
        Assert.True(map.WasHeld("jump"));
        map.Update(Frame(keys: ["Space"]), Ms(500));
        Assert.False(map.WasHeld("jump"));

        map.Update(Frame(), Ms(516));
        map.Update(Frame(keys: ["Space"]), Ms(532));
        map.Update(Frame(keys: ["Space"]), Ms(932));
        Assert.True(map.WasHeld("jump"), "the next press is new");
    }

    [Fact]
    public void ASecondPressSoonAfterATapIsADoubleTap()
    {
        var map = Map();
        map.Update(Frame(keys: ["Space"]), Ms(0));
        map.Update(Frame(), Ms(100));

        map.Update(Frame(keys: ["Space"]), Ms(250));

        Assert.True(map.WasDoubleTapped("jump"));
        Assert.True(map.WasPressed("jump"));
    }

    [Fact]
    public void ASecondPressThatIsTooLateIsNot()
    {
        var map = Map();
        map.Update(Frame(keys: ["Space"]), Ms(0));
        map.Update(Frame(), Ms(100));

        map.Update(Frame(keys: ["Space"]), Ms(450));

        Assert.False(map.WasDoubleTapped("jump"));
    }

    [Fact]
    public void ADoubleTapDoesNotStartAnother()
    {
        var map = Map();
        map.Update(Frame(keys: ["Space"]), Ms(0));
        map.Update(Frame(), Ms(50));
        map.Update(Frame(keys: ["Space"]), Ms(100));
        map.Update(Frame(), Ms(150));

        map.Update(Frame(keys: ["Space"]), Ms(200));

        Assert.False(map.WasDoubleTapped("jump"));
    }

    [Fact]
    public void ItUsesTheTimesAnActionWasGiven()
    {
        var map = Play(
            InputAction.Button("charge", new Gestures(hold: TimeSpan.FromSeconds(1))),
            new Binding(Key("KeyC")));
        map.Update(Frame(keys: ["KeyC"]), Ms(0));

        map.Update(Frame(keys: ["KeyC"]), Ms(999));
        Assert.False(map.WasHeld("charge"));
        map.Update(Frame(keys: ["KeyC"]), Ms(1000));
        Assert.True(map.WasHeld("charge"));
    }
}
