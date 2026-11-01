using Orblit.VectorMath;
using static Orblit.Pathing.Tests.PathSupport;

namespace Orblit.Pathing.Tests;

public class NavMeshTests
{
    private static double Top(IReadOnlyList<Vector3> path) => path.Max(p => p.Z);

    // A 20 by 20 field with a wall at x 10 to 11 from z 0 up to 15.
    private static NavGrid Walled()
    {
        var grid = new NavGrid(width: 20, depth: 20, cell: 1);
        grid.BlockRect(minX: 10, minZ: 0, maxX: 10.5, maxZ: 14.5);
        return grid;
    }

    // Two corridors three cells wide that meet at a right angle. Open ground
    // is the bottom rows and the right columns, turned by flipX and flipZ.
    private static NavGrid Elbow(bool flipX, bool flipZ)
    {
        var grid = new NavGrid(width: 20, depth: 20, cell: 1);
        for (var j = 0; j < 20; j++)
        {
            for (var i = 0; i < 20; i++)
            {
                var x = flipX ? 19 - i : i;
                var z = flipZ ? 19 - j : j;
                grid.SetOpen(x, z, open: j < 3 || i >= 17);
            }
        }

        return grid;
    }

    // building

    [Fact]
    public void MergesOpenGroundIntoAFewRectangles()
    {
        var mesh = NavMesh.FromGrid(new NavGrid(width: 50, depth: 50, cell: 1));
        Assert.Equal(1, mesh.Count);
    }

    [Fact]
    public void KeepsAWalledFieldToAHandful()
    {
        var mesh = NavMesh.FromGrid(Walled());
        Assert.True(mesh.Count < 10);
        Assert.True(mesh.Count > 1);
    }

    [Fact]
    public void HasNothingInAGridWithNothingOpen()
    {
        var grid = new NavGrid(width: 4, depth: 4, cell: 1);
        grid.BlockRect(minX: 0, minZ: 0, maxX: 4, maxZ: 4);
        var mesh = NavMesh.FromGrid(grid);
        Assert.Equal(0, mesh.Count);
        Assert.Null(mesh.FindPath(new Vector3(1, 0, 1), new Vector3(3, 0, 3)));
    }

    [Fact]
    public void DoesNotFollowTheGridWhenItChangesAfterwards()
    {
        var grid = new NavGrid(width: 20, depth: 20, cell: 1);
        var mesh = NavMesh.FromGrid(grid);
        grid.BlockRect(minX: 0, minZ: 9, maxX: 20, maxZ: 10);
        Assert.NotNull(mesh.FindPath(new Vector3(5, 0, 2), new Vector3(5, 0, 18)));
    }

    // finding a path

    [Fact]
    public void IsAStraightLineOverOpenGround()
    {
        var mesh = NavMesh.FromGrid(new NavGrid(width: 30, depth: 30, cell: 1));
        var path = mesh.FindPath(new Vector3(2, 0, 3), new Vector3(25, 0, 20))!;
        Assert.Equal(2, path.Count);
        Assert.Equal(2, path[0].X);
        Assert.Equal(20, path[^1].Z);
    }

    [Fact]
    public void TurnsAtTheTwoCornersOfAWallEndAndNowhereElse()
    {
        var mesh = NavMesh.FromGrid(Walled());
        var path = mesh.FindPath(new Vector3(5.5, 0, 5.5), new Vector3(15.5, 0, 5.5))!;
        Assert.Equal(4, path.Count);
        Assert.Equal(10, path[1].X, 1e-9);
        Assert.Equal(15, path[1].Z, 1e-9);
        Assert.Equal(11, path[2].X, 1e-9);
        Assert.Equal(15, path[2].Z, 1e-9);
    }

    [Fact]
    public void IsNoLongerThanTheGridPathAndOftenShorter()
    {
        var grid = Walled();
        var mesh = NavMesh.FromGrid(grid);
        var from = new Vector3(2.5, 0, 3.5);
        var to = new Vector3(17.5, 0, 8.5);
        var onGrid = grid.FindPath(from, to)!;
        var onMesh = mesh.FindPath(from, to)!;
        Assert.True(LengthOf(onMesh) <= LengthOf(onGrid) + 1e-9);
        Assert.True(LengthOf(onMesh) > from.DistanceTo(to));
    }

    [Fact]
    public void NeverCrossesAClosedCell()
    {
        var grid = Walled();
        grid.BlockRect(minX: 3, minZ: 16, maxX: 15, maxZ: 17);
        var mesh = NavMesh.FromGrid(grid);
        var path = mesh.FindPath(new Vector3(2.5, 0, 2.5), new Vector3(17.5, 0, 18.5))!;
        for (var i = 1; i < path.Count; i++)
        {
            Assert.True(grid.IsClear(path[i - 1], path[i]), $"{i}");
        }
    }

    [Fact]
    public void SaysSoWhenThereIsNoWay()
    {
        var grid = new NavGrid(width: 10, depth: 10, cell: 1);
        grid.BlockRect(minX: 0, minZ: 4.5, maxX: 10, maxZ: 5.5);
        var mesh = NavMesh.FromGrid(grid);
        Assert.Null(mesh.FindPath(new Vector3(5, 0, 1), new Vector3(5, 0, 8)));
    }

    [Fact]
    public void TakesAGoalInAWallToTheNearestOpenCell()
    {
        var grid = Walled();
        var mesh = NavMesh.FromGrid(grid);
        var path = mesh.FindPath(new Vector3(5.5, 0, 5.5), new Vector3(10.5, 0, 5.5))!;
        var end = grid.CellAt(path[^1].X, path[^1].Z)!.Value;
        Assert.True(grid.IsOpen(end.I, end.J));
    }

    [Fact]
    public void CarriesTheHeightOfTheGroundAlong()
    {
        var grid = new NavGrid(width: 10, depth: 4, cell: 1);
        for (var j = 0; j < 4; j++)
        {
            for (var i = 0; i < 10; i++)
            {
                grid.SetHeight(i, j, 2);
            }
        }

        var path = NavMesh.FromGrid(grid).FindPath(new Vector3(0.5, 0, 1.5), new Vector3(8.5, 0, 1.5))!;
        foreach (var point in path)
        {
            Assert.Equal(2, point.Y, 1e-6);
        }
    }

    [Fact]
    public void KeepsARouteOffDearGroundWhenThereIsACheapWay()
    {
        var grid = new NavGrid(width: 21, depth: 9, cell: 1);
        for (var j = 0; j < 6; j++)
        {
            for (var i = 8; i < 13; i++)
            {
                grid.SetWeight(i, j, 8);
            }
        }

        var path = NavMesh.FromGrid(grid).FindPath(new Vector3(1.5, 0, 2.5), new Vector3(19.5, 0, 2.5))!;
        Assert.True(Top(path) > 5.9);
    }

    // the funnel

    // Open corridors meet at an inner corner (17, 3) before the flip. A
    // funnel with its sides swapped goes round the outer corner instead.
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TurnsTheInnerCornerWithFlipXAndFlipZ(bool flipX, bool flipZ)
    {
        var mesh = NavMesh.FromGrid(Elbow(flipX, flipZ));
        double X(double v) => flipX ? 20 - v : v;
        double Z(double v) => flipZ ? 20 - v : v;
        var from = new Vector3(X(1.5), 0, Z(1.5));
        var to = new Vector3(X(18.5), 0, Z(18.5));

        var there = mesh.FindPath(from, to)!;
        Assert.Equal(3, there.Count);
        Assert.Equal(X(17), there[1].X, 1e-9);
        Assert.Equal(Z(3), there[1].Z, 1e-9);
        var back = mesh.FindPath(to, from)!;
        Assert.Equal(3, back.Count);
        Assert.Equal(X(17), back[1].X, 1e-9);
        Assert.Equal(Z(3), back[1].Z, 1e-9);
    }

    // for something with a size

    [Fact]
    public void ARouteStaysOneCellFurtherOutRoundAWallEnd()
    {
        var grid = Walled();
        var from = new Vector3(5.5, 0, 5.5);
        var to = new Vector3(15.5, 0, 5.5);
        var thin = NavMesh.FromGrid(grid).FindPath(from, to)!;
        var fat = NavMesh.FromGrid(grid, radius: 1).FindPath(from, to)!;
        Assert.Equal(Top(thin) + 1, Top(fat), 1e-9);
    }
}
