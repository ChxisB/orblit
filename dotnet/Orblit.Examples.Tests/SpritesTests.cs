using Orblit.Filament;

namespace Orblit.Examples.Tests;

/// <summary>
/// The sprites example with its sheet kept. The fixture ran before the
/// sheet was painted, so its scenes have no layers.
/// </summary>
public sealed class SpritesTests
{
    private static readonly RenderCamera Camera = new ViewPoint().ToRenderCamera();

    /// <summary>
    /// Digests from a one-off run of the previous example with its atlas set at
    /// once. The coins are seeded from a different generator, so as decision 77
    /// has it they match on shape only. Everything else matches on value: every
    /// key but the sprite floats, and the backdrop and spark floats on their own.
    /// </summary>
    [Theory]
    [InlineData("rest", 1.0, "6d1285351cc8d99e", "a7dd84d3b5080563", "22362f4019adae7f")]
    [InlineData("soft", 0.7, "099b2c269dd8f5c7", "a7dd84d3b5080563", "d85fcacbcdbf590d")]
    [InlineData("still", 2.0, "13100fabf783a916", "03de183f8782452a", null)]
    [InlineData("wrap", 2.0, "386769810e16d368", "063eee6505bce163", "6263dde057b7ff29")]
    public void TheSceneMatchesTheFixture(string setting, double seconds, string rest, string shape, string? sparks)
    {
        var example = new SpritesExample(new Named());
        Set(example, setting);

        var scene = example.Scene(Camera, seconds);
        var message = SceneDigest.Message(scene, seconds);

        Assert.Equal(shape, SceneDigest.ShapeOf(message));
        message.Remove("spriteData");
        Assert.Equal(rest, SceneDigest.Of(message));
        Assert.Equal("ca9f4da05fca8145", SceneDigest.Raw(scene.Sprites[0].Sprites));
        Assert.Equal(sparks, scene.Sprites.Count > 2 ? SceneDigest.Raw(scene.Sprites[2].Sprites) : null);
    }

    /// <summary>The backdrop scrolls left and starts over every tile, so it is back at nothing on a whole tile.</summary>
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.5, 1.25)]
    [InlineData(2.0, 1.0)]
    public void TheBackdropStartsOverEveryTile(double seconds, double scroll)
    {
        var tiles = new SpritesExample(new Named()).Scene(Camera, seconds).Sprites[0];

        Assert.Equal(scroll, tiles.Transform.GetTranslation().X);
    }

    [Fact]
    public void TheSeedGivesTheSameCrowdEveryRun()
    {
        var first = new SpritesExample(new Named()).Scene(Camera, 1.5).Sprites[1].Sprites;
        var second = new SpritesExample(new Named()).Scene(Camera, 1.5).Sprites[1].Sprites;

        Assert.Equal(first, second);
        Assert.Equal(2000, first.Length / LayerSprite.Stride);
        // Pinned from this code's own run. The fixture's crowd came from another generator.
        Assert.Equal("49e6e7f5e83450ab", SceneDigest.Raw(first));
    }

    /// <summary>Coins all move every frame, so each frame is a new revision of the one buffer.</summary>
    [Fact]
    public void EachFrameRewritesTheCoinsAndTheSparks()
    {
        var example = new SpritesExample(new Named());

        var before = example.Scene(Camera, 0.2);
        var coins = before.Sprites[1].Sprites[..LayerSprite.Stride];
        var after = example.Scene(Camera, 0.4);

        Assert.Equal([0, 1, 1], before.Sprites.Select(layer => layer.Revision));
        Assert.Equal([0, 2, 2], after.Sprites.Select(layer => layer.Revision));
        Assert.NotEqual(coins, after.Sprites[1].Sprites[..LayerSprite.Stride]);
    }

    [Fact]
    public void ASheetThatCannotBeKeptDrawsNothingAndSaysSo()
    {
        var example = new SpritesExample(new Nowhere());

        Assert.Empty(example.Scene(Camera, 1).Sprites);
        Assert.Equal("The sprite sheet could not be kept, so there is nothing to draw.", example.Note);
    }

    [Fact]
    public void TheCoinsComeInThreeCrowds()
    {
        var example = new SpritesExample(new Named());
        var coins = example.Control<Choice>("Coins");
        Assert.Equal(["200", "2k", "20k"], coins.Options);
        Assert.Equal("2k", coins.Selected);

        Pick(example, "20k");

        Assert.Equal("20k", example.Control<Choice>("Coins").Selected);
        Assert.Equal(20000, example.Scene(Camera, 1).Sprites[1].Count);
    }

    private static void Set(SpritesExample example, string setting)
    {
        switch (setting)
        {
            case "soft":
                example.Control<Toggle>("Sharp pixels").Change(false);
                example.Control<Toggle>("Snap to pixels").Change(false);
                break;
            case "still":
                example.Control<Toggle>("Scroll the backdrop").Change(false);
                example.Control<Toggle>("Sparks").Change(false);
                Pick(example, "200");
                break;
            case "wrap":
                Pick(example, "20k");
                break;
        }
    }

    private static void Pick(SpritesExample example, string option)
    {
        var select = example.Control<Choice>("Coins").Select;
        Assert.NotNull(select);
        select(option);
    }

    /// <summary>Names the sheet as the fixture's run did, so the layers' paths match its digests.</summary>
    private sealed class Named : IPictureFolder
    {
        public string? Keep(string example, string name, int size, byte[] rgba) =>
            "orblit:resource/examples/sprites/sheet.png";
    }

    private sealed class Nowhere : IPictureFolder
    {
        public string? Keep(string example, string name, int size, byte[] rgba) => null;
    }
}
