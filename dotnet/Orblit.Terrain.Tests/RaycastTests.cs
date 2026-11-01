using Orblit.VectorMath;

namespace Orblit.Terrain.Tests;

public class RaycastTests
{
    private static void Close(double expected, double actual, double tolerance) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"expected {expected:R} within {tolerance}, got {actual:R}");

    [Fact]
    public void MeetsFlatGroundStraightDown()
    {
        var terrain = new Terrain(regionSize: 64);
        terrain.AddRegion(new RegionKey(0, 0));
        var hit = terrain.Raycast(new Vector3(20, 50, 30), new Vector3(0, -1, 0))!;
        Close(20, hit.X, 1e-9);
        Close(0, hit.Y, 1e-6);
        Close(30, hit.Z, 1e-9);
    }

    [Fact]
    public void MeetsASlopeWhereHeightAtSaysItIsAtAnAngle()
    {
        var terrain = new Terrain(regionSize: 64);
        terrain.FillHeights(new RegionKey(0, 0), (x, _) => x * 0.5);
        var hit = terrain.Raycast(new Vector3(0, 40, 10), new Vector3(1, -1, 0.2))!;
        Close(terrain.HeightAt(hit.X, hit.Z)!.Value, hit.Y, 1e-6);

        // Along the ray: y = 40 - t, x = t, and the ground is x / 2.
        Close(80.0 / 3, hit.X, 1e-3);
    }

    [Fact]
    public void StopsAtTheFirstGroundNotTheGroundBehindIt()
    {
        var terrain = new Terrain(regionSize: 64);
        terrain.FillHeights(new RegionKey(0, 0), (x, _) => x >= 30 && x < 34 ? 20 : 0);
        var hit = terrain.Raycast(new Vector3(0, 10, 5), new Vector3(1, 0, 0))!;
        Assert.InRange(hit.X, 29, 30.5);
    }

    [Fact]
    public void MissesThroughAHoleOffTheEdgeAndIntoTheSky()
    {
        var terrain = new Terrain(regionSize: 64);
        terrain.AddRegion(new RegionKey(0, 0));
        var region = terrain.RegionAt(new RegionKey(0, 0))!;
        region.SetCover(20, 20, Cover.Auto.WithHole(true));
        region.SetCover(21, 20, Cover.Auto.WithHole(true));
        region.SetCover(20, 21, Cover.Auto.WithHole(true));
        region.SetCover(21, 21, Cover.Auto.WithHole(true));
        Assert.Null(terrain.Raycast(new Vector3(20.5, 10, 20.5), new Vector3(0, -1, 0)));
        Assert.Null(terrain.Raycast(new Vector3(100, 10, 10), new Vector3(0, -1, 0)));
        Assert.Null(terrain.Raycast(new Vector3(10, 10, 10), new Vector3(0, 1, 0)));
        Assert.Null(terrain.Raycast(new Vector3(10, 10, 10), new Vector3(0, -1, 0), maxDistance: 5));
    }

    [Fact]
    public void MeetsNothingOnATerrainWithNoRegions() =>
        Assert.Null(new Terrain().Raycast(new Vector3(0, 10, 0), new Vector3(0, -1, 0)));
}
