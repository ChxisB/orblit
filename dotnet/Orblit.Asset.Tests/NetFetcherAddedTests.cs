using static Orblit.Asset.Tests.NetWorld;

namespace Orblit.Asset.Tests;

// What no fixture covers: its clock, its backoff arithmetic and the places a thread pool can fail.
public class NetFetcherAddedTests
{
    private sealed class Pinned(double value) : Random
    {
        public override double NextDouble() => value;
    }

    private sealed class SetClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class BrokenRecords : IFetchRecords
    {
        public Task<FetchRecord?> GetAsync(WebUrl url) => throw new InvalidOperationException("the records are gone");

        public Task PutAsync(WebUrl url, FetchRecord record) => Task.CompletedTask;

        public Task RemoveAsync(WebUrl url) => Task.CompletedTask;

        public Task ClearAsync() => Task.CompletedTask;
    }

    [Fact]
    public void AFetcherWithNoSlotsOrNoAttemptsIsRefused()
    {
        using var transport = Map();

        Assert.Throws<ArgumentOutOfRangeException>(() => new AssetFetcher(Origin, transport, concurrent: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AssetFetcher(Origin, transport, attempts: 0));
    }

    [Fact]
    public void TheWaitDoublesWithEachAttemptAndMovesWithTheNoise()
    {
        using var transport = Map();
        var backoff = TimeSpan.FromMilliseconds(100);

        var plain = new AssetFetcher(Origin, transport, backoff: backoff, jitter: new Pinned(0.5));
        Assert.Equal([100.0, 200.0, 400.0], new[] { 1, 2, 3 }.Select(n => plain.WaitBefore(n).TotalMilliseconds));

        // The noise spreads a wait by a fifth either way, so clients do not all come back together.
        var early = new AssetFetcher(Origin, transport, backoff: backoff, jitter: new Pinned(0.0));
        var late = new AssetFetcher(Origin, transport, backoff: backoff, jitter: new Pinned(1.0));
        Assert.Equal(80.0, early.WaitBefore(1).TotalMilliseconds);
        Assert.Equal(120.0, late.WaitBefore(1).TotalMilliseconds);
    }

    [Fact]
    public void TheWaitStopsGrowingAtADayWhateverTheAttempt()
    {
        using var transport = Map();
        var fetcher = new AssetFetcher(Origin, transport, backoff: TimeSpan.FromSeconds(1), jitter: new Pinned(0.5));

        Assert.Equal(TimeSpan.FromDays(1), fetcher.WaitBefore(100));
        Assert.Equal(TimeSpan.FromDays(1), fetcher.WaitBefore(int.MaxValue));
    }

    // This is a failed attempt, tried again like any other, and the caller hears about it.
    [Fact]
    public async Task ACallbackThatThrowsFailsTheAttemptAndNotTheProcess()
    {
        var transport = Map(("calls.bin", new TransportPage(Body("data"))));
        var fetcher = FetcherFor(transport, attempts: 2);

        var failure = await Assert.ThrowsAsync<FetchFailedException>(
            () => fetcher.Fetch(Id("calls.bin"), onProgress: _ => throw new InvalidOperationException("boom")).Bytes);

        Assert.IsType<InvalidOperationException>(failure.Cause);
        Assert.Equal(2, failure.Attempts);
        Assert.Equal(2, transport.Sent.Count);
    }

    // A pool thread that dies quietly would leave everyone waiting.
    [Fact]
    public async Task RecordsThatThrowFailTheDownloadInsteadOfHangingIt()
    {
        var fetcher = FetcherFor(Map(("any.bin", new TransportPage(Body("data")))), records: new BrokenRecords());

        await Assert.ThrowsAsync<InvalidOperationException>(() => Read(fetcher, "any.bin"));
        await Waits.Until(() => fetcher.Running == 0, "the slot to be given back");
    }

    [Fact]
    public async Task ReadingStopsWhenItsTokenIsCancelled()
    {
        var held = new HeldTransport(Body("never"));
        var fetcher = new AssetFetcher(Origin, held, backoff: TimeSpan.Zero, jitter: NoNoise);

        using var cancel = new CancellationTokenSource();
        var reading = fetcher.ReadAsync(Id("slow.bin"), cancellation: cancel.Token);
        await Waits.Until(() => held.Asked.Length == 1, "the request");
        await cancel.CancelAsync();

        await Assert.ThrowsAsync<FetchCancelledException>(() => reading);
        await Waits.Until(() => fetcher.Running == 0, "the slot to be given back");
    }

    [Fact]
    public async Task StampsTheRecordWithTheTimeItWasFetchedAndRestampsItWhenTheServerConfirms()
    {
        var records = new MemoryFetchRecords();
        var store = new MemoryContentStore();
        var page = new TransportPage(Body("dated"), etag: "\"v1\"");
        var first = new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
        var clock = new SetClock(first);

        var fetcher = new AssetFetcher(
            Origin, Map(("dated.bin", page)), store, records, backoff: TimeSpan.Zero, jitter: NoNoise, clock: clock);
        await Read(fetcher, "dated.bin");
        Assert.Equal(first, (await records.GetAsync(Url("dated.bin")))!.Fetched);

        // Asking again with the tag gets a 304, which says the copy is still good as of now.
        clock.Now = first.AddDays(3);
        var again = new AssetFetcher(
            Origin, Map(("dated.bin", page)), store, records, backoff: TimeSpan.Zero, jitter: NoNoise, clock: clock);
        await Read(again, "dated.bin");
        Assert.Equal(first.AddDays(3), (await records.GetAsync(Url("dated.bin")))!.Fetched);
    }

    [Fact]
    public async Task AFileOfExactlyTheLimitIsKeptAndOneByteMoreIsNot()
    {
        var limit = AssetOrigin.Parse("https://cdn.example.com/game/", new FetchPolicy(maxBytes: 16));
        using var transport = Map(("a.bin", new TransportPage(Filled(16, 1))), ("b.bin", new TransportPage(Filled(17, 1))));
        using var fetcher = FetcherFor(transport, from: limit, attempts: 1);

        Assert.Equal(16, (await Read(fetcher, "a.bin")).Length);
        await Assert.ThrowsAsync<FetchRefusedException>(() => Read(fetcher, "b.bin"));
    }
}
