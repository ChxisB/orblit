using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

public sealed class MaterialAddedTests
{
    [Fact]
    public void WithKeepsTheScreenAndBlendFieldsEveryTime()
    {
        var layered = new RenderMaterial
        {
            Key = 1,
            Shading = MaterialShading.Video,
            Video = 7,
            LitScreen = true,
            BlendMode = MaterialBlendMode.Masked,
            BlendAmount = 0.4,
            BlendSharpness = 12,
            BlendTiling = new Vector2(8, 8),
            BlendOffset = new Vector2(0.25, 0.5),
            BlendBaseColourMap = new Texture("/a.png"),
            BlendMaskMap = new Texture("/b.png"),
        };

        var changed = layered with { Roughness = 0.9 };

        Assert.Equal(7, changed.Video);
        Assert.Equal(MaterialBlendMode.Masked, changed.BlendMode);
        Assert.Equal(0.4, changed.BlendAmount);
        Assert.Equal(12, changed.BlendSharpness);
        Assert.Equal(new Vector2(8, 8), changed.BlendTiling);
        Assert.Equal(new Vector2(0.25, 0.5), changed.BlendOffset);
        Assert.Equal("/a.png", changed.BlendBaseColourMap?.Path);
        Assert.Equal("/b.png", changed.BlendMaskMap?.Path);
        Assert.Equal(layered.Flags, changed.Flags);
    }

    [Fact]
    public void ASecondMaterialWithTheSameKeyWinsAndTheObjectPointsAtIt()
    {
        var scene = SceneBuilders.Of([SceneBuilders.Plain(1) with { Material = 5 }]) with
        {
            Materials = [new RenderMaterial { Key = 5, Roughness = 0.1 }, new RenderMaterial { Key = 5, Roughness = 0.9 }],
        };

        var indices = SceneBuilders.Read<int[]>(scene.ToMessage(), "objectMaterials");

        Assert.Equal([1], indices);
    }
}
