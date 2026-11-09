using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;

namespace Orblit.Ui.Tests;

public sealed class UiInputRouterTests
{
    static readonly Vector2 s_onControl = new(10, 10);
    static readonly Vector2 s_empty = new(150, 10);

    readonly FakeUi _ui = new();
    readonly FakeScene _scene = new();
    readonly UiInputRouter _router;

    public UiInputRouterTests() => _router = new UiInputRouter(_ui, _scene);

    [Fact]
    public void APressOnAControlGoesToTheUiOnly()
    {
        _router.MouseButton(1, s_onControl, true);

        Assert.Equal(["button Left True"], _ui.Calls);
        Assert.Empty(_scene.Calls);
    }

    [Fact]
    public void APressOnEmptySpaceGoesToTheSceneOnly()
    {
        _router.MouseButton(1, s_empty, true);

        Assert.Equal(["button 1 True"], _scene.Calls);
        Assert.Empty(_ui.Calls);
    }

    [Theory]
    [InlineData(1, "Left")]
    [InlineData(2, "Right")]
    [InlineData(3, "Middle")]
    public void EachMouseButtonIsNamedForTheUi(long button, string name)
    {
        _router.MouseButton(button, s_onControl, true);

        Assert.Equal([$"button {name} True"], _ui.Calls);
    }

    [Fact]
    public void AReleaseGoesToTheSideThatTookThePress()
    {
        _router.MouseButton(1, s_onControl, true);
        _router.MouseButton(1, s_empty, false);
        _router.MouseButton(2, s_empty, true);
        _router.MouseButton(2, s_onControl, false);

        Assert.Equal(["button Left True", "button Left False"], _ui.Calls);
        Assert.Equal(["button 2 True", "button 2 False"], _scene.Calls);
    }

    [Fact]
    public void TheUiSeesEveryMoveAndTheSceneOnlyOverEmptySpace()
    {
        _router.MouseMove(s_onControl, Vector2.One);
        _router.MouseMove(s_empty, Vector2.One);

        Assert.Equal(["move", "move"], _ui.Calls);
        Assert.Equal(["move"], _scene.Calls);
    }

    [Fact]
    public void MovesStayWithTheUiWhileADragHoldsAControl()
    {
        _router.MouseButton(1, s_onControl, true);
        _router.MouseMove(s_empty, Vector2.One);
        _router.MouseButton(1, s_empty, false);
        _router.MouseMove(s_empty, Vector2.One);

        Assert.Equal(["move"], _scene.Calls);
    }

    [Fact]
    public void AButtonTheUiCannotNameIsSwallowedOverAControl()
    {
        _router.MouseButton(4, s_onControl, true);
        _router.MouseButton(4, s_empty, false);

        Assert.Empty(_ui.Calls);
        Assert.Empty(_scene.Calls);
    }

    [Fact]
    public void KeysGoToTheSceneUntilATextBoxHasFocus()
    {
        _router.Key(65, true);
        _ui.Keys = true;
        _router.Key(65, false);
        _router.Key(66, true);

        Assert.Equal(["key 65 True", "key 65 False"], _scene.Calls);
        Assert.Equal(["key B True"], _ui.Calls);
    }

    [Fact]
    public void AKeyReleaseFollowsItsPressWhenTheFocusMovesBetween()
    {
        _ui.Keys = true;
        _router.Key(66, true);
        _ui.Keys = false;
        _router.Key(66, false);

        Assert.Equal(["key B True", "key B False"], _ui.Calls);
        Assert.Empty(_scene.Calls);
    }

    [Fact]
    public void AKeyTheUiCannotNameIsSwallowedWhileATextBoxHasFocus()
    {
        _ui.Keys = true;
        _router.Key(1, true);
        _router.Key(1, false);

        Assert.Empty(_ui.Calls);
        Assert.Empty(_scene.Calls);
    }

    [Fact]
    public void TextReachesTheUiOnlyWhileATextBoxHasFocus()
    {
        _router.Text("a");
        _ui.Keys = true;
        _router.Text("b");

        Assert.Equal(["text b"], _ui.Calls);
        Assert.Empty(_scene.Calls);
    }

    // The engine's codes for the keys a text box or a button reads.
    [Theory]
    [InlineData(65, Key.A)]
    [InlineData(90, Key.Z)]
    [InlineData(48, Key.D0)]
    [InlineData(57, Key.D9)]
    [InlineData(32, Key.Space)]
    [InlineData(4194305, Key.Escape)]
    [InlineData(4194306, Key.Tab)]
    [InlineData(4194308, Key.Back)]
    [InlineData(4194309, Key.Enter)]
    [InlineData(4194310, Key.Enter)]
    [InlineData(4194312, Key.Delete)]
    [InlineData(4194317, Key.Home)]
    [InlineData(4194318, Key.End)]
    [InlineData(4194319, Key.Left)]
    [InlineData(4194320, Key.Up)]
    [InlineData(4194321, Key.Right)]
    [InlineData(4194322, Key.Down)]
    [InlineData(4194323, Key.PageUp)]
    [InlineData(4194324, Key.PageDown)]
    public void EachEngineKeyIsNamedForTheUi(long keycode, Key expected)
    {
        _ui.Keys = true;

        _router.Key(keycode, true);

        Assert.Equal([$"key {expected} True"], _ui.Calls);
    }

    [Theory]
    [InlineData(64)]
    [InlineData(91)]
    [InlineData(47)]
    [InlineData(58)]
    [InlineData(97)]
    [InlineData(4194304)]
    [InlineData(4194307)]
    public void AKeyNextToAMappedRangeIsNotMapped(long keycode)
    {
        _ui.Keys = true;

        _router.Key(keycode, true);

        Assert.Empty(_ui.Calls);
    }

    [Fact]
    public void RoutedTextAndKeysReachARealFocusedBoxAndNotTheScene() => UiThread.Run(() =>
    {
        var box = new TextBox { Width = 100, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        using var layer = new UiLayer(box, new PixelSize(200, 100), new FakeSurface());
        var router = new UiInputRouter(layer, _scene);

        router.Key(65, true);
        router.Key(65, false);
        box.Focus();
        router.Text("abc");
        router.Key(4194308, true);
        router.Key(4194308, false);

        Assert.Equal("ab", box.Text);
        Assert.Equal(["key 65 True", "key 65 False"], _scene.Calls);
    });
}
