using Orblit.VectorMath;

namespace Orblit.Terrain.Tests;

public class TerrainTests
{
    private static Terrain Small(double spacing = 1) => new(regionSize: 4, spacing: spacing);

    private static void Close(double expected, double? actual, double tolerance) =>
        Assert.True(
            actual is { } value && Math.Abs(expected - value) <= tolerance,
            $"expected {expected:R} within {tolerance}, got {actual:R}");

    // heightAt

    [Fact]
    public void IsTheTexelHeightOnATexel()
    {
        var terrain = Small();
        terrain.AddRegion(new RegionKey(0, 0));
        terrain.RegionAt(new RegionKey(0, 0))!.SetHeight(2, 1, 3.5);
        Assert.Equal(3.5, terrain.HeightAt(2, 1));
        Assert.Equal(0, terrain.HeightAt(1, 1));
    }

    [Fact]
    public void FollowsTheTwoTrianglesSplitLowCornerToHigh()
    {
        var terrain = Small();
        var region = terrain.AddRegion(new RegionKey(0, 0));
        region.SetHeight(1, 1, 1);

        // Only the high corner is raised. Bilinear would give 0.125 at both;
        // on the triangles each point is a quarter of the way up.
        Close(0.25, terrain.HeightAt(0.5, 0.25), 1e-9);
        Close(0.25, terrain.HeightAt(0.25, 0.5), 1e-9);
        region.SetHeight(1, 1, 0);
        region.SetHeight(1, 0, 1);

        // Only the +x corner is raised: the triangle that holds it slopes,
        // the other is flat.
        Close(0.5, terrain.HeightAt(0.75, 0.25), 1e-9);
        Close(0, terrain.HeightAt(0.25, 0.75), 1e-9);
    }

    [Fact]
    public void ReadsAcrossARegionEdge()
    {
        var terrain = Small();
        terrain.FillHeights(new RegionKey(0, 0), (x, _) => x);
        terrain.FillHeights(new RegionKey(1, 0), (x, _) => x);
        Close(3.5, terrain.HeightAt(3.5, 2), 1e-9);
        Close(4, terrain.HeightAt(4, 2), 1e-9);
    }

    [Fact]
    public void WorksBelowZero()
    {
        var terrain = Small();
        terrain.FillHeights(new RegionKey(-1, -1), (x, z) => x + (2 * z));
        Close(-6, terrain.HeightAt(-1.5, -2.25), 1e-9);
    }

    [Fact]
    public void ScalesWithSpacing()
    {
        var terrain = Small(spacing: 2);
        terrain.FillHeights(new RegionKey(0, 0), (x, _) => x * 10);
        Close(20, terrain.HeightAt(2, 0), 1e-9);
        Close(30, terrain.HeightAt(3, 0), 1e-9);
    }

    [Fact]
    public void IsNullWhereThereIsNoRegion()
    {
        var terrain = Small();
        terrain.AddRegion(new RegionKey(0, 0));
        Assert.Equal(0, terrain.HeightAt(1, 1));
        Assert.Null(terrain.HeightAt(-0.5, 1));
        Assert.Null(terrain.HeightAt(3.5, 1));
        Assert.Null(terrain.HeightAt(double.NaN, 0));
    }

    [Fact]
    public void IsNullOnATriangleTouchingAHoleNotItsNeighbour()
    {
        var terrain = Small();
        terrain.AddRegion(new RegionKey(0, 0)).SetCover(1, 0, Cover.Plain.WithHole(true));
        Assert.Null(terrain.HeightAt(0.75, 0.25));
        Assert.Equal(0, terrain.HeightAt(0.25, 0.75));
    }

    // normalAt

    [Fact]
    public void PointsUpOnLevelGround()
    {
        var terrain = Small();
        terrain.AddRegion(new RegionKey(0, 0));
        var normal = terrain.NormalAt(1.3, 2.6)!;
        Close(0, normal.X, 1e-9);
        Close(1, normal.Y, 1e-9);
        Close(0, normal.Z, 1e-9);
    }

    [Fact]
    public void LeansAwayFromTheRiseOnAPlane()
    {
        var terrain = new Terrain(regionSize: 8, spacing: 2);
        terrain.FillHeights(new RegionKey(0, 0), (x, z) => (0.5 * x) - (0.25 * z));
        var normal = terrain.NormalAt(5.2, 7.9)!;
        var expected = new Vector3(-0.5, 1, 0.25);
        expected.Normalize();
        Close(expected.X, normal.X, 1e-6);
        Close(expected.Y, normal.Y, 1e-6);
        Close(expected.Z, normal.Z, 1e-6);
    }

    [Fact]
    public void IgnoresHolesAndIsNullWhereThereIsNothing()
    {
        var terrain = Small();
        terrain.AddRegion(new RegionKey(0, 0)).SetCover(1, 1, Cover.Plain.WithHole(true));
        Assert.NotNull(terrain.NormalAt(1, 1));
        Assert.Null(terrain.NormalAt(-3, -3));
    }

    // cover

    [Fact]
    public void CoverAtReadsTheNearestTexelAsStored()
    {
        var terrain = Small();
        terrain.AddRegion(new RegionKey(0, 0)).SetCover(2, 1, Cover.Plain);
        Assert.Equal(Cover.Plain, terrain.CoverAt(1.6, 0.6));
        Assert.Equal(Cover.Auto, terrain.CoverAt(0.2, 0.2));
        Assert.Null(terrain.CoverAt(-5, 0));
    }

    [Fact]
    public void AutomaticGroundTurnsSteepWithSlopeAndHeight()
    {
        var auto = new AutoCover();
        Assert.Equal(1, auto.Flatness(1, 0));
        Assert.Equal(0, auto.Flatness(0.5, 0));
        Assert.Equal(0, auto.Flatness(1, 1000));
        Close(0.732, auto.Flatness(Math.Cos(Math.PI / 6), 0), 1e-3);
        var cover = auto.CoverFor(0.75, 0);
        Assert.Equal((0, 1), (cover.Base, cover.Overlay));
        Close(0.5, cover.Blend, 1.0 / 255);
    }

    // settings files

    private static Terrain Sample()
    {
        var terrain = new Terrain(
            regionSize: 64,
            spacing: 0.5,
            sets:
            [
                new TerrainSet("grass", albedo: "terrain/grass_albedo.png", normal: "terrain/grass_normal.png"),
                new TerrainSet("rock", tileSize: 8, triplanar: true),
            ],
            autoCover: new AutoCover(steep: 1, flat: 0, slope: 2),
            blendSharpness: 0.5);
        terrain.AddRegion(new RegionKey(1, 0));
        terrain.AddRegion(new RegionKey(-1, 0));
        terrain.AddRegion(new RegionKey(0, -1));
        return terrain;
    }

    [Fact]
    public void ComeBackAsTheyWentRegionsAsKeys()
    {
        var load = Terrain.Decode(Sample().Encode());
        Assert.Empty(load.Problems);
        var terrain = load.Terrain;
        Assert.Equal(64, terrain.RegionSize);
        Assert.Equal(0.5, terrain.Spacing);
        Assert.Equal(0.5, terrain.BlendSharpness);
        Assert.Equal(["grass", "rock"], terrain.Sets.Select(set => set.Name));
        Assert.Equal("terrain/grass_albedo.png", terrain.Sets[0].Albedo);
        Assert.True(terrain.Sets[1].Triplanar);
        Assert.Equal(8, terrain.Sets[1].TileSize);
        Assert.Equal(1, terrain.AutoCover.Steep);
        Assert.Equal(2, terrain.AutoCover.Slope);
        Assert.Empty(terrain.Regions);
        Assert.Equal([new RegionKey(0, -1), new RegionKey(-1, 0), new RegionKey(1, 0)], load.Regions);
    }

    [Fact]
    public void PutAKeyToALineAndASetToALine()
    {
        var lines = Sample().Encode().Split('\n');
        Assert.Equal("{", lines[0]);
        Assert.Contains("  \"kind\": \"orblit.terrain\",", lines);
        Assert.Contains("  \"regionSize\": 64,", lines);
        Assert.Equal(2, lines.Count(line => line.StartsWith("    {\"name\":", StringComparison.Ordinal)));
    }

    [Fact]
    public void RefuseWhatIsNotATerrainOrOneFromANewerOrblit()
    {
        Assert.Throws<TerrainFormatException>(() => Terrain.Decode("nope"));
        Assert.Throws<TerrainFormatException>(() =>
            Terrain.Decode("{\"kind\":\"orblit.terrain\",\"formatVersion\":2,\"regionSize\":64}"));
        Assert.Throws<TerrainFormatException>(() =>
            Terrain.Decode("{\"kind\":\"orblit.terrain\",\"regionSize\":100}"));
    }

    [Fact]
    public void DropWhatTheyCannotReadWithANote()
    {
        var load = Terrain.Decode(
            "{\"kind\":\"orblit.terrain\",\"formatVersion\":1,\"regionSize\":64," +
            "\"spacing\":-1,\"sets\":[{\"tileSize\":2},{\"name\":\"mud\",\"tileSize\":0}]," +
            "\"regions\":[[0,0],[0,0],[\"a\"]]}");
        Assert.Equal(1, load.Terrain.Spacing);
        Assert.Equal("mud", Assert.Single(load.Terrain.Sets).Name);
        Assert.Equal(4, load.Terrain.Sets[0].TileSize);
        Assert.Equal([new RegionKey(0, 0)], load.Regions);
        Assert.Equal(5, load.Problems.Count);
    }

    [Fact]
    public void KeepsItsOwnListOfSetsWhichCanBeChanged()
    {
        TerrainSet[] given = [new TerrainSet("grass")];
        var terrain = new Terrain(regionSize: 16, sets: given);
        terrain.Sets.Add(new TerrainSet("rock"));
        terrain.Sets[0] = new TerrainSet("moss");
        Assert.Equal(["moss", "rock"], terrain.Sets.Select(set => set.Name));
        Assert.Equal("grass", Assert.Single(given).Name);
    }

    [Fact]
    public void ARegionOfTheWrongSizeIsRefused() =>
        Assert.ThrowsAny<ArgumentException>(() => Small().PutRegion(new TerrainRegion(new RegionKey(0, 0), 8)));
}
