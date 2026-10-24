using Avalonia.Controls;
using Avalonia.Layout;

namespace Orblit.Filament.Avalonia.Tests;

public sealed class RendererViewportTests
{
    private const int Width = 64;
    private const int Height = 48;
    private const int Ticks = 12;

    [Fact]
    public Task TheWayOfShowingFramesIsPendingUntilTheControlIsInAWindow() => Headless.Run(() =>
    {
        var viewport = new RendererViewport(new FakePresenter().Choose());

        Assert.Equal(ViewportPath.Pending, viewport.Path);
        Assert.False(viewport.Frame(Scenes.Box(), 0));
        Assert.Null(viewport.Renderer);
        return Task.CompletedTask;
    });

    [Fact]
    public async Task AnInteropFreeCompositorTakesTheReadbackPathAndSaysWhy() => await Headless.Run(async () =>
    {
        var viewport = new RendererViewport();

        var window = await Headless.ShowAsync(viewport, Width, Height);

        Assert.Equal(ViewportPath.Readback, viewport.Path);
        Assert.False(string.IsNullOrWhiteSpace(viewport.Fallback));
        window.Close();
    });

    [Fact]
    public async Task APathWithNoReasonIsTheSharedTexture() => await Headless.Run(async () =>
    {
        var viewport = new RendererViewport(new FakePresenter().Choose());

        var window = await Headless.ShowAsync(viewport, Width, Height);

        Assert.Equal(ViewportPath.SharedTexture, viewport.Path);
        Assert.Null(viewport.Fallback);
        window.Close();
    });

    [RendererFact]
    public async Task FramesDrawAtTheSizeOfTheControlAndReachTheScreen() => await Headless.Run(async () =>
    {
        var viewport = new RendererViewport();
        var window = await Headless.ShowAsync(viewport, Width, Height);

        for (var tick = 0; tick < Ticks; tick++)
        {
            Assert.True(viewport.Frame(Scenes.Box(), tick / 60.0));
        }

        Assert.True(viewport.Renderer!.RenderedFrames >= Ticks);
        // The first captures arrive late, and after that every frame is shown.
        Assert.True(viewport.DroppedFrames <= 3, $"{viewport.DroppedFrames} of {Ticks} frames were dropped.");
        window.Close();
    });

    [RendererFact]
    public async Task AResizedControlResizesTheRenderer() => await Headless.Run(async () =>
    {
        var viewport = new RendererViewport();
        var window = await Headless.ShowAsync(viewport, Width, Height);
        for (var tick = 0; tick < Ticks; tick++)
        {
            viewport.Frame(Scenes.Box(), tick / 60.0);
        }

        viewport.Width = Width * 2;
        viewport.Height = Height * 2;
        window.Width = Width * 2;
        window.Height = Height * 2;
        window.UpdateLayout();
        for (var tick = 0; tick < Ticks; tick++)
        {
            viewport.Frame(Scenes.Box(), (Ticks + tick) / 60.0);
        }

        var capture = viewport.Renderer!.ReadCapture()!.Value;
        Assert.Equal((uint)(Width * 2), capture.Width);
        Assert.Equal((uint)(Height * 2), capture.Height);
        window.Close();
    });

    [RendererFact]
    public async Task LeavingTheWindowFreesTheRendererAndAReturnStartsAnother() => await Headless.Run(async () =>
    {
        var viewport = new RendererViewport();
        var window = await Headless.ShowAsync(viewport, Width, Height);
        viewport.Frame(Scenes.Box(), 0);
        var first = viewport.Renderer!;

        window.Content = null;

        Assert.Null(viewport.Renderer);
        Assert.Equal(ViewportPath.Pending, viewport.Path);
        Assert.Null(viewport.Fallback);
        Assert.Throws<InvalidOperationException>(() => first.RenderedFrames);
        window.Content = viewport;
        window.UpdateLayout();
        await viewport.Choosing;
        Assert.True(viewport.Frame(Scenes.Box(), 1));
        Assert.NotSame(first, viewport.Renderer);
        window.Close();
    });

    [Fact]
    public async Task LeavingTheWindowDisposesThePath() => await Headless.Run(async () =>
    {
        var path = new FakePresenter();
        var viewport = new RendererViewport(path.Choose());
        var window = await Headless.ShowAsync(viewport, Width, Height);

        window.Content = null;

        Assert.True(path.Disposed);
        window.Close();
    });

    [Fact]
    public async Task APathThatArrivesAfterTheControlLeftIsDisposedAtOnce() => await Headless.Run(async () =>
    {
        var path = new FakePresenter();
        var arrival = new TaskCompletionSource<(IFramePresenter, string?)>();
        var viewport = new RendererViewport(_ => arrival.Task);
        var window = new Window { Content = viewport };
        window.Show();
        window.Content = null;

        arrival.SetResult((path, null));
        await viewport.Choosing;

        Assert.True(path.Disposed);
        Assert.Equal(ViewportPath.Pending, viewport.Path);
        window.Close();
    });

    [RendererFact]
    public async Task ABusyScreenDropsTheFrameBeforeAnythingIsDrawn() => await Headless.Run(async () =>
    {
        var path = new FakePresenter { Busy = true };
        var viewport = new RendererViewport(path.Choose());
        var window = await Headless.ShowAsync(viewport, Width, Height);

        Assert.False(viewport.Frame(Scenes.Box(), 0));

        Assert.Equal(1, viewport.DroppedFrames);
        Assert.Null(viewport.Renderer);
        Assert.Empty(path.Calls);
        window.Close();
    });

    [RendererFact]
    public async Task AFrameIsDrawnBetweenBeginAndEnd() => await Headless.Run(async () =>
    {
        var path = new FakePresenter();
        var viewport = new RendererViewport(path.Choose());
        var window = await Headless.ShowAsync(viewport, Width, Height);

        Assert.True(viewport.Frame(Scenes.Box(), 0));

        var size = $"{(int)viewport.Bounds.Width}x{(int)viewport.Bounds.Height}";
        Assert.Equal(["begin", $"end {size}"], path.Calls);
        Assert.Equal(path.FramesAtBegin + 1, path.FramesAtEnd);
        Assert.Equal(0, viewport.DroppedFrames);
        window.Close();
    });

    [RendererFact]
    public async Task TheSceneIsDescribedBeforeTheFrameIsDrawn() => await Headless.Run(async () =>
    {
        var viewport = new RendererViewport(new FakePresenter().Choose());
        var window = await Headless.ShowAsync(viewport, Width, Height);

        viewport.Frame(Scenes.Row(), 0);

        // Only a scene the renderer was told about has objects to merge.
        Assert.Equal(6u, viewport.Renderer!.GetStats().BatchedObjects);
        window.Close();
    });

    [RendererFact]
    public async Task AFrameTheScreenCouldNotShowIsCounted() => await Headless.Run(async () =>
    {
        var path = new FakePresenter { Shows = false };
        var viewport = new RendererViewport(path.Choose());
        var window = await Headless.ShowAsync(viewport, Width, Height);

        Assert.True(viewport.Frame(Scenes.Box(), 0));

        Assert.Equal(1, viewport.DroppedFrames);
        window.Close();
    });

    [RendererFact]
    public async Task AFaultOnTheScreensSideIsThrownWithItsCause() => await Headless.Run(async () =>
    {
        var cause = new InvalidOperationException("the compositor said no");
        var viewport = new RendererViewport(new FakePresenter { Fault = cause }.Choose());
        var window = await Headless.ShowAsync(viewport, Width, Height);

        var thrown = Assert.Throws<InvalidOperationException>(() => viewport.Frame(Scenes.Box(), 0));

        Assert.Same(cause, thrown.InnerException);
        window.Close();
    });

    [Fact]
    public async Task TheControlDrawsWhatItsPathShows() => await Headless.Run(async () =>
    {
        var path = new FakePresenter();
        var viewport = new RendererViewport(path.Choose());
        var window = await Headless.ShowAsync(viewport, Width, Height);
        path.Calls.Clear();

        viewport.InvalidateVisual();
        Headless.Render();

        Assert.Contains("paint", path.Calls);
        window.Close();
    });

    [Theory]
    [InlineData(0, 0)]
    [InlineData(Width, 0)]
    [InlineData(0, Height)]
    public async Task AControlWithNoAreaDrawsNothing(int width, int height) => await Headless.Run(async () =>
    {
        var path = new FakePresenter();
        var viewport = new RendererViewport(path.Choose());
        var window = await Headless.ShowAsync(viewport, width, height);

        Assert.False(viewport.Frame(Scenes.Box(), 0));

        Assert.Null(viewport.Renderer);
        Assert.Empty(path.Calls);
        window.Close();
    });

    [RendererFact]
    public async Task TwoViewportsDrawSideBySide() => await Headless.Run(async () =>
    {
        var left = new RendererViewport();
        var right = new RendererViewport();
        var window = new Window { Width = Width * 2, Height = Height, Content = new StackPanel { Orientation = Orientation.Horizontal, Children = { left, right } } };
        left.Width = right.Width = Width;
        left.Height = right.Height = Height;
        window.Show();
        window.UpdateLayout();
        await Task.WhenAll(left.Choosing, right.Choosing);

        for (var tick = 0; tick < Ticks; tick++)
        {
            Assert.True(left.Frame(Scenes.Box(), tick / 60.0));
            Assert.True(right.Frame(Scenes.Box(), tick / 60.0));
        }

        Assert.NotSame(left.Renderer, right.Renderer);
        window.Close();
    });
}
