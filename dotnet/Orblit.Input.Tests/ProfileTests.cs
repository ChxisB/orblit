using Orblit.Json;
using static Orblit.Input.Tests.ActionFixture;

namespace Orblit.Input.Tests;

public class ProfileTests
{
    private static readonly KeyControl Space = Key("Space");

    private static readonly KeyControl Left = Key("ArrowLeft");

    private const string Nothing = """{"contexts":{}}""";

    private readonly ActionMap _map = Map();

    private BindingTable Table => _map.Bindings;

    private List<Control> Controls(string action, string context = "play") =>
        Table.BindingsIn(context)[action].Select(binding => binding.Control).ToList();

    private static List<Control> ControlsOf(ActionMap map, string action) =>
        map.Bindings.BindingsIn("play")[action].Select(binding => binding.Control).ToList();

    private void Load(string json) => Table.Load(JsonText.Decode(json));

    [Fact]
    public void ItHasNothingInItUntilSomethingIsChanged()
    {
        Assert.Equal(Written(Nothing), Canonical(Table.ToJson()));
    }

    [Fact]
    public void ItHoldsOnlyTheActionsThatWereChanged()
    {
        Table.Rebind("steer", Left, "play");

        Assert.Equal(Written("""{"contexts":{"play":{"steer":[{"key":"ArrowLeft"},null]}}}"""), SavedText(Table));
    }

    [Fact]
    public void ItDropsAnActionThatIsPutBack()
    {
        Table.Rebind("fire", Left, "play");
        Table.Rebind("fire", Key("KeyF"), "play");

        Assert.Equal(Written(Nothing), Canonical(Table.ToJson()));
    }

    [Fact]
    public void ItLoadsIntoANewMapAsItWas()
    {
        Table.Rebind("steer", Left, "play");
        Table.Rebind("jump", Key("KeyJ"), "play", slot: 2);
        var fresh = Map();

        fresh.Bindings.Load(Saved(Table));

        Assert.Equal(SavedText(Table), SavedText(fresh.Bindings));
        fresh.Update(Frame(keys: ["ArrowLeft"]), Ms(0));
        Assert.Equal(-1, fresh.Value("steer"));
        fresh.Update(Frame(keys: ["KeyJ"]), Ms(16));
        Assert.True(fresh.IsDown("jump"));
    }

    [Fact]
    public void ItKeepsTheScaleAndHalfAPlayerChose()
    {
        var half = new PadAxisControl(PadAxis.RightY, AxisSide.Negative);
        Table.Rebind("steer", new PadAxisControl(PadAxis.LeftX), "play", scale: -1);
        Table.Rebind("steer", Key("ArrowRight"), "play", slot: 1, scale: 0.5);
        Table.Rebind("fire", Key("KeyF"), "play", scale: 2);
        Table.Rebind("move", half, "play", slot: 5, scale: -1, part: AxisPart.Y);
        Table.Rebind("move", Left, "play", slot: 1, part: AxisPart.Y);

        const string expected = """
            {"contexts":{"play":{
              "steer":[{"padAxis":"leftX","scale":-1.0},{"key":"ArrowRight","scale":0.5}],
              "fire":[{"key":"KeyF","scale":2.0}],
              "move":[null,{"key":"ArrowLeft","part":"y"},null,null,null,
                {"padAxisNegative":"rightY","scale":-1.0,"part":"y"}]}}}
            """;
        Assert.Equal(Written(expected), SavedText(Table));
        var fresh = Map();
        fresh.Bindings.Load(Saved(Table));
        Assert.Equal(SavedText(Table), SavedText(fresh.Bindings));
        foreach (var action in new[] { "steer", "fire", "move" })
        {
            var now = fresh.Bindings.BindingsIn("play")[action].Select(b => (b.Control, b.Scale, b.Part));
            var was = Table.BindingsIn("play")[action].Select(b => (b.Control, b.Scale, b.Part));
            Assert.Equal(was, now);
        }
    }

    [Fact]
    public void ItLeavesASlotAtTheDefaultWhenItsScaleOrHalfIsWrong()
    {
        Load("""
            {"contexts":{"play":{"jump":[
              {"key":"KeyJ","scale":"big"},
              {"padButton":"north","scale":1e999},
              {"key":"KeyK","part":"z"},
              {"key":"KeyL","part":1},
              {"key":"KeyM","scale":null}]}}}
            """);

        Assert.Equal<Control>([Space, new PadButtonControl(PadButton.South)], Controls("jump"));
    }

    [Fact]
    public void ItTakesOnlyTheControlsAnActionCanTakeFromAList()
    {
        Load("""{"contexts":{"play":{"steer":[{"padStick":"left"},{"key":"KeyZ"}]}}}""");

        Assert.Equal<Control>([Key("KeyA"), Key("KeyZ")], Controls("steer"));
    }

    [Fact]
    public void ItLetsAChangedDefaultThroughToAPlayerWhoLeftItAlone()
    {
        Table.Rebind("steer", Left, "play");
        var newer = Map(jumpKey: "KeyJ");

        newer.Bindings.Load(Saved(Table));

        Assert.Equal<Control>(Key("KeyJ"), ControlsOf(newer, "jump")[0]);
        Assert.Equal<Control>(Left, ControlsOf(newer, "steer")[0]);
    }

    [Fact]
    public void ItKeepsAChangedDefaultInASlotThePlayerNeverTouched()
    {
        Table.Rebind("jump", Key("KeyJ"), "play", slot: 1);
        var newer = Map(jumpKey: "KeyZ");

        newer.Bindings.Load(Saved(Table));

        Assert.Equal<Control>([Key("KeyZ"), Key("KeyJ")], ControlsOf(newer, "jump"));
    }

    [Fact]
    public void ItKeepsTheDefaultsAShorterListDoesNotReach()
    {
        Load("""{"contexts":{"play":{"jump":[{"key":"KeyJ"}]}}}""");

        Assert.Equal<Control>([Key("KeyJ"), new PadButtonControl(PadButton.South)], Controls("jump"));
    }

    [Fact]
    public void ItAddsTheBindingsALongerListBrings()
    {
        Load("""{"contexts":{"play":{"fire":[{"key":"KeyF"},{"key":"KeyG"},{"touch":"fire"}]}}}""");

        Assert.Equal<Control>([Key("KeyF"), Key("KeyG"), new TouchControl("fire")], Controls("fire"));
    }

    [Fact]
    public void ItSkipsAContextOrAnActionTheGameNoLongerHas()
    {
        Load("""
            {"contexts":{
              "gone":{"jump":[{"key":"KeyZ"}]},
              "play":{"gone":[{"key":"KeyZ"}],"steer":[{"key":"ArrowLeft"}]},
              "menu":{"fire":[{"key":"KeyZ"}]}}}
            """);

        Assert.Equal<Control>(Left, Controls("steer")[0]);
        Assert.Equal<Control>(Space, Controls("jump")[0]);
    }

    [Fact]
    public void ItSkipsAControlTheActionCanNoLongerTake()
    {
        Load("""{"contexts":{"play":{"jump":[{"padStick":"left"}],"steer":[{"key":"ArrowLeft"}]}}}""");

        Assert.Equal<Control>(Space, Controls("jump")[0]);
        Assert.Equal<Control>(Left, Controls("steer")[0]);
    }

    [Fact]
    public void ItSkipsAControlItCannotReadAndKeepsTheRest()
    {
        Load("""
            {"contexts":{
              "play":{
                "jump":[{"padButton":"goneInTheNextVersion"},{"key":"KeyJ"}],
                "steer":[{"key":"ArrowLeft"},7],
                "fire":[{"key":1}]},
              "gone":{"jump":[{"wat":"x"}]}}}
            """);

        Assert.Equal<Control>([Space, Key("KeyJ")], Controls("jump"));
        Assert.Equal<Control>([Left, Key("KeyD")], Controls("steer"));
        Assert.Equal<Control>([Key("KeyF")], Controls("fire"));
    }

    [Fact]
    public void ItKeepsAViewSomebodyAlreadyHoldsUpToDate()
    {
        var view = Table.BindingsIn("play");

        Load("""{"contexts":{"play":{"fire":[{"key":"KeyG"}]}}}""");

        Assert.Equal<Control>(Key("KeyG"), Assert.Single(view["fire"]).Control);
        Load("{}");
        Assert.Equal<Control>(Key("KeyF"), Assert.Single(view["fire"]).Control);
    }

    [Fact]
    public void ItTakesAwayAnActionAPlayerBoundInAContext()
    {
        Table.Rebind("fire", Left, "menu");

        Load("{}");

        Assert.False(Table.BindingsIn("menu").ContainsKey("fire"));
    }

    [Fact]
    public void ItHandsOutListsThatCannotBeChangedAfterALoad()
    {
        Load("""{"contexts":{"play":{"jump":[{"key":"KeyJ"}]}}}""");

        Assert.Throws<NotSupportedException>(() => ((IList<Binding>)Table.BindingsIn("play")["jump"]).Clear());
    }

    [Fact]
    public void ItIsReplacedNotAddedToByTheNextLoad()
    {
        Table.Rebind("steer", Left, "play");

        Load("""{"contexts":{"play":{"fire":[{"key":"KeyG"}]}}}""");

        Assert.Equal<Control>(Key("KeyA"), Controls("steer")[0]);
        Assert.Equal<Control>([Key("KeyG")], Controls("fire"));
    }

    [Fact]
    public void ItGoesBackToTheDefaultsWhenLoadedEmpty()
    {
        Table.Rebind("steer", Left, "play");
        Table.Rebind("jump", Key("KeyJ"), "play", slot: 2);

        Load("{}");

        Assert.Equal(Written(Nothing), Canonical(Table.ToJson()));
        Assert.Equal(2, Controls("jump").Count);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"profile\"")]
    [InlineData("""{"contexts":3}""")]
    [InlineData("""{"contexts":{"play":3}}""")]
    [InlineData("""{"contexts":{"play":{"jump":"KeyZ"}}}""")]
    [InlineData("""{"contexts":{"play":{"steer":[{"key":"KeyZ"}],"jump":"KeyZ"}}}""")]
    public void ItIsRefusedWholeWhenItIsNotAProfile(string json)
    {
        Table.Rebind("steer", Left, "play");
        var before = SavedText(Table);

        Assert.Throws<FormatException>(() => Load(json));

        Assert.Equal(before, SavedText(Table));
    }
}
