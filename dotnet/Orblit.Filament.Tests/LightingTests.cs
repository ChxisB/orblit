using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>
/// The lights as they are packed, and the light clustering that
/// rides on the end of the pipeline block.
/// </summary>
public sealed class LightingTests
{
    private static RenderScene SceneWith(params RenderLight[] lights) =>
        SceneBuilders.Of([], SceneBuilders.CameraAt(0, 0, 5)) with { Lights = lights };

    [Fact]
    public void ARectangleOfLightSendsItsSizeAndItsEdgeToTheRenderer()
    {
        // A rectangle is the one light whose orientation is not settled by where it points: the
        // face leaves it free to spin in its own plane. So the edge crosses as well as the aim,
        // and both have to arrive.
        var scene = SceneWith(
            new RenderLight
            {
                Key = 1,
                Kind = RenderLightKind.Area,
                Intensity = 5000,
                Position = new Vector3(0, 3, 0),
                Direction = new Vector3(0, -1, 0),
                Tangent = new Vector3(0, 0, 1),
                Width = 2.5,
                Height = 0.75,
            });

        var packed = SceneBuilders.Read<float[]>(scene.ToMessage(), "lightParams");

        Assert.Equal(RenderLight.Stride, packed.Length);
        Assert.Equal(2.5f, packed[17]);
        Assert.Equal(0.75f, packed[18]);
        Assert.Equal(0f, packed[19]);
        Assert.Equal(0f, packed[20]);
        Assert.Equal(1f, packed[21]);
    }

    [Fact]
    public void ARectangleIsAKindOfItsOwnNotAPointInDisguise()
    {
        // The renderer refuses a kind it does not know rather than falling through to one it
        // does, so the index matters as much as the floats.
        var scene = SceneWith(new RenderLight { Key = 1, Kind = RenderLightKind.Area, Intensity = 5000 });

        Assert.Equal((int)RenderLightKind.Area, SceneBuilders.Read<int[]>(scene.ToMessage(), "lightKinds").Single());
        Assert.Equal(3, (int)RenderLightKind.Area);
    }

    [Fact]
    public void ARectangleLeftUndescribedIsStillARectangle()
    {
        // Nought width would divide by nothing when the flux is turned into a luminance, so the
        // defaults have to be a real panel rather than a degenerate one.
        var light = new RenderLight { Key = 1, Kind = RenderLightKind.Area, Intensity = 100 };

        Assert.True(light.Width > 0);
        Assert.True(light.Height > 0);
        Assert.True(light.Tangent.Length > 0);
    }

    [Fact]
    public void ALightWritesExactlyTheStrideItDeclares()
    {
        // The renderer walks this array by a stride of its own. When the two disagree every light
        // after the first reads a mixture of the one before it and itself, which is what happened
        // when the halo fields took a light from sixteen floats to eighteen.
        var scene = SceneWith(
            new RenderLight
            {
                Key = 1,
                Kind = RenderLightKind.Directional,
                Intensity = 100000,
                Direction = new Vector3(0, -1, 0),
            },
            new RenderLight
            {
                Key = 2,
                Kind = RenderLightKind.Point,
                Intensity = 800,
                Position = new Vector3(3, 2, 1),
            });

        var packed = SceneBuilders.Read<float[]>(scene.ToMessage(), "lightParams");

        Assert.Equal(2 * RenderLight.Stride, packed.Length);
        // The second light starts exactly one stride in, and its own numbers are there rather
        // than the tail of the first.
        Assert.Equal(800f, packed[RenderLight.Stride + 3]);
        Assert.Equal(3f, packed[RenderLight.Stride + 4]);
        Assert.Equal(2f, packed[RenderLight.Stride + 5]);
        Assert.Equal(1f, packed[RenderLight.Stride + 6]);
    }

    [Fact]
    public void AShadingModelKeepsTheNumberTheRendererReadsItBy()
    {
        // The renderer branches on this index. Reordering the enum to put a new model in the
        // middle is a silent swap of every video screen in every scene for whatever took its place.
        Assert.Equal(0, (int)MaterialShading.Lit);
        Assert.Equal(1, (int)MaterialShading.Unlit);
        Assert.Equal(2, (int)MaterialShading.Video);
        Assert.Equal(3, (int)MaterialShading.ShadowCatcher);
    }

    [Fact]
    public void AShadowCatcherIsAShadingModelNotABlendMode()
    {
        // Its blending is fixed by what it is: a surface that is only its own shadow is
        // see-through by definition.
        var floor = new RenderMaterial { Key = 9, Shading = MaterialShading.ShadowCatcher };

        Assert.Equal(MaterialShading.ShadowCatcher, floor.Shading);
        Assert.False(MaterialShadings.IsSurface(MaterialShading.ShadowCatcher));
        Assert.True(MaterialShadings.IsSurface(MaterialShading.Lit));
    }

    [Fact]
    public void AShadowCatcherReachesTheRendererAsAShadingModelInTheFlags()
    {
        var scene = SceneBuilders.Of([], SceneBuilders.CameraAt(0, 0, 5)) with
        {
            Materials = [new RenderMaterial { Key = 9, Shading = MaterialShading.ShadowCatcher }],
        };

        Assert.Equal(3, SceneBuilders.Read<int[]>(scene.ToMessage(), "materialFlags").Single() & 3);
    }

    [Fact]
    public void ClusteringDefaultsAreTheOnesARoomSizedSceneWants()
    {
        var lighting = new Lighting();

        Assert.Equal(5, lighting.ClusterNear);
        Assert.Equal(100, lighting.ClusterFar);
    }

    [Fact]
    public void ClusteringCrossesOnTheEndOfThePipelineBlock()
    {
        var packed = new Pipeline { Lighting = new Lighting { ClusterNear = 2, ClusterFar = 400 } }.Packed;

        Assert.Equal(Pipeline.Stride, packed.Length);
        Assert.Equal(2f, packed[16]);
        Assert.Equal(400f, packed[17]);
    }

    [Fact]
    public void APipelineThatSaysNothingStillSendsTheNumbers()
    {
        // A short block is one from before clustering existed, and the renderer reads it as "use
        // the renderer's own defaults". A pipeline built today is never short, so the two paths do
        // not have to agree about anything.
        Assert.Equal(5f, new Pipeline().Packed[16]);
        Assert.Equal(100f, new Pipeline().Packed[17]);
    }

    [Fact]
    public void EveryNamedDetailTierCarriesClusteringToo()
    {
        foreach (var detail in Enum.GetValues<Detail>())
        {
            Assert.Equal(Pipeline.Stride, Pipeline.At(detail).Packed.Length);
        }
    }
}
