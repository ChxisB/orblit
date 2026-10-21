using Orblit.Terrain;
using Orblit.VectorMath;

namespace Orblit.Pathing;

/// <summary>
/// How a mesh is read for walking: what it is made of, and who walks on it.
/// <c>Positions</c> is x, y, z for each vertex, one after another, and <c>Indices</c>
/// three vertices for each triangle, already in world space.
/// </summary>
public readonly record struct MeshWalk(
    IReadOnlyList<double> Positions,
    IReadOnlyList<long> Indices,
    double AgentHeight,
    double StepHeight,
    double MaxSlope);

/// <summary>Grids made from ground that already exists: a terrain, or a mesh.</summary>
public static class NavBake
{
    /// <summary>
    /// Whether <paramref name="cover"/> marks ground an agent may walk on: the cover's
    /// navigation bit.
    /// </summary>
    public static bool Marked(Cover cover) => cover.Navigation;

    /// <summary>
    /// A <see cref="NavGrid"/> over a terrain, one cell for each <paramref name="cell"/> metres.
    /// A cell is open where the ground exists, is not a hole, is no steeper
    /// than <paramref name="maxSlope"/> (radians) and <paramref name="walkable"/> says its cover is ground to
    /// walk on. By default that is the cover's navigation bit, so ground nobody
    /// marked is closed; pass <c>(cover) =&gt; true</c> to walk anywhere. Each open
    /// cell's weight is how much longer a metre of slope is than a metre of
    /// map: one over the cosine of the slope.
    /// The area is the whole terrain unless <paramref name="minX"/>, <paramref name="minZ"/>, <paramref name="maxX"/> and <paramref name="maxZ"/>
    /// narrow it. A terrain with no regions gives a grid of one closed cell.
    /// </summary>
    public static NavGrid FromTerrain(
        Terrain.Terrain terrain,
        double cell = 1,
        double maxSlope = Math.PI / 4,
        Func<Cover, bool>? walkable = null,
        double? minX = null,
        double? minZ = null,
        double? maxX = null,
        double? maxZ = null)
    {
        var area = AreaOf(terrain, minX, minZ, maxX, maxZ);
        var grid = new NavGrid(
            CellsAcross(area.MaxX - area.MinX, cell),
            CellsAcross(area.MaxZ - area.MinZ, cell),
            cell,
            area.MinX,
            area.MinZ);
        var steepest = Math.Cos(maxSlope);
        for (var j = 0; j < grid.Depth; j++)
        {
            for (var i = 0; i < grid.Width; i++)
            {
                BakeCell(grid, terrain, i, j, steepest, walkable ?? Marked);
            }
        }

        return grid;
    }

    /// <summary>
    /// Closes every cell of <paramref name="grid"/> that <paramref name="mesh"/> has a wall or a ceiling in
    /// where an agent would stand.
    /// A triangle that is steeper than <see cref="MeshWalk.MaxSlope"/>, or faces down, is
    /// solid. A cell is closed when solid lies above its floor by more than
    /// <see cref="MeshWalk.StepHeight"/> and below <see cref="MeshWalk.AgentHeight"/>: a step is walked
    /// over, a wall is not. A surface that faces up never closes anything, so
    /// a mesh that is one plane seen from above has nothing under it blocked.
    /// </summary>
    public static void Carve(NavGrid grid, MeshWalk mesh) =>
        Solids(grid, mesh, (i, j, y) =>
        {
            if (!grid.IsOpen(i, j))
            {
                return;
            }

            var floor = grid.HeightOf(i, j);
            if (y > floor + mesh.StepHeight && y < floor + mesh.AgentHeight)
            {
                grid.SetOpen(i, j, open: false);
            }
        });

    /// <summary>
    /// A <see cref="NavGrid"/> over a mesh: open where an up-facing triangle lies no
    /// steeper than <see cref="MeshWalk.MaxSlope"/>, with room for the agent above it.
    /// One floor to a cell, the lowest that has room. A bridge over a road is
    /// not walkable; the road under it is. The area is the mesh's footprint
    /// unless it is given.
    /// </summary>
    public static NavGrid FromMesh(
        MeshWalk mesh,
        double cell = 0.5,
        double? minX = null,
        double? minZ = null,
        double? maxX = null,
        double? maxZ = null)
    {
        var footprint = FootprintOf(mesh.Positions);
        var left = minX ?? footprint.MinX;
        var top = minZ ?? footprint.MinZ;
        var grid = new NavGrid(
            CellsAcross((maxX ?? footprint.MaxX) - left, cell),
            CellsAcross((maxZ ?? footprint.MaxZ) - top, cell),
            cell,
            left,
            top);
        var floors = FloorsOf(grid, mesh);
        var solids = new Dictionary<long, List<double>>();
        Solids(grid, mesh, (i, j, y) =>
        {
            var key = (j * grid.Width) + i;
            if (!solids.TryGetValue(key, out var found))
            {
                solids[key] = found = [];
            }

            found.Add(y);
        });
        for (var j = 0; j < grid.Depth; j++)
        {
            for (var i = 0; i < grid.Width; i++)
            {
                var key = ((long)j * grid.Width) + i;
                var floor = LowestWithRoom(floors.GetValueOrDefault(key), solids.GetValueOrDefault(key), mesh);
                grid.SetOpen(i, j, open: floor is not null);
                if (floor is { } height)
                {
                    grid.SetHeight(i, j, height);
                }
            }
        }

        return grid;
    }

    /// <summary>
    /// How many cells of <paramref name="cell"/> metres cover <paramref name="span"/>, at least one. A count past what an int
    /// holds is refused by the grid, as Dart's is when the list is made.
    /// </summary>
    private static int CellsAcross(double span, double cell) =>
        (int)Math.Min(Math.Max(1, Doubles.Ceil(span / cell)), int.MaxValue);

    private static void BakeCell(NavGrid grid, Terrain.Terrain terrain, int i, int j, double steepest, Func<Cover, bool> walkable)
    {
        var at = grid.CentreOf(i, j);
        var ground = terrain.HeightAt(at.X, at.Z);
        var cover = terrain.CoverAt(at.X, at.Z);
        var normal = terrain.NormalAt(at.X, at.Z);
        var open = ground is not null
            && cover is { } found
            && normal is not null
            && walkable(found)
            && normal.Y >= steepest;
        grid.SetOpen(i, j, open);
        if (open)
        {
            grid.SetHeight(i, j, ground!.Value);
            grid.SetWeight(i, j, Math.Min(10, Math.Max(1, 1 / normal!.Y)));
        }
    }

    private static (double MinX, double MinZ, double MaxX, double MaxZ) AreaOf(
        Terrain.Terrain terrain,
        double? minX,
        double? minZ,
        double? maxX,
        double? maxZ)
    {
        var metres = terrain.RegionSize * terrain.Spacing;
        var keys = terrain.Regions.Select(region => region.Key).ToList();
        if (keys.Count == 0)
        {
            return (minX ?? 0, minZ ?? 0, maxX ?? 1, maxZ ?? 1);
        }

        return (
            minX ?? (keys.Min(k => k.X) * metres),
            minZ ?? (keys.Min(k => k.Z) * metres),
            maxX ?? ((keys.Max(k => k.X) + 1) * metres),
            maxZ ?? ((keys.Max(k => k.Z) + 1) * metres));
    }

    private static double? LowestWithRoom(List<double>? floors, List<double>? solids, MeshWalk mesh)
    {
        if (floors is null)
        {
            return null;
        }

        var ordered = new List<double>(floors);
        ordered.Sort(Doubles.Compare);
        foreach (var floor in ordered)
        {
            var blocked = solids?.Any(y => y > floor + mesh.StepHeight && y < floor + mesh.AgentHeight) ?? false;
            if (!blocked)
            {
                return floor;
            }
        }

        return null;
    }

    private static (double MinX, double MinZ, double MaxX, double MaxZ) FootprintOf(IReadOnlyList<double> positions)
    {
        if (positions.Count < 3)
        {
            return (0, 0, 1, 1);
        }

        var minX = double.PositiveInfinity;
        var minZ = double.PositiveInfinity;
        var maxX = double.NegativeInfinity;
        var maxZ = double.NegativeInfinity;
        for (var v = 0; v + 2 < positions.Count; v += 3)
        {
            minX = Math.Min(minX, positions[v]);
            maxX = Math.Max(maxX, positions[v]);
            minZ = Math.Min(minZ, positions[v + 2]);
            maxZ = Math.Max(maxZ, positions[v + 2]);
        }

        return (minX, minZ, maxX, maxZ);
    }

    /// <summary>
    /// One number of the mesh, by place. Off the end throws, where Dart's list throws a
    /// <c>RangeError</c>, and a 64-bit index past what a list holds is off the end.
    /// </summary>
    private static double Number(IReadOnlyList<double> positions, long at) =>
        (ulong)at >= (ulong)positions.Count ? throw new ArgumentOutOfRangeException(nameof(at), at, "Not a place in the mesh.") : positions[(int)at];

    private static Vector3 Vertex(IReadOnlyList<double> positions, long index) =>
        new(Number(positions, index * 3), Number(positions, (index * 3) + 1), Number(positions, (index * 3) + 2));

    /// <summary>Each triangle of the mesh, as its three corners and the normal that its winding gives.</summary>
    private static IEnumerable<(Vector3 A, Vector3 B, Vector3 C, Vector3 Normal)> Triangles(MeshWalk mesh)
    {
        for (var t = 0; t + 2 < mesh.Indices.Count; t += 3)
        {
            var a = Vertex(mesh.Positions, mesh.Indices[t]);
            var b = Vertex(mesh.Positions, mesh.Indices[t + 1]);
            var c = Vertex(mesh.Positions, mesh.Indices[t + 2]);
            yield return (a, b, c, (b - a).Cross(c - a));
        }
    }

    /// <summary>
    /// The height of each up-facing triangle where it covers the middle of a
    /// cell, by cell number.
    /// </summary>
    private static Dictionary<long, List<double>> FloorsOf(NavGrid grid, MeshWalk mesh)
    {
        var floors = new Dictionary<long, List<double>>();
        var steepest = Math.Cos(mesh.MaxSlope);
        foreach (var (a, b, c, normal) in Triangles(mesh))
        {
            if (normal.Length2 < 1e-18 || normal.Normalized().Y < steepest)
            {
                continue;
            }

            Covering(grid, a, b, c, (i, j, x, z) =>
            {
                var key = (j * grid.Width) + i;
                if (!floors.TryGetValue(key, out var found))
                {
                    floors[key] = found = [];
                }

                found.Add(PlaneHeight(a, normal, x, z));
            });
        }

        return floors;
    }

    /// <summary>
    /// Calls <paramref name="hit"/> for every cell whose middle lies inside the triangle seen
    /// from above.
    /// </summary>
    private static void Covering(NavGrid grid, Vector3 a, Vector3 b, Vector3 c, Action<long, long, double, double> hit)
    {
        var first = Math.Max(0, Doubles.Floor((Math.Min(a.X, Math.Min(b.X, c.X)) - grid.OriginX) / grid.Cell));
        var last = Math.Min(grid.Width - 1, Doubles.Floor((Math.Max(a.X, Math.Max(b.X, c.X)) - grid.OriginX) / grid.Cell));
        var top = Math.Max(0, Doubles.Floor((Math.Min(a.Z, Math.Min(b.Z, c.Z)) - grid.OriginZ) / grid.Cell));
        var bottom = Math.Min(grid.Depth - 1, Doubles.Floor((Math.Max(a.Z, Math.Max(b.Z, c.Z)) - grid.OriginZ) / grid.Cell));
        for (var j = top; j <= bottom; j++)
        {
            for (var i = first; i <= last; i++)
            {
                var x = grid.OriginX + ((i + 0.5) * grid.Cell);
                var z = grid.OriginZ + ((j + 0.5) * grid.Cell);
                if (InTriangle(a, b, c, x, z))
                {
                    hit(i, j, x, z);
                }
            }
        }
    }

    private static bool InTriangle(Vector3 a, Vector3 b, Vector3 c, double x, double z)
    {
        double Side(Vector3 p, Vector3 q) => ((q.X - p.X) * (z - p.Z)) - ((q.Z - p.Z) * (x - p.X));
        var d1 = Side(a, b);
        var d2 = Side(b, c);
        var d3 = Side(c, a);
        var negative = d1 < 0 || d2 < 0 || d3 < 0;
        var positive = d1 > 0 || d2 > 0 || d3 > 0;
        return !(negative && positive);
    }

    private static double PlaneHeight(Vector3 a, Vector3 normal, double x, double z) =>
        a.Y - (((normal.X * (x - a.X)) + (normal.Z * (z - a.Z))) / normal.Y);

    /// <summary>
    /// Calls <paramref name="hit"/> with the cell and height of samples spread over every
    /// triangle that is solid for walking, closely enough that no cell under
    /// one is skipped.
    /// </summary>
    private static void Solids(NavGrid grid, MeshWalk mesh, Action<long, long, double> hit)
    {
        var steepest = Math.Cos(mesh.MaxSlope);
        var step = grid.Cell / 3;
        foreach (var (a, b, c, normal) in Triangles(mesh))
        {
            if (normal.Length2 < 1e-18 || normal.Normalized().Y >= steepest)
            {
                continue;
            }

            var reach = Math.Max(a.DistanceTo(b), Math.Max(b.DistanceTo(c), c.DistanceTo(a)));
            Spread(grid, (a, b, c), Math.Max(1, Doubles.Ceil(reach / step)), hit);
        }
    }

    /// <summary>Samples the triangle on an <paramref name="n"/> by <paramref name="n"/> lattice, corners and edges included.</summary>
    private static void Spread(NavGrid grid, (Vector3 A, Vector3 B, Vector3 C) triangle, long n, Action<long, long, double> hit)
    {
        var (a, b, c) = triangle;
        for (long u = 0; u <= n; u++)
        {
            for (long v = 0; v <= n - u; v++)
            {
                var p = a + ((b - a) * ((double)u / n)) + ((c - a) * ((double)v / n));
                if (grid.CellAt(p.X, p.Z) is { } cell)
                {
                    hit(cell.I, cell.J, p.Y);
                }
            }
        }
    }
}
