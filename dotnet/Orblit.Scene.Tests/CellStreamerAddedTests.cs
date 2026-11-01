using Orblit.Cells;
using static Orblit.Scene.Tests.StreamWorld;

namespace Orblit.Scene.Tests;

/// <summary>What the port checks that the ported streamer tests do not.</summary>
public class CellStreamerAddedTests
{
    private static readonly Cell Origin = new(0, 0);

    private readonly StreamWorld _world = new();

    private static SceneEntity Instance(string id) => new(
        id,
        id,
        components: new OrderedDictionary<string, SceneComponent>
        {
            [SceneComponents.Prefab] = new PrefabComponent(asset: "props/lamp.oprefab"),
        });

    [Fact]
    public void ACellAtANegativeNumberKeepsItsSignInTheId()
    {
        Assert.Equal("cell_-1_-2:a", CellStreamer.IdIn(new Cell(-1, -2), "a"));
    }

    [Fact]
    public void ZeroCellsInFlightIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _world.Streamer(inFlight: 0));
    }

    [Fact]
    public async Task AFetchThatAnswersOnAnotherThreadIsPutInOnALaterStep()
    {
        var answer = _world.Hold(Origin);
        var streamer = _world.Streamer(radius: 0);
        streamer.Step(Middle, Middle);
        Assert.Equal(1, streamer.Pending);
        Assert.False(streamer.IsLoaded(Origin));

        _ = Task.Run(() => answer.SetResult(Tree("late")), TestContext.Current.CancellationToken);

        await Waits.Until(
            () =>
            {
                streamer.Step(Middle, Middle);
                return streamer.IsLoaded(Origin);
            },
            "the cell to load");
        Assert.Equal(2, _world.Scene.Length);
        Assert.Equal(0, streamer.Pending);
    }

    [Fact]
    public void TheCellUnderThePointGoesInFirstWhateverOrderTheyArrivedIn()
    {
        _world.Files[Origin] = Tree("a");
        _world.Files[new Cell(1, 0)] = Tree("b");
        var streamer = _world.Streamer(radius: 1, inFlight: 9);
        streamer.Step(Middle, Middle);

        // The point has moved into the next cell, which arrived after the first.
        streamer.Step(14, Middle);

        // A cell's first entity goes in at the head, so the cell that went in first ends up last.
        Assert.Equal(
            new[] { "cell_0_0:root", "cell_0_0:leaf", "cell_1_0:root", "cell_1_0:leaf" },
            _world.Scene.Entities.Select(static e => e.Id));
    }

    [Fact]
    public void AFetcherThatThrowsBeforeItReturnsATaskIsAFailedCell()
    {
        var loads = new List<CellLoad>();
        var streamer = new CellStreamer(
            new CellWindow(Ground, 0),
            static _ => throw new IOException("no connection"),
            static _ => { },
            loads.Add);

        streamer.Step(Middle, Middle);

        Assert.IsType<IOException>(loads.Single().Error);
        Assert.Equal(0, streamer.Pending);
        Assert.False(streamer.IsLoaded(Origin));
    }

    [Fact]
    public void BytesThatAreNotTextAreAFailedCell()
    {
        _world.Files[Origin] = new byte[] { 0xFF, 0xFE };
        var streamer = _world.Streamer(radius: 0);

        Settle(streamer);

        Assert.IsType<System.Text.DecoderFallbackException>(_world.Loads.Single().Error);
        Assert.False(streamer.IsLoaded(Origin));
    }

    [Fact]
    public void APrefabThatCannotBeReadMakesTheCellAFailedOne()
    {
        _world.Files[Origin] = File(Instance("lamp1"));
        var streamer = _world.Streamer(radius: 0, prefabs: static _ => throw new PrefabException("gone"));

        Settle(streamer);

        Assert.False(streamer.IsLoaded(Origin));
        Assert.IsType<PrefabException>(_world.Loads.Single().Error);
    }

    [Fact]
    public void APrefabFileThatCannotBeOpenedMakesTheCellAFailedOne()
    {
        _world.Files[Origin] = File(Instance("lamp1"));
        var streamer = _world.Streamer(radius: 0, prefabs: static _ => throw new IOException("disk"));

        Settle(streamer);

        Assert.False(streamer.IsLoaded(Origin));
        Assert.IsType<IOException>(_world.Loads.Single().Error);
    }

    [Fact]
    public void APrefabThatIsNotThereIsNotedWithTheLoad()
    {
        _world.Files[Origin] = File(Instance("lamp1"));
        var streamer = _world.Streamer(radius: 0, prefabs: static _ => null);

        Settle(streamer);

        Assert.True(streamer.IsLoaded(Origin));
        Assert.NotEmpty(_world.Loads.Single().Problems);
    }

    [Fact]
    public void ACellStillBeingFetchedIsNotAskedForAgain()
    {
        _world.Hold(Origin);
        var streamer = _world.Streamer(radius: 1, inFlight: 9);

        streamer.Step(Middle, Middle);
        streamer.Step(Middle, Middle);
        streamer.Step(Middle, Middle);

        Assert.Single(_world.Asked, Origin);
    }

    [Fact]
    public void ACellWithNothingInItNeverReachesTheApplyFunction()
    {
        _world.Cost = TimeSpan.FromSeconds(1);
        var streamer = _world.Streamer(radius: 0);
        Settle(streamer);
        Assert.True(streamer.IsLoaded(Origin));

        // Far enough that the empty cell is let go.
        Settle(streamer, 105);

        Assert.False(streamer.IsLoaded(Origin));
        Assert.Equal(TimeSpan.Zero, _world.Now);
    }

    [Fact]
    public void TheLoadRecordsHowLongTheFetchTook()
    {
        _world.FetchCost = TimeSpan.FromMilliseconds(7);
        _world.Files[Origin] = Tree("a");
        var streamer = _world.Streamer(radius: 0);

        Settle(streamer);

        Assert.Equal(TimeSpan.FromMilliseconds(7), _world.Loads.Single().Fetch);
    }

    [Fact]
    public void ACellKeptButNotWantedGoesInAfterTheOnesThatAre()
    {
        var next = new Cell(1, 0);
        _world.Files[Origin] = Tree("a");
        _world.Files[next] = Tree("b");
        var streamer = _world.Streamer(radius: 0);

        // The east cell arrives first, then the pinned one. The point then moves onto the pinned one,
        // so the east cell is kept but no longer wanted.
        streamer.Step(14, Middle, pinned: new HashSet<Cell> { Origin });
        streamer.Step(Middle, Middle);

        Assert.Equal([Origin, next], _world.Loads.Select(static load => load.Cell));
    }

    [Fact]
    public void AFetchThatFailsAtOnceFreesItsPlaceForTheNextCellInTheSameStep()
    {
        _world.Files[Origin] = new InvalidOperationException("the network");
        var streamer = _world.Streamer(radius: 1, inFlight: 1);

        streamer.Step(Middle, Middle);

        Assert.Equal(2, _world.Asked.Count);
        Assert.Equal(Origin, _world.Asked[0]);
        Assert.Equal(1, streamer.Pending);
    }

    [Fact]
    public void ABugInTheApplyFunctionStillReachesTheCaller()
    {
        var streamer = new CellStreamer(
            new CellWindow(Ground, 0),
            static _ => Task.FromResult<byte[]?>(Tree("a")),
            static _ => throw new ArgumentException("not a diff the scene takes"));
        streamer.Step(Middle, Middle);

        Assert.Throws<ArgumentException>(() => streamer.Step(Middle, Middle));
    }
}
