namespace Orblit.Filament.Tests;

public sealed class SpriteAddedTests
{
    private static readonly float[] One = SpriteLayer.Pack([new LayerSprite { X = 1, Y = 1 }]);

    [Fact]
    public void RecordsThatAreNotWholeAreRefused()
    {
        Assert.ThrowsAny<ArgumentException>(() => new SpriteLayer(1, new float[LayerSprite.Stride + 1]));
    }

    [Fact]
    public void EachFlagIsItsOwnBit()
    {
        var plain = new SpriteLayer(1, One) { Filter = MaterialFilter.Smooth };

        Assert.Equal(0, plain.Flags);
        Assert.Equal(1 | 2, new SpriteLayer(1, One).Flags);
        Assert.Equal(1, new SpriteLayer(1, One) { Snap = false }.Flags);
        Assert.Equal(2, new SpriteLayer(1, One) { Filter = MaterialFilter.Smooth, Snap = true }.Flags);
        Assert.Equal(4, new SpriteLayer(1, One) { Filter = MaterialFilter.Smooth, Image = new Texture("n.png", Srgb: false) }.Flags);
        Assert.Equal(8, new SpriteLayer(1, One) { Filter = MaterialFilter.Smooth, Blend = SpriteBlend.Add }.Flags);
    }

    [Fact]
    public void AnImageOfColourDoesNotSetTheMeasurementsBit()
    {
        var layer = new SpriteLayer(1, One) { Filter = MaterialFilter.Smooth, Image = new Texture("a.png") };

        Assert.Equal(0, layer.Flags);
    }

    [Fact]
    public void ANegativeWidthPassesThroughToFlipTheSprite()
    {
        var packed = SpriteLayer.Pack([new LayerSprite { X = 0, Y = 0, Width = -2 }]);

        Assert.Equal(-2f, packed[4]);
    }

    [Fact]
    public void SeveralChangedLayersAreJoinedInTheOrderTheyAreNamed()
    {
        var first = SpriteLayer.Pack([new LayerSprite { X = 1, Y = 0 }]);
        var second = SpriteLayer.Pack([new LayerSprite { X = 2, Y = 0 }, new LayerSprite { X = 3, Y = 0 }]);
        var scene = SceneBuilders.Of([]) with { Sprites = [new SpriteLayer(5, first), new SpriteLayer(4, second)] };

        var message = scene.ToMessage(1);

        Assert.Equal([5, 4], SceneBuilders.Read<int[]>(message, "spriteChanged"));
        Assert.Equal([1, 2], SceneBuilders.Read<int[]>(message, "spriteChangedCounts"));
        Assert.Equal([.. first, .. second], SceneBuilders.Read<float[]>(message, "spriteData"));
    }

    [Fact]
    public void ARevisionThatWentBackwardsIsSentAgain()
    {
        var scene = SceneBuilders.Of([]) with { Sprites = [new SpriteLayer(1, One) { Revision = 2 }] };
        var held = new Held();
        held.Sprites[1] = 5;

        var message = scene.ToMessage(1, held);

        Assert.Equal([1], SceneBuilders.Read<int[]>(message, "spriteChanged"));
    }

    [Fact]
    public void HeldRemembersEveryLayerAndForgetsThemWhenTheSceneHasNone()
    {
        var held = new Held();
        held.Remember(SceneBuilders.Of([]) with { Sprites = [new SpriteLayer(1, One) { Revision = 3 }] });

        Assert.Equal(3, held.Sprites[1]);
        Assert.True(held.HasSprites);

        held.Remember(SceneBuilders.Of([]));
        Assert.Empty(held.Sprites);
        Assert.False(held.HasSprites);
    }
}
