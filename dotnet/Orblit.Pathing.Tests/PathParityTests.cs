using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Parity;
using Orblit.VectorMath;
using static Orblit.Pathing.Tests.PathCases;

namespace Orblit.Pathing.Tests;

public class PathParityTests
{
    private static readonly Fixture Golden = Fixture.Load("orblit_path", "path");

    // The grid.

    [Fact]
    public void GridsMatchTheFixture() =>
        Golden.Check("cells", c => Dump.Attempt(() => Whole(Build(In(c, 0)))));

    [Fact]
    public void InflatedGridsMatchTheFixture() =>
        Golden.Check("inflated", c => Dump.Attempt(() => Cells(Build(In(c, 0)).Grid.Inflated(Num(c, 1)))));

    [Fact]
    public void CellsAtAPointMatchTheFixture() =>
        Golden.Check("cell", c => Dump.Attempt(() => CellOut(Build(In(c, 0)).Grid.CellAt(Num(c, 1), Num(c, 2)))));

    [Fact]
    public void NearestOpenCellsMatchTheFixture() =>
        Golden.Check("nearest", c => Dump.Attempt(() => CellOut(
            Build(In(c, 0)).Grid.NearestOpen(Num(c, 1), Num(c, 2), within: In(c, 3).GetInt32()))));

    [Fact]
    public void CellCentresMatchTheFixture() =>
        Golden.Check("centre", c => Dump.Attempt(() =>
            Triple(Build(In(c, 0)).Grid.CentreOf(In(c, 1).GetInt64(), In(c, 2).GetInt64()))));

    [Fact]
    public void ClearLinesMatchTheFixture() =>
        Golden.Check("clear", c => Dump.Attempt(() =>
            JsonValue.Create(Build(In(c, 0)).Grid.IsClear(Vec(In(c, 1)), Vec(In(c, 2))))));

    [Fact]
    public void PathsMatchTheFixture() => Golden.Check("path", c => Dump.Attempt(() => PathOf(c)));

    [Fact]
    public void MeshesMatchTheFixture() => Golden.Check("mesh", c => Dump.Attempt(() => MeshPath(c)));

    // Walking and links.

    [Fact]
    public void WalkersMatchTheFixture() => Golden.Check("walker", c => Dump.Attempt(() => RunWalker(c)));

    [Fact]
    public void RoutesOverLinksMatchTheFixture() => Golden.Check("links", c => Dump.Attempt(() => Route(c)));

    [Fact]
    public void LockingAGateMatchesTheFixture() => Golden.Check("linkLock", c => Dump.Attempt(() => Locking(c)));

    [Fact]
    public void LinksThatAreRefusedMatchTheFixture() =>
        Golden.Check("linkMake", c => Dump.Attempt(() => JsonValue.Create(LinkOf(In(c, 0)).Kind.Length)));

    // Splines.

    [Fact]
    public void SplinesMatchTheFixture() => Golden.Check("spline", c => Dump.Attempt(() => Described(c)));

    [Fact]
    public void PositionsAlongASplineMatchTheFixture() =>
        Golden.Check("position", c => Dump.Attempt(() => Triple(SplineOf(In(c, 0)).PositionAt(Num(c, 1)))));

    [Fact]
    public void FramesAlongASplineMatchTheFixture() =>
        Golden.Check("frame", c => Dump.Attempt(() => Framed(SplineOf(In(c, 0)).FrameAt(Num(c, 1)))));

    [Fact]
    public void ForwardsAlongASplineMatchTheFixture() =>
        Golden.Check("forward", c => Dump.Attempt(() => Triple(SplineOf(In(c, 0)).FrameAt(Num(c, 1)).Forward)));

    [Fact]
    public void NearestPointsOnASplineMatchTheFixture() => Golden.Check("closest", c => Dump.Attempt(() => Nearest(c)));

    [Fact]
    public void DecodedTextsMatchTheFixture() => Golden.Check("decode", c => Dump.Attempt(() => Decoded(c)));

    // Followers.

    [Fact]
    public void FollowersMatchTheFixture() => Golden.Check("follow", c => Dump.Attempt(() => Follow(c)));

    [Fact]
    public void FollowersStartedAnywhereMatchTheFixture() =>
        Golden.Check("followFrom", c => Dump.Attempt(() => FollowFrom(c)));

    [Fact]
    public void RailsMatchTheFixture() => Golden.Check("rail", c => Dump.Attempt(() => RideRail(c)));

    [Fact]
    public void PatrolsMatchTheFixture() => Golden.Check("patrol", c => Dump.Attempt(() => Patrolled(c)));

    // Baking.

    [Fact]
    public void BakedTerrainsMatchTheFixture() => Golden.Check("bakeTerrain", c => Dump.Attempt(() => BakedTerrain(c)));

    [Fact]
    public void BakedMeshesMatchTheFixture() => Golden.Check("bakeMesh", c => Dump.Attempt(() => BakedMesh(c)));

    [Fact]
    public void CarvedGridsMatchTheFixture() => Golden.Check("carve", c => Dump.Attempt(() => Carved(c)));

    private static JsonObject Whole(Made made) => Cells(made.Grid, made.Obstacles);

    private static JsonNode? PathOf(JsonElement c)
    {
        var grid = Build(In(c, 0)).Grid;
        if (In(c, 3).ValueKind != JsonValueKind.Null)
        {
            grid = grid.Inflated(Num(c, 3));
        }

        return PathOut(grid.FindPath(Vec(In(c, 1)), Vec(In(c, 2))));
    }

    private static JsonObject MeshPath(JsonElement c)
    {
        var mesh = NavMesh.FromGrid(Build(In(c, 0)).Grid, Num(c, 1));
        return new JsonObject
        {
            ["count"] = mesh.Count,
            ["path"] = PathOut(mesh.FindPath(Vec(In(c, 2)), Vec(In(c, 3)))),
        };
    }

    private static JsonObject Route(JsonElement c)
    {
        var links = In(c, 1).EnumerateArray().Select(LinkOf).ToList();
        var planner = LinksOver(In(c, 0), links, In(c, 4).GetBoolean());
        foreach (var gate in In(c, 3).EnumerateArray())
        {
            planner.Lock(gate.GetString()!);
        }

        var route = planner.FindRoute(Vec(In(c, 5)), Vec(In(c, 6)), agent: In(c, 2).GetString()!);
        return new JsonObject
        {
            ["route"] = RouteOut(route, links),
            ["locked"] = Dump.Arr(new[] { "gate", "other" }, g => JsonValue.Create(planner.IsLocked(g))),
        };
    }

    private static JsonObject Locking(JsonElement c)
    {
        var links = In(c, 1).EnumerateArray().Select(LinkOf).ToList();
        var planner = LinksOver(In(c, 0), links, useMesh: false);
        planner.Lock("gate");
        var shut = GoblinRoute(planner, links);
        planner.Unlock("gate");
        return new JsonObject
        {
            ["shut"] = shut,
            ["open"] = GoblinRoute(planner, links),
            ["locked"] = planner.IsLocked("gate"),
        };
    }

    private static JsonNode? GoblinRoute(LinkPlanner planner, List<NavLink> links) =>
        RouteOut(planner.FindRoute(new Vector3(1.5, 0, 1.5), new Vector3(10.5, 0, 1.5), agent: "goblin"), links);

    private static JsonObject Described(JsonElement c)
    {
        var spline = SplineOf(In(c, 0));
        return new JsonObject
        {
            ["length"] = Dump.Num(spline.Length),
            ["closed"] = spline.Closed,
            ["count"] = spline.Knots.Count,
            ["text"] = spline.Encode(),
            ["json"] = Dump.Tree(spline.ToJson()),
        };
    }

    private static JsonArray Framed(PathFrame f) =>
        new(Triple(f.Position), Triple(f.Forward), Triple(f.Up), Triple(f.Right));

    private static JsonArray Nearest(JsonElement c)
    {
        var near = SplineOf(In(c, 0)).Closest(Vec(In(c, 1)));
        return new JsonArray(Dump.Num(near.Distance), Triple(near.Position), Dump.Num(near.Offset));
    }

    private static JsonObject Decoded(JsonElement c)
    {
        var spline = Spline.Decode(In(c, 0).GetString()!);
        return new JsonObject
        {
            ["text"] = spline.Encode(),
            ["length"] = Dump.Num(spline.Length),
            ["closed"] = spline.Closed,
        };
    }

    private static JsonArray Follow(JsonElement c)
    {
        var rider = new PathFollower(
            SplineOf(In(c, 0)), speed: Num(c, 2), end: EndOf(In(c, 1)), distance: Num(c, 3));
        var results = new JsonArray();
        foreach (var dt in In(c, 4).EnumerateArray())
        {
            rider.Advance(Fixture.Number(dt));
            var frame = rider.Frame;
            results.Add(new JsonArray(
                Dump.Num(rider.Distance), rider.IsFinished, Triple(frame.Position), Triple(frame.Forward)));
        }

        return results;
    }

    private static JsonArray FollowFrom(JsonElement c)
    {
        var rider = new PathFollower(SplineOf(In(c, 0)), end: EndOf(In(c, 1)), distance: Num(c, 2));
        rider.Advance(0.5);
        return new JsonArray(Dump.Num(rider.Distance), rider.IsFinished);
    }

    private static JsonArray RideRail(JsonElement c)
    {
        var rail = new Rail(SplineOf(In(c, 0)), speed: Num(c, 1), distance: Num(c, 2));
        var dts = In(c, 4).EnumerateArray().Select(Fixture.Number).ToList();
        var results = new JsonArray();
        var k = 0;
        foreach (var spot in In(c, 3).EnumerateArray())
        {
            rail.Track(Vec(spot), dts[k++]);
            results.Add(new JsonArray(Dump.Num(rail.Distance), Triple(rail.Position)));
        }

        return results;
    }

    private static JsonArray Patrolled(JsonElement c)
    {
        var stops = In(c, 0).EnumerateArray().Select(s => new PatrolStop(Vec(s[0]), wait: N(s, 1))).ToList();
        var patrol = new Patrol(stops, end: EndOf(In(c, 1)), reached: Num(c, 2));
        var results = new JsonArray();
        foreach (var step in In(c, 3).EnumerateArray())
        {
            patrol.Update(Vec(step[0]), N(step, 1));
            results.Add(new JsonArray(
                patrol.Target is { } target ? Triple(target) : null, patrol.IsWaiting, patrol.IsFinished));
        }

        return results;
    }

    private static JsonObject BakedTerrain(JsonElement c)
    {
        var terrain = TerrainOf(In(c, 0));
        if (In(c, 4).ValueKind == JsonValueKind.Null)
        {
            var walkable = Walkable(In(c, 3).GetString()!);
            return Cells(NavBake.FromTerrain(terrain, Num(c, 1), Num(c, 2), walkable), rows: true);
        }

        var area = Area(In(c, 4));
        return Cells(NavBake.FromTerrain(terrain, minX: area[0], minZ: area[1], maxX: area[2], maxZ: area[3]), rows: true);
    }

    private static JsonObject BakedMesh(JsonElement c)
    {
        var mesh = MeshOf(In(c, 0));
        if (In(c, 2).ValueKind == JsonValueKind.Null)
        {
            return Cells(NavBake.FromMesh(mesh, Num(c, 1)), rows: true);
        }

        var area = Area(In(c, 2));
        return Cells(NavBake.FromMesh(mesh, Num(c, 1), area[0], area[1], area[2], area[3]), rows: true);
    }

    private static JsonObject Carved(JsonElement c)
    {
        var grid = Build(In(c, 0)).Grid;
        NavBake.Carve(grid, MeshOf(In(c, 1)));
        return Cells(grid, rows: true);
    }
}
