using System.Diagnostics;
using System.Text;
using static Orblit.Asset.Tests.NetWorld;

namespace Orblit.Asset.Tests;

public class NetFetcherTests
{
    // fetching

    [Fact]
    public async Task HandsOverTheBytesTheServerSent()
    {
        var transport = Map(("models/robot.glb", new TransportPage(Body("a robot"))));
        var fetcher = FetcherFor(transport);

        Assert.Equal("a robot", Text(await Read(fetcher, "models/robot.glb")));
        Assert.Equal(Url("models/robot.glb"), Assert.Single(transport.Sent).Url);
    }

    [Fact]
    public async Task AsksOnceHoweverManyCallersWantTheSameThing()
    {
        var transport = Map(("t/wall.ktx2", new TransportPage(Body("bricks"))));
        var gate = new Gated(transport);
        var fetcher = FetcherFor(gate);

        var reads = new[] { Read(fetcher, "t/wall.ktx2"), Read(fetcher, "t/wall.ktx2"), Read(fetcher, "t/wall.ktx2") };
        gate.Open();
        var together = await Task.WhenAll(reads);

        Assert.All(together, bytes => Assert.Equal("bricks", Text(bytes)));
        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task A404BecomesAFailureThatNamesTheStatus()
    {
        var fetcher = FetcherFor(Map());

        var failure = await Assert.ThrowsAsync<FetchFailedException>(() => Read(fetcher, "models/missing.glb"));
        Assert.Equal<object>(404, failure.Cause);
        Assert.Equal(1, failure.Attempts);
    }

    [Fact]
    public async Task ReportsProgressEndingAtTheWhole()
    {
        var transport = Map(("big.bin", new TransportPage(Filled(400, 7), cut: 100)));
        var fetcher = FetcherFor(transport);

        var seen = new List<FetchProgress>();
        await fetcher.Fetch(Id("big.bin"), onProgress: seen.Add).Bytes;

        Assert.Equal([100L, 200, 300, 400], seen.Select(p => p.Received));
        Assert.Equal(400L, seen[^1].Total);
        Assert.Equal(1.0, seen[^1].Fraction);
    }

    // the queue

    [Fact]
    public async Task TakesWhatIsOnScreenBeforeWhatIsNot()
    {
        var transport = Map(("a.bin", new TransportPage(Body("a"))), ("b.bin", new TransportPage(Body("b"))), ("c.bin", new TransportPage(Body("c"))));
        var gate = new Gated(transport);
        var fetcher = FetcherFor(gate, concurrent: 1);

        // The first one starts at once and holds the only slot. The other two queue behind it, and the urgent
        // one goes first whatever the order they were asked for in.
        var jobs = new[]
        {
            fetcher.Fetch(Id("a.bin"), FetchUrgency.Eventually),
            fetcher.Fetch(Id("b.bin"), FetchUrgency.Eventually),
            fetcher.Fetch(Id("c.bin"), FetchUrgency.OnScreen),
        };
        gate.Open();
        await Task.WhenAll(jobs.Select(j => j.Bytes));

        Assert.Equal(["a.bin", "c.bin", "b.bin"], Names(transport));
    }

    [Fact]
    public async Task SomethingAlreadyQueuedCanJumpTheQueue()
    {
        var transport = Map(("a.bin", new TransportPage(Body("a"))), ("b.bin", new TransportPage(Body("b"))), ("c.bin", new TransportPage(Body("c"))));
        var gate = new Gated(transport);
        var fetcher = FetcherFor(gate, concurrent: 1);

        var held = fetcher.Fetch(Id("a.bin"), FetchUrgency.OnScreen);
        var later = fetcher.Fetch(Id("b.bin"), FetchUrgency.Eventually);
        var next = fetcher.Fetch(Id("c.bin"), FetchUrgency.Eventually);
        // It came into view while it was waiting.
        var again = fetcher.Fetch(Id("c.bin"), FetchUrgency.OnScreen);

        gate.Open();
        await Task.WhenAll(held.Bytes, later.Bytes, next.Bytes, again.Bytes);

        Assert.Equal(["a.bin", "c.bin", "b.bin"], Names(transport));
    }

    [Fact]
    public async Task CountsWhatIsWaitingAndWhatIsRunning()
    {
        var transport = Map(("a.bin", new TransportPage(Body("a"))), ("b.bin", new TransportPage(Body("b"))), ("c.bin", new TransportPage(Body("c"))));
        var gate = new Gated(transport);
        var fetcher = FetcherFor(gate, concurrent: 1);

        var jobs = new[] { "a", "b", "c" }.Select(name => fetcher.Fetch(Id($"{name}.bin"))).ToArray();
        Assert.Equal(1, fetcher.Running);
        Assert.Equal(2, fetcher.Waiting);

        gate.Open();
        await Task.WhenAll(jobs.Select(j => j.Bytes));
        await Waits.Until(() => fetcher.Running == 0, "the last slot to be given back");
        Assert.Equal(0, fetcher.Waiting);
    }

    // giving up on one

    [Fact]
    public async Task OneCallerLeavingDoesNotStopTheOthers()
    {
        var transport = Map(("shared.bin", new TransportPage(Body("kept"))));
        var gate = new Gated(transport);
        var fetcher = FetcherFor(gate);

        var leaving = fetcher.Fetch(Id("shared.bin"));
        var staying = fetcher.Fetch(Id("shared.bin"));
        leaving.Cancel();
        gate.Open();

        Assert.Equal("kept", Text(await staying.Bytes));
        await Assert.ThrowsAsync<FetchCancelledException>(() => leaving.Bytes);
        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task ADownloadNobodyWaitsForAnyMoreIsNeverSent()
    {
        var transport = Map(("a.bin", new TransportPage(Body("a"))), ("b.bin", new TransportPage(Body("b"))));
        var gate = new Gated(transport);
        var fetcher = FetcherFor(gate, concurrent: 1);

        var first = fetcher.Fetch(Id("a.bin"));
        var dropped = fetcher.Fetch(Id("b.bin"));
        dropped.Cancel();
        gate.Open();

        await first.Bytes;
        await Waits.Until(() => fetcher.Running == 0 && fetcher.Waiting == 0, "the queue to empty");

        Assert.Equal(["a.bin"], Names(transport));
    }

    // The bug this is here for: cancelling mid-body left the read waiting on a future nobody would
    // complete, so the slot it held was never returned and everything queued behind it stopped.
    [Fact]
    public async Task AnAbandonedDownloadGivesItsSlotBack()
    {
        var held = new HeldTransport(Body("slow"));
        var fetcher = new AssetFetcher(Origin, held, concurrent: 1, backoff: TimeSpan.Zero, jitter: NoNoise);

        var slow = fetcher.FetchUrl(Url("slow.bin"));
        await Waits.Until(() => held.Asked.Length == 1, "the first request");

        var behind = fetcher.FetchUrl(Url("quick.bin"));
        Assert.Equal(1, fetcher.Waiting);

        slow.Cancel();
        await Assert.ThrowsAsync<FetchCancelledException>(() => slow.Bytes);
        await Waits.Until(() => held.Asked.Length == 2, "the slot to be given back");

        held.Finish("quick.bin", Body("quick"));
        Assert.Equal("quick", Text(await behind.Bytes));
    }

    // Guards the other half of the same bug: a download left in the live map after it was abandoned would be
    // joined by the next caller, who would then wait for bytes nobody was fetching.
    [Fact]
    public async Task AskingAgainAfterEveryoneGaveUpStartsANewDownload()
    {
        var transport = Map(("again.bin", new TransportPage(Body("second time"))));
        var fetcher = FetcherFor(transport);

        fetcher.Fetch(Id("again.bin")).Cancel();
        await Waits.Until(() => fetcher.Running == 0, "the abandoned download to end");

        Assert.Equal("second time", Text(await Read(fetcher, "again.bin")));
    }

    [Fact]
    public async Task ClosingFailsEverythingInFlight()
    {
        var held = new HeldTransport(Body("never"));
        var fetcher = new AssetFetcher(Origin, held, backoff: TimeSpan.Zero, jitter: NoNoise);

        var job = fetcher.FetchUrl(Url("gone.bin"));
        await Waits.Until(() => held.Asked.Length == 1, "the request");
        fetcher.Dispose();

        await Assert.ThrowsAsync<FetchCancelledException>(() => job.Bytes);
        Assert.Throws<ObjectDisposedException>(() => fetcher.FetchUrl(Url("gone.bin")));
    }

    // trying again

    [Fact]
    public async Task ALostConnectionIsTriedAgain()
    {
        var transport = Map(("flaky.bin", new TransportPage(Body("got there"), faults: [Fault.Thrown(), Fault.Thrown()])));
        var fetcher = FetcherFor(transport, attempts: 3);

        Assert.Equal("got there", Text(await Read(fetcher, "flaky.bin")));
        Assert.Equal(3, transport.Sent.Count);
    }

    [Fact]
    public async Task A503IsTriedAgainA403IsNot()
    {
        var transport = Map(
            ("busy.bin", new TransportPage(Body("eventually"), faults: [Fault.OfStatus(503)])),
            ("shut.bin", new TransportPage(Body("never seen"), faults: [Fault.OfStatus(403)])));
        var fetcher = FetcherFor(transport, attempts: 3);

        Assert.Equal("eventually", Text(await Read(fetcher, "busy.bin")));
        var failure = await Assert.ThrowsAsync<FetchFailedException>(() => Read(fetcher, "shut.bin"));
        Assert.Equal<object>(403, failure.Cause);

        // A 403 is an answer, not a bad moment.
        Assert.Single(transport.Sent, r => r.Url.Equals(Url("shut.bin")));
    }

    [Fact]
    public async Task RunningOutOfAttemptsSaysHowManyThereWere()
    {
        var transport = Map(("down.bin", new TransportPage(Body("unreachable"), faults: [Fault.OfStatus(500), Fault.OfStatus(500)])));
        var fetcher = FetcherFor(transport, attempts: 2);

        var failure = await Assert.ThrowsAsync<FetchFailedException>(() => Read(fetcher, "down.bin"));
        Assert.Equal<object>(500, failure.Cause);
        Assert.Equal(2, failure.Attempts);
    }

    [Fact]
    public async Task WaitsLongerBeforeEachAttempt()
    {
        var transport = Map(("slowly.bin", new TransportPage(Body("at last"), faults: [Fault.Thrown(), Fault.Thrown()])));
        var fetcher = new AssetFetcher(Origin, transport, attempts: 3, backoff: TimeSpan.FromMilliseconds(20), jitter: NoNoise);

        var started = Stopwatch.StartNew();
        await Read(fetcher, "slowly.bin");

        // 20ms then 40ms, so at least 60ms whatever else the machine is doing.
        Assert.True(started.Elapsed >= TimeSpan.FromMilliseconds(55), $"took {started.Elapsed}");
    }

    // resuming

    [Fact]
    public async Task PicksUpWhereTheConnectionDropped()
    {
        var whole = Counting(500);
        var transport = Map(("capture.spz", new TransportPage(whole, cut: 100, stopAfter: 300)));
        var fetcher = FetcherFor(transport, attempts: 3);

        Assert.Equal(whole, await Read(fetcher, "capture.spz"));
        Assert.Equal(2, transport.Sent.Count);

        // The second request asks for the rest, not the whole thing.
        Assert.Equal(300L, transport.Sent[1].From);
    }

    // A 200 to a resumed request means the bytes already held are the start of something else. Keeping them
    // would splice two files together.
    [Fact]
    public async Task StartsOverWhenTheServerIgnoresTheRange()
    {
        var whole = Counting(300);
        var fetcher = new AssetFetcher(Origin, new IgnoresRanges(whole, 120), attempts: 3, backoff: TimeSpan.Zero, jitter: NoNoise);

        Assert.Equal(whole, await fetcher.FetchUrl(Url("whole.bin")).Bytes);
    }

    // asking whether it changed

    [Fact]
    public async Task SendsTheTagItHoldsAndTakesA304FromTheCache()
    {
        var store = new MemoryContentStore();
        var records = new MemoryFetchRecords();
        var page = new TransportPage(Body("version one"), etag: "\"v1\"");

        var first = FetcherFor(Map(("tagged.bin", page)), store, records);
        Assert.Equal("version one", Text(await Read(first, "tagged.bin")));

        var again = Map(("tagged.bin", page));
        var second = FetcherFor(again, store, records);

        Assert.Equal("version one", Text(await Read(second, "tagged.bin")));
        Assert.Equal("\"v1\"", Assert.Single(again.Sent).IfNoneMatch);
    }

    [Fact]
    public async Task A304WithTheBytesGoneFetchesThemAgain()
    {
        var records = new MemoryFetchRecords();
        var page = new TransportPage(Body("still here"), etag: "\"v1\"");

        var first = FetcherFor(Map(("tagged.bin", page)), new MemoryContentStore(), records);
        await Read(first, "tagged.bin");

        // A new store: the system emptied the cache but the note survived.
        var again = Map(("tagged.bin", page));
        var second = FetcherFor(again, new MemoryContentStore(), records);

        Assert.Equal("still here", Text(await Read(second, "tagged.bin")));
        Assert.Equal(2, again.Sent.Count);
        Assert.Null(again.Sent[1].IfNoneMatch);
    }

    // checking against the manifest

    private static AssetManifest ManifestOf(string path, byte[] bytes) =>
        new(new Dictionary<AssetId, AssetEntry> { [Id(path)] = new AssetEntry(ContentHash.Of(bytes), bytes.Length) });

    [Fact]
    public async Task ACopyTheManifestVouchesForNeedsNoRequest()
    {
        var bytes = Body("already here");
        var store = new MemoryContentStore();
        await store.PutAsync(bytes);

        var transport = Map(("known.bin", new TransportPage(Body("would be fetched"))));
        var fetcher = FetcherFor(transport, store, manifest: ManifestOf("known.bin", bytes));

        Assert.Equal("already here", Text(await Read(fetcher, "known.bin")));

        // The hash is a promise about the bytes, not about the URL.
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task BytesThatDoNotMatchAreRefusedAndNotTriedAgain()
    {
        var transport = Map(("swapped.bin", new TransportPage(Body("something else"))));
        var fetcher = FetcherFor(transport, manifest: ManifestOf("swapped.bin", Body("what was promised")), attempts: 3);

        var failure = await Assert.ThrowsAsync<FetchCorruptException>(() => Read(fetcher, "swapped.bin"));
        Assert.Equal(ContentHash.Of(Body("what was promised")), failure.Wanted);
        Assert.Equal(ContentHash.Of(Body("something else")), failure.Got);

        // Asking the same server again gets the same wrong bytes.
        Assert.Single(transport.Sent);
    }

    // being offline

    [Fact]
    public async Task FallsBackToTheCopyAlreadyHeld()
    {
        var store = new MemoryContentStore();
        var records = new MemoryFetchRecords();
        var warm = FetcherFor(Map(("cached.bin", new TransportPage(Body("from before")))), store, records);
        await Read(warm, "cached.bin");

        var gone = Map(("cached.bin", new TransportPage(Body("unreachable"), faults: [Fault.Thrown(), Fault.Thrown(), Fault.Thrown()])));
        var offline = FetcherFor(gone, store, records, attempts: 3);

        Assert.Equal("from before", Text(await Read(offline, "cached.bin")));
        Assert.Equal(3, gone.Sent.Count);
    }

    [Fact]
    public async Task WithNothingHeldItFailsRatherThanHanging()
    {
        var transport = Map(("cold.bin", new TransportPage(Body("unreachable"), faults: [Fault.Thrown(), Fault.Thrown()])));
        var fetcher = FetcherFor(transport, attempts: 2);

        var failure = await Assert.ThrowsAsync<FetchFailedException>(() => Read(fetcher, "cold.bin"));
        Assert.IsType<SocketFailureException>(failure.Cause);
    }

    // the limits

    [Fact]
    public async Task ADeclaredLengthOverTheLimitIsRefusedBeforeReading()
    {
        var small = AssetOrigin.Parse("https://cdn.example.com/game/", new FetchPolicy(maxBytes: 16));
        var transport = new MapTransport(new Dictionary<WebUrl, TransportPage> { [small.UrlOf(Id("huge.bin"))] = new(Filled(64, 1)) });
        var fetcher = FetcherFor(transport, from: small);

        var refused = await Assert.ThrowsAsync<FetchRefusedException>(() => Read(fetcher, "huge.bin"));
        Assert.Contains("64 bytes and the policy allows 16", refused.Reason, StringComparison.Ordinal);
        Assert.Single(transport.Sent);
    }

    [Fact]
    public async Task ABodyThatOutgrowsTheLimitIsStoppedPartway()
    {
        var small = AssetOrigin.Parse("https://cdn.example.com/game/", new FetchPolicy(maxBytes: 100));

        // No declared length, so the only place to catch it is while it arrives.
        var transport = new Undeclared(Filled(1000, 1), 50);
        var fetcher = new AssetFetcher(small, transport, attempts: 1, backoff: TimeSpan.Zero, jitter: NoNoise);

        await Assert.ThrowsAsync<FetchRefusedException>(() => fetcher.FetchUrl(small.UrlOf(Id("stream.bin"))).Bytes);

        // It stopped at the byte that passed the limit.
        Assert.True(transport.Handed < 1000, $"handed {transport.Handed}");
    }

    [Fact]
    public async Task APictureTooBigToDecodeIsRefusedFromItsHeader()
    {
        var small = AssetOrigin.Parse("https://cdn.example.com/game/", new FetchPolicy(maxPixels: 1000));
        var transport = new Undeclared(PngHeader(40000, 40000, 4000), 64);
        var fetcher = new AssetFetcher(small, transport, attempts: 1, backoff: TimeSpan.Zero, jitter: NoNoise);

        var refused = await Assert.ThrowsAsync<FetchRefusedException>(() => fetcher.FetchUrl(small.UrlOf(Id("vast.png"))).Bytes);
        Assert.Contains("40000×40000", refused.Reason, StringComparison.Ordinal);

        // The header is enough; the rest is never pulled.
        Assert.True(transport.Handed <= 128, $"handed {transport.Handed}");
    }

    // A PNG that says it is width by height, padded out to length bytes.
    private static byte[] PngHeader(uint width, uint height, int length)
    {
        var bytes = new byte[length];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), 13);
        Encoding.UTF8.GetBytes("IHDR").CopyTo(bytes, 12);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), width);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), height);
        return bytes;
    }

    // the coarse levels first

    private static byte[] Mipped() => Ktx2Files.Mipped(width: 512, height: 512, levels: 10);

    [Fact]
    public async Task HandsThemOverBeforeTheRestOfTheFileHasArrived()
    {
        var whole = Mipped();
        var chain = Ktx2Chain.Read(whole)!;
        var transport = Map(("t/wall.ktx2", new TransportPage(whole, cut: 1024)));
        var fetcher = FetcherFor(transport);

        byte[]? rough = null;
        var roughAt = -1L;
        var received = 0L;
        var job = fetcher.Fetch(
            Id("t/wall.ktx2"),
            roughSize: 64,
            onProgress: progress => received = progress.Received,
            onRough: bytes =>
            {
                rough = bytes;
                roughAt = received;
            });
        var all = await job.Bytes;

        // A mipped texture has coarse levels, handed over part way and not at the end: the point of the exercise.
        Assert.NotNull(rough);
        Assert.True(roughAt < all.Length);
        Assert.True(rough.Length < whole.Length);

        var smaller = Ktx2Chain.Read(rough)!;
        Assert.Equal(64L, smaller.Width);
        Assert.Equal(10 - chain.LevelAtLeast(64)!.Value, smaller.Levels.Count);
        Assert.Equal(whole, all);
    }

    [Fact]
    public async Task CostsNoExtraRequest()
    {
        var transport = Map(("t/wall.ktx2", new TransportPage(Mipped(), cut: 1024)));
        var fetcher = FetcherFor(transport);

        await fetcher.Fetch(Id("t/wall.ktx2"), onRough: _ => { }).Bytes;
        Assert.Single(transport.Sent);
    }

    // A stand-in for something already here is a wasted upload and a visible flash.
    [Fact]
    public async Task NothingForATextureThatArrivedInOnePiece()
    {
        var transport = Map(("t/wall.ktx2", new TransportPage(Mipped())));
        var fetcher = FetcherFor(transport);

        var offered = false;
        await fetcher.Fetch(Id("t/wall.ktx2"), onRough: _ => offered = true).Bytes;
        Assert.False(offered);
    }

    [Fact]
    public async Task NothingForSomethingThatIsNotAMippedTexture()
    {
        var transport = Map(("models/robot.glb", new TransportPage(Filled(40000, 7), cut: 1024)));
        var fetcher = FetcherFor(transport);

        var offered = false;
        await fetcher.Fetch(Id("models/robot.glb"), onRough: _ => offered = true).Bytes;
        Assert.False(offered);
    }

    [Fact]
    public async Task OnceHoweverManyChunksFollow()
    {
        var transport = Map(("t/wall.ktx2", new TransportPage(Mipped(), cut: 256)));
        var fetcher = FetcherFor(transport);

        var offers = 0;
        await fetcher.Fetch(Id("t/wall.ktx2"), roughSize: 32, onRough: _ => offers++).Bytes;
        Assert.Equal(1, offers);
    }

    [Fact]
    public async Task EveryCallerSharingTheDownloadGetsThem()
    {
        var transport = Map(("t/wall.ktx2", new TransportPage(Mipped(), cut: 512)));
        var gate = new Gated(transport);
        var fetcher = FetcherFor(gate);

        var offered = new List<int>();
        var first = fetcher.Fetch(Id("t/wall.ktx2"), roughSize: 64, onRough: bytes => offered.Add(bytes.Length));
        var second = fetcher.Fetch(Id("t/wall.ktx2"), roughSize: 64, onRough: bytes => offered.Add(bytes.Length));
        gate.Open();
        await Task.WhenAll(first.Bytes, second.Bytes);

        Assert.Equal(2, offered.Count);
        Assert.Equal(offered[0], offered[^1]);
        Assert.Single(transport.Sent);
    }

    // It arrives first, and it is still better than nothing for whoever wanted a sharper one.
    [Fact]
    public async Task TheSmallestAnybodyAskedForIsTheOneThatIsBuilt()
    {
        var transport = Map(("t/wall.ktx2", new TransportPage(Mipped(), cut: 512)));
        var gate = new Gated(transport);
        var fetcher = FetcherFor(gate);

        byte[]? built = null;
        var sharp = fetcher.Fetch(Id("t/wall.ktx2"), roughSize: 256, onRough: bytes => built = bytes);
        var blurry = fetcher.Fetch(Id("t/wall.ktx2"), roughSize: 32, onRough: _ => { });
        gate.Open();
        await Task.WhenAll(sharp.Bytes, blurry.Bytes);

        Assert.Equal(32L, Ktx2Chain.Read(built!)!.Width);
    }

    // as an asset source

    [Fact]
    public async Task AMissingAssetIsAnAssetNotFoundSoLayersFallThrough()
    {
        var transport = Map(("there.bin", new TransportPage(Body("found"))));
        var network = new NetworkAssetSource(FetcherFor(transport));

        Assert.Equal("found", Text(await network.ReadAsync(Id("there.bin"))));
        await Assert.ThrowsAsync<AssetNotFoundException>(() => network.ReadAsync(Id("nowhere.bin")));
    }

    // The distinction that matters to a layered source: falling through on a 500 would quietly serve a stale
    // layer whenever a CDN hiccupped.
    [Fact]
    public async Task AServerHavingAMomentIsNotAMissingAsset()
    {
        var transport = Map(("wobbly.bin", new TransportPage(Body("never seen"), faults: [Fault.OfStatus(500), Fault.OfStatus(500)])));
        var network = new NetworkAssetSource(FetcherFor(transport, attempts: 2));

        await Assert.ThrowsAsync<FetchFailedException>(() => network.ReadAsync(Id("wobbly.bin")));
    }
}
