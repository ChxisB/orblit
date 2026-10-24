using Orblit.VectorMath;
using static Orblit.Input.Tests.ActionFixture;

namespace Orblit.Input.Tests;

public class ActionMapTests
{
    private const double Tolerance = 1e-9;

    [Fact]
    public void AButtonGoesDownStaysDownAndComesUpWithAnEdgeForEach()
    {
        var map = Map();

        map.Update(Frame(keys: ["Space"]), Ms(0));
        Assert.Equal((true, true), (map.IsDown("jump"), map.WasPressed("jump")));

        map.Update(Frame(keys: ["Space"]), Ms(16));
        Assert.Equal((true, false), (map.IsDown("jump"), map.WasPressed("jump")));

        map.Update(Frame(), Ms(32));
        Assert.Equal((false, true), (map.IsDown("jump"), map.WasReleased("jump")));

        map.Update(Frame(), Ms(48));
        Assert.False(map.WasReleased("jump"));
    }

    [Fact]
    public void AButtonIsDownWhileAnyOneOfItsBindingsIs()
    {
        var map = Map();

        map.Update(Frame(keys: ["Space"], down: [PadButton.South]), Ms(0));
        map.Update(Frame(down: [PadButton.South]), Ms(16));
        Assert.True(map.IsDown("jump"));
        Assert.False(map.WasReleased("jump"));

        map.Update(Frame(), Ms(32));
        Assert.True(map.WasReleased("jump"));
    }

    [Fact]
    public void AButtonCanBeATriggerWhichIsDownFromHalfway()
    {
        var map = Play(
            InputAction.Button("gas"),
            new Binding(new PadAxisControl(PadAxis.RightTrigger), shaping: Shaping.None));

        map.Update(Frame(axes: Axes((PadAxis.RightTrigger, 0.4))), Ms(0));
        Assert.False(map.IsDown("gas"));
        map.Update(Frame(axes: Axes((PadAxis.RightTrigger, 0.6))), Ms(16));
        Assert.True(map.IsDown("gas"));
    }

    [Fact]
    public void AButtonCanBeAStickPushedOneWayWithANegativeScale()
    {
        var map = Play(
            InputAction.Button("crouch"),
            new Binding(new PadAxisControl(PadAxis.LeftY), scale: -1, shaping: Shaping.None));

        map.Update(Frame(axes: Axes((PadAxis.LeftY, 0.8))), Ms(0));
        Assert.False(map.IsDown("crouch"));
        map.Update(Frame(axes: Axes((PadAxis.LeftY, -0.8))), Ms(16));
        Assert.True(map.IsDown("crouch"));
    }

    [Fact]
    public void AnAxisAddsUpWhatItsBindingsGive()
    {
        var map = Map();

        map.Update(Frame(keys: ["KeyD"]), Ms(0));
        Assert.Equal(1, map.Value("steer"));
        map.Update(Frame(keys: ["KeyA"]), Ms(16));
        Assert.Equal(-1, map.Value("steer"));
        map.Update(Frame(keys: ["KeyA", "KeyD"]), Ms(32));
        Assert.Equal(0, map.Value("steer"));
        map.Update(Frame(), Ms(48));
        Assert.Equal(0, map.Value("steer"));
    }

    [Fact]
    public void AnAxisStaysBetweenMinusOneAndOne()
    {
        var map = Play(
            InputAction.Axis("zoom"),
            new Binding(Key("KeyP"), scale: 3),
            new Binding(Key("KeyQ"), scale: 3),
            new Binding(Key("KeyN"), scale: -5));

        map.Update(Frame(keys: ["KeyP", "KeyQ"]), Ms(0));
        Assert.Equal(1, map.Value("zoom"));
        map.Update(Frame(keys: ["KeyN"]), Ms(16));
        Assert.Equal(-1, map.Value("zoom"));
    }

    [Fact]
    public void AnAxisDropsWhatABindingsDeadZoneTakesOut()
    {
        var map = Play(
            InputAction.Axis("steer"),
            new Binding(new PadAxisControl(PadAxis.LeftX), shaping: new Shaping(inner: 0.2, outer: 1)));

        map.Update(Frame(axes: Axes((PadAxis.LeftX, 0.1))), Ms(0));
        Assert.Equal(0, map.Value("steer"));
        map.Update(Frame(axes: Axes((PadAxis.LeftX, 1))), Ms(16));
        Assert.Equal(1, map.Value("steer"));
    }

    [Fact]
    public void ATwoAxisActionAddsKeysOnEachHalf()
    {
        var map = Map();

        map.Update(Frame(keys: ["KeyW"]), Ms(0));
        Assert.Equal(new Vector2(0, 1), map.Vector("move"));
        map.Update(Frame(keys: ["KeyA", "KeyS"]), Ms(16));
        Assert.Equal(1, map.Vector("move").Length, Tolerance);
        Assert.True(map.Vector("move").X < 0);
        Assert.True(map.Vector("move").Y < 0);
    }

    [Fact]
    public void ATwoAxisActionIsNoFasterOnTheDiagonal()
    {
        var map = Map();

        map.Update(Frame(keys: ["KeyW", "KeyD"]), Ms(0));

        Assert.Equal(Math.Sqrt(0.5), map.Vector("move").X, Tolerance);
        Assert.Equal(Math.Sqrt(0.5), map.Vector("move").Y, Tolerance);
    }

    [Fact]
    public void ATwoAxisActionReadsAWholeStick()
    {
        var map = Map();

        map.Update(Frame(axes: Axes((PadAxis.LeftX, 0.3), (PadAxis.LeftY, 0.4))), Ms(0));

        Assert.Equal(0.3, map.Vector("move").X, Tolerance);
        Assert.Equal(0.4, map.Vector("move").Y, Tolerance);
    }

    [Fact]
    public void ATwoAxisActionAddsAStickAndKeysThenBringsTheTotalBackToOne()
    {
        var map = Map();

        map.Update(Frame(keys: ["KeyD"], axes: Axes((PadAxis.LeftX, 0.9))), Ms(0));

        Assert.Equal(1, map.Vector("move").X, Tolerance);
        Assert.Equal(0, map.Vector("move").Y);
    }

    [Fact]
    public void ATwoAxisActionShapesAStickAsAStick()
    {
        var map = Play(
            InputAction.Axis2("look"),
            new Binding(new PadStickControl(PadStick.Right), shaping: new Shaping(inner: 0.2, outer: 1)));

        map.Update(Frame(axes: Axes((PadAxis.RightX, 0.15), (PadAxis.RightY, 0.15))), Ms(0));
        var gentle = map.Vector("look");
        Assert.True(gentle.Length > 0, "0.21 is past 0.2");
        Assert.Equal(gentle.Y, gentle.X, Tolerance);

        map.Update(Frame(axes: Axes((PadAxis.RightX, 0.1), (PadAxis.RightY, 0.1))), Ms(16));
        Assert.Equal(0, map.Vector("look").Length);
    }

    [Fact]
    public void ATwoAxisActionHandsOutACopy()
    {
        var map = Map();
        map.Update(Frame(keys: ["KeyW"]), Ms(0));

        var copy = map.Vector("move");
        copy.X = 5;
        copy.Y = 5;

        Assert.Equal(new Vector2(0, 1), map.Vector("move"));
    }

    [Fact]
    public void AskingAboutAnActionThatDoesNotExistIsAnArgumentError()
    {
        var map = Map();

        Assert.Throws<ArgumentException>(() => map.IsDown("nope"));
        Assert.Throws<ArgumentException>(() => map.Value("nope"));
        Assert.Throws<ArgumentException>(() => map.Vector("nope"));
    }

    [Fact]
    public void AskingInTheWrongWayIsAStateError()
    {
        var map = Map();

        Assert.Throws<InvalidOperationException>(() => map.IsDown("steer"));
        Assert.Throws<InvalidOperationException>(() => map.WasTapped("move"));
        Assert.Throws<InvalidOperationException>(() => map.Value("jump"));
        Assert.Throws<InvalidOperationException>(() => map.Value("move"));
        Assert.Throws<InvalidOperationException>(() => map.Vector("steer"));
    }

    [Fact]
    public void BuildingRefusesTwoActionsOfOneName()
    {
        Assert.Throws<ArgumentException>(() => new ActionMap([InputAction.Button("a"), InputAction.Axis("a")], []));
    }

    [Fact]
    public void BuildingRefusesTwoContextsOfOneName()
    {
        var none = new Dictionary<string, IReadOnlyList<Binding>>();

        Assert.Throws<ArgumentException>(() => new ActionMap([], [new InputContext("a", none), new InputContext("a", none)]));
    }

    [Fact]
    public void BuildingRefusesABindingForAnActionThatDoesNotExist()
    {
        var bindings = new Dictionary<string, IReadOnlyList<Binding>> { ["jump"] = [new Binding(Key("Space"))] };

        Assert.Throws<ArgumentException>(() => new ActionMap([], [new InputContext("a", bindings)]));
    }

    [Theory]
    [InlineData(ActionKind.Button)]
    [InlineData(ActionKind.Axis)]
    public void BuildingRefusesAWholeStickOnAnythingButATwoAxisAction(ActionKind kind)
    {
        var action = kind == ActionKind.Button ? InputAction.Button("x") : InputAction.Axis("x");

        Assert.Throws<ArgumentException>(() => Play(action, new Binding(new PadStickControl(PadStick.Left))));
    }

    [Fact]
    public void BuildingRefusesAScaleThatIsNotANumber()
    {
        Assert.ThrowsAny<ArgumentException>(() => Play(InputAction.Axis("x"), new Binding(Key("K"), scale: double.NaN)));
    }

    [Fact]
    public void BuildingRefusesToStartInAContextThatDoesNotExist()
    {
        Assert.Throws<ArgumentException>(() => new ActionMap([], [], ["a"]));
    }
}
