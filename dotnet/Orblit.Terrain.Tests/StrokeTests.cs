using Orblit.VectorMath;

namespace Orblit.Terrain.Tests;

public class StrokeTests
{
    /// <summary>Four regions of 64 texels at 1 m, from (0, 0) to (128, 128).</summary>
    private static Terrain Ground()
    {
        var terrain = new Terrain(regionSize: 64);
        foreach (var key in new[] { new RegionKey(0, 0), new RegionKey(1, 0), new RegionKey(0, 1), new RegionKey(1, 1) })
        {
            terrain.AddRegion(key);
        }

        return terrain;
    }

    private static double At(Terrain terrain, int i, int j) => terrain.TexelHeight(i, j)!.Value;

    private static void Near(double expected, double actual, double tolerance) =>
        Assert.True(Math.Abs(expected - actual) <= tolerance, $"expected {expected:R} within {tolerance}, got {actual:R}");

    // a brush

    [Fact]
    public void WeighsItsWholeDiscAtNoFalloffAndNothingPastItsEdge()
    {
        var brush = new Brush(size: 10, falloff: 0);
        Assert.Equal(1, brush.WeightAt(0));
        Assert.Equal(1, brush.WeightAt(4.99));
        Assert.Equal(0, brush.WeightAt(5));
    }

    [Fact]
    public void FadesFromTheCentreAtFullFalloff()
    {
        var brush = new Brush(size: 10, falloff: 1);
        Assert.Equal(1, brush.WeightAt(0));
        Near(0.5, brush.WeightAt(2.5), 1e-9);
        Assert.True(brush.WeightAt(4.9) < 0.01);
    }

    // raise and lower

    [Fact]
    public void LiftTheGroundUnderTheBrushAndLeaveTheRest()
    {
        var terrain = Ground();
        new TerrainStroke(terrain, BrushTool.Raise, new Brush(size: 8, falloff: 0)).MoveTo(20, 20);
        Assert.True(At(terrain, 20, 20) > 0);
        Assert.Equal(At(terrain, 20, 20), At(terrain, 23, 20));
        Assert.Equal(0, At(terrain, 25, 20));
    }

    [Fact]
    public void InvertTurnsOneIntoTheOther()
    {
        var raised = Ground();
        var lowered = Ground();
        var brush = new Brush(size: 8);
        new TerrainStroke(raised, BrushTool.Raise, brush).MoveTo(20, 20);
        new TerrainStroke(lowered, BrushTool.Lower, brush, invert: true).MoveTo(20, 20);
        Assert.Equal(At(raised, 20, 20), At(lowered, 20, 20));
        new TerrainStroke(lowered, BrushTool.Lower, brush).MoveTo(20, 20);
        Assert.Equal(0, At(lowered, 20, 20));
    }

    [Fact]
    public void RiseByTheSameAmountAPassAtAnySpacing()
    {
        static double Pass(double spacing)
        {
            var terrain = Ground();
            var stroke = new TerrainStroke(terrain, BrushTool.Raise, new Brush(size: 16, falloff: 0, spacing: spacing));
            stroke.MoveTo(10, 40);
            stroke.MoveTo(70, 40);
            return At(terrain, 40, 40);
        }

        Near(Pass(0.25), Pass(0.1), 0.25 * Pass(0.25));
    }

    [Fact]
    public void CrossFromOneRegionIntoTheNextWithoutASeam()
    {
        var terrain = Ground();
        new TerrainStroke(terrain, BrushTool.Raise, new Brush(size: 10, falloff: 0)).MoveTo(64, 20);
        Assert.Equal(At(terrain, 64, 20), At(terrain, 63, 20));
        Assert.True(At(terrain, 64, 20) > 0);
    }

    [Fact]
    public void LaysDabsASpacingApartAlongTheWay()
    {
        var stroke = new TerrainStroke(Ground(), BrushTool.Raise, new Brush(size: 8, spacing: 0.5));
        stroke.MoveTo(10, 10);
        Assert.Equal(1, stroke.Dabs);
        stroke.MoveTo(13, 10);
        Assert.True(stroke.Dabs == 1, "three metres is short of the four");
        stroke.MoveTo(30, 10);
        Assert.True(stroke.Dabs == 6, "at 14, 18, 22, 26 and 30");
    }

    [Fact]
    public void ScattersTheSameWayTwiceFromTheSameSeed()
    {
        static double[] Run(int seed)
        {
            var terrain = Ground();
            var stroke = new TerrainStroke(terrain, BrushTool.Raise, new Brush(size: 6, jitter: 1), seed: seed);
            stroke.MoveTo(10, 30);
            stroke.MoveTo(50, 30);
            return [.. Enumerable.Range(0, 64).Select(i => At(terrain, i, 30))];
        }

        Assert.Equal(Run(7), Run(7));
        Assert.NotEqual(Run(7), Run(8));
    }

    [Fact]
    public void SmoothTakesTheEdgeOffAStep()
    {
        var terrain = Ground();
        var region = terrain.RegionAt(new RegionKey(0, 0))!;
        for (var j = 0; j < 64; j++)
        {
            for (var i = 20; i < 64; i++)
            {
                region.Heights[(j * 64) + i] = 4;
            }
        }

        new TerrainStroke(terrain, BrushTool.Smooth, new Brush(size: 8, strength: 1)).MoveTo(20, 20);
        Assert.True(At(terrain, 19, 20) > 0);
        Assert.True(At(terrain, 20, 20) < 4);
        Assert.Equal(0, At(terrain, 10, 20));
    }

    [Fact]
    public void FlattenDrawsTheGroundToWhereTheStrokeBegan()
    {
        var terrain = Ground();
        terrain.FillHeights(new RegionKey(0, 0), (x, _) => x);
        var stroke = new TerrainStroke(terrain, BrushTool.Flatten, new Brush(size: 6, strength: 1, falloff: 0));
        stroke.MoveTo(20, 20);
        stroke.MoveTo(30, 20);
        Near(20, At(terrain, 28, 20), 0.5);
        Assert.Equal(40, At(terrain, 40, 20));
    }

    [Fact]
    public void SlopeLaysARampFromTheStartToTheBrush()
    {
        var terrain = Ground();
        var region = terrain.RegionAt(new RegionKey(0, 0))!;
        for (var j = 0; j < 64; j++)
        {
            for (var i = 40; i < 64; i++)
            {
                region.Heights[(j * 64) + i] = 10;
            }
        }

        var brush = new Brush(size: 6, strength: 1, falloff: 0, spacing: 0.1);
        var outward = new TerrainStroke(terrain, BrushTool.Slope, brush);
        outward.MoveTo(10, 20);
        outward.MoveTo(45, 20);
        Assert.True(At(terrain, 46, 20) == 10, "climbing on leaves the ledge");
        Assert.True(At(terrain, 27, 20) == 0, "the way out found flat ground");

        var back = new TerrainStroke(terrain, BrushTool.Slope, brush);
        back.MoveTo(10, 20);
        back.MoveTo(45, 20);
        back.MoveTo(10, 20);

        // Halfway along the ramp, halfway up it.
        Near(5, At(terrain, 27, 20), 0.5);
        Near(10.0 * 10 / 35, At(terrain, 20, 20), 0.5);
        Assert.True(At(terrain, 5, 20) == 0, "behind the start is left alone");
        Assert.True(At(terrain, 50, 20) == 10, "beyond the far end too");
    }

    // painting cover

    [Fact]
    public void StartsFromWhatAutomaticGroundShowed()
    {
        var terrain = new Terrain(
            regionSize: 64,
            sets: [new TerrainSet(name: "rock"), new TerrainSet(name: "grass"), new TerrainSet(name: "sand")]);
        terrain.AddRegion(new RegionKey(0, 0));
        new TerrainStroke(terrain, BrushTool.Cover, new Brush(size: 4, strength: 0.5, falloff: 0), set: 2).MoveTo(20, 20);
        var cover = terrain.TexelCover(20, 20)!.Value;
        Assert.False(cover.Automatic);

        // Level ground is all grass; grass stays under the sand.
        Assert.Equal(1, cover.Base);
        Assert.Equal(2, cover.Overlay);
        Near(0.5, cover.Blend, 0.01);
        Assert.True(terrain.TexelCover(30, 20)!.Value.Automatic);
    }

    [Fact]
    public void GetsAllTheWayToTheSetItPaints()
    {
        var terrain = Ground();
        var stroke = new TerrainStroke(terrain, BrushTool.Cover, new Brush(size: 4, strength: 0.2, falloff: 0), set: 3);
        for (var pass = 0; pass < 40; pass++)
        {
            stroke.MoveTo(20, 20);
            stroke.MoveTo(21, 20);
        }

        var cover = terrain.TexelCover(20, 20)!.Value;
        Assert.Equal(3, cover.Base);
        Assert.True(cover.Overlay == 3 || cover.Blend == 0);
    }

    [Fact]
    public void InvertHandsTheGroundBackToAutomaticCover()
    {
        var terrain = Ground();
        var region = terrain.RegionAt(new RegionKey(0, 0))!;
        region.SetCover(20, 20, Cover.Of(@base: 4));
        new TerrainStroke(terrain, BrushTool.Cover, new Brush(size: 4), invert: true).MoveTo(20, 20);
        Assert.True(region.CoverAt(20, 20).Automatic);
    }

    [Fact]
    public void HolesArePunchedAndFilledAndStopTheGroundBeingThere()
    {
        var terrain = Ground();
        var brush = new Brush(size: 4, falloff: 0);
        new TerrainStroke(terrain, BrushTool.Hole, brush).MoveTo(20, 20);
        Assert.True(terrain.TexelCover(20, 20)!.Value.Hole);
        Assert.Null(terrain.HeightAt(20, 20));
        new TerrainStroke(terrain, BrushTool.Hole, brush, invert: true).MoveTo(20, 20);
        Assert.False(terrain.TexelCover(20, 20)!.Value.Hole);
    }

    [Fact]
    public void ColourAndRoughnessPaintTheirOwnBytesAndNotEachOther()
    {
        var terrain = Ground();
        var brush = new Brush(size: 4, strength: 1, falloff: 0);
        new TerrainStroke(terrain, BrushTool.Colour, brush, colour: GroundColour.Of(red: 200, green: 100, blue: 0))
            .MoveTo(20, 20);
        var colour = terrain.TexelColour(20, 20)!.Value;
        Assert.Equal(200, colour.Red);
        Assert.Equal(0, colour.Blue);
        Assert.Equal(GroundColour.None.RoughnessByte, colour.RoughnessByte);

        new TerrainStroke(terrain, BrushTool.Roughness, brush, roughness: 1).MoveTo(20, 20);
        colour = terrain.TexelColour(20, 20)!.Value;
        Assert.Equal(1, colour.Roughness);
        Assert.Equal(200, colour.Red);
    }

    [Fact]
    public void LeavesGroundWithNoRegionAlone()
    {
        var terrain = new Terrain(regionSize: 64);
        terrain.AddRegion(new RegionKey(0, 0));
        var patch = new TerrainStroke(terrain, BrushTool.Raise, new Brush(size: 10)).MoveTo(64, 20);
        Assert.Null(terrain.RegionAt(new RegionKey(1, 0)));
        Assert.True(patch.Regions.SetEquals([new RegionKey(0, 0)]));
    }

    [Fact]
    public void TouchesTheRegionsItWritesAndOnlyThose()
    {
        var terrain = Ground();
        var revisions = terrain.Regions.ToDictionary(region => region.Key, region => region.Revision);
        new TerrainStroke(terrain, BrushTool.Raise, new Brush(size: 4)).MoveTo(20, 20);
        foreach (var region in terrain.Regions)
        {
            Assert.True(
                (region.Revision != revisions[region.Key]) == (region.Key == new RegionKey(0, 0)),
                $"{region.Key}");
        }
    }
}
