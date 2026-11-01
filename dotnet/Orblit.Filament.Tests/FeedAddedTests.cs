namespace Orblit.Filament.Tests;

public sealed class FeedAddedTests
{
    private static FeedSource Made(FakeFeedHost host) => FeedSource.Create(64, 48, host);

    [Fact]
    public void ARotationOutsideTheFourQuarterTurnsIsRefusedAtBothEnds()
    {
        var source = Made(new FakeFeedHost());

        Assert.Throws<ArgumentOutOfRangeException>(() => new Feed(1, source, rotation: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Feed(1, source, rotation: 4));
        Assert.Equal(3 << 2, new Feed(1, source, rotation: 3).Flags);
    }

    [Fact]
    public void ASubmitGoesToTheHostWithTheSourcesNumberAndIsRefusedOnceReleased()
    {
        var host = new FakeFeedHost();
        var source = Made(host);

        source.Submit(0x1234);
        source.Dispose();

        Assert.Equal([(7L, (nint)0x1234)], host.Submitted);
        Assert.Throws<ObjectDisposedException>(() => source.Submit(0x1234));
        Assert.Single(host.Submitted);
    }

    [Fact]
    public void AFailedSubmitIsThrownWithTheRenderersResult()
    {
        var source = Made(new FakeFeedHost { SubmitResult = (int)RendererResult.Failed });

        var thrown = Assert.Throws<RendererException>(() => source.Submit(0x1234));

        Assert.Equal(RendererResult.Failed, thrown.Result);
        Assert.Contains("submit_feed_buffer", thrown.Message);
    }

    [Fact]
    public void ASourceReleasesNothingUntilItIsDisposed()
    {
        var host = new FakeFeedHost();
        var source = Made(host);

        Assert.False(source.IsReleased);
        Assert.Empty(host.Released);
    }

    [Fact]
    public void ATextureOfAFeedRefusesASideBelowOneAndSwapsADefaultedSideForAQuarterTurn()
    {
        var feed = new Feed(9, FeedSource.Create(1280, 720, new FakeFeedHost()), rotation: 1);

        Assert.ThrowsAny<ArgumentException>(() => Texture.OfFeed(feed, height: 0));
        Assert.ThrowsAny<ArgumentException>(() => Texture.OfFeed(feed, width: -5));
        Assert.Equal("orblit:feed/9/320x1280", Texture.OfFeed(feed, width: 320).Path);
        Assert.Equal("orblit:feed/9/720x100", Texture.OfFeed(feed, height: 100).Path);
    }

    [Fact]
    public void ASceneNamingTheSameKeyAsTwoFeedsStillPacksOneEntryEach()
    {
        var host = new FakeFeedHost();
        var scene = SceneBuilders.Of([]) with
        {
            Feeds = [new Feed(9, Made(host)), new Feed(10, Made(host))],
        };

        var message = scene.ToMessage();

        Assert.Equal([9L, 10L], SceneBuilders.Read<long[]>(message, "videoKeys"));
        Assert.Equal(["feed:7", "feed:8"], SceneBuilders.Read<string[]>(message, "videoPaths"));
    }

    [Fact(Skip = RendererLibrary.AppleSkipReason, SkipUnless = nameof(RendererLibrary.ApplePresent), SkipType = typeof(RendererLibrary))]
    public void ALiveFeedTakesABufferAndAScreenShowingItIsAcceptedAndDrawn()
    {
        using var renderer = new Renderer(64, 48);
        using var source = FeedSource.Create(64, 48);
        var buffer = PixelBuffers.CreateSharedWithGpu(64, 48);
        try
        {
            source.Submit(buffer);
            var feed = new Feed(9, source);
            var scene = SceneBuilders.Of([SceneBuilders.Plain(1) with { Material = 3 }]) with
            {
                Feeds = [feed],
                Materials = [new RenderMaterial { Key = 3, Shading = MaterialShading.Video, Video = 9 }],
            };

            renderer.Apply(scene);
            renderer.Draw(0);

            Assert.True(renderer.RenderedFrames >= 1);
        }
        finally
        {
            PixelBuffers.Release(buffer);
        }
    }

    [Fact(Skip = RendererLibrary.AppleSkipReason, SkipUnless = nameof(RendererLibrary.ApplePresent), SkipType = typeof(RendererLibrary))]
    public void ALiveSourceRefusesABufferWithNoIoSurfaceAndAReleasedOne()
    {
        var source = FeedSource.Create(64, 48);
        var buffer = PixelBuffers.CreateInMemoryOnly(64, 48);
        try
        {
            var thrown = Assert.Throws<RendererException>(() => source.Submit(buffer));
            Assert.Equal(RendererResult.Failed, thrown.Result);
        }
        finally
        {
            PixelBuffers.Release(buffer);
            source.Dispose();
        }
    }
}
