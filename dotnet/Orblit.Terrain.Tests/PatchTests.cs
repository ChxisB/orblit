namespace Orblit.Terrain.Tests;

public class PatchTests
{
    private static Terrain Ground()
    {
        var terrain = new Terrain(regionSize: 128);
        terrain.AddRegion(new RegionKey(0, 0));
        terrain.AddRegion(new RegionKey(1, 0));
        return terrain;
    }

    private static TerrainStroke Raise(Terrain terrain) =>
        new(terrain, BrushTool.Raise, new Brush(size: 12));

    [Fact]
    public void AStrokeOfManyMovesUndoesAndRedoesAsOne()
    {
        var terrain = Ground();
        var stroke = Raise(terrain);
        var patch = stroke.MoveTo(20, 20);
        for (var x = 22.0; x <= 100; x += 2)
        {
            patch = patch.FollowedBy(stroke.MoveTo(x, 20));
        }

        var after = terrain.RegionAt(new RegionKey(0, 0))!.Heights.ToList();
        Assert.Contains(after, height => height > 0);

        patch.Revert(terrain);
        Assert.All(terrain.RegionAt(new RegionKey(0, 0))!.Heights, height => Assert.Equal(0, height));
        patch.Apply(terrain);
        Assert.Equal(after, terrain.RegionAt(new RegionKey(0, 0))!.Heights);
    }

    [Fact]
    public void CostsKilobytesForASmallBrushNotTheRegion()
    {
        var terrain = new Terrain(regionSize: 1024);
        terrain.AddRegion(new RegionKey(0, 0));
        var patch = Raise(terrain).MoveTo(300, 300);

        // One 32² tile of heights before and after, or four if it straddles.
        Assert.True(patch.ByteCount <= 4 * 2 * 32 * 32 * 4);
        Assert.True(patch.Layers.SetEquals([TerrainLayer.Height]));
    }

    [Fact]
    public void KeepsTheEarliestBeforeAndTheLatestAfter()
    {
        var terrain = Ground();
        var region = terrain.RegionAt(new RegionKey(0, 0))!;
        var first = Raise(terrain).MoveTo(20, 20);
        var middle = region.HeightAt(20, 20);
        var second = Raise(terrain).MoveTo(20, 20);
        var both = first.FollowedBy(second);
        both.Revert(terrain);
        Assert.Equal(0, region.HeightAt(20, 20));
        both.Apply(terrain);
        Assert.Equal(2 * middle, region.HeightAt(20, 20));
    }

    [Fact]
    public void PutsBackMapsOfTwoToolsThatTouchedOneTile()
    {
        var terrain = Ground();
        var region = terrain.RegionAt(new RegionKey(0, 0))!;
        var shaped = Raise(terrain).MoveTo(20, 20);
        var painted = new TerrainStroke(terrain, BrushTool.Hole, new Brush(size: 4)).MoveTo(20, 20);
        var both = shaped.FollowedBy(painted);
        Assert.True(both.Layers.SetEquals([TerrainLayer.Height, TerrainLayer.Cover]));
        both.Revert(terrain);
        Assert.Equal(0, region.HeightAt(20, 20));
        Assert.False(region.CoverAt(20, 20).Hole);
    }

    [Fact]
    public void SaysWhichRegionsItChangedAndTouchesThem()
    {
        var terrain = Ground();
        var patch = Raise(terrain).MoveTo(128, 20);
        Assert.True(patch.Regions.SetEquals([new RegionKey(0, 0), new RegionKey(1, 0)]));
        var was = terrain.RegionAt(new RegionKey(1, 0))!.Revision;
        Assert.True(patch.Revert(terrain).SetEquals(patch.Regions));
        Assert.NotEqual(was, terrain.RegionAt(new RegionKey(1, 0))!.Revision);
    }

    [Fact]
    public void SkipsARegionThatHasGoneSince()
    {
        var terrain = Ground();
        var patch = Raise(terrain).MoveTo(128, 20);
        terrain.RemoveRegion(new RegionKey(1, 0));
        Assert.True(patch.Revert(terrain).SetEquals([new RegionKey(0, 0)]));
        Assert.Null(terrain.RegionAt(new RegionKey(1, 0)));
    }

    [Fact]
    public void RefusesATerrainWithOtherRegions()
    {
        var patch = Raise(Ground()).MoveTo(20, 20);
        Assert.ThrowsAny<ArgumentException>(() => patch.Apply(new Terrain(regionSize: 64)));
    }

    [Fact]
    public void TheEmptyPatchChangesNothingAndFoldsAway()
    {
        var terrain = Ground();
        var patch = Raise(terrain).MoveTo(20, 20);
        var empty = TerrainPatch.Empty(regionSize: 128);
        Assert.True(empty.IsEmpty);
        Assert.Same(patch, empty.FollowedBy(patch));
        Assert.Same(patch, patch.FollowedBy(empty));
        Assert.Empty(empty.Apply(terrain));
    }

    [Fact]
    public void ARecorderKeepsATileOnceHoweverOftenItIsAsked()
    {
        var terrain = Ground();
        var recorder = new TerrainRecorder(terrain, [TerrainLayer.Height]);
        recorder.Keep(0, 0, 10, 10);
        recorder.Keep(5, 5, 20, 20);
        terrain.RegionAt(new RegionKey(0, 0))!.SetHeight(3, 3, 9);
        recorder.Keep(0, 0, 4, 4);
        var patch = recorder.Finish();
        Assert.Equal(1, patch.TileCount);
        patch.Revert(terrain);
        Assert.Equal(0, terrain.TexelHeight(3, 3));
    }
}
