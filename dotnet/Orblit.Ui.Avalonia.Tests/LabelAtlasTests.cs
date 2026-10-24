using Avalonia;
using SkiaSharp;

namespace Orblit.Ui.Avalonia.Tests;

public class LabelAtlasTests
{
    private const int Padding = 2;

    private static LabelAtlas Atlas(int width = 1024, int initialHeight = 128, int maxHeight = 4096) =>
        new(new LabelAtlasOptions { Width = width, InitialHeight = initialHeight, MaxHeight = maxHeight, Padding = Padding });

    // The size of a label as the atlas measures it, so a test can pick a width that is just too small.
    private static Size Measured(string text)
    {
        var probe = Atlas();
        probe.Add(text);
        return probe.RectOf(text)!.Value.Size;
    }

    // Wide enough for any one of the labels and too narrow for two on a row.
    private static int Widest(params string[] texts) => (int)texts.Max(text => Measured(text).Width) + (2 * Padding);

    private static SKBitmap Decode(byte[] png) => SKBitmap.Decode(png);

    private static int Lit(SKBitmap picture, Rect inside)
    {
        var lit = 0;
        for (var y = (int)inside.Top; y < (int)inside.Bottom; y++)
        {
            for (var x = (int)inside.Left; x < (int)inside.Right; x++)
            {
                lit += picture.GetPixel(x, y).Alpha > 0 ? 1 : 0;
            }
        }

        return lit;
    }

    [Fact]
    public Task TwoStringsGetRectanglesThatDoNotOverlap() => Headless.Run(() =>
    {
        var atlas = Atlas(512);
        atlas.Add("Ada");
        atlas.Add("Brannock");

        var a = atlas.RectOf("Ada")!.Value;
        var b = atlas.RectOf("Brannock")!.Value;
        Assert.True(b.Width > a.Width);
        Assert.Equal(a.Height, b.Height);
        Assert.False(a.Intersects(b));
        Assert.Equal(Math.Ceiling(a.Width), a.Width);
    });

    [Fact]
    public Task TheSameStringIsTheSamePlace() => Headless.Run(() =>
    {
        var atlas = Atlas();
        atlas.Add("Ada");
        var first = atlas.RectOf("Ada");

        atlas.Add("Brannock");
        atlas.Add("Ada");

        Assert.Equal(first, atlas.RectOf("Ada"));
        Assert.Null(atlas.RectOf("never added"));
    });

    [Fact]
    public Task RowsWrapAtTheWidth() => Headless.Run(() =>
    {
        var atlas = Atlas(width: (int)(Measured("aaaa").Width + Measured("bbbb").Width) + (3 * Padding) - 1);
        atlas.Add("aaaa");
        atlas.Add("bbbb");

        Assert.True(atlas.RectOf("bbbb")!.Value.Top > atlas.RectOf("aaaa")!.Value.Top);
        Assert.Equal(Padding, atlas.RectOf("bbbb")!.Value.Left);
    });

    [Fact]
    public Task ALabelWiderThanTheAtlasIsRefused() => Headless.Run(() =>
    {
        var atlas = Atlas(width: 32);

        Assert.ThrowsAny<ArgumentException>(() => atlas.Add("far too long"));
        Assert.Null(atlas.RectOf("far too long"));
        Assert.False(atlas.IsDirty);
    });

    [Fact]
    public Task ThePixelsInsideARectangleAreTheLabel() => Headless.Run(() =>
    {
        var atlas = Atlas(256);
        atlas.Add("Ada");

        using var picture = Decode(atlas.Paint());

        var a = atlas.RectOf("Ada")!.Value;
        Assert.Equal(256, picture.Width);
        Assert.Equal(atlas.Size.Height, picture.Height);
        Assert.True(Lit(picture, a) > 0, "the label is drawn inside its rectangle");
        Assert.True(Lit(picture, new Rect(a.Right, a.Top, Padding, a.Height)) == 0, "the gutter stays empty");
        Assert.True(Lit(picture, new Rect(0, a.Bottom + Padding, 256, picture.Height - a.Bottom - Padding)) == 0, "nothing is drawn below it");
    });

    [Fact]
    public Task TheStringsAreDrawnInWhiteSoASurfaceCanTintThem() => Headless.Run(() =>
    {
        var atlas = Atlas(256);
        atlas.Add("Ada");

        using var picture = Decode(atlas.Paint());

        var a = atlas.RectOf("Ada")!.Value;
        var solid = Enumerable.Range((int)a.Left, (int)a.Width)
            .SelectMany(x => Enumerable.Range((int)a.Top, (int)a.Height).Select(y => picture.GetPixel(x, y)))
            .Where(pixel => pixel.Alpha == 255)
            .ToList();
        Assert.NotEmpty(solid);
        Assert.All(solid, pixel => Assert.Equal((255, 255, 255), (pixel.Red, pixel.Green, pixel.Blue)));
    });

    [Fact]
    public Task AFullAtlasGrowsAndSaysSo() => Headless.Run(() =>
    {
        var atlas = Atlas(width: Widest("aaaa", "bbbb", "cccc", "dddd"), initialHeight: (int)Measured("aaaa").Height + (2 * Padding));
        atlas.Add("aaaa");
        atlas.Paint();
        var before = (atlas.Revision, atlas.Size.Height);
        var first = atlas.RectOf("aaaa");

        atlas.Add("bbbb");
        atlas.Add("cccc");
        atlas.Add("dddd");
        using var picture = Decode(atlas.Paint());

        Assert.True(atlas.Size.Height > before.Height);
        Assert.True(atlas.Revision > before.Revision);
        Assert.Equal(atlas.Size.Height, picture.Height);
        Assert.Equal(first, atlas.RectOf("aaaa"));
    });

    [Fact]
    public Task TheRevisionMovesOnlyWhenThePictureDoes() => Headless.Run(() =>
    {
        var atlas = Atlas();
        atlas.Add("Ada");
        Assert.True(atlas.IsDirty);

        atlas.Paint();
        var drawn = atlas.Revision;
        Assert.False(atlas.IsDirty);

        atlas.Add("Ada");
        Assert.False(atlas.IsDirty);
        atlas.Paint();
        Assert.Equal(drawn, atlas.Revision);

        atlas.Add("Brannock");
        atlas.Paint();
        Assert.Equal(drawn + 1, atlas.Revision);
    });

    [Fact]
    public Task AnEmptyAtlasPaintsAnEmptyPictureOfItsFullSize() => Headless.Run(() =>
    {
        var atlas = Atlas(64, 32);

        using var picture = Decode(atlas.Paint());

        Assert.Equal((64, 32), (picture.Width, picture.Height));
        Assert.Equal(0, Lit(picture, new Rect(0, 0, 64, 32)));
        Assert.Equal(0, atlas.Revision);
    });

    [Fact]
    public Task AFullAtlasAtItsLimitSaysSo() => Headless.Run(() =>
    {
        var height = (int)Measured("aaa").Height + (2 * Padding);
        var atlas = Atlas(width: Widest("aaa", "bbb"), initialHeight: height, maxHeight: height);
        atlas.Add("aaa");

        Assert.Throws<InvalidOperationException>(() => atlas.Add("bbb"));
        Assert.Null(atlas.RectOf("bbb"));
    });

    [Fact]
    public Task GrowingStopsAtTheLimitAndALabelThatFitsThereIsPlaced() => Headless.Run(() =>
    {
        var one = Measured("aaa");
        var atlas = Atlas(width: Widest("aaa"), initialHeight: (int)one.Height, maxHeight: (int)one.Height + 10);

        atlas.Add("aaa");

        Assert.Equal(one.Height + 10, atlas.Size.Height);
    });

    [Theory]
    [InlineData(0, 4096)]
    [InlineData(-5, 4096)]
    [InlineData(200, 100)]
    public Task AHeightOutsideTheLimitsIsRefused(int initial, int most) => Headless.Run(() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Atlas(initialHeight: initial, maxHeight: most)));
}
