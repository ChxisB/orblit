using Orblit.Json;
using static Orblit.Input.Tests.ActionFixture;

namespace Orblit.Input.Tests;

public class ContextTests
{
    [Fact]
    public void ContextsStartOffUnlessAskedFor()
    {
        var map = Map(active: []);

        map.Update(Frame(keys: ["Space"]), Ms(0));

        Assert.False(map.IsActive("play"));
        Assert.False(map.IsDown("jump"));
    }

    [Fact]
    public void ContextsShadowOnlyTheActionsTheyBind()
    {
        var map = Map(active: ["play", "vehicle"]);

        map.Update(Frame(keys: ["Space", "KeyD"]), Ms(0));
        Assert.False(map.IsDown("jump"), "the vehicle took jump");
        Assert.Equal(1, map.Value("steer"));

        map.Update(Frame(keys: ["KeyE"]), Ms(16));
        Assert.True(map.IsDown("jump"));

        map.Deactivate("vehicle");
        map.Update(Frame(), Ms(32));
        map.Update(Frame(keys: ["Space"]), Ms(48));
        Assert.True(map.IsDown("jump"));
    }

    [Fact]
    public void ContextsThatBlockHideEverythingUnderThem()
    {
        var map = Map(active: ["play", "menu"]);

        map.Update(Frame(keys: ["KeyD", "KeyF", "Enter"]), Ms(0));

        Assert.True(map.IsDown("confirm"));
        Assert.False(map.IsDown("fire"));
        Assert.Equal(0, map.Value("steer"));
        Assert.Equal(0, map.Vector("move").Length);

        map.Deactivate("menu");
        map.Update(Frame(keys: ["KeyD", "KeyF"]), Ms(16));
        Assert.Equal(1, map.Value("steer"));
        map.Update(Frame(), Ms(32));
        map.Update(Frame(keys: ["KeyF"]), Ms(48));
        Assert.True(map.IsDown("fire"));
    }

    [Fact]
    public void ContextsThatBlockStillLetAContextAboveThemSpeak()
    {
        var map = Map(active: ["play", "menu", "vehicle"]);

        map.Update(Frame(keys: ["KeyE", "KeyD"]), Ms(0));

        Assert.True(map.IsDown("jump"), "above the menu");
        Assert.Equal(0, map.Value("steer"));
    }

    [Fact]
    public void AMenuClosingDoesNotPressWhatWasAlreadyHeld()
    {
        var map = Map(active: ["play", "menu"]);
        map.Update(Frame(keys: ["Space"]), Ms(0));
        map.Update(Frame(keys: ["Space"]), Ms(16));

        map.Deactivate("menu");
        map.Update(Frame(keys: ["Space"]), Ms(32));
        map.Update(Frame(keys: ["Space"]), Ms(48));
        Assert.Equal((false, false), (map.IsDown("jump"), map.WasPressed("jump")));

        map.Update(Frame(), Ms(64));
        map.Update(Frame(keys: ["Space"]), Ms(80));
        Assert.Equal((true, true), (map.IsDown("jump"), map.WasPressed("jump")));
    }

    [Fact]
    public void AButtonThatIsHiddenWhileItIsDownIsReleased()
    {
        var map = Map();
        map.Update(Frame(keys: ["Space"]), Ms(0));

        map.Activate("menu");
        map.Update(Frame(keys: ["Space"]), Ms(16));

        Assert.Equal((false, true), (map.IsDown("jump"), map.WasReleased("jump")));
        Assert.False(map.WasTapped("jump"), "it was cut off");
    }

    [Fact]
    public void AButtonWhoseBindingIsTakenOverWhileItIsDownIsReleased()
    {
        var map = Map(active: ["play"]);
        map.Update(Frame(keys: ["Space"]), Ms(0));
        map.Update(Frame(keys: ["Space"]), Ms(16));

        map.Activate("vehicle");
        map.Update(Frame(keys: ["Space"]), Ms(32));
        Assert.Equal((false, true, false), (map.IsDown("jump"), map.WasReleased("jump"), map.WasTapped("jump")));

        map.Deactivate("vehicle");
        map.Update(Frame(keys: ["Space"]), Ms(48));
        Assert.Equal((false, false), (map.IsDown("jump"), map.WasPressed("jump")));

        map.Update(Frame(), Ms(64));
        map.Update(Frame(keys: ["Space"]), Ms(80));
        Assert.True(map.WasPressed("jump"));
    }

    [Fact]
    public void AButtonWhoseKeyIsReboundWhileItIsDownIsReleased()
    {
        var map = Map();
        map.Update(Frame(keys: ["Space"]), Ms(0));

        map.Bindings.Rebind("jump", Key("KeyJ"), "play");
        map.Update(Frame(keys: ["Space"]), Ms(16));

        Assert.Equal((false, true, false), (map.IsDown("jump"), map.WasReleased("jump"), map.WasTapped("jump")));
    }

    [Fact]
    public void AHeldButtonStaysDownThroughALoadThatChangesNothing()
    {
        var map = Map();
        map.Bindings.Rebind("jump", Key("KeyJ"), "play");
        map.Update(Frame(keys: ["KeyJ"]), Ms(0));
        map.Update(Frame(keys: ["KeyJ"]), Ms(16));

        map.Bindings.Load(Saved(map.Bindings));
        map.Update(Frame(keys: ["KeyJ"]), Ms(32));

        Assert.Equal((true, false), (map.IsDown("jump"), map.WasReleased("jump")));
    }

    [Fact]
    public void AHeldButtonIsCutWhenALoadChangesItsKey()
    {
        var map = Map();
        map.Update(Frame(keys: ["Space"]), Ms(0));

        map.Bindings.Load(JsonText.Decode("""{"contexts":{"play":{"jump":[{"key":"KeyJ"}]}}}"""));
        map.Update(Frame(keys: ["Space"]), Ms(16));

        Assert.Equal((false, true), (map.IsDown("jump"), map.WasReleased("jump")));
    }

    [Fact]
    public void AControlThatIsDownOnTheFirstUpdateCountsAsAPress()
    {
        var map = Map();

        map.Update(Frame(keys: ["Space"]), Ms(0));

        Assert.True(map.WasPressed("jump"));
    }

    [Fact]
    public void AContextGoesToTheTopWhenActivatedAgain()
    {
        var map = Map(active: ["play", "vehicle"]);

        map.Activate("play");
        map.Update(Frame(keys: ["Space"]), Ms(0));
        Assert.True(map.IsDown("jump"));

        map.Update(Frame(keys: ["KeyE"]), Ms(16));
        Assert.False(map.IsDown("jump"), "play now shadows vehicle");
    }

    [Fact]
    public void AContextIsOnOnceHoweverOftenItIsActivated()
    {
        var map = Map();

        map.Activate("play");
        map.Activate("play");
        map.Deactivate("play");

        Assert.False(map.IsActive("play"));
    }

    [Fact]
    public void AContextThatDoesNotExistIsRefused()
    {
        var map = Map();

        Assert.Throws<ArgumentException>(() => map.Activate("nope"));
        map.Deactivate("nope");
    }
}
