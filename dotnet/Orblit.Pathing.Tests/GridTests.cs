using Orblit.VectorMath;
using static Orblit.Pathing.Tests.PathSupport;

namespace Orblit.Pathing.Tests;

public class GridTests
{
    // A 20 by 20 field with a wall at x 10 to 11 from z 0 up to 15.
    private static NavGrid Walled()
    {
        var grid = new NavGrid(width: 20, depth: 20, cell: 1);
        grid.BlockRect(minX: 10, minZ: 0, maxX: 10.5, maxZ: 14.5);
        return grid;
    }

    // the grid

    [Fact]
    public void IsOpenUntilClosedAndClosedOffTheEdge()
    {
        var grid = new NavGrid(width: 4, depth: 4, cell: 2);
        Assert.True(grid.IsOpen(0, 0));
        Assert.False(grid.IsOpen(-1, 0));
        Assert.False(grid.IsOpen(4, 0));
        grid.SetOpen(1, 1, open: false);
        Assert.False(grid.IsOpen(1, 1));
    }

    [Fact]
    public void FindsTheCellAtAPlaceAndTheMiddleOfACell()
    {
        var grid = new NavGrid(width: 4, depth: 4, cell: 2, originX: 10, originZ: -4);
        Assert.Equal(new NavCell(1, 0), grid.CellAt(13, -3));
        Assert.Null(grid.CellAt(9, 0));
        var middle = grid.CentreOf(1, 0);
        Assert.Equal(13, middle.X);
        Assert.Equal(-3, middle.Z);
    }

    [Fact]
    public void KeepsAHeightAndAWeightForEachCell()
    {
        var grid = new NavGrid(width: 2, depth: 2, cell: 1);
        grid.SetHeight(1, 0, 3.5);
        grid.SetWeight(0, 1, 4);
        Assert.Equal(3.5, grid.HeightOf(1, 0));
        Assert.Equal(4, grid.WeightOf(0, 1));
        Assert.Equal(1, grid.WeightOf(0, 0));
        Assert.ThrowsAny<ArgumentException>(() => grid.SetWeight(0, 0, 0.5));
    }

    [Fact]
    public void RefusesAnEmptyGridOrACellOfNoSize()
    {
        Assert.ThrowsAny<ArgumentException>(() => new NavGrid(width: 0, depth: 4, cell: 1));
        Assert.ThrowsAny<ArgumentException>(() => new NavGrid(width: 4, depth: 4, cell: 0));
    }

    [Fact]
    public void BlocksACircleOfCells()
    {
        var grid = new NavGrid(width: 10, depth: 10, cell: 1);
        grid.BlockCircle(5, 5, 1.2);
        Assert.False(grid.IsOpen(4, 4));
        Assert.False(grid.IsOpen(5, 5));
        Assert.True(grid.IsOpen(7, 5));
    }

    // lines of sight

    [Fact]
    public void ALineOverOpenCellsIsClear()
    {
        var grid = new NavGrid(width: 10, depth: 10, cell: 1);
        Assert.True(grid.IsClear(new Vector3(0.5, 0, 0.5), new Vector3(9.5, 0, 9.5)));
    }

    [Fact]
    public void AWallInTheWayIsNot()
    {
        var grid = Walled();
        Assert.False(grid.IsClear(new Vector3(5, 0, 5), new Vector3(15, 0, 5)));
        Assert.True(grid.IsClear(new Vector3(5, 0, 17), new Vector3(15, 0, 17)));
    }

    [Fact]
    public void ALineCannotSqueezeBetweenTwoWallsThatTouch()
    {
        var grid = new NavGrid(width: 10, depth: 10, cell: 1);
        grid.SetOpen(4, 4, open: false);
        grid.SetOpen(5, 5, open: false);
        Assert.False(grid.IsClear(new Vector3(4.5, 0, 5.5), new Vector3(5.5, 0, 4.5)));
    }

    // finding a path

    [Fact]
    public void GoesStraightOverOpenGround()
    {
        var grid = new NavGrid(width: 20, depth: 20, cell: 1);
        var path = grid.FindPath(new Vector3(2, 0, 3), new Vector3(17, 0, 12))!;
        Assert.Equal(2, path.Count);
        Assert.Equal(2, path[0].X);
        Assert.Equal(12, path[^1].Z);
    }

    [Fact]
    public void GoesRoundAWallAndNotThroughIt()
    {
        var grid = Walled();
        var path = grid.FindPath(new Vector3(5.5, 0, 5.5), new Vector3(15.5, 0, 5.5))!;
        Assert.True(path.Count > 2);
        Assert.True(path.Max(p => p.Z) > 14.5);
        for (var i = 1; i < path.Count; i++)
        {
            Assert.True(grid.IsClear(path[i - 1], path[i]));
        }

        Assert.True(LengthOf(path) > 10);
    }

    [Fact]
    public void SaysSoWhenThereIsNoWay()
    {
        var grid = new NavGrid(width: 10, depth: 10, cell: 1);
        grid.BlockRect(minX: 0, minZ: 4.5, maxX: 10, maxZ: 5.5);
        Assert.Null(grid.FindPath(new Vector3(5, 0, 1), new Vector3(5, 0, 8)));
    }

    [Fact]
    public void TakesAStartInAWallToTheNearestOpenCell()
    {
        var grid = Walled();
        var path = grid.FindPath(new Vector3(10.5, 0, 5), new Vector3(15.5, 0, 5))!;
        var cell = grid.CellAt(path[0].X, path[0].Z)!.Value;
        Assert.True(grid.IsOpen(cell.I, cell.J));
    }

    [Fact]
    public void IsNullWhenNothingIsOpen()
    {
        var grid = new NavGrid(width: 3, depth: 3, cell: 1);
        grid.BlockRect(minX: 0, minZ: 0, maxX: 3, maxZ: 3);
        Assert.Null(grid.FindPath(new Vector3(1, 0, 1), new Vector3(2, 0, 2)));
    }

    [Fact]
    public void PrefersCheapGroundToAShorterWayOverDearGround()
    {
        var grid = new NavGrid(width: 21, depth: 9, cell: 1);
        for (var j = 0; j < 6; j++)
        {
            for (var i = 8; i < 13; i++)
            {
                grid.SetWeight(i, j, 8);
            }
        }

        var path = grid.FindPath(new Vector3(1.5, 0, 2.5), new Vector3(19.5, 0, 2.5))!;
        Assert.True(path.Max(p => p.Z) > 5.9);
    }

    [Fact]
    public void CarriesTheHeightOfTheGroundAlong()
    {
        var grid = new NavGrid(width: 10, depth: 1, cell: 1);
        for (var i = 0; i < 10; i++)
        {
            grid.SetHeight(i, 0, i * 0.5);
        }

        var path = grid.FindPath(new Vector3(0.5, 0, 0.5), new Vector3(8.5, 0, 0.5))!;
        Assert.Equal(4, path[^1].Y, 1e-6);
    }

    // for something with a size

    [Fact]
    public void AnInflatedGridClosesTheCellsNearAWall()
    {
        var grid = Walled();
        var fat = grid.Inflated(1.5);
        Assert.True(grid.IsOpen(9, 5));
        Assert.False(fat.IsOpen(9, 5));
        Assert.True(fat.IsOpen(8, 5));
        Assert.False(fat.IsOpen(10, 5));
    }

    [Fact]
    public void ARouteOnItStaysFurtherFromTheWallEnd()
    {
        var grid = Walled();
        var from = new Vector3(5.5, 0, 5.5);
        var to = new Vector3(15.5, 0, 5.5);
        double Top(NavGrid g) => g.FindPath(from, to)!.Max(p => p.Z);
        Assert.True(Top(grid.Inflated(1.5)) > Top(grid) + 0.5);
    }

    [Fact]
    public void DoesNotChangeTheGridItWasMadeFrom()
    {
        var grid = Walled();
        grid.Inflated(3);
        Assert.True(grid.IsOpen(7, 5));
    }
}
