using Orblit.Terrain;
using Orblit.VectorMath;

namespace Orblit.Pathing.Tests;

public class BakeTests
{
    private readonly record struct Part(double[] Positions, long[] Indices);

    // A terrain of one 16 texel region at key, one metre a texel, its ground
    // from height and every texel marked walkable by mark.
    private static Terrain.Terrain Ground(
        RegionKey? key = null,
        Func<double, double, double>? height = null,
        Func<int, int, Cover>? mark = null)
    {
        var terrain = new Terrain.Terrain(regionSize: 16);
        var region = terrain.FillHeights(key ?? new RegionKey(0, 0), height ?? ((_, _) => 0));
        for (var j = 0; j < 16; j++)
        {
            for (var i = 0; i < 16; i++)
            {
                region.Cover[(j * 16) + i] = (mark?.Invoke(i, j) ?? Cover.Of(navigation: true)).Word;
            }
        }

        return terrain;
    }

    // A floor of two triangles from (0, 0) to (size, size) at height y.
    private static Part Floor(double size, double y) => new(
        [0, y, 0, 0, y, size, size, y, size, size, y, 0],
        [0, 1, 3, 1, 2, 3]);

    // A vertical wall along z from x 0 to width, height tall.
    private static Part Wall(double z, double width, double height) => new(
        [0, 0, z, width, 0, z, width, height, z, 0, height, z],
        [0, 1, 2, 0, 2, 3]);

    private static MeshWalk MeshOf(params Part[] parts)
    {
        var positions = new List<double>();
        var indices = new List<long>();
        foreach (var part in parts)
        {
            var baseIndex = positions.Count / 3;
            positions.AddRange(part.Positions);
            indices.AddRange(part.Indices.Select(i => i + baseIndex));
        }

        return new MeshWalk(positions, indices, AgentHeight: 1.8, StepHeight: 0.3, MaxSlope: Math.PI / 4);
    }

    // a terrain

    [Fact]
    public void IsOpenWhereTheCoverIsMarkedForWalking()
    {
        var grid = NavBake.FromTerrain(Ground());
        Assert.Equal(16, grid.Width);
        Assert.Equal(16, grid.Depth);
        Assert.True(grid.IsOpen(5, 5));
        Assert.Equal(0, grid.HeightOf(5, 5), 1e-6);
    }

    [Fact]
    public void IsClosedWhereNobodyMarkedIt()
    {
        var terrain = Ground(mark: (i, _) => Cover.Of(navigation: i < 8));
        var grid = NavBake.FromTerrain(terrain);
        Assert.True(grid.IsOpen(3, 5));
        Assert.False(grid.IsOpen(12, 5));
    }

    [Fact]
    public void CanBeToldToWalkAnywhere()
    {
        var terrain = Ground(mark: (_, _) => Cover.Plain);
        var grid = NavBake.FromTerrain(terrain, walkable: _ => true);
        Assert.True(grid.IsOpen(5, 5));
    }

    [Fact]
    public void IsClosedAtAHole()
    {
        var terrain = Ground(mark: (i, j) => Cover.Of(navigation: true, hole: i == 7 && j == 7));
        var grid = NavBake.FromTerrain(terrain);
        Assert.False(grid.IsOpen(7, 7));
        Assert.True(grid.IsOpen(3, 3));
    }

    [Fact]
    public void IsClosedWhereItIsTooSteepAndDearerWhereItClimbs()
    {
        var steep = NavBake.FromTerrain(Ground(height: (x, _) => x * 2));
        Assert.False(steep.IsOpen(5, 5));
        var gentle = NavBake.FromTerrain(Ground(height: (x, _) => x * 0.5));
        Assert.True(gentle.IsOpen(5, 5));
        Assert.Equal(Math.Sqrt(1.25), gentle.WeightOf(5, 5), 1e-3);
        Assert.Equal(2.75, gentle.HeightOf(5, 5), 1e-3);
    }

    [Fact]
    public void LiesWhereTheRegionsAre()
    {
        var grid = NavBake.FromTerrain(Ground(key: new RegionKey(2, -1)));
        Assert.Equal(32, grid.OriginX);
        Assert.Equal(-16, grid.OriginZ);
        Assert.True(grid.IsOpen(5, 5));
    }

    [Fact]
    public void TakesASmallerAreaAndALargerCell()
    {
        var grid = NavBake.FromTerrain(Ground(), cell: 2, minX: 4, minZ: 4, maxX: 12, maxZ: 12);
        Assert.Equal(4, grid.Width);
        Assert.Equal(4, grid.Depth);
        Assert.Equal(4, grid.OriginX);
    }

    [Fact]
    public void ATerrainWithNoRegionsIsOneClosedCell()
    {
        var grid = NavBake.FromTerrain(new Terrain.Terrain(regionSize: 16));
        Assert.Equal(1, grid.Width);
        Assert.False(grid.IsOpen(0, 0));
    }

    [Fact]
    public void CanBeWalkedAcrossAfterItIsBaked()
    {
        var terrain = Ground(mark: (i, j) => Cover.Of(navigation: true, hole: i == 8 && j < 12));
        var grid = NavBake.FromTerrain(terrain);
        var path = grid.FindPath(new Vector3(3.5, 0, 3.5), new Vector3(12.5, 0, 3.5))!;
        Assert.True(path.Max(p => p.Z) > 11);
    }

    // a mesh

    [Fact]
    public void IsOpenOverAFloorAtItsHeight()
    {
        var grid = NavBake.FromMesh(MeshOf(Floor(10, 1.5)), cell: 1);
        Assert.Equal(10, grid.Width);
        Assert.True(grid.IsOpen(4, 4));
        Assert.Equal(1.5, grid.HeightOf(4, 4), 1e-9);
    }

    [Fact]
    public void IsClosedAtAWallAndOpenEitherSide()
    {
        var grid = NavBake.FromMesh(MeshOf(Floor(10, 0), Wall(5, 10, 3)), cell: 1);
        Assert.True(grid.IsOpen(4, 3));
        Assert.True(grid.IsOpen(4, 7));
        Assert.False(grid.IsOpen(4, 5));
    }

    [Fact]
    public void WalksOverAKerb()
    {
        var grid = NavBake.FromMesh(MeshOf(Floor(10, 0), Wall(5, 10, 0.2)), cell: 1);
        Assert.True(grid.IsOpen(4, 5));
    }

    [Fact]
    public void IsClosedUnderALowCeilingAndOpenUnderAHighOne()
    {
        // Facing down: wound the other way round to a floor.
        var low = new Part([0, 1.2, 0, 4, 1.2, 0, 4, 1.2, 10, 0, 1.2, 10], [0, 1, 2, 0, 2, 3]);
        var high = new Part([6, 2.5, 0, 10, 2.5, 0, 10, 2.5, 10, 6, 2.5, 10], [0, 1, 2, 0, 2, 3]);
        var grid = NavBake.FromMesh(MeshOf(Floor(10, 0), low, high), cell: 1);
        Assert.False(grid.IsOpen(2, 5));
        Assert.True(grid.IsOpen(8, 5));
    }

    [Fact]
    public void IsClosedBesideARampThatIsTooSteepToClimb()
    {
        var ramp = new Part([0, 0, 0, 0, 0, 10, 4, 8, 10, 4, 8, 0], [0, 1, 2, 0, 2, 3]);
        var grid = NavBake.FromMesh(MeshOf(Floor(10, 0), ramp), cell: 1);
        Assert.False(grid.IsOpen(0, 5), "under it, head height");
        Assert.True(grid.IsOpen(7, 5));
    }

    [Fact]
    public void IsOpenOnARampThatIsGentleAtTheHeightOfIt()
    {
        var ramp = new Part([0, 0, 0, 0, 0, 10, 10, 2, 10, 10, 2, 0], [0, 1, 2, 0, 2, 3]);
        var grid = NavBake.FromMesh(MeshOf(ramp), cell: 1);
        Assert.True(grid.IsOpen(5, 5));
        Assert.Equal(1.1, grid.HeightOf(5, 5), 1e-6);
    }

    [Fact]
    public void CarvesAWallOutOfATerrainGrid()
    {
        var grid = NavBake.FromTerrain(Ground());
        NavBake.Carve(grid, MeshOf(Wall(8, 16, 3)));
        Assert.False(grid.IsOpen(3, 8));
        Assert.True(grid.IsOpen(3, 3));
    }

    [Fact]
    public void LeavesAFloorThatIsOnlySeenFromAbove()
    {
        var grid = NavBake.FromTerrain(Ground());
        NavBake.Carve(grid, MeshOf(Floor(16, 0)));
        Assert.True(grid.IsOpen(3, 3));
    }
}
