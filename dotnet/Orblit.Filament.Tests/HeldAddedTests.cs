using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

public sealed class HeldAddedTests
{
    private static Population Forest(int key, int revision) =>
        new(key, new float[16], new float[3], new Bounds(Vector3.Zero(), Vector3.All(1))) { Revision = revision };

    private static RenderTerrain Land(int key) =>
        new(key, [new TerrainRegion(0, 0, new float[256], new uint[256], new byte[1024])], options: new TerrainOptions { RegionSize = 16 });

    [Fact]
    public void RememberingASceneReplacesWhatWasHeldAndNotAddsToIt()
    {
        var held = new Held();
        held.Remember(SceneBuilders.Of([]) with { Populations = [Forest(1, 4), Forest(2, 1)], Terrain = [Land(7)] });
        Assert.Equal(4, held.Populations[1]);
        Assert.True(held.HasPopulations);
        Assert.True(held.Terrain.ContainsKey(7));

        held.Remember(SceneBuilders.Of([]) with { Populations = [Forest(2, 3)] });
        Assert.Equal([2], held.Populations.Keys);
        Assert.Equal(3, held.Populations[2]);
        Assert.Empty(held.Terrain);
    }

    [Fact]
    public void ASceneWithNothingLeavesNothingHeld()
    {
        var held = new Held();
        held.Remember(SceneBuilders.Of([]) with { Populations = [Forest(1, 1)] });
        held.Remember(SceneBuilders.Of([]));
        Assert.False(held.HasPopulations);
        Assert.Empty(held.Terrain);
    }

    [Fact]
    public void ATerrainKeyUsedTwiceIsRefusedWhateverIsHeld()
    {
        var scene = SceneBuilders.Of([]) with { Terrain = [Land(5), Land(5)] };
        Assert.ThrowsAny<ArgumentException>(() => scene.ToMessage(1, new Held()));
    }
}
