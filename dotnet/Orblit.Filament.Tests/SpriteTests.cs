using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>
/// Sprite layers as they cross to the renderer: the records a
/// sprite is packed into, a layer's settings, and when its sprites travel at all.
/// </summary>
public sealed class SpriteTests
{
    private static readonly float[] Two = SpriteLayer.Pack(
    [
        new LayerSprite { X = 1, Y = 2, Width = 3, Height = 4, Rotation = 0.5 },
        new LayerSprite { X = -1, Y = -2, U0 = 0.25, V1 = 0.5, Alpha = 0.75 },
    ]);

    private static RenderScene SceneWith(params SpriteLayer[] sprites) =>
        SceneBuilders.Of([], SceneBuilders.CameraAt(0, 0, 10) with { Orthographic = true }) with { Sprites = sprites };

    private static Held HeldWith(int key, int revision)
    {
        var held = new Held();
        held.Sprites[key] = revision;
        return held;
    }

    [Fact]
    public void ASpriteIsSixteenFloatsInTheOrderTheRendererReads()
    {
        Assert.Equal(2 * LayerSprite.Stride, Two.Length);
        Assert.Equal([1f, 2f, 0f, 0.5f, 3f, 4f, 0.5f, 0.5f, 0f, 0f, 1f, 1f, 1f, 1f, 1f, 1f], Two[..16]);
        Assert.Equal(0.25f, Two[16 + 8]);
        Assert.Equal(0.5f, Two[16 + 11]);
        Assert.Equal(0.75f, Two[16 + 15]);
    }

    [Fact]
    public void ARectangleGivenInPixelsBecomesFractionsOfTheImage()
    {
        var sprite = new LayerSprite { X = 0, Y = 0 }.Cut(16, 32, 16, 8, 64, 64);

        Assert.Equal([0.25, 0.5, 0.5, 0.625], [sprite.U0, sprite.V0, sprite.U1, sprite.V1]);
    }

    [Fact]
    public void ALayerIsItsTransformAndThenItsTint()
    {
        var layer = new SpriteLayer(1, Two)
        {
            Transform = Matrix4.TranslationValues(5, 6, 7),
            Tint = new Vector4(0.1, 0.2, 0.3, 0.4),
        };
        var into = new float[SpriteLayer.Stride];

        layer.PackParams(into, 0);

        Assert.Equal(5f, into[12]);
        Assert.Equal(6f, into[13]);
        Assert.Equal(7f, into[14]);
        SceneBuilders.NearAll(into[16..], 0.1, 0.2, 0.3, 0.4);
    }

    [Fact]
    public void ASharpLayerSnapsUnlessToldNotToAndASmoothOneDoesNot()
    {
        Assert.True(new SpriteLayer(1, Two).Snap);
        Assert.False(new SpriteLayer(1, Two) { Filter = MaterialFilter.Smooth }.Snap);
        Assert.False(new SpriteLayer(1, Two) { Snap = false }.Snap);
    }

    [Fact]
    public void ASceneWithNoSpritesSaysNothingAboutThem()
    {
        var message = SceneWith().ToMessage(1);

        Assert.DoesNotContain(message.Keys, key => key.StartsWith("sprite", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryLayerIsDescribedAndItsSpritesTravelTheFirstTime()
    {
        var message = SceneWith(new SpriteLayer(7, Two) { Image = new Texture("orblit:resource/atlas.png"), Order = -3 }).ToMessage(1);

        Assert.Equal([7], SceneBuilders.Read<int[]>(message, "spriteKeys"));
        Assert.Equal([-3], SceneBuilders.Read<int[]>(message, "spriteOrders"));
        Assert.Equal(["orblit:resource/atlas.png"], SceneBuilders.Read<string[]>(message, "spritePaths"));
        Assert.Equal(SpriteLayer.Stride, SceneBuilders.Read<float[]>(message, "spriteParams").Length);
        Assert.Equal([7], SceneBuilders.Read<int[]>(message, "spriteChanged"));
        Assert.Equal([2], SceneBuilders.Read<int[]>(message, "spriteChangedCounts"));
        Assert.Equal(Two, SceneBuilders.Read<float[]>(message, "spriteData"));
    }

    [Fact]
    public void ALayerTheRendererAlreadyHoldsSendsItsSettingsOnly()
    {
        var message = SceneWith(new SpriteLayer(7, Two) { Revision = 4 }).ToMessage(1, HeldWith(7, 4));

        Assert.Equal([7], SceneBuilders.Read<int[]>(message, "spriteKeys"));
        Assert.Empty(SceneBuilders.Read<int[]>(message, "spriteChanged"));
        Assert.Empty(SceneBuilders.Read<float[]>(message, "spriteData"));
    }

    [Fact]
    public void ALayerWhoseRevisionMovedSendsItsSpritesAgain()
    {
        var message = SceneWith(new SpriteLayer(7, Two) { Revision = 5 }).ToMessage(1, HeldWith(7, 4));

        Assert.Equal([7], SceneBuilders.Read<int[]>(message, "spriteChanged"));
        Assert.Equal(Two.Length, SceneBuilders.Read<float[]>(message, "spriteData").Length);
    }

    [Fact]
    public void ALayerWithNoImageNamesNone()
    {
        var message = SceneWith(new SpriteLayer(1, Two)).ToMessage(1);

        Assert.Equal([""], SceneBuilders.Read<string[]>(message, "spritePaths"));
    }
}
