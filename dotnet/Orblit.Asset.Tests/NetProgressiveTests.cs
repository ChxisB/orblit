using static Orblit.Asset.Tests.NetWorld;

namespace Orblit.Asset.Tests;

public class NetProgressiveTests
{
    // Nothing waits for more than this to decide that a stand-in is worth drawing.
    private static readonly TimeSpan Brief = TimeSpan.FromMilliseconds(200);

    private static AssetFetcher OneTry(IAssetTransport transport, IContentStore? store = null) =>
        FetcherFor(transport, store, attempts: 1);

    private static MapTransport Pages(int cut = 0) => Map(
        ("wall.ktx2", new TransportPage(Body("the sharp one"), cut: cut)),
        ("wall_low.ktx2", new TransportPage(Body("the blurry one"), cut: cut)));

    private static async Task<List<AssetStage>> All(IAsyncEnumerable<AssetStage> stages)
    {
        var seen = new List<AssetStage>();
        await foreach (var stage in stages.WithCancellation(TestContext.Current.CancellationToken))
        {
            seen.Add(stage);
        }

        return seen;
    }

    private static IAsyncEnumerable<AssetStage> Stages(
        AssetFetcher fetcher,
        string path,
        string? standIn = null,
        TimeSpan? standInAfter = null,
        int roughSize = 256,
        Action<FetchProgress>? onProgress = null) =>
        fetcher.FetchInStagesAsync(
            Id(path),
            standIn is null ? null : Id(standIn),
            FetchUrgency.OnScreen,
            standInAfter,
            roughSize,
            onProgress,
            TestContext.Current.CancellationToken);

    private static string[] Texts(IEnumerable<AssetStage> stages) => [.. stages.Select(s => Text(s.Bytes))];

    // A listener that stays on the stream while the test looks at what has arrived so far.
    private sealed class Watcher : IDisposable
    {
        private readonly CancellationTokenSource _stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        private readonly List<AssetStage> _stages = [];
        private readonly Task _listening;

        public Watcher(IAsyncEnumerable<AssetStage> source) => _listening = Task.Run(() => ListenAsync(source));

        public AssetStage[] Stages
        {
            get
            {
                lock (_stages)
                {
                    return [.. _stages];
                }
            }
        }

        public async Task StopAsync()
        {
            await _stop.CancelAsync();
            await _listening;
        }

        public void Dispose() => _stop.Dispose();

        private async Task ListenAsync(IAsyncEnumerable<AssetStage> source)
        {
            try
            {
                await foreach (var stage in source.WithCancellation(_stop.Token))
                {
                    lock (_stages)
                    {
                        _stages.Add(stage);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Stopping is how a test walks away.
            }
        }
    }

    [Fact]
    public async Task DrawsTheStandInFirstThenTheRealThing()
    {
        // The big one is held back, because that is the only case a stand-in is
        // for: a wait long enough that something blurry beats nothing at all.
        var slow = new Staggered(
            new Dictionary<string, byte[]> { ["wall_low.ktx2"] = Body("the blurry one") },
            new Dictionary<string, byte[]> { ["wall.ktx2"] = Body("the sharp one") });
        var fetcher = new AssetFetcher(Origin, slow, attempts: 1, backoff: TimeSpan.Zero);

        using var watching = new Watcher(
            Stages(fetcher, "wall.ktx2", standIn: "wall_low.ktx2", standInAfter: TimeSpan.Zero));

        await Waits.Until(() => watching.Stages.Length == 1 && slow.IsHolding("wall.ktx2"), "the stand-in");
        var first = Assert.Single(watching.Stages);
        Assert.Equal("the blurry one", Text(first.Bytes));
        Assert.False(first.IsFinal);
        Assert.Equal(Id("wall_low.ktx2"), first.Id);

        slow.Release("wall.ktx2");
        await Waits.Until(() => watching.Stages.Length == 2, "the real one");
        await watching.StopAsync();

        Assert.Equal(["the blurry one", "the sharp one"], Texts(watching.Stages));
        Assert.True(watching.Stages[^1].IsFinal);
        Assert.Equal(Id("wall.ktx2"), watching.Stages[^1].Id);
    }

    // Both arrive in the same breath, as they do from a warm cache or a fast link. Flashing a blurry
    // texture for one frame on the way to a sharp one is worse than never showing it. A threaded timer
    // cannot be told to fire after the turn, so the delay here is a real one.
    [Fact]
    public async Task ALoadThatFinishesAtOnceShowsNoStandIn()
    {
        var fetcher = OneTry(Pages());

        var stages = await All(Stages(fetcher, "wall.ktx2", standIn: "wall_low.ktx2", standInAfter: Brief));

        Assert.Equal(["the sharp one"], Texts(stages));
    }

    [Fact]
    public async Task WithNoStandInItIsOneStage()
    {
        var fetcher = OneTry(Pages());

        var stages = await All(Stages(fetcher, "wall.ktx2"));
        Assert.True(Assert.Single(stages).IsFinal);
    }

    // The reason for the delay: a second launch reads from the cache in less time than a frame, and a
    // stand-in shown for one frame is a flicker, not a help.
    [Fact]
    public async Task SkipsTheStandInWhenTheRealOneIsAlreadyThere()
    {
        var store = new MemoryContentStore();
        var warm = OneTry(Pages(), store);
        await Read(warm, "wall.ktx2");
        await Read(warm, "wall_low.ktx2");

        var fetcher = OneTry(Pages(), store);
        var stages = await All(Stages(fetcher, "wall.ktx2", standIn: "wall_low.ktx2", standInAfter: TimeSpan.FromMilliseconds(50)));

        Assert.Equal(["the sharp one"], Texts(stages));
    }

    [Fact]
    public async Task AStandInThatWillNotLoadIsNotAFailure()
    {
        var fetcher = OneTry(Map(("wall.ktx2", new TransportPage(Body("the sharp one")))));

        var stages = await All(Stages(fetcher, "wall.ktx2", standIn: "nothing.ktx2", standInAfter: TimeSpan.Zero));

        Assert.Equal(["the sharp one"], Texts(stages));
    }

    [Fact]
    public async Task TheRealOneFailingEndsTheStreamWithThatFailure()
    {
        var fetcher = OneTry(Map(("wall_low.ktx2", new TransportPage(Body("the blurry one")))));

        var failure = await Assert.ThrowsAsync<FetchFailedException>(() => All(
            Stages(fetcher, "wall.ktx2", standIn: "wall_low.ktx2", standInAfter: TimeSpan.Zero)));

        Assert.Equal<object>(404, failure.Cause);
    }

    [Fact]
    public async Task WalkingAwayCancelsBothFetches()
    {
        var held = new Staggered(
            new Dictionary<string, byte[]>(),
            new Dictionary<string, byte[]> { ["wall.ktx2"] = Body("the sharp one"), ["wall_low.ktx2"] = Body("the blurry one") });
        var fetcher = new AssetFetcher(Origin, held, attempts: 1, backoff: TimeSpan.Zero);

        using var watching = new Watcher(Stages(fetcher, "wall.ktx2", standIn: "wall_low.ktx2", standInAfter: TimeSpan.FromSeconds(10)));
        await Waits.Until(() => held.IsHolding("wall.ktx2"), "the real request");
        await watching.StopAsync();

        // Nothing is left running, and nothing is left waiting to be drawn.
        await Waits.Until(() => fetcher.Running == 0 && fetcher.Waiting == 0, "both fetches to end");
    }

    [Fact]
    public async Task NothingIsFetchedUntilSomebodyIsListening()
    {
        var transport = Pages();
        var fetcher = OneTry(transport);

        _ = Stages(fetcher, "wall.ktx2", standIn: "wall_low.ktx2");
        await Waits.Settle();

        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task ProgressIsReportedForTheOneBeingWaitedOn()
    {
        var fetcher = OneTry(Pages(cut: 4));

        var seen = new List<long>();
        await All(Stages(fetcher, "wall.ktx2", standIn: "wall_low.ktx2", standInAfter: TimeSpan.Zero, onProgress: p => seen.Add(p.Received)));

        Assert.NotEmpty(seen);
        Assert.Equal(Body("the sharp one").Length, seen[^1]);
    }

    private static byte[] Mipped() => Ktx2Files.Mipped(width: 512, height: 512, levels: 10);

    [Fact]
    public async Task DrawsTheTexturesOwnCoarseLevelsThenTheSharpOne()
    {
        var whole = Mipped();
        var transport = Map(("wall.ktx2", new TransportPage(whole, cut: 512)));
        var fetcher = OneTry(transport);

        var stages = await All(Stages(fetcher, "wall.ktx2", roughSize: 64));

        Assert.Equal(2, stages.Count);
        Assert.False(stages[0].IsFinal);

        // The same asset, smaller. Not a substitute for it.
        Assert.Equal(Id("wall.ktx2"), stages[0].Id);
        Assert.Equal(64L, Ktx2Chain.Read(stages[0].Bytes)!.Width);
        Assert.True(stages[1].IsFinal);
        Assert.Equal(whole, stages[1].Bytes);
        Assert.Single(transport.Sent);
    }

    // Once the real texture's own small levels are on screen, a different picture standing in for it is a
    // step backwards. The real one is slowed after its front half so the stand-in's timer does run out
    // while it is still arriving, and only the coarse levels can have called it off.
    [Fact]
    public async Task TheCoarseLevelsCallOffTheStandIn()
    {
        var whole = Mipped();
        var inner = Map(("wall.ktx2", new TransportPage(whole, cut: whole.Length / 8)), ("wall_low.ktx2", new TransportPage(Body("the blurry one"))));
        var fetcher = OneTry(new Slowed(inner, fast: whole.Length / 2, between: TimeSpan.FromMilliseconds(150)));

        var stages = await All(Stages(fetcher, "wall.ktx2", standIn: "wall_low.ktx2", roughSize: 64, standInAfter: TimeSpan.FromMilliseconds(40)));

        Assert.All(stages, stage => Assert.Equal(Id("wall.ktx2"), stage.Id));
        Assert.Single(stages, stage => !stage.IsFinal);
    }

    [Fact]
    public async Task ATextureWithNoMipChainJustArrives()
    {
        var transport = Map(("wall.ktx2", new TransportPage(Filled(40000, 3), cut: 512)));
        var fetcher = OneTry(transport);

        var stages = await All(Stages(fetcher, "wall.ktx2"));
        Assert.True(Assert.Single(stages).IsFinal);
    }
}
