namespace Orblit.Filament.Tests;

/// <summary>
/// The lookup belongs to a package source, so these run against
/// a fake one and check what the caller makes of each answer.
/// </summary>
public sealed class AssetFilesTests
{
    private sealed class FakeSource : IAssetFileSource
    {
        public AssetFile? Found { get; init; }

        public Exception? Fails { get; init; }

        public bool Held { get; init; }

        public List<string> Asked { get; } = [];

        public AssetFile? Find(string key)
        {
            Asked.Add(key);
            return Fails is null ? Found : throw Fails;
        }

        public bool Release(string key)
        {
            Asked.Add(key);
            return Held;
        }
    }

    [Fact]
    public void AnAssetIsAskedForByItsKeyAndAnsweredAsARange()
    {
        var expected = new AssetFile("/proc/self/fd/41", 4096, 900);
        var source = new FakeSource { Found = expected };

        var file = new AssetFiles(source).Of("assets/cooked/game.android.opack");

        Assert.Equal(["assets/cooked/game.android.opack"], source.Asked);
        Assert.Equal(expected, file);
        Assert.Equal("/proc/self/fd/41", file!.Path);
        Assert.Equal(4096, file.Offset);
        Assert.Equal(900, file.Length);
    }

    [Fact]
    public void AnAssetThePackageDoesNotHaveIsNull()
    {
        Assert.Null(new AssetFiles(new FakeSource()).Of("assets/none.opack"));
    }

    [Fact]
    public void AnyOtherFailureReachesTheCaller()
    {
        var source = new FakeSource { Fails = new IOException("orblit_failed") };

        Assert.Throws<IOException>(() => new AssetFiles(source).Of("assets/a.opack"));
    }

    [Fact]
    public void AnAnswerThatIsNotARangeIsRefused()
    {
        Assert.ThrowsAny<ArgumentException>(() => new AssetFile("/x", -1, 3));
        Assert.ThrowsAny<ArgumentException>(() => new AssetFile("/x", 0, -3));
    }

    [Fact]
    public void APlatformWithNoSourceIsNotAskedAndHasNoRange()
    {
        var files = new AssetFiles(null);

        Assert.Null(files.Of("assets/a.opack"));
        Assert.False(files.Release("assets/a.opack"));
    }

    [Fact]
    public void ReleasingSaysWhetherThePackageHeldTheAsset()
    {
        var held = new FakeSource { Held = true };
        Assert.True(new AssetFiles(held).Release("assets/a.opack"));
        Assert.Equal(["assets/a.opack"], held.Asked);

        Assert.False(new AssetFiles(new FakeSource()).Release("assets/a.opack"));
    }

    [Fact]
    public void AnAssetWithNoKeyIsRefusedBeforeItIsAsked()
    {
        var source = new FakeSource();
        var files = new AssetFiles(source);

        Assert.ThrowsAny<ArgumentException>(() => files.Of(""));
        Assert.ThrowsAny<ArgumentException>(() => files.Release(""));
        Assert.Empty(source.Asked);
    }
}
