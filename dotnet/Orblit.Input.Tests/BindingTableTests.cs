using static Orblit.Input.Tests.ActionFixture;

namespace Orblit.Input.Tests;

public class BindingTableTests
{
    private static readonly KeyControl Space = Key("Space");

    private static readonly KeyControl Left = Key("ArrowLeft");

    private readonly ActionMap _map = Map();

    private BindingTable Table => _map.Bindings;

    private List<Control> Controls(string action, string context = "play") =>
        Table.BindingsIn(context)[action].Select(binding => binding.Control).ToList();

    [Fact]
    public void ReadingGivesWhatEachContextWasMadeWith()
    {
        Assert.Equal<Control>([Space, new PadButtonControl(PadButton.South)], Controls("jump"));
        Assert.Equal<Control>([Key("Enter"), Space], Controls("confirm", "menu"));
        Assert.True(Table.Blocks("menu"));
        Assert.False(Table.Blocks("play"));
        Assert.True(Table.HasContext("menu"));
        Assert.False(Table.HasContext("nope"));
    }

    [Fact]
    public void ReadingHandsOutViewsThatCannotBeChanged()
    {
        Assert.Throws<NotSupportedException>(() => ((IList<Binding>)Table.BindingsIn("play")["jump"]).Add(new Binding(Space)));
        Assert.Throws<NotSupportedException>(
            () => ((IDictionary<string, IReadOnlyList<Binding>>)Table.BindingsIn("play"))["x"] = []);
    }

    [Fact]
    public void ReadingDoesNotChangeAListSomebodyAlreadyHolds()
    {
        var before = Table.BindingsIn("play")["jump"];

        Table.Rebind("jump", Left, "play");

        Assert.Equal<Control>(Space, before[0].Control);
        Assert.Equal<Control>(Left, Controls("jump")[0]);
        Assert.Throws<NotSupportedException>(() => ((IList<Binding>)Table.BindingsIn("play")["jump"]).Add(new Binding(Space)));
    }

    [Fact]
    public void ReadingRefusesAContextThatDoesNotExist()
    {
        Assert.Throws<ArgumentException>(() => Table.BindingsIn("nope"));
        Assert.Throws<ArgumentException>(() => Table.Blocks("nope"));
    }

    [Fact]
    public void ClashNamesTheActionThatHasTheControl()
    {
        Assert.Equal("jump", Table.Clash("fire", Space, "play"));
    }

    [Fact]
    public void ClashIsNotAClashWithTheSlotBeingChanged()
    {
        Assert.Null(Table.Clash("jump", Space, "play"));
    }

    [Fact]
    public void ClashIsAClashWithAnotherSlotOfTheSameAction()
    {
        Assert.Equal("jump", Table.Clash("jump", Space, "play", slot: 1));
    }

    [Fact]
    public void ClashIsOnlyAClashInsideTheContext()
    {
        Assert.Equal("jump", Table.Clash("fire", Space, "play"));
        Assert.Null(Table.Clash("jump", Space, "vehicle"));
        Assert.Equal("confirm", Table.Clash("confirm", Space, "menu"));
    }

    [Fact]
    public void ClashFindsNothingForAControlNobodyHas()
    {
        Assert.Null(Table.Clash("fire", Left, "play"));
    }

    [Fact]
    public void RebindReplacesTheControlAndKeepsTheScale()
    {
        Table.Rebind("steer", Left, "play");

        Assert.Equal<Control>([Left, Key("KeyD")], Controls("steer"));
        Assert.Equal(-1, Table.BindingsIn("play")["steer"][0].Scale);
        _map.Update(Frame(keys: ["ArrowLeft"]), Ms(0));
        Assert.Equal(-1, _map.Value("steer"));
        _map.Update(Frame(keys: ["KeyA"]), Ms(16));
        Assert.Equal(0, _map.Value("steer"));
    }

    [Fact]
    public void RebindKeepsTheHalfOfATwoAxisAction()
    {
        Table.Rebind("move", Left, "play", slot: 3);

        Assert.Equal(AxisPart.Y, Table.BindingsIn("play")["move"][3].Part);
    }

    [Fact]
    public void RebindLeavesTheOtherContextsAlone()
    {
        Table.Rebind("jump", Left, "play");

        Assert.Equal<Control>([Key("KeyE")], Controls("jump", "vehicle"));
    }

    [Fact]
    public void RebindAddsABindingOnePastTheLast()
    {
        Table.Rebind("jump", Left, "play", slot: 2);

        Assert.Equal<Control>([Space, new PadButtonControl(PadButton.South), Left], Controls("jump"));
        Assert.Equal(1, Table.BindingsIn("play")["jump"][2].Scale);
    }

    [Fact]
    public void RebindRefusesASlotThatIsNotThere()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Table.Rebind("jump", Left, "play", slot: 3));
        Assert.Throws<ArgumentOutOfRangeException>(() => Table.Rebind("jump", Left, "play", slot: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Table.Rebind("fire", Space, "play", slot: 4));
    }

    [Fact]
    public void RebindTakesTheScaleAndHalfItIsToldTo()
    {
        Table.Rebind(
            "move",
            new PadAxisControl(PadAxis.RightY, AxisSide.Negative),
            "play",
            slot: 5,
            scale: -1,
            part: AxisPart.Y);

        var added = Table.BindingsIn("play")["move"][5];
        Assert.Equal((-1.0, AxisPart.Y), (added.Scale, added.Part));

        Table.Rebind("steer", Left, "play", scale: 0.5);
        Assert.Equal(0.5, Table.BindingsIn("play")["steer"][0].Scale);
        _map.Update(Frame(axes: Axes((PadAxis.RightY, 0.9))), Ms(0));
        Assert.Equal(0, _map.Vector("move").Y);
        _map.Update(Frame(axes: Axes((PadAxis.RightY, -0.9))), Ms(16));
        Assert.Equal(-0.9, _map.Vector("move").Y, 1e-9);
    }

    [Fact]
    public void RebindStartsTheScaleAndHalfAgainWhenAKeyBecomesAStick()
    {
        Table.Rebind("move", new PadStickControl(PadStick.Right), "play", slot: 2);
        var onStick = Table.BindingsIn("play")["move"][2];
        Assert.Equal((1.0, AxisPart.X), (onStick.Scale, onStick.Part));

        Table.Rebind("move", Key("KeyI"), "play", slot: 4);
        var onKey = Table.BindingsIn("play")["move"][4];
        Assert.Equal((1.0, AxisPart.X), (onKey.Scale, onKey.Part));
    }

    [Fact]
    public void RebindStartsTheScaleAgainForABothWaysAxisNotBetweenHalves()
    {
        var both = new PadAxisControl(PadAxis.LeftX);
        var other = new PadAxisControl(PadAxis.RightX);
        var positive = new PadAxisControl(PadAxis.LeftX, AxisSide.Positive);
        var negative = new PadAxisControl(PadAxis.LeftX, AxisSide.Negative);

        double ScaleOf() => Table.BindingsIn("play")["steer"][0].Scale;

        Table.Rebind("steer", both, "play");
        Assert.Equal(1, ScaleOf());

        Table.Rebind("steer", other, "play", scale: -1);
        Table.Rebind("steer", both, "play");
        Assert.Equal(-1, ScaleOf());

        Table.Rebind("steer", positive, "play");
        Assert.Equal(1, ScaleOf());

        Table.Rebind("steer", positive, "play", scale: -1);
        Table.Rebind("steer", negative, "play");
        Assert.Equal(-1, ScaleOf());
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RebindRefusesAScaleThatIsNotAFiniteNumber(double scale)
    {
        var error = Assert.Throws<ArgumentException>(() => Table.Rebind("steer", Left, "play", scale: scale));

        Assert.Equal("scale", error.ParamName);
        Assert.Equal<Control>(Key("KeyA"), Controls("steer")[0]);
    }

    [Fact]
    public void RebindBindsAnActionTheContextLeftOut()
    {
        Table.Rebind("fire", Left, "menu");

        Assert.Equal<Control>([Left], Controls("fire", "menu"));
        Assert.Equal(Written("""{"contexts":{"menu":{"fire":[{"key":"ArrowLeft"}]}}}"""), SavedText(Table));
        var fresh = Map(["play", "menu"]);
        fresh.Bindings.Load(Saved(Table));
        fresh.Update(Frame(keys: ["ArrowLeft"]), Ms(0));
        Assert.True(fresh.IsDown("fire"));
    }

    [Fact]
    public void RebindRefusesAControlTheActionCannotTake()
    {
        Assert.ThrowsAny<ArgumentException>(() => Table.Rebind("jump", new PadStickControl(PadStick.Left), "play"));
        Assert.Equal<Control>(Space, Controls("jump")[0]);
    }

    [Fact]
    public void RebindRefusesAContextOrAnActionThatDoesNotExist()
    {
        Assert.ThrowsAny<ArgumentException>(() => Table.Rebind("jump", Left, "nope"));
        Assert.ThrowsAny<ArgumentException>(() => Table.Rebind("nope", Left, "play"));
    }

    [Fact]
    public void RebindNamesTheFirstOfTwoBindingsThatHaveTheControl()
    {
        var a = Key("KeyA");

        Assert.Equal("steer", Table.Clash("fire", a, "play"));
        var clash = Assert.Throws<BindingClashException>(() => Table.Rebind("fire", a, "play", swap: true));
        Assert.Equal("steer", clash.Action);
        Assert.Equal<Control>([Key("KeyF")], Controls("fire"));
        Assert.Equal<Control>(a, Controls("steer")[0]);
    }

    [Fact]
    public void RebindThrowsAClashAndChangesNothingWhenAnotherActionHasIt()
    {
        var clash = Assert.Throws<BindingClashException>(() => Table.Rebind("fire", Space, "play"));

        Assert.Equal("jump", clash.Action);
        Assert.Equal<Control>(Space, clash.Control);
        Assert.Equal<Control>([Key("KeyF")], Controls("fire"));
        Assert.Equal<Control>(Space, Controls("jump")[0]);
    }

    [Fact]
    public void RebindThrowsAClashForAnotherSlotOfTheSameAction()
    {
        Assert.Throws<BindingClashException>(() => Table.Rebind("jump", new PadButtonControl(PadButton.South), "play"));
    }

    [Fact]
    public void RebindingToWhatTheSlotAlreadyHasIsNotAClash()
    {
        Table.Rebind("jump", Space, "play");

        Assert.Equal<Control>(Space, Controls("jump")[0]);
    }

    [Fact]
    public void ChangingOnlyTheHalfOfASharedKeyIsNotAClash()
    {
        var keyD = Key("KeyD");

        Table.Rebind("move", keyD, "play", slot: 1, part: AxisPart.Y);

        Assert.Equal(AxisPart.Y, Table.BindingsIn("play")["move"][1].Part);
        Assert.Equal<Control>(keyD, Controls("steer")[1]);
        Assert.Equal(
            Written("""{"contexts":{"play":{"move":[null,{"key":"KeyD","part":"y"},null,null,null]}}}"""),
            SavedText(Table));
        Assert.Null(Table.Clash("move", keyD, "play", slot: 1));
        Assert.Equal("steer", Table.Clash("move", keyD, "play", slot: 0));
    }

    [Fact]
    public void ASwapTradesWithTheActionThatHadIt()
    {
        Table.Rebind("fire", Space, "play", swap: true);

        Assert.Equal<Control>([Space], Controls("fire"));
        Assert.Equal<Control>(Key("KeyF"), Controls("jump")[0]);
    }

    [Fact]
    public void ASwapTheOtherActionCannotTakeIsRefusedAndChangesNothing()
    {
        Assert.ThrowsAny<ArgumentException>(() => Table.Rebind("move", Space, "play", slot: 4, swap: true));

        Assert.Equal<Control>(Space, Controls("jump")[0]);
        Assert.Equal<Control>(new PadStickControl(PadStick.Left), Controls("move")[4]);
    }

    [Fact]
    public void ASwapOnABindingThatIsBeingAddedIsRefused()
    {
        var error = Assert.Throws<ArgumentException>(() => Table.Rebind("fire", Space, "play", slot: 1, swap: true));

        Assert.Contains("nothing to swap", error.Message);
        Assert.Equal<Control>([Key("KeyF")], Controls("fire"));
        Assert.Equal<Control>(Space, Controls("jump")[0]);
    }

    [Fact]
    public void ASwapWithNothingInTheWayIsAPlainRebind()
    {
        Table.Rebind("fire", Left, "play", swap: true);

        Assert.Equal<Control>([Left], Controls("fire"));
    }
}
