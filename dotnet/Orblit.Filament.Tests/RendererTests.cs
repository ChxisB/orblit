namespace Orblit.Filament.Tests;

public sealed class RendererTests
{
    private const uint Width = 64;
    private const uint Height = 48;
    private const int WarmUpFrames = 4;

    [Theory]
    [InlineData(-1, RendererResult.Null)]
    [InlineData(-2, RendererResult.Length)]
    [InlineData(-3, RendererResult.Failed)]
    [InlineData(-4, RendererResult.Range)]
    public void ANegativeResultThrowsWithTheCallAndTheCode(int code, RendererResult expected)
    {
        var thrown = Assert.Throws<RendererException>(() => Renderer.Check(code, "draw"));

        Assert.Equal(expected, thrown.Result);
        Assert.Contains("orblit_renderer_draw", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASuccessThrowsNothing() => Renderer.Check(0, "draw");

    [RendererFact]
    public void ADisposedRendererRefusesEveryCall()
    {
        var renderer = new Renderer(Width, Height);
        renderer.Dispose();
        renderer.Dispose();

        Assert.Throws<InvalidOperationException>(() => renderer.Draw(0));
    }

    [RendererFact]
    public void ARenderersBackendIsTheOneItIsDrawingWith()
    {
        using var renderer = new Renderer(Width, Height);

        Assert.NotEqual(Backend.Default, renderer.Backend);
    }

    [RendererFact]
    public void AWindowSurfaceWithNoWindowDoesNotStart()
    {
        var thrown = Assert.Throws<InvalidOperationException>(
            () => new Renderer(Width, Height, surface: new Surface(SurfaceKind.Window, 0)));

        Assert.IsNotType<RendererException>(thrown);
    }

    [RendererFact]
    public void NothingIsReadBackUntilAFrameHasBeenAskedFor()
    {
        using var renderer = new Renderer(Width, Height);
        renderer.Draw(0);

        Assert.Null(renderer.ReadCapture());
    }

    [RendererFact]
    public void AResizeTakesEffectAtTheNextFrame()
    {
        using var renderer = new Renderer(Width, Height);
        renderer.Resize(32, 24);
        DrawWarmUp(renderer);
        renderer.RequestCapture();

        var capture = Captures.Wait(renderer, WarmUpFrames);

        Assert.Equal((32u, 24u), (capture.Width, capture.Height));
    }

    [RendererFact]
    public void ANewHeadlessSurfaceKeepsTheRendererDrawing()
    {
        using var renderer = new Renderer(Width, Height);
        renderer.AttachSurface(Surface.Headless, 32, 24);
        DrawWarmUp(renderer);
        renderer.RequestCapture();

        var capture = Captures.Wait(renderer, WarmUpFrames);

        Assert.Equal((32u, 24u), (capture.Width, capture.Height));
    }

    [RendererFact]
    public void TheSharedSurfaceIsRefusedAndTheRendererLivesOn()
    {
        using var renderer = new Renderer(Width, Height);

        var thrown = Assert.Throws<RendererException>(() => renderer.AttachSurface(new Surface(SurfaceKind.Platform, 0), Width, Height));
        renderer.Draw(0);

        Assert.Equal(RendererResult.Failed, thrown.Result);
        Assert.Equal(1UL, renderer.RenderedFrames);
    }

    [RendererFact]
    public void DrawingWhileDetachedRendersNothing()
    {
        using var renderer = new Renderer(Width, Height);
        renderer.Draw(0);
        renderer.DetachSurface();
        renderer.Draw(1);

        Assert.Equal(1UL, renderer.RenderedFrames);
    }

    [RendererFact]
    public void AnEmptySceneRunsNoPassesAndBatchesNothing()
    {
        using var renderer = new Renderer(Width, Height);
        DrawWarmUp(renderer);

        var stats = renderer.GetStats();
        var passes = renderer.GetPassTimings();

        // No render graph is set, so no pass runs.
        Assert.Empty(passes);
        Assert.Equal(0u, stats.PassCount);
        Assert.Equal((0u, 0u), (stats.BatchedObjects, stats.BatchGroups));
        Assert.All(passes, pass => Assert.True(pass.Milliseconds >= 0 && pass.Drawn >= 0));
    }

    [RendererFact]
    public void AnEmptySceneHasNothingItCouldNotDo()
    {
        using var renderer = new Renderer(Width, Height);
        DrawWarmUp(renderer);

        Assert.Empty(renderer.GetNotes());
    }

    [RendererFact]
    public void ANoteThatIsNotThereIsOutOfRange()
    {
        using var renderer = new Renderer(Width, Height);

        var thrown = Assert.Throws<RendererException>(() => Renderer.Check(NativeMethods.NoteAt(renderer.Alive, 0, out _, out _), "note"));

        Assert.Equal(RendererResult.Range, thrown.Result);
    }

    [RendererFact]
    public void ACameraTakesThreeNumbersForEachPoint()
    {
        using var renderer = new Renderer(Width, Height);

        Assert.Throws<ArgumentException>(() => renderer.SetCamera([0f, 1f], [0f, 0f, 0f], 60f, false, 0f, 0.0));
        Assert.Throws<ArgumentException>(() => renderer.SetCamera([0f, 1f, 5f], [0f, 0f, 0f, 0f], 60f, false, 0f, 0.0));
    }

    private static void DrawWarmUp(Renderer renderer)
    {
        for (var frame = 0; frame < WarmUpFrames; frame++)
        {
            renderer.Draw(frame / 60.0);
        }
    }
}
