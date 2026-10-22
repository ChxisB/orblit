using System.Text;
using Orblit.Cells;
using Orblit.Json;
using static Orblit.Scene.Tests.StreamWorld;
using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class CellStreamerTests
{
    private const string LampAsset = "props/lamp.oprefab";

    private static readonly Cell Origin = new(0, 0);

    private readonly StreamWorld _world = new();

    [Fact]
    public void PutsACellInWithItsCellOnEveryIdAndKeepsTheTree()
    {
        _world.Files[Origin] = Tree("here");
        var streamer = _world.Streamer(radius: 0);

        Settle(streamer);

        Assert.Equal(new[] { "cell_0_0:root", "cell_0_0:leaf" }, _world.Scene.Entities.Select(static e => e.Id));
        Assert.Equal("cell_0_0:root", _world.Scene["cell_0_0:leaf"]!.Parent);
        Assert.Null(_world.Scene["cell_0_0:root"]!.Parent);
    }

    [Fact]
    public void TwoCellsWithTheSameIdsDoNotClash()
    {
        _world.Files[Origin] = Tree("a");
        _world.Files[new Cell(1, 0)] = Tree("b");
        var streamer = _world.Streamer(radius: 1);

        Settle(streamer);

        Assert.Equal(4, _world.Scene.Length);
        Assert.Equal("b", _world.Scene["cell_1_0:root"]!.Name);
    }

    [Fact]
    public void AsksForTheCellUnderThePointFirst()
    {
        var streamer = _world.Streamer(radius: 2, inFlight: 1);

        streamer.Step(Middle, Middle);

        Assert.Equal([Origin], _world.Asked);
    }

    [Fact]
    public void NeverHasMoreThanInFlightCellsAskedForOrWaiting()
    {
        var streamer = _world.Streamer(radius: 2, inFlight: 2);

        streamer.Step(Middle, Middle);
        Assert.Equal(2, streamer.Pending);
        streamer.Step(Middle, Middle);

        Assert.Equal(_world.Asked.Distinct().Count(), _world.Asked.Count);
    }

    [Fact]
    public void TheBudgetStopsAfterTheCellThatUsedItUp()
    {
        foreach (var cell in Ground.Around(Origin, 1))
        {
            _world.Files[cell] = Tree("c");
        }

        _world.Cost = TimeSpan.FromMilliseconds(3);
        var streamer = _world.Streamer(radius: 1, inFlight: 9);
        streamer.Step(Middle, Middle);

        streamer.Step(Middle, Middle);

        // 3 ms is under the 4 ms budget, so a second goes; 6 ms is not.
        Assert.Equal(2, streamer.LastStep.Loaded);
        Assert.Equal(TimeSpan.FromMilliseconds(6), streamer.LastStep.Spent);
    }

    [Fact]
    public void TheBudgetDoesOneCellInAFrameEvenWhenOneCostsMoreThanAll()
    {
        _world.Files[Origin] = Tree("big");
        _world.Cost = TimeSpan.FromMilliseconds(50);
        var streamer = _world.Streamer(radius: 0);
        streamer.Step(Middle, Middle);

        streamer.Step(Middle, Middle);

        Assert.Equal(1, streamer.LastStep.Loaded);
    }

    [Fact]
    public void ABudgetOfZeroIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _world.Streamer(budget: TimeSpan.Zero));
    }

    [Fact]
    public void LeavingDropsTheCellsPastTheMarginAndTakesTheirEntitiesOut()
    {
        _world.Files[Origin] = Tree("start");
        var streamer = _world.Streamer(radius: 0);
        Settle(streamer);
        Assert.Equal(2, _world.Scene.Length);

        // Cell 2 is past the margin of one cell.
        Settle(streamer, x: 25);

        Assert.False(streamer.IsLoaded(Origin));
        Assert.DoesNotContain(_world.Scene.Entities, static e => e.Id.StartsWith("cell_0_0:", StringComparison.Ordinal));
    }

    [Fact]
    public void LeavingKeepsACellThatIsInTheMargin()
    {
        _world.Files[Origin] = Tree("start");
        var streamer = _world.Streamer(radius: 0);
        Settle(streamer);

        Settle(streamer, x: 15);

        Assert.True(streamer.IsLoaded(Origin));
        Assert.Equal(2, _world.Scene.Length);
    }

    [Fact]
    public void LeavingKeepsAPinnedCellWhereverThePointGoes()
    {
        _world.Files[Origin] = Tree("start");
        var streamer = _world.Streamer(radius: 0);
        Settle(streamer);

        for (var i = 0; i < 5; i++)
        {
            streamer.Step(95, Middle, pinned: new HashSet<Cell> { Origin });
        }

        Assert.True(streamer.IsLoaded(Origin));
    }

    [Fact]
    public void LeavingThrowsAwayACellThatArrivesAfterThePointHasLeft()
    {
        _world.Files[Origin] = Tree("late");
        var streamer = _world.Streamer(radius: 0, inFlight: 1);
        streamer.Step(Middle, Middle);

        // Fetched, but the point is nowhere near it by the next frame.
        streamer.Step(95, Middle);

        Assert.False(streamer.IsLoaded(Origin));
        Assert.Equal(0, _world.Scene.Length);
    }

    [Fact]
    public void ACellThatIsNotThereIsLoadedAsEmptyAndNotAskedForAgain()
    {
        var streamer = _world.Streamer(radius: 0);

        Settle(streamer);

        Assert.True(streamer.IsLoaded(Origin));
        Assert.Equal([Origin], _world.Asked);
        Assert.Equal(0, _world.Scene.Length);
    }

    [Fact]
    public void ACellThatFailsToReadIsReportedAndAskedForAfterTheWait()
    {
        _world.Files[Origin] = new IOException("offline");
        var streamer = _world.Streamer(radius: 0);

        streamer.Step(Middle, Middle);
        Assert.IsType<IOException>(_world.Loads.Single().Error);

        _world.Now += TimeSpan.FromSeconds(1);
        streamer.Step(Middle, Middle);
        Assert.Single(_world.Asked);

        _world.Files[Origin] = Tree("back");
        _world.Now += TimeSpan.FromSeconds(5);
        Settle(streamer);
        Assert.Equal(2, _world.Scene.Length);
    }

    [Fact]
    public void ACellThatIsNotASceneIsReportedAndLeftOut()
    {
        _world.Files[Origin] = Encoding.UTF8.GetBytes("not a scene");
        var streamer = _world.Streamer(radius: 0);

        Settle(streamer);

        Assert.IsType<SceneFormatException>(_world.Loads.Single().Error);
        Assert.False(streamer.IsLoaded(Origin));
        Assert.Equal(0, _world.Scene.Length);
    }

    [Fact]
    public void ReportsWhatEachCellCost()
    {
        var bytes = Tree("profiled");
        _world.Files[Origin] = bytes;
        _world.Cost = TimeSpan.FromMilliseconds(2);
        var streamer = _world.Streamer(radius: 0);

        Settle(streamer);

        var load = _world.Loads.Single();
        Assert.Equal(Origin, load.Cell);
        Assert.Equal(bytes.Length, load.Bytes);
        Assert.Equal(2, load.Entities);
        Assert.Equal(TimeSpan.FromMilliseconds(2), load.Apply);
        Assert.Null(load.Error);
    }

    [Fact]
    public void ACellThatDoesNotReadIsNotCountedAsLoaded()
    {
        _world.Files[Origin] = Encoding.UTF8.GetBytes("not a scene");
        var streamer = _world.Streamer(radius: 0);

        var counted = 0;
        for (var i = 0; i < 10; i++)
        {
            streamer.Step(Middle, Middle);
            counted += streamer.LastStep.Loaded;
        }

        Assert.Equal(0, counted);
        Assert.NotNull(_world.Loads.Single().Error);
    }

    [Fact]
    public void AnApplyThatThrowsStillLeavesTheCellToBeDropped()
    {
        _world.Files[Origin] = Tree("half");
        var streamer = _world.Streamer(radius: 0);
        _world.Explode = true;
        streamer.Step(Middle, Middle);

        Assert.Throws<InvalidOperationException>(() => streamer.Step(Middle, Middle));
        Assert.True(streamer.IsLoaded(Origin));

        _world.Explode = false;
        streamer.Step(Middle + 100, Middle);
        Assert.Equal(0, _world.Scene.Length);
    }

    [Fact]
    public async Task AFetchThatNeverAnswersFailsAndLetsTheRestThrough()
    {
        _ = _world.Hold(Origin);
        foreach (var cell in Ground.Around(Origin, 1))
        {
            _world.Files[cell] = Tree("c");
        }

        var streamer = _world.Streamer(radius: 1, fetchTimeout: TimeSpan.FromMilliseconds(20));
        streamer.Step(Middle, Middle);
        await Waits.Until(() => _world.Loads.Any(static l => l.Error is TimeoutException), "the timeout");

        Settle(streamer);

        Assert.Single(_world.Loads, static l => l.Error is TimeoutException);
        Assert.True(streamer.IsLoaded(new Cell(-1, 0)));
        Assert.False(streamer.IsLoaded(Origin));
    }

    [Fact]
    public void WhatTheSceneReaderLeavesOutIsReported()
    {
        var written = (JsonMap)JsonText.Decode(new SceneDocument(entities: [new SceneEntity("a", "a")]).Encode())!;
        var entities = ((JsonList)written["entities"]!).Append(Map(("name", "no id"))).ToArray();
        _world.Files[Origin] = Encoding.UTF8.GetBytes(JsonText.Encode(With(written, ("entities", entities))));
        var streamer = _world.Streamer(radius: 0);

        Settle(streamer);

        var load = _world.Loads.Single();
        Assert.NotEmpty(load.Problems);
        Assert.Null(load.Error);
        Assert.Equal(1, _world.Scene.Length);
    }

    [Fact]
    public void APrefabInstanceInACellFileIsOpenedWhenTheStreamerIsGivenThePrefabs()
    {
        _world.Files[Origin] = House();
        var streamer = _world.Streamer(radius: 0, prefabs: Lamp);

        Settle(streamer);

        Assert.Contains("cell_0_0:house/pole", _world.Scene.Entities.Select(static e => e.Id));
        Assert.True(_world.Scene.Length > 1);
    }

    [Fact]
    public void APrefabInstanceInACellFileStaysALinkWithoutThem()
    {
        _world.Files[Origin] = House();
        var streamer = _world.Streamer(radius: 0);

        Settle(streamer);

        Assert.Equal(1, _world.Scene.Length);
    }

    [Fact]
    public void AWaitOrTimeoutOfNothingIsRefused()
    {
        static CellStreamer Make(TimeSpan? retryAfter = null, TimeSpan? fetchTimeout = null) => new(
            new CellWindow(Ground, 0),
            static _ => Task.FromResult<byte[]?>(null),
            static _ => { },
            retryAfter: retryAfter ?? TimeSpan.FromSeconds(1),
            fetchTimeout: fetchTimeout ?? TimeSpan.FromSeconds(1));

        Assert.Throws<ArgumentOutOfRangeException>(() => Make(retryAfter: TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => Make(fetchTimeout: TimeSpan.Zero));
    }

    [Fact]
    public void TheLoadedCellsAreACopyThatAStepDoesNotChange()
    {
        _world.Files[Origin] = Tree("a");
        var streamer = _world.Streamer(radius: 0);
        Settle(streamer);
        var held = streamer.Loaded;

        streamer.Step(Middle + 100, Middle);

        Assert.False(streamer.IsLoaded(Origin));
        Assert.Equal([Origin], held);
    }

    [Fact]
    public void APointThatIsNotANumberLeavesEverythingAsItIs()
    {
        _world.Files[Origin] = Tree("stays");
        var streamer = _world.Streamer(radius: 0);
        Settle(streamer);

        streamer.Step(double.NaN, 0);

        Assert.True(streamer.IsLoaded(Origin));
        Assert.Equal(2, _world.Scene.Length);
    }

    private static byte[] House() => File(new SceneEntity(
        "house",
        "house",
        components: new OrderedDictionary<string, SceneComponent>
        {
            [SceneComponents.Prefab] = new PrefabComponent(asset: LampAsset),
        }));

    private static PrefabDocument? Lamp(string asset) => asset != LampAsset
        ? null
        : new PrefabDocument(
            "Lamp",
            "lamp",
            new SceneDocument(entities: [new SceneEntity("lamp", "Lamp"), new SceneEntity("pole", "Pole", "lamp")]));
}
