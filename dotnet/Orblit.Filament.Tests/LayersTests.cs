using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>Ported from the render layers group of layers_test.dart. The graph and texture target groups follow with the graph.</summary>
public sealed class LayersTests
{
    private static int Flags(int layer)
    {
        var one = SceneBuilders.Plain(1) with { Layer = layer };
        var scene = SceneBuilders.Of([one], SceneBuilders.CameraAt(0, 0, 5));
        return SceneBuilders.Read<int[]>(scene.ToMessage(), "objectFlags").Single();
    }

    [Fact]
    public void AnObjectThatHasNeverHeardOfLayersIsOnTheFirstOne()
    {
        // The same bits it always sent: cast, receive, visible, and a layer of nought that changes nothing.
        Assert.Equal(1 | 2 | 4, Flags(0) & 0xFF);
        Assert.Equal(0, Flags(0) >> 8);
    }

    [Fact]
    public void TheLayerRidesInTheHighBitsRatherThanInAFourthArray()
    {
        Assert.Equal(3, Flags(3) >> 8);
        Assert.Equal(1 | 2 | 4, Flags(3) & 0xFF);
    }

    [Fact]
    public void ALayerPastTheLastOneIsClampedNotWrapped()
    {
        // Wrapping would put an object on layer nought, which every pass draws.
        Assert.Equal(RenderScene.MaxLayer, Flags(99) >> 8);
    }
}
