using Orblit.VectorMath;

namespace Orblit.Pathing.Tests;

// What C# does that Dart cannot be asked: an array cannot hold a grid as large as Dart accepts, a bad enum value is
// a number, and a link keeps its own copy of the agents it was given.
public class AddedTests
{
    private readonly Spline _line = Spline.Line([new Vector3(0, 0, 0), new Vector3(10, 0, 0)]);

    [Fact]
    public void AGridLargerThanAnArrayIsRefused() =>
        Assert.Throws<ArgumentException>(() => new NavGrid(int.MaxValue, int.MaxValue, 1));

    [Fact]
    public void AGridThatWrapsA32BitProductIsRefused() =>
        Assert.Throws<ArgumentException>(() => new NavGrid(65536, 65536, 1));

    [Fact]
    public void ANegativeSizeIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new NavGrid(-1, 4, 1));
        Assert.Throws<ArgumentException>(() => new NavGrid(4, -1, 1));
        Assert.Throws<ArgumentException>(() => new NavGrid(0, 4, 1));
    }

    [Fact]
    public void ARefusedCellNamesTheArgument()
    {
        var error = Assert.Throws<ArgumentException>(() => new NavGrid(4, 4, 0));
        Assert.Equal("cell", error.ParamName);
    }

    [Fact]
    public void ARefusedWeightNamesTheArgument()
    {
        var error = Assert.Throws<ArgumentException>(() => new NavGrid(4, 4, 1).SetWeight(0, 0, 0.5));
        Assert.Equal("weight", error.ParamName);
    }

    [Fact]
    public void AnEmptyHeapRefusesToGiveAnItem() => Assert.Throws<InvalidOperationException>(() => new MinHeap().RemoveFirst());

    [Fact]
    public void AHeapGivesItemsBackBySmallestKey()
    {
        var heap = new MinHeap();
        var keys = new[] { 9.0, 3, 7, 1, 8, 2, 6, 5, 4, 0 };
        for (var item = 0; item < keys.Length; item++)
        {
            heap.Add(item, keys[item]);
        }

        var order = new List<double>();
        while (!heap.IsEmpty)
        {
            order.Add(keys[heap.RemoveFirst()]);
        }

        Assert.Equal(order.Order().ToList(), order);
    }

    [Fact]
    public void AFollowerWithABadEndIsRefused()
    {
        var rider = new PathFollower(_line, end: (PathEnd)99);
        Assert.Throws<InvalidOperationException>(() => rider.Advance(1));
    }

    [Fact]
    public void APatrolWithABadEndIsRefusedAtTheLastStop()
    {
        var patrol = new Patrol([new PatrolStop(new Vector3(0, 0, 0))], end: (PathEnd)99);
        Assert.Throws<InvalidOperationException>(() => patrol.Update(new Vector3(0, 0, 0), 1));
    }

    [Fact]
    public void AMeshIndexOffTheEndIsRefused()
    {
        var mesh = new MeshWalk([0, 0, 0, 1, 0, 0, 0, 0, 1], [0, 1, 5], 2, 0.5, 0.8);
        Assert.Throws<ArgumentOutOfRangeException>(() => NavBake.FromMesh(mesh));
    }

    [Fact]
    public void ANegativeMeshIndexIsRefused()
    {
        var mesh = new MeshWalk([0, 0, 0, 1, 0, 0, 0, 0, 1], [0, 1, -1], 2, 0.5, 0.8);
        Assert.Throws<ArgumentOutOfRangeException>(() => NavBake.FromMesh(mesh));
    }

    [Fact]
    public void ALinkKeepsItsOwnCopyOfItsAgents()
    {
        var agents = new List<string> { "goblin" };
        var link = new NavLink(new Vector3(0, 0, 0), new Vector3(1, 0, 0), "door", agents: agents);
        agents.Add("elf");
        Assert.Equal(["goblin"], link.Agents);
    }

    [Fact]
    public void ALinkKeepsItsOwnCopyOfItsEnds()
    {
        var from = new Vector3(0, 0, 0);
        var link = new NavLink(from, new Vector3(1, 0, 0), "door");
        from.X = 5;
        Assert.Equal(0, link.From.X);
    }

    [Fact]
    public void ACellAtAHugePointIsOffTheGrid()
    {
        var grid = new NavGrid(4, 4, 1);
        Assert.Null(grid.CellAt(1e300, 0));
        Assert.Null(grid.CellAt(0, -1e300));
    }

    [Fact]
    public void ACellAtANanPointIsRefused() => Assert.ThrowsAny<ArgumentException>(() => new NavGrid(4, 4, 1).CellAt(double.NaN, 0));

    [Fact]
    public void AnObstacleCountWrapsAt65536ThenOpensTheCell()
    {
        var grid = new NavGrid(2, 2, 1);
        for (var n = 0; n < 65536; n++)
        {
            grid.AddObstacleRect(0, 0, 0.5, 0.5);
        }

        Assert.True(grid.IsOpen(0, 0));
    }

    [Fact]
    public void AnObstacleCountBelow65536KeepsTheCellShut()
    {
        var grid = new NavGrid(2, 2, 1);
        for (var n = 0; n < 65535; n++)
        {
            grid.AddObstacleRect(0, 0, 0.5, 0.5);
        }

        Assert.False(grid.IsOpen(0, 0));
    }

    [Fact]
    public void LiftingAnObstacleTwiceReleasesItOnce()
    {
        var grid = new NavGrid(2, 2, 1);
        var first = grid.AddObstacleRect(0, 0, 0.5, 0.5);
        var second = grid.AddObstacleRect(0, 0, 0.5, 0.5);
        first.Lift();
        first.Lift();
        Assert.False(grid.IsOpen(0, 0));
        second.Lift();
        Assert.True(grid.IsOpen(0, 0));
    }
}
