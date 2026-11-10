using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Orblit.Ui.Tests;

public sealed class UiLayerTests
{
    static readonly PixelSize s_size = new(200, 100);
    static readonly Point s_onButton = new(20, 20);
    static readonly Point s_empty = new(150, 80);

    static Border Red() => new()
    {
        Width = 100,
        Height = 50,
        Background = Brushes.Red,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
    };

    static Button Go() => new()
    {
        Width = 100,
        Height = 40,
        Content = "Go",
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
    };

    [Fact]
    public void RenderedTreeHoldsItsColourAndLeavesTheRestClear() => UiThread.Run(() =>
    {
        var surface = new FakeSurface();
        using var layer = new UiLayer(Red(), s_size, surface);

        layer.Render();

        Assert.Equal((200, 100), (surface.Width, surface.Height));
        Assert.Equal(((byte)255, (byte)0, (byte)0, (byte)255), surface.At(10, 10));
        Assert.Equal(0, surface.At(150, 80).A);
    });

    [Fact]
    public void AFrameThatDidNotChangeIsNotUploadedAgain() => UiThread.Run(() =>
    {
        var surface = new FakeSurface();
        var red = Red();
        using var layer = new UiLayer(red, s_size, surface);

        layer.Render();
        layer.Render();
        Assert.Equal(1, surface.Uploads);

        red.Background = Brushes.Blue;
        layer.Render();
        Assert.Equal(2, surface.Uploads);
        Assert.Equal(((byte)0, (byte)0, (byte)255, (byte)255), surface.At(10, 10));
    });

    [Fact]
    public void AClearFirstFrameIsStillUploaded() => UiThread.Run(() =>
    {
        var surface = new FakeSurface();
        using var layer = new UiLayer(new Panel(), s_size, surface);

        layer.Render();

        Assert.Equal(1, surface.Uploads);
        Assert.Equal(0, surface.At(10, 10).A);
    });

    [Fact]
    public void TextIsDrawnWithTheBundledFont() => UiThread.Run(() =>
    {
        var surface = new FakeSurface();
        var label = new Border
        {
            Background = Brushes.Blue,
            Child = new TextBlock { Text = "Hi", FontSize = 48, Foreground = Brushes.White },
        };
        using var layer = new UiLayer(label, s_size, surface);

        layer.Render();

        var white = Enumerable.Range(0, 200 * 100).Count(i => surface.Pixels[i * 4] > 200 && surface.Pixels[i * 4 + 1] > 200);
        Assert.True(white > 50, $"Only {white} light pixels, so the text was not drawn.");
    });

    [Fact]
    public void AClickOnAButtonReachesItsHandler() => UiThread.Run(() =>
    {
        var clicks = 0;
        var button = Go();
        button.Click += (_, _) => clicks++;
        using var layer = new UiLayer(button, s_size, new FakeSurface());
        layer.Render();

        layer.PointerMoved(s_onButton);
        layer.PointerButton(s_onButton, MouseButton.Left, true);
        layer.PointerButton(s_onButton, MouseButton.Left, false);

        Assert.Equal(1, clicks);
    });

    [Fact]
    public void AClickOnEmptySpaceDoesNotReachTheButton() => UiThread.Run(() =>
    {
        var clicks = 0;
        var button = Go();
        button.Click += (_, _) => clicks++;
        using var layer = new UiLayer(button, s_size, new FakeSurface());
        layer.Render();

        layer.PointerMoved(s_empty);
        layer.PointerButton(s_empty, MouseButton.Left, true);
        layer.PointerButton(s_empty, MouseButton.Left, false);

        Assert.Equal(0, clicks);
    });

    [Fact]
    public void TheUiWantsAClickOnAControlAndNotOnEmptySpace() => UiThread.Run(() =>
    {
        using var layer = new UiLayer(Go(), s_size, new FakeSurface());
        layer.Render();

        Assert.True(layer.WantsPointer(s_onButton));
        Assert.False(layer.WantsPointer(s_empty));
    });

    [Fact]
    public void AskingWhatTheUiWantsChangesNothing() => UiThread.Run(() =>
    {
        var surface = new FakeSurface();
        using var layer = new UiLayer(Go(), s_size, surface);
        layer.Render();

        layer.WantsPointer(s_onButton);
        layer.Render();

        Assert.Equal(1, surface.Uploads);
    });

    [Fact]
    public void TextAndKeysReachTheFocusedBox() => UiThread.Run(() =>
    {
        var box = new TextBox { Width = 100, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        using var layer = new UiLayer(box, s_size, new FakeSurface());
        box.Focus();

        layer.Text("abc");
        layer.Key(Key.Back, true);
        Assert.Equal("ab", box.Text);

        layer.Key(Key.Back, false);
        Assert.Equal("ab", box.Text);
    });

    [Fact]
    public void TheUiWantsKeysOnlyWhileATextBoxHasFocus() => UiThread.Run(() =>
    {
        var box = new TextBox();
        var button = new Button { Content = "Go" };
        using var layer = new UiLayer(new StackPanel { Children = { button, box } }, s_size, new FakeSurface());
        Assert.False(layer.WantsKeys);

        button.Focus();
        Assert.False(layer.WantsKeys);

        box.Focus();
        Assert.True(layer.WantsKeys);
    });

    [Fact]
    public void DisposeFreesTheSurfaceAndTheWindowOnce() => UiThread.Run(() =>
    {
        var surface = new FakeSurface();
        var red = Red();
        var layer = new UiLayer(red, s_size, surface);
        Assert.True(red.IsAttachedToVisualTree());

        layer.Dispose();
        layer.Dispose();

        Assert.Equal(1, surface.Disposals);
        Assert.False(red.IsAttachedToVisualTree());
        Assert.Throws<ObjectDisposedException>(layer.Render);
    });

    [Fact]
    public void ALayerNeedsAContentAndASize() => UiThread.Run(() =>
    {
        var surface = new FakeSurface();

        Assert.Throws<ArgumentOutOfRangeException>(() => new UiLayer(Red(), new PixelSize(0, 100), surface));
        Assert.Throws<ArgumentNullException>(() => new UiLayer(null!, s_size, surface));
        Assert.Throws<ArgumentNullException>(() => new UiLayer(Red(), s_size, null!));
    });
}
