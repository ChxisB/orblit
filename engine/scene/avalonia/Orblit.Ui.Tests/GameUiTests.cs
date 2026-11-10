using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Orblit.Ui.Sample;

namespace Orblit.Ui.Tests;

public sealed class GameUiTests
{
    static readonly PixelSize s_size = new(640, 360);
    static readonly Vector2 s_empty = new(600, 330);

    static Vector2 Center(Control control, GameUi ui)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), ui.Root)!.Value;
        return new Vector2((float)point.X, (float)point.Y);
    }

    [Fact]
    public void TheMenuIsDrawnPremultipliedAndTheRestOfTheFrameIsClear() => UiThread.Run(() =>
    {
        var ui = new GameUi();
        var surface = new FakeSurface();
        using var layer = new UiLayer(ui.Root, s_size, surface);

        layer.Render();

        // The menu's padding is 0xCC alpha over 0x20, 0x28, 0x30. Premultiplied, red is 0x20 * 0xCC / 0xFF, about 26.
        var (r, g, b, a) = surface.At(320 - 110 + 12, 180);
        Assert.InRange(a, 0xC8, 0xCC);
        Assert.InRange(r, 24, 28);
        Assert.InRange(g, 30, 34);
        Assert.InRange(b, 36, 40);
        Assert.Equal(0, surface.At(600, 330).A);
        Assert.Equal(0, surface.At(5, 355).A);
    });

    // A game builds its layer once and renders every frame after. The 24 px title "Paused" is about 84 px wide, and
    // wrong glyphs make a narrower mark.
    [Fact]
    public void TheTitleHasItsWidthWhenTheFirstRenderComesInALaterCall()
    {
        var surface = new FakeSurface();
        UiLayer layer = null!; // set by the first call below
        UiThread.Run(() => layer = new UiLayer(new GameUi().Root, s_size, surface));

        UiThread.Run(() => layer.Render());
        UiThread.Run(() => layer.Dispose());

        Assert.True(surface.Uploads > 0, "The first render in a later call gave no frame.");
        var inked = Enumerable.Range(225, 120).Where(x => Enumerable.Range(120, 40).Any(y => surface.At(x, y).R > 150)).ToList();
        Assert.InRange(inked.Max() - inked.Min(), 75, 95);
    }

    [Fact]
    public void TheHudDrawsAtTheTopLeft() => UiThread.Run(() =>
    {
        var ui = new GameUi();
        var surface = new FakeSurface();
        using var layer = new UiLayer(ui.Root, s_size, surface);

        layer.Render();

        var inked = Enumerable.Range(16, 200).Sum(x => Enumerable.Range(16, 40).Count(y => surface.At(x, y).A > 0));
        Assert.True(inked > 200, $"Only {inked} pixels drawn in the health corner.");
        Assert.Equal(0, surface.At(300, 20).A);
    });

    [Fact]
    public void AClickOnAMenuButtonReachesItAndNotTheScene() => UiThread.Run(() =>
    {
        var ui = new GameUi();
        var (resumed, quit) = (0, 0);
        ui.Resume.Click += (_, _) => resumed++;
        ui.Quit.Click += (_, _) => quit++;
        var scene = new FakeScene();
        using var layer = new UiLayer(ui.Root, s_size, new FakeSurface());
        layer.Render();
        var router = new UiInputRouter(layer, scene);
        var on = Center(ui.Resume, ui);

        router.MouseMove(on, Vector2.Zero);
        router.MouseButton(1, on, true);
        router.MouseButton(1, on, false);

        Assert.Equal((1, 0), (resumed, quit));
        Assert.Empty(scene.Calls);
    });

    [Fact]
    public void AClickOnEmptySpaceReachesTheSceneAndNoButton() => UiThread.Run(() =>
    {
        var ui = new GameUi();
        var clicks = 0;
        ui.Resume.Click += (_, _) => clicks++;
        ui.Quit.Click += (_, _) => clicks++;
        var scene = new FakeScene();
        using var layer = new UiLayer(ui.Root, s_size, new FakeSurface());
        layer.Render();
        var router = new UiInputRouter(layer, scene);

        router.MouseMove(s_empty, Vector2.One);
        router.MouseButton(1, s_empty, true);
        router.MouseButton(1, s_empty, false);

        Assert.Equal(0, clicks);
        Assert.Equal(["move", "button 1 True", "button 1 False"], scene.Calls);
    });

    [Fact]
    public void TheMenuTakesClicksOnlyWhileItIsShown() => UiThread.Run(() =>
    {
        var ui = new GameUi();
        using var layer = new UiLayer(ui.Root, s_size, new FakeSurface());
        layer.Render();
        var on = Center(ui.Resume, ui);
        Assert.True(layer.WantsPointer(new Point(on.X, on.Y)));

        ui.Menu.IsVisible = false;
        layer.Render();

        Assert.False(layer.WantsPointer(new Point(on.X, on.Y)));
    });
}
