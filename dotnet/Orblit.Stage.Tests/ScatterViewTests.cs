using Orblit.Terrain;
using Ground = Orblit.Terrain.Terrain;

namespace Orblit.Stage.Tests;

public class ScatterViewTests
{
    private static readonly ScatterLayer Grass = new(
        "grass", density: 2, size: (0.05, 0.4, 0.05), colour: 0x55AA33, range: 40);

    private static readonly ScatterLayer Trees = new(
        "trees", seed: 3, density: 0.05, mesh: "models/tree.glb", material: "materials/bark.omat", castShadows: true);

    private static (Ground Terrain, ScatterPlacer Placer) Placed(params ScatterLayer[] scatter)
    {
        var terrain = new Ground(regionSize: 16, scatter: scatter);
        terrain.AddRegion(new RegionKey(0, 0));
        terrain.AddRegion(new RegionKey(1, 0));
        var placer = new ScatterPlacer();
        placer.Update(terrain);
        return (terrain, placer);
    }

    [Fact]
    public void ABlockLayerIsAPopulationARegionSharingItsBuffers()
    {
        var (_, placer) = Placed(Grass);

        var drawn = ScatterView.From(placer, 500);

        Assert.Empty(drawn.Objects);
        Assert.Equal(2, drawn.Populations.Count);
        for (var i = 0; i < drawn.Populations.Count; i++)
        {
            var population = drawn.Populations[i];
            var group = placer.Groups[i];
            Assert.Equal(500 + group.Id, population.Key);
            Assert.Same(group.Transforms, population.Transforms);
            Assert.Same(group.Colours, population.Colours);
            Assert.Equal(group.Minimum, population.Bounds.Minimum);
            Assert.Equal(group.Maximum, population.Bounds.Maximum);
            Assert.Equal((int)group.Revision, population.Revision);
            Assert.Equal(40, population.Range);
            Assert.False(population.CastShadows);
            Assert.Null(population.Mesh);
        }

        Assert.Equal(2, drawn.Populations.Select(population => population.Key).Distinct().Count());
    }

    [Fact]
    public void ARangeScaleShortensALayerRangeAndLeavesTheBuffers()
    {
        var bare = new ScatterLayer("moss", seed: 9, density: 2);
        var (_, placer) = Placed(Grass, bare);

        var full = ScatterView.From(placer, 0).Populations;
        var half = ScatterView.From(placer, 0, new ScatterViewOptions { RangeScale = 0.5 }).Populations;

        Assert.Equal(full.Count, half.Count);
        for (var i = 0; i < half.Count; i++)
        {
            Assert.Equal(placer.Layers[placer.Groups[i].Layer].Range * 0.5, half[i].Range);
            Assert.Equal(full[i].Revision, half[i].Revision);
            Assert.Same(full[i].Transforms, half[i].Transforms);
        }

        Assert.Equal([0.0, 20.0], half.Select(population => population.Range).Distinct().Order());
    }

    [Fact]
    public void AnEditSendsAgainOnlyTheRegionItMoved()
    {
        var (terrain, placer) = Placed(Grass);
        var before = ScatterView.From(placer, 0).Populations;

        terrain.RegionAt(new RegionKey(1, 0))!.SetHeight(8, 8, 3);
        placer.Update(terrain);
        var after = ScatterView.From(placer, 0).Populations;

        Assert.Equal(before.Select(population => population.Key), after.Select(population => population.Key));
        Assert.Equal(before[0].Revision, after[0].Revision);
        Assert.NotEqual(before[1].Revision, after[1].Revision);
    }

    [Fact]
    public void ALayerWithNothingInARegionIsLeftOut()
    {
        var (terrain, placer) = Placed(Grass);
        var hole = terrain.RegionAt(new RegionKey(0, 0))!;
        for (var j = 0; j < 16; j++)
        {
            for (var i = 0; i < 16; i++)
            {
                hole.SetCover(i, j, hole.CoverAt(i, j).WithHole(true));
            }
        }

        placer.Update(terrain);

        Assert.Equal(2, placer.Groups.Count);
        var drawn = ScatterView.From(placer, 0).Populations;
        Assert.Equal(placer.GroupsIn(new RegionKey(1, 0))[0].Id, Assert.Single(drawn).Key);
    }

    [Fact]
    public void AModelLayerIsAnObjectEachInItsMaterial()
    {
        var (_, placer) = Placed(Grass, Trees);

        var drawn = ScatterView.From(
            placer,
            0,
            new ScatterViewOptions
            {
                ObjectKey = 1L << 40,
                Mesh = path => $"/project/{path}",
                Material = path => path == "materials/bark.omat" ? 17L : null,
            });

        var models = placer.Groups.Where(group => group.Layer == 1).ToList();
        var count = models.Sum(group => group.Count);
        Assert.True(count > 0);
        Assert.Equal(count, drawn.Objects.Count);
        Assert.Equal(2, drawn.Populations.Count);

        var at = 0;
        foreach (var group in models)
        {
            for (var i = 0; i < group.Count; i++)
            {
                var one = drawn.Objects[at++];
                Assert.Equal((1L << 40) + (group.Id * 1048576L) + i, one.Key);
                Assert.Equal("/project/models/tree.glb", one.Mesh);
                Assert.Equal(17L, one.Material);
                Assert.True(one.CastShadows);
                for (var n = 0; n < 16; n++)
                {
                    Assert.Equal(group.Transforms[(i * 16) + n], one.Transform[n]);
                }

                Assert.Equal(
                    [group.Colours[i * 3], group.Colours[(i * 3) + 1], group.Colours[(i * 3) + 2]],
                    [one.Colour.X, one.Colour.Y, one.Colour.Z]);
            }
        }

        Assert.Equal(drawn.Objects.Count, drawn.Objects.Select(one => one.Key).Distinct().Count());
    }

    [Fact]
    public void LeavesModelLayersOutWhenAskedTo()
    {
        var (_, placer) = Placed(Grass, Trees);

        var drawn = ScatterView.From(placer, 0, new ScatterViewOptions { Models = false });

        Assert.Empty(drawn.Objects);
        Assert.Equal(2, drawn.Populations.Count);
    }

    [Fact]
    public void WithoutResolversAModelKeepsItsPathAndItsOwnMaterials()
    {
        var (_, placer) = Placed(Trees);

        var drawn = ScatterView.From(placer, 0);

        Assert.NotEmpty(drawn.Objects);
        Assert.Equal("models/tree.glb", drawn.Objects[0].Mesh);
        Assert.Null(drawn.Objects[0].Material);
    }

    [Fact]
    public void DrawsByTheRulesTheGroupsWerePlacedBy()
    {
        var (terrain, placer) = Placed(Grass);
        terrain.Scatter[0] = new ScatterLayer("grass", mesh: "blade.glb");

        var drawn = ScatterView.From(placer, 0);

        Assert.Equal(2, drawn.Populations.Count);
        Assert.Empty(drawn.Objects);
    }
}
