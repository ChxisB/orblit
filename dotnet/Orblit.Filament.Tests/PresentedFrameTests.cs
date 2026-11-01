namespace Orblit.Filament.Tests;

public sealed class PresentedFrameTests
{
    private const uint Width = 64;
    private const uint Height = 48;
    private const int Frames = 12;

    private static Renderer OnThePlatformSurface() =>
        new(Width, Height, Backend.Default, new Surface(SurfaceKind.Platform, 0));

    [RendererFact]
    public void AHeadlessRendererHasNothingPresented()
    {
        using var renderer = new Renderer(Width, Height);
        renderer.Draw(0);

        Assert.Null(renderer.CopyPresented());
    }

    [Fact(Skip = RendererLibrary.AppleSkipReason, SkipUnless = nameof(RendererLibrary.ApplePresent), SkipType = typeof(RendererLibrary))]
    public void APlatformRendererPresentsAnIoSurfaceAtItsSize()
    {
        using var renderer = OnThePlatformSurface();
        renderer.Draw(0);

        using var frame = renderer.CopyPresented();

        Assert.NotNull(frame);
        Assert.Equal((64, 48), (frame.Width, frame.Height));
        Assert.NotEqual(nint.Zero, frame.Surface);
        Assert.NotEqual(0u, frame.SurfaceId);
    }

    [Fact(Skip = RendererLibrary.AppleSkipReason, SkipUnless = nameof(RendererLibrary.ApplePresent), SkipType = typeof(RendererLibrary))]
    public void FramesRotateThroughFourSurfacesSoAHostCanCacheByIdentifier()
    {
        using var renderer = OnThePlatformSurface();
        var ids = new HashSet<uint>();

        for (var frame = 0; frame < Frames; frame++)
        {
            renderer.Draw(frame);
            using var presented = renderer.CopyPresented();
            ids.Add(presented!.SurfaceId);
        }

        Assert.InRange(ids.Count, 2, 4);
    }

    [Fact(Skip = RendererLibrary.AppleSkipReason, SkipUnless = nameof(RendererLibrary.ApplePresent), SkipType = typeof(RendererLibrary))]
    public void AResizedRendererPresentsTheNewSize()
    {
        using var renderer = OnThePlatformSurface();
        renderer.Draw(0);
        renderer.Resize(96, 72);
        renderer.Draw(1);

        using var frame = renderer.CopyPresented();

        Assert.Equal((96, 72), (frame!.Width, frame.Height));
    }

    [Fact(Skip = RendererLibrary.AppleSkipReason, SkipUnless = nameof(RendererLibrary.ApplePresent), SkipType = typeof(RendererLibrary))]
    public void ADisposedFrameCanBeDisposedAgain()
    {
        using var renderer = OnThePlatformSurface();
        renderer.Draw(0);
        var frame = renderer.CopyPresented();

        frame!.Dispose();
        frame.Dispose();
    }
}
