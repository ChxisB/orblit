namespace Orblit.Filament.Tests;

public sealed class FeedBufferTests
{
    private const int Ok = 0;
    private const int Null = -1;
    private const int Failed = -3;

    [Fact(Skip = RendererLibrary.AppleSkipReason, SkipUnless = nameof(RendererLibrary.ApplePresent), SkipType = typeof(RendererLibrary))]
    public void AFeedTakesABufferBackedByAnIoSurface()
    {
        var feed = NativeMethods.CreateFeed(64, 48);
        Assert.True(feed > 0);
        var buffer = PixelBuffers.CreateSharedWithGpu(64, 48);
        try
        {
            Assert.Equal(Ok, NativeMethods.SubmitFeedBuffer(feed, buffer));
        }
        finally
        {
            PixelBuffers.Release(buffer);
            NativeMethods.ReleaseFeed(feed);
        }
    }

    [Fact(Skip = RendererLibrary.AppleSkipReason, SkipUnless = nameof(RendererLibrary.ApplePresent), SkipType = typeof(RendererLibrary))]
    public void AFeedRefusesABufferWithNoIoSurface()
    {
        var feed = NativeMethods.CreateFeed(64, 48);
        var buffer = PixelBuffers.CreateInMemoryOnly(64, 48);
        try
        {
            Assert.Equal(Failed, NativeMethods.SubmitFeedBuffer(feed, buffer));
        }
        finally
        {
            PixelBuffers.Release(buffer);
            NativeMethods.ReleaseFeed(feed);
        }
    }

    [Fact(Skip = RendererLibrary.AppleSkipReason, SkipUnless = nameof(RendererLibrary.ApplePresent), SkipType = typeof(RendererLibrary))]
    public void AReleasedFeedRefusesABuffer()
    {
        var feed = NativeMethods.CreateFeed(64, 48);
        NativeMethods.ReleaseFeed(feed);
        var buffer = PixelBuffers.CreateSharedWithGpu(64, 48);
        try
        {
            Assert.Equal(Failed, NativeMethods.SubmitFeedBuffer(feed, buffer));
        }
        finally
        {
            PixelBuffers.Release(buffer);
        }
    }

    [Fact(Skip = RendererLibrary.AppleSkipReason, SkipUnless = nameof(RendererLibrary.ApplePresent), SkipType = typeof(RendererLibrary))]
    public void EachFeedHasItsOwnNumber()
    {
        var first = NativeMethods.CreateFeed(64, 48);
        var second = NativeMethods.CreateFeed(64, 48);
        try
        {
            Assert.NotEqual(first, second);
        }
        finally
        {
            NativeMethods.ReleaseFeed(first);
            NativeMethods.ReleaseFeed(second);
        }
    }

    [Fact(Skip = RendererLibrary.SkipReason, SkipUnless = nameof(RendererLibrary.Present), SkipType = typeof(RendererLibrary))]
    public void ANullBufferIsRefusedAsNull()
    {
        Assert.Equal(Null, NativeMethods.SubmitFeedBuffer(1, 0));
    }

    [Fact(Skip = RendererLibrary.SkipReason, SkipUnless = nameof(RendererLibrary.Present), SkipType = typeof(RendererLibrary))]
    public void ReleasingANumberThatIsNotAFeedDoesNothing()
    {
        NativeMethods.ReleaseFeed(long.MaxValue);
        NativeMethods.ReleaseFeed(0);
    }
}
