namespace Orblit.Terrain.Tests;

public class StreamerTests
{
    /// <summary>Regions 4 m across, so region x spans metres 4x to 4x + 4.</summary>
    private const int Size = 4;

    private static byte[] FileOf(RegionKey key, int size = Size, RegionKey? named = null)
    {
        var region = new TerrainRegion(named ?? key, size);
        Array.Fill(region.Heights, (float)(key.X + 1.0));
        region.Touch();
        return region.Encode();
    }

    private sealed class OfflineException : Exception;

    private sealed class Source : IRegionSource
    {
        // When set, a read waits for Release.
        private readonly bool _hold;
        private readonly OrderedDictionary<RegionKey, TaskCompletionSource<byte[]>> _held = [];

        public Source(IEnumerable<RegionKey> keys, bool hold = false)
        {
            Files = keys.ToDictionary(key => key, key => FileOf(key));
            _hold = hold;
        }

        public Dictionary<RegionKey, byte[]> Files { get; }

        public List<RegionKey> Asked { get; } = [];

        public HashSet<RegionKey> Offline { get; } = [];

        public List<RegionKey> Waiting => [.. _held.Keys];

        public IEnumerable<RegionKey> Keys => Files.Keys;

        public Task<byte[]> ReadAsync(RegionKey key)
        {
            Asked.Add(key);
            if (Offline.Contains(key))
            {
                return Task.FromException<byte[]>(new OfflineException());
            }

            if (!_hold)
            {
                return Task.FromResult(Files[key]);
            }

            return (_held[key] = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        }

        public void Release(RegionKey key)
        {
            var held = _held[key];
            _held.Remove(key);
            held.SetResult(Files[key]);
        }
    }

    /// <summary>
    /// A synchronization context that moves only when the test says so: everything the streamer has been told
    /// to continue with runs in <see cref="Settle"/>, on the test's own thread, in the order it was queued.
    /// </summary>
    private sealed class Pump : SynchronizationContext, IDisposable
    {
        private readonly Queue<(SendOrPostCallback Run, object? State)> _queue = [];
        private readonly SynchronizationContext? _before = Current;

        public Pump() => SetSynchronizationContext(this);

        public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue((d, state));

        public void Settle()
        {
            while (_queue.TryDequeue(out var next))
            {
                next.Run(next.State);
            }
        }

        /// <summary>Settles, then checks that every run has finished.</summary>
        public void Finish(params Task[] runs)
        {
            Settle();
            Assert.All(runs, run => Assert.True(run.IsCompletedSuccessfully, $"a run ended as {run.Status}"));
        }

        public void Dispose() => SetSynchronizationContext(_before);
    }

    private static IEnumerable<RegionKey> Line(int count) =>
        Enumerable.Range(0, count).Select(x => new RegionKey(x, 0));

    private static Terrain NewTerrain() => new(regionSize: Size);

    private static void AssertColumns(Terrain terrain, params long[] expected) =>
        Assert.Equal(expected.Order(), terrain.Regions.Select(region => region.Key.X).Order());

    // following a focus

    [Fact]
    public void LoadsWhatIsInRangeNearestFirstAndNothingElse()
    {
        using var pump = new Pump();
        var terrain = NewTerrain();
        var source = new Source(Line(10));
        var streamer = new TerrainStreamer(terrain: terrain, source: source, loadRadius: 5, concurrency: 1);

        pump.Finish(streamer.FollowAsync(2, 2));

        AssertColumns(terrain, 0, 1);
        Assert.Equal([new RegionKey(0, 0), new RegionKey(1, 0)], source.Asked);
        Assert.True(streamer.Resident.SetEquals([new RegionKey(0, 0), new RegionKey(1, 0)]));
    }

    [Fact]
    public void PutsInTheRegionsTheFilesDescribe()
    {
        using var pump = new Pump();
        var terrain = NewTerrain();
        var streamer = new TerrainStreamer(terrain: terrain, source: new Source(Line(3)), loadRadius: 5);

        pump.Finish(streamer.FollowAsync(2, 2));

        Assert.Equal(2, terrain.RegionAt(new RegionKey(1, 0))!.HeightAt(0, 0));
        Assert.Equal(2.0, terrain.HeightAt(5, 1));
    }

    [Fact]
    public void HasAtMostConcurrencyReadsOutAtOnce()
    {
        using var pump = new Pump();
        var terrain = NewTerrain();
        var source = new Source(Line(10), hold: true);
        var streamer = new TerrainStreamer(terrain: terrain, source: source, loadRadius: 17, concurrency: 2);

        var done = streamer.FollowAsync(2, 2);
        pump.Settle();
        Assert.Equal(2, source.Waiting.Count);
        Assert.Equal(2, streamer.Loading);

        source.Release(new RegionKey(0, 0));
        pump.Settle();
        Assert.Equal(2, source.Waiting.Count);
        Assert.Equal(3, source.Asked.Count);

        while (source.Waiting.Count > 0)
        {
            source.Release(source.Waiting[0]);
            pump.Settle();
        }

        pump.Finish(done);
        AssertColumns(terrain, 0, 1, 2, 3, 4);
    }

    [Fact]
    public void KeepsARegionBetweenTheLoadAndKeepRadii()
    {
        using var pump = new Pump();
        var terrain = NewTerrain();
        var streamer = new TerrainStreamer(terrain: terrain, source: new Source(Line(10)), loadRadius: 5, keepRadius: 9);
        pump.Finish(streamer.FollowAsync(2, 2));
        AssertColumns(terrain, 0, 1);

        pump.Finish(streamer.FollowAsync(10, 2));

        // Region 0 is 6 m off: past the load radius, inside the keep radius.
        AssertColumns(terrain, 0, 1, 2, 3);

        pump.Finish(streamer.FollowAsync(16, 2));
        AssertColumns(terrain, 1, 2, 3, 4, 5);
    }

    [Fact]
    public void DoesNotReadARegionAgainThatItIsStillHolding()
    {
        using var pump = new Pump();
        var source = new Source(Line(10));
        var streamer = new TerrainStreamer(terrain: NewTerrain(), source: source, loadRadius: 5);
        pump.Finish(streamer.FollowAsync(2, 2));
        pump.Finish(streamer.FollowAsync(1, 2));
        pump.Finish(streamer.FollowAsync(2, 2));

        Assert.Equal(2, source.Asked.Count);
    }

    [Fact]
    public void ReadsNothingWhenEverythingInRangeIsHeld()
    {
        using var pump = new Pump();
        var source = new Source(Line(3));
        var streamer = new TerrainStreamer(terrain: NewTerrain(), source: source, loadRadius: 50);
        pump.Finish(streamer.FollowAsync(0, 0));
        source.Asked.Clear();

        pump.Finish(streamer.FollowAsync(1, 1));

        Assert.Empty(source.Asked);
    }

    // the limit

    [Fact]
    public void CountsRegionsThatWereAlreadyThere()
    {
        using var pump = new Pump();
        var terrain = NewTerrain();
        terrain.AddRegion(new RegionKey(-5, 0));
        var streamer = new TerrainStreamer(terrain: terrain, source: new Source(Line(10)), loadRadius: 50, maxResident: 3);

        pump.Finish(streamer.FollowAsync(2, 2));

        AssertColumns(terrain, -5, 0, 1);
    }

    [Fact]
    public void LetsGoOfTheFarOnesToMakeRoomForTheNearOnes()
    {
        using var pump = new Pump();
        var terrain = NewTerrain();
        var streamer = new TerrainStreamer(terrain: terrain, source: new Source(Line(10)), loadRadius: 50, maxResident: 3);
        pump.Finish(streamer.FollowAsync(2, 2));
        AssertColumns(terrain, 0, 1, 2);

        pump.Finish(streamer.FollowAsync(34, 2));

        AssertColumns(terrain, 7, 8, 9);
    }

    [Fact]
    public void IsNeverExceededWhileRegionsAreOnTheirWay()
    {
        using var pump = new Pump();
        var terrain = NewTerrain();
        var source = new Source(Line(10), hold: true);
        var streamer = new TerrainStreamer(
            terrain: terrain,
            source: source,
            loadRadius: 50,
            maxResident: 2,
            concurrency: 4);

        var done = streamer.FollowAsync(2, 2);
        pump.Settle();
        Assert.Equal(2, source.Waiting.Count);
        while (source.Waiting.Count > 0)
        {
            source.Release(source.Waiting[0]);
            pump.Settle();
            Assert.True(terrain.Regions.Count() <= 2);
        }

        pump.Finish(done);
    }

    // what is not the streamer's

    [Fact]
    public void ARegionThatWasThereFirstIsLeftAlone()
    {
        using var pump = new Pump();
        var own = new TerrainRegion(new RegionKey(0, 0), Size);
        var terrain = NewTerrain();
        terrain.PutRegion(own);
        var source = new Source(Line(3));
        var streamer = new TerrainStreamer(terrain: terrain, source: source, loadRadius: 5);

        pump.Finish(streamer.FollowAsync(2, 2));
        pump.Finish(streamer.FollowAsync(1000, 2));

        Assert.Equal([new RegionKey(1, 0)], source.Asked);
        Assert.Same(own, terrain.RegionAt(new RegionKey(0, 0)));
    }

    [Fact]
    public void ARegionThatWasEditedIsKeptWhenTheFocusLeaves()
    {
        using var pump = new Pump();
        var terrain = NewTerrain();
        var streamer = new TerrainStreamer(terrain: terrain, source: new Source(Line(10)), loadRadius: 5);
        pump.Finish(streamer.FollowAsync(2, 2));
        terrain.RegionAt(new RegionKey(0, 0))!.SetHeight(1, 1, 9);

        pump.Finish(streamer.FollowAsync(1000, 2));

        AssertColumns(terrain, 0);
        Assert.Empty(streamer.Resident);
        Assert.Equal(9, terrain.RegionAt(new RegionKey(0, 0))!.HeightAt(1, 1));
    }

    [Fact]
    public void AnEditedRegionIsNotReadOverWhenTheFocusReturns()
    {
        using var pump = new Pump();
        var terrain = NewTerrain();
        var source = new Source(Line(10));
        var streamer = new TerrainStreamer(terrain: terrain, source: source, loadRadius: 5);
        pump.Finish(streamer.FollowAsync(2, 2));
        terrain.RegionAt(new RegionKey(0, 0))!.SetHeight(1, 1, 9);
        pump.Finish(streamer.FollowAsync(1000, 2));
        source.Asked.Clear();

        pump.Finish(streamer.FollowAsync(2, 2));

        Assert.DoesNotContain(new RegionKey(0, 0), source.Asked);
        Assert.Equal(9, terrain.RegionAt(new RegionKey(0, 0))!.HeightAt(1, 1));
    }

    [Fact]
    public void ARegionTheCallerRemovesIsReadAgain()
    {
        using var pump = new Pump();
        var terrain = NewTerrain();
        var source = new Source(Line(3));
        var streamer = new TerrainStreamer(terrain: terrain, source: source, loadRadius: 5);
        pump.Finish(streamer.FollowAsync(2, 2));
        terrain.RemoveRegion(new RegionKey(0, 0));

        pump.Finish(streamer.FollowAsync(2, 2));

        Assert.NotNull(terrain.RegionAt(new RegionKey(0, 0)));
        Assert.Equal(2, source.Asked.Count(key => key.X == 0));
    }

    // a read that arrives late

    [Fact]
    public void IsDroppedWhenTheFocusHasMovedOn()
    {
        using var pump = new Pump();
        var terrain = NewTerrain();
        var source = new Source(Line(10), hold: true);
        var streamer = new TerrainStreamer(terrain: terrain, source: source, loadRadius: 5, concurrency: 1);
        var first = streamer.FollowAsync(2, 2);
        pump.Settle();
        Assert.Equal([new RegionKey(0, 0)], source.Waiting);

        var second = streamer.FollowAsync(1000, 2);
        source.Release(new RegionKey(0, 0));
        pump.Finish(first, second);

        Assert.Empty(terrain.Regions);
        Assert.Empty(streamer.Resident);
    }

    [Fact]
    public void FollowsTheFocusThatWasGivenWhileItRan()
    {
        using var pump = new Pump();
        var terrain = NewTerrain();
        var source = new Source(Line(10), hold: true);
        var streamer = new TerrainStreamer(terrain: terrain, source: source, loadRadius: 3, concurrency: 1);
        var first = streamer.FollowAsync(2, 2);
        pump.Settle();
        var second = streamer.FollowAsync(34, 2);

        while (source.Waiting.Count > 0)
        {
            source.Release(source.Waiting[0]);
            pump.Settle();
        }

        pump.Finish(first, second);

        AssertColumns(terrain, 7, 8, 9);
    }

    [Fact]
    public void IsDroppedWhenTheCallerHasPutARegionThere()
    {
        using var pump = new Pump();
        var terrain = NewTerrain();
        var source = new Source(Line(3), hold: true);
        var streamer = new TerrainStreamer(terrain: terrain, source: source, loadRadius: 5, concurrency: 1);
        var done = streamer.FollowAsync(2, 2);
        pump.Settle();
        var own = new TerrainRegion(new RegionKey(0, 0), Size);
        terrain.PutRegion(own);

        while (source.Waiting.Count > 0)
        {
            source.Release(source.Waiting[0]);
            pump.Settle();
        }

        pump.Finish(done);

        Assert.Same(own, terrain.RegionAt(new RegionKey(0, 0)));
        Assert.DoesNotContain(new RegionKey(0, 0), streamer.Resident);
    }

    // failures

    [Fact]
    public void AReadThatFailsIsOneRegionsFailure()
    {
        using var pump = new Pump();
        var terrain = NewTerrain();
        var source = new Source(Line(3));
        source.Offline.Add(new RegionKey(0, 0));
        var streamer = new TerrainStreamer(terrain: terrain, source: source, loadRadius: 50);

        pump.Finish(streamer.FollowAsync(2, 2));

        AssertColumns(terrain, 1, 2);
        Assert.Equal([new RegionKey(0, 0)], streamer.Failures.Keys);
    }

    [Fact]
    public void IsNotAskedForAgainUntilToldToRetry()
    {
        using var pump = new Pump();
        var source = new Source(Line(3));
        source.Offline.Add(new RegionKey(0, 0));
        var streamer = new TerrainStreamer(terrain: NewTerrain(), source: source, loadRadius: 50);
        pump.Finish(streamer.FollowAsync(2, 2));
        pump.Finish(streamer.FollowAsync(2, 2));
        Assert.Equal(1, source.Asked.Count(key => key.X == 0));

        source.Offline.Clear();
        streamer.RetryFailed();
        pump.Finish(streamer.FollowAsync(2, 2));

        Assert.Equal(2, source.Asked.Count(key => key.X == 0));
        Assert.Empty(streamer.Failures);
    }

    [Fact]
    public void BytesThatAreNotARegionAreAFailureThatSaysSo()
    {
        using var pump = new Pump();
        var terrain = NewTerrain();
        var source = new Source(Line(2));
        source.Files[new RegionKey(0, 0)] = [1, 2, 3];
        var streamer = new TerrainStreamer(terrain: terrain, source: source, loadRadius: 50);

        pump.Finish(streamer.FollowAsync(2, 2));

        AssertColumns(terrain, 1);
        Assert.Contains("not a region", streamer.Failures[new RegionKey(0, 0)]);
    }

    [Fact]
    public void AFileForAnotherRegionIsRefused()
    {
        using var pump = new Pump();
        var terrain = NewTerrain();
        var source = new Source(Line(2));
        source.Files[new RegionKey(0, 0)] = FileOf(new RegionKey(0, 0), named: new RegionKey(7, 7));
        var streamer = new TerrainStreamer(terrain: terrain, source: source, loadRadius: 50);

        pump.Finish(streamer.FollowAsync(2, 2));

        Assert.Null(terrain.RegionAt(new RegionKey(0, 0)));
        Assert.Contains("(7, 7)", streamer.Failures[new RegionKey(0, 0)]);
    }

    [Fact]
    public void AFileOfAnotherSizeIsRefused()
    {
        using var pump = new Pump();
        var terrain = NewTerrain();
        var source = new Source(Line(2));
        source.Files[new RegionKey(0, 0)] = FileOf(new RegionKey(0, 0), size: 8);
        var streamer = new TerrainStreamer(terrain: terrain, source: source, loadRadius: 50);

        pump.Finish(streamer.FollowAsync(2, 2));

        Assert.Null(terrain.RegionAt(new RegionKey(0, 0)));
        Assert.Contains("8 texels", streamer.Failures[new RegionKey(0, 0)]);
    }

    // asking for something it cannot do

    [Fact]
    public void RefusesAFocusThatIsNotAPoint()
    {
        var streamer = new TerrainStreamer(terrain: NewTerrain(), source: new Source(Line(1)));
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            _ = streamer.FollowAsync(double.NaN, 0);
        });
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            _ = streamer.FollowAsync(0, double.PositiveInfinity);
        });
    }

    [Fact]
    public void RefusesRadiiAndLimitsThatCannotWork()
    {
        var source = new Source(Line(1));

        TerrainStreamer Make(double radius = 10, double? keep = null, int resident = 8, int concurrency = 1) => new(
            terrain: NewTerrain(),
            source: source,
            loadRadius: radius,
            keepRadius: keep,
            maxResident: resident,
            concurrency: concurrency);

        Assert.ThrowsAny<ArgumentException>(() => Make(radius: 0));
        Assert.ThrowsAny<ArgumentException>(() => Make(radius: double.PositiveInfinity));
        Assert.ThrowsAny<ArgumentException>(() => Make(keep: 9));
        Assert.ThrowsAny<ArgumentException>(() => Make(resident: 0));
        Assert.ThrowsAny<ArgumentException>(() => Make(concurrency: 0));
    }

    [Fact]
    public void KeepsARegionOneRegionWidePastTheLoadRadiusByDefault()
    {
        var streamer = new TerrainStreamer(
            terrain: new Terrain(regionSize: 8, spacing: 2),
            source: new Source([]),
            loadRadius: 100);
        Assert.Equal(116, streamer.KeepRadius);
    }

    // the metres

    [Fact]
    public void MeasureFromTheNearestEdgeOfARegionNotItsCorner()
    {
        using var pump = new Pump();
        var terrain = new Terrain(regionSize: Size, spacing: 2);
        var source = new Source([new RegionKey(0, 0), new RegionKey(1, 0), new RegionKey(2, 0), new RegionKey(-1, 0)]);
        var streamer = new TerrainStreamer(terrain: terrain, source: source, loadRadius: 3);

        // A region is 8 m across here. The focus is 1 m inside region 0, so region -1 is 1 m off and
        // region 1 is 7 m off.
        pump.Finish(streamer.FollowAsync(1, 4));

        AssertColumns(terrain, -1, 0);
    }

    [Fact]
    public void CountAcrossAsWellAsAlong()
    {
        using var pump = new Pump();
        var terrain = NewTerrain();
        var source = new Source([new RegionKey(0, 0), new RegionKey(0, 1), new RegionKey(0, 5)]);
        var streamer = new TerrainStreamer(terrain: terrain, source: source, loadRadius: 5);

        pump.Finish(streamer.FollowAsync(2, 2));

        Assert.NotNull(terrain.RegionAt(new RegionKey(0, 1)));
        Assert.Null(terrain.RegionAt(new RegionKey(0, 5)));
    }
}
