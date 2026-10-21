using Orblit.Agent;
using Orblit.VectorMath;
using static Orblit.Pathing.Tests.PathSupport;

namespace Orblit.Pathing.Tests;

public class ObstaclesTests
{
    private static Vector3 West => new(3.5, 0, 11.5);

    private static Vector3 East => new(36.5, 0, 11.5);

    // A 40 by 30 field cut by a wall at x 20, with a four cell door at z 10 to
    // 13 and the long way round over the top, past z 21.
    private static NavGrid Walled()
    {
        var grid = new NavGrid(width: 40, depth: 30, cell: 1);
        grid.BlockRect(minX: 20, minZ: 0, maxX: 20.5, maxZ: 9.5);
        grid.BlockRect(minX: 20, minZ: 14, maxX: 20.5, maxZ: 21.5);
        return grid;
    }

    private static NavObstacle ShutDoor(NavGrid grid) =>
        grid.AddObstacleRect(minX: 19.5, minZ: 10, maxX: 20.5, maxZ: 13.5);

    private static NavWalker WalkerOn(
        NavGrid grid,
        List<int>? asked = null,
        Planner? planner = null,
        Clear? clear = null,
        long lookahead = 3,
        Vector3? at = null)
    {
        var plan = planner ?? grid.FindPath;
        return new NavWalker(
            body: new Steerable(position: at ?? West, maxSpeed: 5, maxForce: 40),
            planner: (from, to) =>
            {
                asked?.Add(1);
                return plan(from, to);
            },
            isClear: clear ?? grid.IsClear,
            lookahead: lookahead);
    }

    // an obstacle

    [Fact]
    public void ShutsTheCellsItCoversAndNothingElse()
    {
        var grid = Walled();

        var door = ShutDoor(grid);

        Assert.True(door.Standing);
        for (var j = 10; j <= 13; j++)
        {
            Assert.False(grid.IsOpen(19, j), $"cell 19, {j}");
            Assert.False(grid.IsOpen(20, j), $"cell 20, {j}");
        }

        Assert.True(grid.IsOpen(18, 11));
        Assert.True(grid.IsOpen(21, 11));
        Assert.True(grid.IsOpen(19, 9));
        Assert.True(grid.IsOpen(19, 14));
    }

    [Fact]
    public void OpensThemAgainWhenItIsLifted()
    {
        var grid = Walled();
        var door = ShutDoor(grid);

        door.Lift();

        Assert.False(door.Standing);
        Assert.True(grid.IsOpen(19, 11));
        Assert.True(grid.IsOpen(20, 11));
    }

    [Fact]
    public void LiftingTwiceDoesNotOpenWhatAnotherOneShuts()
    {
        var grid = Walled();
        var first = ShutDoor(grid);
        var second = ShutDoor(grid);

        first.Lift();
        first.Lift();

        Assert.False(grid.IsOpen(19, 11));
        second.Lift();
        Assert.True(grid.IsOpen(19, 11));
    }

    [Fact]
    public void NeverOpensACellAWallCloses()
    {
        var grid = Walled();
        var over = grid.AddObstacleRect(minX: 19.5, minZ: 8, maxX: 20.5, maxZ: 11.5);
        Assert.False(grid.IsOpen(20, 9));

        over.Lift();

        Assert.False(grid.IsOpen(20, 9), "the wall stays");
        Assert.True(grid.IsOpen(20, 10));
    }

    [Fact]
    public void ACircleShutsTheCellsWhoseMiddlesAreInsideIt()
    {
        var grid = new NavGrid(width: 20, depth: 20, cell: 1);

        var crate = grid.AddObstacleCircle(10, 10, 1.6);

        Assert.False(grid.IsOpen(9, 9));
        Assert.False(grid.IsOpen(10, 10));
        Assert.False(grid.IsOpen(8, 9), "1.58 from the middle");
        Assert.False(grid.IsOpen(11, 10));
        Assert.True(grid.IsOpen(8, 8), "the corner of a box");
        Assert.True(grid.IsOpen(7, 9));
        Assert.True(grid.IsOpen(11, 12));
        crate.Lift();
        Assert.True(grid.IsOpen(9, 9));
    }

    [Fact]
    public void MovesByLiftingItAndPuttingAnotherDown()
    {
        var grid = new NavGrid(width: 20, depth: 20, cell: 1);
        var crate = grid.AddObstacleCircle(5, 5, 1);

        crate.Lift();
        grid.AddObstacleCircle(14, 14, 1);

        Assert.True(grid.IsOpen(4, 4));
        Assert.False(grid.IsOpen(13, 13));
    }

    [Fact]
    public void SendsARouteRoundItAndBackThroughOnceLifted()
    {
        var grid = Walled();
        var direct = LengthOf(grid.FindPath(West, East)!);

        var door = ShutDoor(grid);
        var round = LengthOf(grid.FindPath(West, East)!);
        door.Lift();
        var again = LengthOf(grid.FindPath(West, East)!);

        Assert.True(round > direct + 5, "over the top");
        Assert.Equal(direct, again, 1e-9);
    }

    [Fact]
    public void IsClosedInAnInflatedCopyWhichDoesNotFollowALift()
    {
        var grid = Walled();
        var door = ShutDoor(grid);

        var copy = grid.Inflated(1);
        door.Lift();

        Assert.False(copy.IsOpen(19, 11));
        Assert.True(grid.IsOpen(19, 11));
        Assert.False(copy.IsOpen(18, 11), "a cell off the door");
    }

    // a walker that checks its way

    private static void Run(NavWalker walker, double seconds, Action? each = null)
    {
        for (var t = 0.0; t < seconds && !walker.IsArrived; t += 1.0 / 60)
        {
            each?.Invoke();
            walker.Update(1.0 / 60);
        }
    }

    [Fact]
    public void GoesRoundADoorThatShutsInFrontOfItWithNoGoTo()
    {
        var grid = Walled();
        var asked = new List<int>();
        var walker = WalkerOn(grid, asked: asked);
        walker.GoTo(East);
        var top = 0.0;
        NavObstacle? door = null;

        Run(walker, 60, each: () =>
        {
            if (door is null && walker.Body.Position.X > 12)
            {
                door = ShutDoor(grid);
            }

            if (walker.Body.Position.Z > top)
            {
                top = walker.Body.Position.Z;
            }
        });

        Assert.NotNull(door);
        Assert.True(walker.IsArrived);
        Assert.True(walker.Body.Position.DistanceTo(East) < 0.6);
        Assert.True(top > 21, "over the top");
        Assert.Equal(2, asked.Count);
    }

    [Fact]
    public void PlansOnlyOnceWhenNothingGetsInTheWay()
    {
        var grid = Walled();
        var asked = new List<int>();
        var walker = WalkerOn(grid, asked: asked);
        walker.GoTo(East);

        Run(walker, 60);

        Assert.True(walker.IsArrived);
        Assert.Single(asked);
    }

    [Fact]
    public void WalksOnThroughAShutDoorWhenItIsNotChecking()
    {
        var grid = Walled();
        var walker = new NavWalker(
            body: new Steerable(position: West, maxSpeed: 5, maxForce: 40),
            planner: grid.FindPath);
        walker.GoTo(East);
        ShutDoor(grid);

        Run(walker, 60);

        Assert.True(walker.IsArrived);
        Assert.Equal(2, walker.Route.Count);
    }

    [Fact]
    public void StandsAndKeepsItsGoalWhenNoWayIsLeft()
    {
        // A wall the whole depth of the field, with a door that then shuts.
        var open = new NavGrid(width: 40, depth: 30, cell: 1);
        open.BlockRect(minX: 20, minZ: 0, maxX: 20.5, maxZ: 9.5);
        open.BlockRect(minX: 20, minZ: 14, maxX: 20.5, maxZ: 29.5);
        var walker = WalkerOn(open);
        walker.GoTo(East);
        Assert.True(walker.HasRoute);

        walker.Update(1.0 / 60);
        var door = ShutDoor(open);
        walker.Update(1.0 / 60);

        Assert.False(walker.HasRoute);
        Assert.Equal(East, walker.Goal);
        var stood = walker.Body.Position.Clone();
        walker.Update(1.0 / 60);
        Assert.Equal(stood, walker.Body.Position);

        door.Lift();
        walker.GoTo(East);
        Run(walker, 60);
        Assert.True(walker.IsArrived);
    }

    [Fact]
    public void StandsWhenThePlannerDoesNotSeeWhatTheCheckSees()
    {
        var grid = Walled();
        var stale = grid.Inflated(0.1);
        var asked = new List<int>();
        var walker = WalkerOn(grid, asked: asked, planner: stale.FindPath);
        walker.GoTo(East);
        ShutDoor(grid);

        walker.Update(1.0 / 60);
        walker.Update(1.0 / 60);

        Assert.False(walker.HasRoute);
        Assert.Equal(2, asked.Count);
    }

    [Fact]
    public void LooksOnlyAsManyLegsAheadAsItIsToldTo()
    {
        Vector3[] route = [new(0, 0, 0), new(10, 0, 0), new(10, 0, 10), new(0, 0, 10)];

        // The third leg from the body is the one from route[1] to route[2].
        bool Clear(Vector3 from, Vector3 to) =>
            !(from.DistanceTo(route[1]) < 1e-9 && to.DistanceTo(route[2]) < 1e-9);
        NavWalker Make(long lookahead, List<int> asked) => new(
            body: new Steerable(position: route[0].Clone(), maxSpeed: 5, maxForce: 40),
            planner: (_, _) =>
            {
                asked.Add(1);
                return route;
            },
            isClear: Clear,
            lookahead: lookahead);
        var nearAsked = new List<int>();
        var farAsked = new List<int>();
        var near = Make(2, nearAsked);
        near.GoTo(route[3]);
        var far = Make(3, farAsked);
        far.GoTo(route[3]);

        near.Update(1.0 / 60);
        far.Update(1.0 / 60);

        Assert.True(near.HasRoute);
        Assert.Single(nearAsked);
        Assert.False(far.HasRoute, "shut again after planning anew");
        Assert.Equal(2, farAsked.Count);
    }

    [Fact]
    public void RefusesALookaheadOfNothing()
    {
        NavWalker Make(long lookahead) => new(
            body: new Steerable(),
            planner: (_, _) => null,
            lookahead: lookahead);

        Assert.ThrowsAny<ArgumentException>(() => Make(0));
        Assert.ThrowsAny<ArgumentException>(() => Make(-1));
        Assert.IsType<NavWalker>(Make(1));
    }
}
