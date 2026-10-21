using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Agent;
using Orblit.Parity;
using Orblit.Terrain;
using Orblit.VectorMath;

namespace Orblit.Pathing.Tests;

// Reads the specs the Dart exporter wrote, and writes what the port gives in
// the shape the exporter used.
internal static class PathCases
{
    public sealed record Made(NavGrid Grid, List<NavObstacle> Obstacles);

    public static JsonElement In(JsonElement c, int index) => c.GetProperty("in")[index];

    public static double Num(JsonElement c, int index) => Fixture.Number(In(c, index));

    public static double N(JsonElement list, int index) => Fixture.Number(list[index]);

    public static Vector3 Vec(JsonElement list) => new(N(list, 0), N(list, 1), N(list, 2));

    public static JsonArray Triple(Vector3 v) => new(Dump.Num(v.X), Dump.Num(v.Y), Dump.Num(v.Z));

    public static JsonNode? PathOut(IReadOnlyList<Vector3>? path) => path is null ? null : Dump.Arr(path, Triple);

    public static JsonNode CellOut(NavCell? cell) =>
        cell is { } at ? new JsonArray(Dump.Big(at.I), Dump.Big(at.J)) : new JsonArray();

    public static PathEnd EndOf(JsonElement name) => Enum.Parse<PathEnd>(name.GetString()!, ignoreCase: true);

    // A grid.

    public static Made Build(JsonElement spec)
    {
        var grid = new NavGrid(spec[0].GetInt32(), spec[1].GetInt32(), N(spec, 2), N(spec, 3), N(spec, 4));
        var made = new Made(grid, []);
        foreach (var edit in spec[5].EnumerateArray())
        {
            Edit(made, edit);
        }

        return made;
    }

    public static void Edit(Made made, JsonElement e)
    {
        var grid = made.Grid;
        switch (e[0].GetString())
        {
            case "rect":
                grid.BlockRect(N(e, 1), N(e, 2), N(e, 3), N(e, 4));
                break;
            case "circle":
                grid.BlockCircle(N(e, 1), N(e, 2), N(e, 3));
                break;
            case "open":
                grid.SetOpen(e[1].GetInt64(), e[2].GetInt64(), e[3].GetBoolean());
                break;
            case "height":
                grid.SetHeight(e[1].GetInt64(), e[2].GetInt64(), N(e, 3));
                break;
            case "weight":
                grid.SetWeight(e[1].GetInt64(), e[2].GetInt64(), N(e, 3));
                break;
            case "obRect":
                made.Obstacles.Add(grid.AddObstacleRect(N(e, 1), N(e, 2), N(e, 3), N(e, 4)));
                break;
            case "obCircle":
                made.Obstacles.Add(grid.AddObstacleCircle(N(e, 1), N(e, 2), N(e, 3)));
                break;
            default:
                made.Obstacles[e[1].GetInt32()].Lift();
                break;
        }
    }

    // Every cell and the ring round the grid. With rows, one sum a row stands in for the numbers.
    public static JsonObject Cells(NavGrid grid, IEnumerable<NavObstacle>? obstacles = null, bool rows = false)
    {
        var open = new StringBuilder();
        var heights = new JsonArray();
        var weights = new JsonArray();
        for (var j = -1; j <= grid.Depth; j++)
        {
            Row(grid, j, rows, open, heights, weights);
        }

        return new JsonObject
        {
            ["width"] = grid.Width,
            ["depth"] = grid.Depth,
            ["cell"] = Dump.Num(grid.Cell),
            ["originX"] = Dump.Num(grid.OriginX),
            ["originZ"] = Dump.Num(grid.OriginZ),
            ["open"] = open.ToString(),
            ["heights"] = heights,
            ["weights"] = weights,
            ["standing"] = Dump.Arr(obstacles ?? [], o => JsonValue.Create(o.Standing)),
        };
    }

    // F2: six parameters, called by name from one place.
    private static void Row(NavGrid grid, int j, bool sums, StringBuilder open, JsonArray heights, JsonArray weights)
    {
        var heightSum = 0.0;
        var weightSum = 0.0;
        for (var i = -1; i <= grid.Width; i++)
        {
            open.Append(grid.IsOpen(i, j) ? '1' : '0');
            heightSum += grid.HeightOf(i, j);
            weightSum += grid.WeightOf(i, j);
            if (!sums)
            {
                heights.Add(Dump.Num(grid.HeightOf(i, j)));
                weights.Add(Dump.Num(grid.WeightOf(i, j)));
            }
        }

        if (sums)
        {
            heights.Add(Dump.Num(heightSum));
            weights.Add(Dump.Num(weightSum));
        }
    }

    // A walker.

    public static JsonObject WalkerState(NavWalker w) => new()
    {
        ["position"] = Triple(w.Body.Position),
        ["velocity"] = Triple(w.Body.Velocity),
        ["goal"] = w.Goal is { } goal ? Triple(goal) : null,
        ["route"] = PathOut(w.Route),
        ["hasRoute"] = w.HasRoute,
        ["arrived"] = w.IsArrived,
    };

    public static JsonNode RunWalker(JsonElement c)
    {
        var made = Build(In(c, 0));
        var grid = made.Grid;
        var walker = new NavWalker(
            body: new Steerable(position: Vec(In(c, 3)), maxSpeed: 5, maxForce: 40),
            planner: grid.FindPath,
            isClear: In(c, 1).GetBoolean() ? new Clear(grid.IsClear) : null,
            lookahead: In(c, 2).GetInt32());
        var states = new JsonArray(WalkerState(walker));
        var index = 0;
        foreach (var command in In(c, 4).EnumerateArray())
        {
            var update = Command(made, walker, command);
            if (!update || index % 4 == 0)
            {
                states.Add(WalkerState(walker));
            }

            index++;
        }

        return states;
    }

    // Whether the command was an update.
    private static bool Command(Made made, NavWalker walker, JsonElement command)
    {
        switch (command[0].GetString())
        {
            case "goto":
                walker.GoTo(Vec(command[1]));
                return false;
            case "update":
                walker.Update(N(command, 1));
                return true;
            case "stop":
                walker.Stop();
                return false;
            default:
                Edit(made, command);
                return false;
        }
    }

    // Links.

    public static NavLink LinkOf(JsonElement spec) => new(
        from: Vec(spec[0]),
        to: Vec(spec[1]),
        kind: spec[2].GetString()!,
        cost: N(spec, 3),
        agents: spec[4].EnumerateArray().Select(a => a.GetString()!).ToList(),
        gate: spec[5].GetString(),
        twoWay: spec[6].GetBoolean());

    public static JsonNode? RouteOut(IReadOnlyList<RouteStep>? route, IReadOnlyList<NavLink> links) =>
        route is null ? null : Dump.Arr(route, step => StepOut(step, links));

    private static JsonArray StepOut(RouteStep step, IReadOnlyList<NavLink> links) => step switch
    {
        WalkStep walk => new JsonArray("walk", PathOut(walk.Points)),
        LinkStep hop => new JsonArray("link", links.ToList().IndexOf(hop.Link), hop.Reversed),
        _ => throw new InvalidOperationException("an unknown step"),
    };

    // A planner over the ground spec and the links, walking the grid or its mesh.
    public static LinkPlanner LinksOver(JsonElement ground, List<NavLink> links, bool useMesh)
    {
        var grid = Build(ground).Grid;
        var mesh = NavMesh.FromGrid(grid);
        return new LinkPlanner(useMesh ? new Planner(mesh.FindPath) : new Planner(grid.FindPath), links);
    }

    // Splines.

    public static Spline SplineOf(JsonElement spec)
    {
        var closed = spec[2].GetBoolean();
        return spec[0].GetString() switch
        {
            "bezier" => Spline.Bezier([.. spec[1].EnumerateArray().Select(KnotOf)], closed),
            "through" => Spline.Through(Points(spec[1]), Tilts(spec[3]), closed),
            _ => Spline.Line(Points(spec[1]), Tilts(spec[3]), closed),
        };
    }

    private static PathKnot KnotOf(JsonElement k) => new(
        Vec(k[0]),
        N(k, 1),
        k[2].ValueKind == JsonValueKind.Null ? null : Vec(k[2]),
        k[3].ValueKind == JsonValueKind.Null ? null : Vec(k[3]));

    private static List<Vector3> Points(JsonElement list) => [.. list.EnumerateArray().Select(Vec)];

    private static List<double>? Tilts(JsonElement list) =>
        list.ValueKind == JsonValueKind.Null ? null : [.. list.EnumerateArray().Select(Fixture.Number)];

    public static JsonArray Doubles(IEnumerable<double> values) => Dump.Arr(values, Dump.Num);

    // Baking.

    public static Terrain.Terrain TerrainOf(JsonElement spec)
    {
        var spacing = spec.TryGetProperty("spacing", out var apart) ? Fixture.Number(apart) : 1;
        var terrain = new Terrain.Terrain(regionSize: 16, spacing: spacing);
        var slope = Fixture.Number(spec.GetProperty("slope"));
        var wave = Fixture.Number(spec.GetProperty("wave"));
        var tilt = Fixture.Number(spec.GetProperty("tilt"));
        var mark = spec.GetProperty("mark").GetString()!;
        foreach (var key in spec.GetProperty("keys").EnumerateArray())
        {
            var region = terrain.FillHeights(
                new RegionKey(key[0].GetInt32(), key[1].GetInt32()),
                (x, z) => (slope * x) + (wave * VectorMath.Doubles.Mod(Math.Floor(x / 3), 2)) + (tilt * z));
            for (var j = 0; j < 16; j++)
            {
                for (var i = 0; i < 16; i++)
                {
                    region.Cover[(j * 16) + i] = CoverOf(mark, i, j).Word;
                }
            }
        }

        return terrain;
    }

    private static Cover CoverOf(string mark, int i, int j) => mark switch
    {
        "all" => Cover.Of(navigation: true),
        "half" => Cover.Of(navigation: i < 8),
        "none" => Cover.Plain,
        "holes" => Cover.Of(navigation: true, hole: (i + j) % 5 == 0),
        "plainHoles" => Cover.Of(hole: (i + j) % 5 == 0),
        _ => Cover.Of(navigation: ((i * 3) + j) % 4 != 0, @base: (i + j) % 3),
    };

    public static Func<Cover, bool> Walkable(string mode) => mode switch
    {
        "any" => _ => true,
        "notHole" => cover => !cover.Hole,
        _ => NavBake.Marked,
    };

    public static double?[] Area(JsonElement list) =>
        [.. list.EnumerateArray().Select(a => a.ValueKind == JsonValueKind.Null ? (double?)null : Fixture.Number(a))];

    public static MeshWalk MeshOf(JsonElement spec) => new(
        [.. spec[0].EnumerateArray().Select(Fixture.Number)],
        [.. spec[1].EnumerateArray().Select(i => i.GetInt64())],
        N(spec, 2),
        N(spec, 3),
        N(spec, 4));
}
