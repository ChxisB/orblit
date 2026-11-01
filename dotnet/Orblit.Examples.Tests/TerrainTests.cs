using Orblit.Filament;
using Orblit.VectorMath;

namespace Orblit.Examples.Tests;

/// <summary>
/// What the terrain sends once and what it only changes settings for, and
/// what stands on it.
/// </summary>
public sealed class TerrainTests
{
    private static readonly RenderCamera Camera = new ViewPoint().ToRenderCamera();

    [Fact]
    public void TheGroundIsWhatTheRendererTakesWithBothSetsPainted()
    {
        var drawn = Assert.Single(new TerrainExample().Scene(Camera, 0).Terrain);

        Assert.Equal(16, drawn.Regions.Count);
        Assert.Equal(64, drawn.Options.RegionSize);
        Assert.Equal(2, drawn.Sets.Count);
        Assert.All(drawn.Sets, set =>
        {
            Assert.NotNull(set.Albedo);
            Assert.NotNull(set.Normal);
        });
        Assert.Equal(128, drawn.TextureSize);
    }

    [Fact]
    public void ThereAreHillsAndTheyEndInAPlain()
    {
        var ground = new TerrainExample().Ground;
        var middle = new List<double>();
        for (var x = -120.0; x <= 120; x += 8)
        {
            for (var z = -120.0; z <= 120; z += 8)
            {
                middle.Add(ground.HeightAt(x, z) ?? double.NaN);
            }
        }

        var edge = TerrainExample.Reach - 2;

        Assert.True(middle.Max() > 20);
        Assert.Equal(0, ground.HeightAt(-edge, -edge) ?? double.NaN, 1e-9);
    }

    [Fact]
    public void TheRegionsAreSentOnceHoweverTheSettingsMove()
    {
        var example = new TerrainExample();
        var first = Revisions(example, 0);
        Assert.Equal("Rock laid from three sides", example.Control<Toggle>("Triplanar").Note);

        example.Control<Setting>("Steepness").Change(3);
        example.Control<Setting>("Altitude").Change(1.5);
        example.Control<Setting>("Sharpness").Change(0.2);
        example.Control<Setting>("Rings").Change(3);
        example.Control<Toggle>("Triplanar").Change(false);
        var drawn = Assert.Single(example.Scene(Camera, 1).Terrain);

        Assert.Equal(first, Revisions(example, 2));
        Assert.Equal(3, drawn.Options.AutoSlope);
        Assert.Equal(1.5, drawn.Options.AutoHeightFalloff);
        Assert.Equal(0.2, drawn.Options.BlendSharpness);
        Assert.Equal(3, drawn.Options.Levels);
        Assert.False(drawn.Sets[0].Triplanar);
        Assert.Equal("Rock dropped from above, and stretched down cliffs", example.Control<Toggle>("Triplanar").Note);
        Assert.Equal(0, drawn.Options.PicturesRevision);
    }

    [Fact]
    public void ReliefAndWorldMakeEveryRegionAgain()
    {
        var example = new TerrainExample();
        var first = Revisions(example, 0);
        var flat = example.Ground.HeightAt(30, 30);

        example.Control<Setting>("Relief").Change(4);
        var dragging = Revisions(example, 1);
        example.Control<Setting>("Relief").Settle?.Invoke(4);
        var low = example.Ground.HeightAt(30, 30);
        example.Control<Choice>("World").Select?.Invoke("3");

        Assert.Equal(first, dragging);
        // Heights are kept as floats, so the scale is only good to a float.
        Assert.Equal((flat ?? 0) * 4 / 60, low ?? double.NaN, 1e-4);
        Assert.Equal("3", example.Control<Choice>("World").Selected);
        Assert.NotEqual(low, example.Ground.HeightAt(30, 30));
    }

    [Fact]
    public void TheScatterIsPlacedByRuleAndOnlyOnce()
    {
        var example = new TerrainExample();
        var first = example.Scene(Camera, 0).Populations;

        var again = example.Scene(Camera, 1).Populations;
        var grass = Counted(example, "grass");
        example.Control<Setting>("Steepness").Change(3);
        example.Scene(Camera, 2);
        var steeper = Counted(example, "grass");
        example.Control<Toggle>("Scatter").Change(false);

        Assert.Equal(["grass", "stones", "trunks", "crowns"], example.Placer.Layers.Select(layer => layer.Name));
        Assert.True(grass > 10000);
        Assert.True(Counted(example, "stones") > 100);
        Assert.True(Counted(example, "trunks") > 50);
        // One seed, so every trunk has its crown.
        Assert.Equal(Counted(example, "trunks"), Counted(example, "crowns"));
        Assert.Equal(first.Count, first.Select(population => population.Key).Distinct().Count());
        Assert.Equal(first.Select(population => population.Revision), again.Select(population => population.Revision));
        // Placed once: the second frame is handed the same list, not one built alike.
        Assert.Same(first, again);
        // A cover rule moves the grass off the steeper slopes.
        Assert.True(steeper < grass);
        Assert.Empty(example.Scene(Camera, 3).Populations);
        Assert.Equal($"{example.Placer.Count} grass, stones and trees, placed by rule", example.Control<Toggle>("Scatter").Note);
    }

    [Fact]
    public void ItsTreesStandOnGentleLowGround()
    {
        var example = new TerrainExample();
        example.Scene(Camera, 0);
        var trunks = Layer(example, "trunks");

        foreach (var group in example.Placer.Groups.Where(group => group.Layer == trunks))
        {
            for (var i = 0; i < group.Count; i++)
            {
                double x = group.Transforms[(i * 16) + 12];
                double z = group.Transforms[(i * 16) + 14];
                Assert.True(example.Ground.HeightAt(x, z) <= 30);
                Assert.True(example.Ground.NormalAt(x, z)?.Y >= Math.Cos(22 * Math.PI / 180) - 1e-6);
            }
        }
    }

    [Fact]
    public void TheRoverStandsOnTheGroundAndLeansWithIt()
    {
        var example = new TerrainExample();

        for (var seconds = 0.0; seconds < 60; seconds += 3.7)
        {
            var rover = Assert.Single(example.Scene(Camera, seconds).Objects);
            var column = rover.Transform.GetColumn(1);
            var up = new Vector3(column.X, column.Y, column.Z).Normalized();
            // Back down its own up to the ground it was put on.
            var foot = rover.Transform.GetTranslation() - (up * (0.8 + 0.15));

            Assert.Equal(TerrainExample.RoverRadius, Math.Sqrt((foot.X * foot.X) + (foot.Z * foot.Z)), 1e-9);
            Assert.Equal(example.Ground.HeightAt(foot.X, foot.Z) ?? double.NaN, foot.Y, 1e-9);
            Assert.Equal(1, up.Dot(example.Ground.NormalAt(foot.X, foot.Z) ?? Vector3.Zero()), 1e-9);
        }

        example.Control<Toggle>("Rover").Change(false);
        Assert.Empty(example.Scene(Camera, 0).Objects);
    }

    private static List<int> Revisions(Example example, double seconds) =>
        [.. Assert.Single(example.Scene(Camera, seconds).Terrain).Regions.Select(region => region.Revision)];

    private static int Layer(TerrainExample example, string name) =>
        example.Placer.Layers.ToList().FindIndex(layer => layer.Name == name);

    private static int Counted(TerrainExample example, string name) =>
        example.Placer.Groups.Where(group => group.Layer == Layer(example, name)).Sum(group => group.Count);
}
