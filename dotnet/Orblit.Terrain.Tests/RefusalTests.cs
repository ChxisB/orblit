using System.Text;

namespace Orblit.Terrain.Tests;

/// <summary>
/// Where the C# port differs from Dart on purpose. Dart's asserts are off outside tests and its
/// seeded <c>Random</c> is not ported (decision 77), so none of these have a fixture twin.
/// </summary>
public class RefusalTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100.5)]
    [InlineData(double.NaN)]
    public void ALayerRefusesADensityDartOnlyAsserts(double density)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScatterLayer("grass", density: density));
    }

    [Fact]
    public void ALayerTakesTheHighestDensityItAllows()
    {
        Assert.Equal(ScatterLayer.MaxDensity, new ScatterLayer("grass", density: ScatterLayer.MaxDensity).Density);
    }

    [Fact]
    public void AMapLengthNearTheTopOfALongIsAFormatErrorWhereDartThrowsRangeError()
    {
        var header = $"{{\"kind\":\"{TerrainRegion.Marker}\",\"formatVersion\":{TerrainRegion.FormatVersion},\"x\":0,\"z\":0,\"size\":4,\"maps\":[{{\"name\":\"height\",\"bytes\":9223372036854775807}}]}}\n";
        Assert.Throws<RegionFormatException>(() => TerrainRegion.Decode(Encoding.UTF8.GetBytes(header)));
    }

    // a seeded jitter

    private static List<double> Raised(int seed, double jitter)
    {
        var terrain = new Terrain(regionSize: 64);
        terrain.AddRegion(new RegionKey(0, 0));
        var stroke = new TerrainStroke(terrain, BrushTool.Raise, new Brush(size: 6, falloff: 0, jitter: jitter, spacing: 0.25), seed: seed);
        for (var x = 10.0; x < 50; x += 3)
        {
            stroke.MoveTo(x, 30);
        }

        return Enumerable.Range(0, 64).SelectMany(i => Enumerable.Range(0, 64).Select(j => terrain.TexelHeight(i, j)!.Value)).ToList();
    }

    [Fact]
    public void ASeedGivesTheSameJitteredStrokeEveryTime()
    {
        Assert.Equal(Raised(7, 1), Raised(7, 1));
    }

    [Fact]
    public void AnotherSeedGivesAnotherJitteredStroke()
    {
        Assert.NotEqual(Raised(7, 1), Raised(8, 1));
    }

    [Fact]
    public void WithoutJitterTheSeedChangesNothing()
    {
        Assert.Equal(Raised(7, 0), Raised(8, 0));
    }
}
