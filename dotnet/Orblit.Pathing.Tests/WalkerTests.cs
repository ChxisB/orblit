using Orblit.Agent;
using Orblit.VectorMath;

namespace Orblit.Pathing.Tests;

public class WalkerTests
{
    private readonly NavGrid _grid = Field();

    // A 40 by 30 field with a wall at x 20 from z 0 up to 22, so the way
    // round is at the top.
    private static NavGrid Field()
    {
        var grid = new NavGrid(width: 40, depth: 30, cell: 1);
        grid.BlockRect(minX: 20, minZ: 0, maxX: 20.5, maxZ: 21.5);
        return grid;
    }

    private static Steerable WalkerBody(Vector3 at) => new(position: at, maxSpeed: 5, maxForce: 40);

    // Runs walker for up to seconds and answers every place it stood.
    private static List<Vector3> Walk(NavWalker walker, double seconds)
    {
        var trail = new List<Vector3> { walker.Body.Position.Clone() };
        for (var t = 0.0; t < seconds && !walker.IsArrived; t += 1.0 / 60)
        {
            walker.Update(1.0 / 60);
            trail.Add(walker.Body.Position.Clone());
        }

        return trail;
    }

    private void AssertOnOpenCells(List<Vector3> trail)
    {
        foreach (var place in trail)
        {
            var cell = _grid.CellAt(place.X, place.Z)!.Value;
            Assert.True(_grid.IsOpen(cell.I, cell.J), $"{place}");
        }
    }

    // walking a route

    [Fact]
    public void AnAgentWalksRoundAWallToATarget()
    {
        var safe = _grid.Inflated(1);
        var walker = new NavWalker(body: WalkerBody(new Vector3(3.5, 0, 5.5)), planner: safe.FindPath);
        walker.GoTo(new Vector3(36.5, 0, 5.5));
        Assert.True(walker.HasRoute);
        var trail = Walk(walker, 60);
        Assert.True(walker.IsArrived);
        Assert.True(walker.Body.Position.DistanceTo(new Vector3(36.5, 0, 5.5)) < 0.6);
        Assert.True(trail.Max(p => p.Z) > 21);
        AssertOnOpenCells(trail);
    }

    [Fact]
    public void TheSameOnANavmesh()
    {
        var mesh = NavMesh.FromGrid(_grid, radius: 1);
        var walker = new NavWalker(body: WalkerBody(new Vector3(3.5, 0, 5.5)), planner: mesh.FindPath);
        walker.GoTo(new Vector3(36.5, 0, 5.5));
        var trail = Walk(walker, 60);
        Assert.True(walker.IsArrived);
        AssertOnOpenCells(trail);
    }

    [Fact]
    public void WithNoRouteItStaysWhereItIs()
    {
        var closed = new NavGrid(width: 10, depth: 10, cell: 1);
        closed.BlockRect(minX: 0, minZ: 4.5, maxX: 10, maxZ: 5.5);
        var walker = new NavWalker(body: WalkerBody(new Vector3(5, 0, 1)), planner: closed.FindPath);
        walker.GoTo(new Vector3(5, 0, 8));
        Assert.False(walker.HasRoute);
        walker.Update(1);
        Assert.Equal(1, walker.Body.Position.Z);
        Assert.False(walker.IsArrived);
    }

    [Fact]
    public void IsNotArrivedBeforeItHasSetOut()
    {
        var walker = new NavWalker(body: WalkerBody(new Vector3(3.5, 0, 5.5)), planner: _grid.FindPath);
        Assert.False(walker.IsArrived);
        Assert.Null(walker.Goal);
    }

    [Fact]
    public void StopForgetsTheRouteAndTheGoal()
    {
        var walker = new NavWalker(body: WalkerBody(new Vector3(3.5, 0, 5.5)), planner: _grid.FindPath);
        walker.GoTo(new Vector3(36.5, 0, 5.5));
        Assert.NotEmpty(walker.Route);
        Assert.Equal(36.5, walker.Goal!.X);
        walker.Stop();
        Assert.False(walker.HasRoute);
        Assert.Null(walker.Goal);
    }

    [Fact]
    public void PlansAgainFromWhereItIsWhenToldANewGoal()
    {
        var walker = new NavWalker(
            body: WalkerBody(new Vector3(3.5, 0, 5.5)),
            planner: _grid.Inflated(1).FindPath);
        walker.GoTo(new Vector3(36.5, 0, 5.5));
        for (var i = 0; i < 120; i++)
        {
            walker.Update(1.0 / 60);
        }

        var here = walker.Body.Position.Clone();
        walker.GoTo(new Vector3(3.5, 0, 25.5));
        Assert.True(walker.Route[0].DistanceTo(here) < 1e-6);
        Assert.Equal(25.5, walker.Route[^1].Z, 1e-6);
    }
}
