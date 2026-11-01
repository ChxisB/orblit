using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>
/// A feed as it is made and as it enters the scene message. What the
/// platform does with a picture is native and is checked there.
/// </summary>
public sealed class FeedTests
{
    private static FeedSource Made(FakeFeedHost? host = null) =>
        FeedSource.Create(1280, 720, host ?? new FakeFeedHost());

    private static RenderScene SceneWith(
        IReadOnlyList<Video>? videos = null,
        IReadOnlyList<Feed>? feeds = null,
        IReadOnlyList<RenderMaterial>? materials = null) =>
        new()
        {
            Objects = [],
            Videos = videos ?? [],
            Feeds = feeds ?? [],
            Materials = materials ?? [],
            Camera = SceneBuilders.CameraAt(0, 0, 5),
        };

    [Fact]
    public void ASourceIsMadeWithItsSizeAndAnswersItsNumber()
    {
        var host = new FakeFeedHost();

        var source = Made(host);

        Assert.Equal([(1280, 720)], host.Created);
        Assert.Equal(7, source.Id);
        Assert.Equal(1280, source.Width);
        Assert.Equal(720, source.Height);
    }

    [Fact]
    public void ASourceRefusesASizeWithNothingInItBeforeAskingThePlatform()
    {
        var host = new FakeFeedHost();

        Assert.ThrowsAny<ArgumentException>(() => FeedSource.Create(0, 720, host));
        Assert.Empty(host.Created);
    }

    [Fact]
    public void ASourceSaysSoWhereThePlatformHasNoFeeds()
    {
        Assert.Throws<PlatformNotSupportedException>(() => Made(new FakeFeedHost { IsSupported = false }));
    }

    [Fact]
    public void ASourcePassesOnAnyOtherPlatformFailure()
    {
        var host = new FakeFeedHost { CreateFails = new IOException("no-feed") };

        Assert.Throws<IOException>(() => Made(host));
    }

    [Fact]
    public void ASourceDoesNotTakeANumberThatIsNotOne()
    {
        Assert.Throws<InvalidOperationException>(() => Made(new FakeFeedHost { Answer = 0 }));
    }

    [Fact]
    public void ASourceIsReleasedOnceHoweverManyTimesItIsAsked()
    {
        var host = new FakeFeedHost();
        var source = Made(host);

        source.Dispose();
        source.Dispose();

        Assert.Equal([7L], host.Released);
        Assert.True(source.IsReleased);
    }

    [Fact]
    public void AFeedIsNamedForItsNumberAndCarriesItsOrientation()
    {
        var source = Made();
        var feed = new Feed(3, source, rotation: 3, mirror: true);

        Assert.Equal("feed:7", feed.Path);
        Assert.Equal((3 << 2) | 16, feed.Flags);
        // Upright and not mirrored.
        Assert.Equal(0, new Feed(3, source).Flags);
    }

    [Fact]
    public void AFeedIsRefusedAFifthQuarterTurn()
    {
        var source = Made();

        Assert.ThrowsAny<ArgumentException>(() => new Feed(1, source, rotation: 4));
    }

    [Fact]
    public void AFeedTravelsAfterTheVideosWithNeutralRateVolumeAndSeek()
    {
        var source = Made();
        var message = SceneWith(
            videos: [new Video { Key = 5, Path = "/tmp/a.mp4", Rate = 2 }],
            feeds: [new Feed(9, source, rotation: 1)]).ToMessage();

        Assert.Equal([5L, 9L], SceneBuilders.Read<long[]>(message, "videoKeys"));
        Assert.Equal(["/tmp/a.mp4", "feed:7"], SceneBuilders.Read<string[]>(message, "videoPaths"));
        Assert.Equal([3, 1 << 2], SceneBuilders.Read<int[]>(message, "videoFlags"));
        SceneBuilders.NearAll(SceneBuilders.Read<float[]>(message, "videoParams")[Video.Stride..], 1, 1, -1, 0);
    }

    [Fact]
    public void AFeedIsWhatAScreenPointsAtByItsPlaceInTheList()
    {
        var source = Made();
        var message = SceneWith(
            videos: [new Video { Key = 5, Path = "/tmp/a.mp4" }],
            feeds: [new Feed(9, source)],
            materials:
            [
                new RenderMaterial { Key = 1, Shading = MaterialShading.Video, Video = 9 },
                new RenderMaterial { Key = 2, Shading = MaterialShading.Video, Video = 5 },
            ]).ToMessage();

        Assert.Equal([1, 0], SceneBuilders.Read<int[]>(message, "materialVideos"));
    }

    [Fact]
    public void AFeedMayNotShareAKeyWithAVideo()
    {
        var scene = SceneWith(
            videos: [new Video { Key = 5, Path = "/tmp/a.mp4" }],
            feeds: [new Feed(5, Made())],
            materials: [new RenderMaterial { Key = 1 }]);

        Assert.ThrowsAny<ArgumentException>(() => scene.ToMessage());
    }

    [Fact]
    public void AFeedIsKeptWhenTheSceneIsCopied()
    {
        var scene = SceneWith(feeds: [new Feed(9, Made())]);

        Assert.Single((scene with { Videos = [] }).Feeds);
    }

    [Fact]
    public void ATextureOfAFeedIsNamedByKeyAndSizeAndIsNeverSrgb()
    {
        var texture = Texture.OfFeed(new Feed(9, Made()));

        Assert.Equal("orblit:feed/9/1280x720", texture.Path);
        Assert.False(texture.Srgb);
    }

    [Fact]
    public void ATextureOfAFeedIsTheSourceUprightSoAQuarterTurnSwapsItsSides()
    {
        var source = Made();

        Assert.Equal("orblit:feed/9/720x1280", Texture.OfFeed(new Feed(9, source, rotation: 1)).Path);
        Assert.Equal("orblit:feed/9/1280x720", Texture.OfFeed(new Feed(9, source, rotation: 2, mirror: true)).Path);
    }

    [Fact]
    public void ATextureOfAFeedMayBeCopiedSmaller()
    {
        var feed = new Feed(9, Made());

        Assert.Equal("orblit:feed/9/320x180", Texture.OfFeed(feed, width: 320, height: 180).Path);
        // A side left out is the source's.
        Assert.Equal("orblit:feed/9/320x720", Texture.OfFeed(feed, width: 320).Path);
        Assert.ThrowsAny<ArgumentException>(() => Texture.OfFeed(feed, width: 0));
    }

    [Fact]
    public void ATextureOfAFeedTravelsAsThePathOfAMap()
    {
        var feed = new Feed(9, Made());
        var message = SceneWith(
            feeds: [feed],
            materials: [new RenderMaterial { Key = 1, BaseColourMap = Texture.OfFeed(feed) }]).ToMessage();

        Assert.Equal(["orblit:feed/9/1280x720"], SceneBuilders.Read<string[]>(message, "texturePaths"));
        Assert.Equal([0], SceneBuilders.Read<int[]>(message, "textureSrgb"));
    }
}
