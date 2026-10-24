using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>Ported from layers_test.dart.</summary>
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

    [Fact]
    public void APopulationCarriesALayerTheSameWay()
    {
        var forest = new Population(1, new float[16], new float[3], new Bounds(Vector3.Zero(), Vector3.All(1))) { Layer = 2 };
        Assert.Equal(2, forest.Flags >> 8);
        Assert.Equal(2, forest.Flags & 0xFF);
    }

    [Fact]
    public void ATargetIsAPathWithASchemeNotASecondField()
    {
        var mirror = Texture.OfTarget("reflection");

        Assert.Equal("reflection", mirror.Target);
        Assert.Equal("orblit:target/reflection", mirror.Path);
        // What a pass drew is already linear, and decoding it again would darken every reflection.
        Assert.False(mirror.Srgb);
    }

    [Fact]
    public void AnOrdinaryTextureNamesNoTarget()
    {
        Assert.Null(new Texture("/tmp/wood.png").Target);
    }

    [Fact]
    public void ASceneThatSaysNothingAboutTheGraphDrawsOnePassIntoTheFrame()
    {
        var scene = SceneBuilders.Of([SceneBuilders.Plain(1)], SceneBuilders.CameraAt(0, 0, 5));
        Assert.Equal(["scene"], scene.PassNames);

        var message = scene.ToMessage();
        Assert.Equal(RenderGraph.PassStride, SceneBuilders.Read<float[]>(message, "graphPasses").Length);
        Assert.Empty(SceneBuilders.Read<float[]>(message, "graphTargets"));
    }

    [Fact]
    public void ThePassesCrossInTheOrderTheyRunWithTheirTargets()
    {
        var scene = SceneBuilders.Of([SceneBuilders.Plain(1)], SceneBuilders.CameraAt(0, 0, 5)) with
        {
            Graph = new RenderGraph
            {
                Targets = [new RenderTarget { Name = "mirror", Scale = 0.5 }],
                Passes =
                [
                    new RenderPass { Name = "frame", Reads = ["mirror"], Layers = 0x01 },
                    new RenderPass
                    {
                        Name = "water",
                        Kind = PassKind.Reflection,
                        Into = "mirror",
                        Layers = 0x02,
                        Plane = [0, 1, 0, 0],
                    },
                ],
            },
        };

        Assert.Equal(["water", "frame"], scene.PassNames);
        var message = scene.ToMessage();
        Assert.Equal(["mirror"], SceneBuilders.Read<string[]>(message, "graphTargetNames"));
        Assert.Equal(2 * RenderGraph.PassStride, SceneBuilders.Read<float[]>(message, "graphPasses").Length);
    }
}
