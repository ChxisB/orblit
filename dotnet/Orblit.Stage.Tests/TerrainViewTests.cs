using Orblit.Filament;
using Ground = Orblit.Terrain.Terrain;
using GroundCover = Orblit.Terrain.AutoCover;
using GroundSet = Orblit.Terrain.TerrainSet;
using GroundKey = Orblit.Terrain.RegionKey;

namespace Orblit.Stage.Tests;

public class TerrainViewTests
{
    private static Ground GroundOf() => new(
        regionSize: 16,
        spacing: 0.5,
        sets:
        [
            new GroundSet("grass", albedo: "grass.png", normal: "grass_n.png"),
            new GroundSet("rock", albedo: "missing.png", tileSize: 9, triplanar: true),
        ],
        autoCover: new GroundCover(steep: 1, flat: 0, slope: 2, heightFalloff: 0.3),
        blendSharpness: 0.5);

    [Fact]
    public void CarriesTheRegionsAsTheyAreAndTheSettings()
    {
        var terrain = GroundOf();
        var region = terrain.AddRegion(new GroundKey(-2, 3));

        var drawn = TerrainView.From(terrain, 9);

        Assert.Equal(9, drawn.Key);
        Assert.Equal(16, drawn.Options.RegionSize);
        Assert.Equal(0.5, drawn.Options.Spacing);
        Assert.Equal(0.5, drawn.Options.BlendSharpness);
        Assert.Equal(
            (1, 0, 2.0, 0.3),
            (drawn.Options.AutoSteep, drawn.Options.AutoFlat, drawn.Options.AutoSlope, drawn.Options.AutoHeightFalloff));
        var only = Assert.Single(drawn.Regions);
        Assert.Equal((-2, 3, (int)region.Revision), (only.X, only.Z, only.Revision));
        Assert.Same(region.Heights, only.Heights);
        Assert.Same(region.Cover, only.Cover);
        Assert.Same(region.Colour, only.Colour);
    }

    [Fact]
    public void AnEditMovesTheRevisionSoTheRegionTravelsAgain()
    {
        var terrain = GroundOf();
        var region = terrain.AddRegion(new GroundKey(0, 0));
        var before = Assert.Single(TerrainView.From(terrain, 1).Regions).Revision;

        region.SetHeight(3, 4, 12);

        Assert.NotEqual(before, Assert.Single(TerrainView.From(terrain, 1).Regions).Revision);
    }

    [Fact]
    public void ImagesComeFromTheCallerByPathAndAMissingOneIsPlain()
    {
        var grass = Enumerable.Repeat((byte)7, 16).ToArray();
        var normal = Enumerable.Repeat((byte)9, 16).ToArray();
        var asked = new List<string>();
        var images = new Dictionary<string, byte[]> { ["grass.png"] = grass, ["grass_n.png"] = normal };

        var drawn = TerrainView.From(
            GroundOf(),
            1,
            new TerrainViewOptions
            {
                Pixels = path =>
                {
                    asked.Add(path);
                    return images.GetValueOrDefault(path);
                },
                PicturesRevision = 4,
            });

        Assert.Equal(["grass.png", "grass_n.png", "missing.png"], asked);
        Assert.Equal(4, drawn.Options.PicturesRevision);
        Assert.Equal(2, drawn.TextureSize);
        Assert.Same(grass, drawn.Sets[0].Albedo);
        Assert.Same(normal, drawn.Sets[0].Normal);
        Assert.Null(drawn.Sets[1].Albedo);
        Assert.Equal((9.0, true), (drawn.Sets[1].TileSize, drawn.Sets[1].Triplanar));
    }

    [Fact]
    public void WithNoImagesAtAllEverySetIsPlain()
    {
        var drawn = TerrainView.From(GroundOf(), 1);

        Assert.All(drawn.Sets, set => Assert.True(set.Albedo is null && set.Normal is null));
        Assert.Equal(1, drawn.TextureSize);
    }

    [Fact]
    public void GroundTheRendererCannotDrawSaysSo() =>
        Assert.Throws<ArgumentException>(() => TerrainView.From(new Ground(regionSize: 8), 1));

    [Fact]
    public void WhatItAddsOfItsOwnStartsWhereTheRendererDoes()
    {
        var drawn = TerrainView.From(new Ground(regionSize: 16), 1);
        var plain = new TerrainOptions();

        Assert.Equal(
            (plain.MeshSize, plain.Levels, plain.CastShadows, plain.ReceiveShadows),
            (drawn.Options.MeshSize, drawn.Options.Levels, drawn.Options.CastShadows, drawn.Options.ReceiveShadows));
    }

    [Fact]
    public void TheDrawOptionsAreCarriedOver()
    {
        var drawn = TerrainView.From(
            new Ground(regionSize: 16),
            1,
            new TerrainViewOptions { MeshSize = 32, Levels = 3, CastShadows = false, ReceiveShadows = false });

        Assert.Equal((32, 3, false, false), (drawn.Options.MeshSize, drawn.Options.Levels, drawn.Options.CastShadows, drawn.Options.ReceiveShadows));
    }
}
