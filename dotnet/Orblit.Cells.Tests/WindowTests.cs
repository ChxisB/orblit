namespace Orblit.Cells.Tests;

public class WindowTests
{
    // The point is the middle of cell (0, 0) unless a test moves it.
    private const double Middle = 5.0;

    private readonly Grid _grid = new(10);

    [Fact]
    public void WhatToLoadIsEveryCellWithinTheRadiusTheOneUnderThePointFirst()
    {
        var window = new CellWindow(_grid, loadRadius: 2);

        var plan = window.Plan(Middle, Middle);

        Assert.Equal(25, plan.Load.Count);
        Assert.Equal(new Cell(0, 0), plan.Load[0]);
    }

    [Fact]
    public void WhatToLoadPutsACellInFrontBeforeAnEquallyNearCellBehind()
    {
        var window = new CellWindow(_grid, loadRadius: 1);

        var plan = window.Plan(Middle, Middle, facingX: 1);

        var load = plan.Load.ToList();
        Assert.True(load.IndexOf(new Cell(1, 0)) < load.IndexOf(new Cell(-1, 0)));
    }

    [Fact]
    public void WhatToLoadRanksByDistanceAloneWhenBehindCountsForNothing()
    {
        var window = new CellWindow(_grid, loadRadius: 1, behindWeight: 0);

        var plan = window.Plan(Middle, Middle, facingX: 1);

        // Same distance, so the coordinates decide and the plan is repeatable.
        var load = plan.Load.ToList();
        Assert.True(load.IndexOf(new Cell(-1, 0)) < load.IndexOf(new Cell(1, 0)));
    }

    [Fact]
    public void WhatToLoadLeavesOutACellThatIsAlreadyLoaded()
    {
        var window = new CellWindow(_grid, loadRadius: 1);

        var plan = window.Plan(Middle, Middle, loaded: new HashSet<Cell> { new(0, 0), new(1, 0) });

        Assert.Equal(7, plan.Load.Count);
        Assert.DoesNotContain(new Cell(0, 0), plan.Load);
    }

    [Fact]
    public void WhatToLoadAsksForAPinnedCellBeyondTheRadius()
    {
        var window = new CellWindow(_grid, loadRadius: 1);

        var plan = window.Plan(Middle, Middle, pinned: new HashSet<Cell> { new(9, 9) });

        Assert.Contains(new Cell(9, 9), plan.Load);
    }

    [Fact]
    public void WhatToDropKeepsACellInTheMarginAndDropsOnePastIt()
    {
        var window = new CellWindow(_grid, loadRadius: 1);
        var inMargin = new Cell(2, 0);
        var past = new Cell(3, 0);

        var plan = window.Plan(Middle, Middle, loaded: new HashSet<Cell> { inMargin, past });

        Assert.Equal(new[] { past }, plan.Drop);
        Assert.DoesNotContain(inMargin, plan.Load);
    }

    [Fact]
    public void WhatToDropDoesNotFlickerForAPointWalkingAcrossABorder()
    {
        var window = new CellWindow(_grid, loadRadius: 1);
        var loaded = new HashSet<Cell>(_grid.Around(new Cell(0, 0), 1));

        // Over the border into cell 1 and back.
        foreach (var x in new[] { 9.0, 11.0, 9.0, 11.0 })
        {
            var plan = window.Plan(x, Middle, loaded: loaded);
            Assert.True(plan.Drop.Count == 0, $"at x = {x}");
            loaded.UnionWith(plan.Load);
        }
    }

    [Fact]
    public void WhatToDropDropsTheFarthestFirst()
    {
        var window = new CellWindow(_grid, loadRadius: 0, dropRadius: 0);

        var plan = window.Plan(
            Middle,
            Middle,
            loaded: new HashSet<Cell> { new(2, 0), new(5, 0), new(3, 0) });

        Assert.Equal(new[] { new Cell(5, 0), new Cell(3, 0), new Cell(2, 0) }, plan.Drop);
    }

    [Fact]
    public void WhatToDropNeverDropsAPinnedCell()
    {
        var window = new CellWindow(_grid, loadRadius: 0);
        var far = new Cell(8, 8);

        var plan = window.Plan(
            Middle,
            Middle,
            loaded: new HashSet<Cell> { far },
            pinned: new HashSet<Cell> { far });

        Assert.Empty(plan.Drop);
    }

    [Fact]
    public void APointThatIsNotANumberPlansNothing()
    {
        var window = new CellWindow(_grid, loadRadius: 1);

        var plan = window.Plan(double.NaN, 0, loaded: new HashSet<Cell> { new(0, 0) });

        Assert.Empty(plan.Load);
        Assert.Empty(plan.Drop);
    }

    [Fact]
    public void APointFarPastTheWorldKeepsTheWorld()
    {
        var window = new CellWindow(_grid, loadRadius: 1);

        var plan = window.Plan(1e300, 0, loaded: new HashSet<Cell> { new(0, 0) });

        Assert.Empty(plan.Load);
        Assert.Empty(plan.Drop);
    }

    [Fact]
    public void TheCellThePointStandsInLoadsBeforeOneAheadOfIt()
    {
        var window = new CellWindow(_grid, loadRadius: 1);

        // Near the border, facing the neighbour. Its middle is straight ahead and
        // the cell underfoot has its middle behind.
        var plan = window.Plan(0.1, 5, facingX: -1);

        Assert.Equal(new Cell(0, 0), plan.Load[0]);
        Assert.Equal(new Cell(-1, 0), plan.Load[1]);
    }

    [Fact]
    public void ADropRadiusBelowTheLoadRadiusIsRefused()
    {
        Assert.ThrowsAny<ArgumentException>(() => new CellWindow(_grid, loadRadius: 2, dropRadius: 1));
        Assert.ThrowsAny<ArgumentException>(() => new CellWindow(_grid, loadRadius: -1));
        Assert.ThrowsAny<ArgumentException>(() => new CellWindow(_grid, loadRadius: 1, behindWeight: -1));
    }
}
