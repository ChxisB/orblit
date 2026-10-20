namespace Orblit.Cells.Tests;

public class GridTests
{
    private readonly Grid _grid = new(10);

    [Fact]
    public void RoundsDownSoTheCellBeforeTheOriginIsMinusOne()
    {
        Assert.Equal(new Cell(0, 0), _grid.CellOf(0, 0));
        Assert.Equal(new Cell(0, 1), _grid.CellOf(9.99, 10));
        Assert.Equal(new Cell(-1, -1), _grid.CellOf(-0.01, -10));
    }

    [Fact]
    public void HasNoCellForAPointThatIsNotANumber()
    {
        Assert.Null(_grid.CellOf(double.NaN, 0));
        Assert.Null(_grid.CellOf(0, double.PositiveInfinity));
    }

    [Fact]
    public void HasNoCellForAPlaceThatIsNotOne()
    {
        Assert.Null(_grid.CellOf(1e300, 0));
        Assert.Null(_grid.CellOf(0, -1e12));
        // A cell size this small makes the division overflow to infinity.
        Assert.Null(new Grid(1e-320).CellOf(1, 0));
    }

    [Fact]
    public void PutsTheCentreInTheMiddleOfTheCell() =>
        Assert.Equal((25.0, -5.0), _grid.CenterOf(new Cell(2, -1)));

    [Fact]
    public void CountsADiagonalStepAsOne()
    {
        Assert.Equal(3, _grid.Apart(new Cell(0, 0), new Cell(3, -2)));
        Assert.Equal(0, _grid.Apart(new Cell(1, 1), new Cell(1, 1)));
    }

    [Fact]
    public void ListsTheSquareAroundACell()
    {
        var cells = _grid.Around(new Cell(5, 5), 2).ToHashSet();
        Assert.Equal(25, cells.Count);
        Assert.Contains(new Cell(3, 7), cells);
        Assert.Equal(new[] { new Cell(5, 5) }, _grid.Around(new Cell(5, 5), 0));
    }

    [Fact]
    public void RefusesASizeThatIsNotAboveZero()
    {
        foreach (var size in new[] { 0.0, -1.0, double.NaN, double.PositiveInfinity })
        {
            Assert.ThrowsAny<ArgumentException>(() => new Grid(size));
        }
    }

    [Fact]
    public void RefusesANegativeRadius() =>
        Assert.ThrowsAny<ArgumentException>(() => _grid.Around(new Cell(0, 0), -1).ToList());
}
