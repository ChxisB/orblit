namespace Orblit.Filament.Tests;

public sealed class SplatAddedTests
{
    private static SplatCloud Memory(int key, int revision = 0) =>
        new(key, data: SplatCloud.Pack([0, 0, 0], [1, 1, 1], [1, 1, 1, 1])) { Revision = revision };

    [Fact]
    public void RecordsThatAreNotWholeAreRefused()
    {
        Assert.ThrowsAny<ArgumentException>(() => new SplatCloud(1, data: new byte[33]));
    }

    [Fact]
    public void ArraysThatDisagreeAboutTheCountAreRefused()
    {
        Assert.ThrowsAny<ArgumentException>(() => SplatCloud.Pack([0, 0, 0], [1, 1], [1, 1, 1, 1]));
        Assert.ThrowsAny<ArgumentException>(() => SplatCloud.Pack([0, 0, 0], [1, 1, 1], [1, 1, 1]));
        Assert.ThrowsAny<ArgumentException>(() => SplatCloud.Pack([0, 0, 0], [1, 1, 1], [1, 1, 1, 1], [1, 0, 0]));
    }

    [Fact]
    public void ARotationOfNothingIsTheIdentityAndNotANaN()
    {
        var bytes = SplatCloud.Pack([0, 0, 0], [1, 1, 1], [1, 1, 1, 1], [0, 0, 0, 0]);

        Assert.Equal<byte>([255, 128, 128, 128], bytes[28..32]);
    }

    [Fact]
    public void AColourOutsideZeroToOneIsHeldToTheEnds()
    {
        var bytes = SplatCloud.Pack([0, 0, 0], [1, 1, 1], [-1, 2, 0.5f, 7]);

        Assert.Equal<byte>([0, 255, 128, 255], bytes[24..28]);
    }

    [Fact]
    public void AFileCloudIsNeverSentRecordsWhateverTheRendererHolds()
    {
        var message = SceneBuilders.Of([]) with { Splats = [new SplatCloud(1, "a.ply") { Revision = 9 }] };

        var sent = message.ToMessage(1, new Held());

        Assert.Empty(SceneBuilders.Read<int[]>(sent, "splatChanged"));
        Assert.Empty(SceneBuilders.Read<byte[]>(sent, "splatData"));
    }

    [Fact]
    public void RecordsOfSeveralChangedCloudsAreJoinedInTheOrderTheyAreNamed()
    {
        var first = SplatCloud.Pack([1, 0, 0], [1, 1, 1], [0, 0, 0, 0]);
        var second = SplatCloud.Pack([2, 0, 0, 3, 0, 0], [1, 1, 1, 1, 1, 1], [0, 0, 0, 0, 0, 0, 0, 0]);
        var scene = SceneBuilders.Of([]) with { Splats = [new SplatCloud(5, data: first), new SplatCloud(4, data: second)] };

        var message = scene.ToMessage(1);

        Assert.Equal([5, 4], SceneBuilders.Read<int[]>(message, "splatChanged"));
        Assert.Equal([1, 2], SceneBuilders.Read<int[]>(message, "splatChangedCounts"));
        Assert.Equal([.. first, .. second], SceneBuilders.Read<byte[]>(message, "splatData"));
    }

    [Fact]
    public void ARevisionThatWentBackwardsIsSentAgain()
    {
        var scene = SceneBuilders.Of([]) with { Splats = [Memory(1, 2)] };
        var held = new Held();
        held.Splats[1] = 5;

        var message = scene.ToMessage(1, held);

        Assert.Equal([1], SceneBuilders.Read<int[]>(message, "splatChanged"));
    }

    [Fact]
    public void HeldRemembersARevisionForMemoryCloudsOnlyButKnowsItHoldsAFile()
    {
        var held = new Held();
        held.Remember(SceneBuilders.Of([]) with { Splats = [Memory(1, 4), new SplatCloud(2, "a.ply")] });

        Assert.Equal([1], held.Splats.Keys);
        Assert.Equal(4, held.Splats[1]);
        Assert.True(held.HasSplats);

        held.Remember(SceneBuilders.Of([]));
        Assert.Empty(held.Splats);
        Assert.False(held.HasSplats);
    }
}
