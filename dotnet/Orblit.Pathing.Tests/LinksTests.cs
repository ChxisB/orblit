using Orblit.VectorMath;
using static Orblit.Pathing.Tests.PathSupport;

namespace Orblit.Pathing.Tests;

public class LinksTests
{
    private static Vector3 West => new(3.5, 0, 2.5);

    private static Vector3 East => new(16.5, 0, 2.5);

    // The ground length of a whole route, with a link counting for its own cost.
    private static double CostOf(IReadOnlyList<RouteStep> route) => route.Sum(step => step switch
    {
        WalkStep walk => LengthOf(walk.Points),
        LinkStep link => link.Link.Cost,
        _ => throw new InvalidOperationException("A step is a walk or a link."),
    });

    // A 20 by 20 field with a wall at x 10 to 11 from z 0 up to 15, so the way
    // round is over the top.
    private static NavGrid Walled()
    {
        var grid = new NavGrid(width: 20, depth: 20, cell: 1);
        grid.BlockRect(minX: 10, minZ: 0, maxX: 10.5, maxZ: 14.5);
        return grid;
    }

    // Three rooms with no way between them over the ground.
    private static NavGrid Rooms()
    {
        var grid = new NavGrid(width: 20, depth: 20, cell: 1);
        grid.BlockRect(minX: 6, minZ: 0, maxX: 6.5, maxZ: 19.5);
        grid.BlockRect(minX: 13, minZ: 0, maxX: 13.5, maxZ: 19.5);
        return grid;
    }

    // A jump across the wall of Walled, low down where the way round is long.
    private static NavLink Jump(
        double cost = 0,
        IEnumerable<string>? agents = null,
        string? gate = null,
        bool twoWay = false) => new(
            from: new Vector3(8.5, 0, 2.5),
            to: new Vector3(12.5, 0, 2.5),
            kind: "jump",
            cost: cost,
            agents: agents,
            gate: gate,
            twoWay: twoWay);

    // a link planner

    [Fact]
    public void CrossesAGapByALinkInsteadOfGoingRound()
    {
        var grid = Walled();
        var planner = new LinkPlanner(grid.FindPath, [Jump()]);

        var route = planner.FindRoute(West, East)!;

        Assert.Equal(3, route.Count);
        var before = (WalkStep)route[0];
        var cross = (LinkStep)route[1];
        var after = (WalkStep)route[2];
        Assert.Equal("jump", cross.Link.Kind);
        Assert.False(cross.Reversed);
        Assert.True(before.Points[^1].DistanceTo(cross.Start) < 0.01);
        Assert.True(after.Points[0].DistanceTo(cross.End) < 0.01);
        var round = LengthOf(grid.FindPath(West, East)!);
        Assert.True(CostOf(route) < round - 20);
    }

    [Fact]
    public void GoesTheLongWayForAnAgentKindThatMayNotUseIt()
    {
        var grid = Walled();
        var planner = new LinkPlanner(grid.FindPath, [Jump(agents: ["climber"])]);
        var round = grid.FindPath(West, East)!;

        foreach (var agent in new[] { "walker", "" })
        {
            var route = planner.FindRoute(West, East, agent: agent)!;
            Assert.True(route.Count == 1, $"agent \"{agent}\"");
            Assert.Equal(round.Count, ((WalkStep)route.Single()).Points.Count);
            Assert.Equal(LengthOf(round), CostOf(route), 1e-9);
        }

        var climbs = planner.FindRoute(West, East, agent: "climber")!;
        Assert.Single(climbs.OfType<LinkStep>());
    }

    [Fact]
    public void SkipsALockedDoorAndUsesItOnceItIsUnlocked()
    {
        var planner = new LinkPlanner(Walled().FindPath, [Jump(gate: "door")]);

        planner.Lock("door");
        Assert.True(planner.IsLocked("door"));
        var shut = planner.FindRoute(West, East)!;
        Assert.Empty(shut.OfType<LinkStep>());

        planner.Unlock("door");
        Assert.False(planner.IsLocked("door"));
        var open = planner.FindRoute(West, East)!;
        Assert.Single(open.OfType<LinkStep>());
        Assert.True(CostOf(open) < CostOf(shut));
    }

    [Fact]
    public void ALockOnOneGateLeavesALinkOnAnotherOpen()
    {
        var planner = new LinkPlanner(Walled().FindPath, [Jump(gate: "door"), Jump()]);

        planner.Lock("door");

        var route = planner.FindRoute(West, East)!;
        Assert.Single(route.OfType<LinkStep>());
        Assert.Null(((LinkStep)route[1]).Link.Gate);
    }

    [Fact]
    public void UsesAOneWayLinkOnlyTheWayItGoes()
    {
        var grid = Walled();
        var back = new LinkPlanner(grid.FindPath, [Jump()]);
        var both = new LinkPlanner(grid.FindPath, [Jump(twoWay: true)]);

        Assert.Empty(back.FindRoute(East, West)!.OfType<LinkStep>());

        var route = both.FindRoute(East, West)!;
        var cross = route.OfType<LinkStep>().Single();
        Assert.True(cross.Reversed);
        Assert.Equal(cross.Link.To, cross.Start);
        Assert.Equal(cross.Link.From, cross.End);
    }

    [Fact]
    public void TakesALinkOnlyWhileItCostsLessThanWalking()
    {
        var grid = Walled();
        var free = CostOf(new LinkPlanner(grid.FindPath, [Jump()]).FindRoute(West, East)!);
        var round = LengthOf(grid.FindPath(West, East)!);
        var margin = round - free;

        LinkPlanner With(double cost) => new(grid.FindPath, [Jump(cost: cost)]);

        Assert.Single(With(margin - 1).FindRoute(West, East)!.OfType<LinkStep>());
        Assert.Empty(With(margin + 1).FindRoute(West, East)!.OfType<LinkStep>());
    }

    [Fact]
    public void WalksWhenALinkCostsExactlyWhatWalkingDoes()
    {
        // A straight line for ground, so the tie is exact.
        IReadOnlyList<Vector3>? Line(Vector3 a, Vector3 b) => [a, b];
        var planner = new LinkPlanner(
            Line,
            [new NavLink(from: Vector3.Zero(), to: new Vector3(10, 0, 0), kind: "jump", cost: 10)]);

        var route = planner.FindRoute(Vector3.Zero(), new Vector3(10, 0, 0))!;

        Assert.IsType<WalkStep>(route.Single());
    }

    [Fact]
    public void ChainsLinksBetweenRoomsThatHaveNoGroundBetweenThem()
    {
        var grid = Rooms();
        var first = new NavLink(from: new Vector3(5.5, 0, 10.5), to: new Vector3(7.5, 0, 10.5), kind: "door");
        var second = new NavLink(
            from: new Vector3(12.5, 0, 4.5),
            to: new Vector3(14.5, 0, 4.5),
            kind: "ladder",
            cost: 3);
        Assert.Null(grid.FindPath(West, East));

        var route = new LinkPlanner(grid.FindPath, [second, first]).FindRoute(West, East)!;

        Assert.Equal(
            [typeof(WalkStep), typeof(LinkStep), typeof(WalkStep), typeof(LinkStep), typeof(WalkStep)],
            route.Select(step => step.GetType()));
        Assert.Same(first, ((LinkStep)route[1]).Link);
        Assert.Same(second, ((LinkStep)route[3]).Link);
    }

    [Fact]
    public void HasNoRouteWhenTheLinksDoNotJoinTheGroundUp()
    {
        var grid = Rooms();
        var planner = new LinkPlanner(
            grid.FindPath,
            [new NavLink(from: new Vector3(5.5, 0, 10.5), to: new Vector3(7.5, 0, 10.5), kind: "door")]);

        Assert.Null(planner.FindRoute(West, East));
        Assert.NotNull(planner.FindRoute(West, new Vector3(9.5, 0, 2.5)));
    }

    [Fact]
    public void StartingAtTheFootOfALinkBeginsWithTheCrossing()
    {
        var planner = new LinkPlanner(Walled().FindPath, [Jump()]);

        var route = planner.FindRoute(Jump().From, East)!;

        Assert.IsType<LinkStep>(route[0]);
        Assert.Equal(2, route.Count);
    }

    [Fact]
    public void AStartThatIsTheGoalIsOneWalk()
    {
        var planner = new LinkPlanner(Walled().FindPath, [Jump()]);

        var route = planner.FindRoute(West, West)!;

        Assert.Single(route);
        Assert.IsType<WalkStep>(route.Single());
    }

    [Fact]
    public void IsAPlainGroundRouteWhenThereAreNoLinks()
    {
        var grid = Walled();
        var route = new LinkPlanner(grid.FindPath, []).FindRoute(West, East)!;

        var expected = grid.FindPath(West, East)!;
        Assert.Equal(expected.Count, ((WalkStep)route.Single()).Points.Count);
    }

    [Fact]
    public void WorksOverANavmeshAsWellAsAGrid()
    {
        var mesh = NavMesh.FromGrid(Walled());
        var planner = new LinkPlanner(mesh.FindPath, [Jump()]);

        var route = planner.FindRoute(West, East)!;

        Assert.Single(route.OfType<LinkStep>());
    }

    [Fact]
    public void TheSameAskTwiceGivesTheSameRoute()
    {
        var planner = new LinkPlanner(Walled().FindPath, [Jump()]);

        var once = planner.FindRoute(West, East)!;
        var twice = planner.FindRoute(West, East)!;

        Assert.Equal(once.Count, twice.Count);
        Assert.Equal(CostOf(once), CostOf(twice));
    }

    // a link

    [Fact]
    public void KeepsItsOwnCopyOfItsEnds()
    {
        var from = new Vector3(1, 2, 3);
        var to = new Vector3(4, 5, 6);
        var link = new NavLink(from: from, to: to, kind: "jump");

        from.X = 9;
        to.X = 9;

        Assert.Equal(1, link.From.X);
        Assert.Equal(4, link.To.X);
    }

    [Fact]
    public void RefusesWhatCannotWork()
    {
        NavLink Make(string kind = "jump", double cost = 0) => new(
            from: Vector3.Zero(),
            to: Vector3.Zero(),
            kind: kind,
            cost: cost);

        Assert.ThrowsAny<ArgumentException>(() => Make(kind: ""));
        Assert.ThrowsAny<ArgumentException>(() => Make(cost: -1));
        Assert.ThrowsAny<ArgumentException>(() => Make(cost: double.NaN));
        Assert.ThrowsAny<ArgumentException>(() => Make(cost: double.PositiveInfinity));
        Assert.Equal(0, Make(cost: 0).Cost);
    }
}
