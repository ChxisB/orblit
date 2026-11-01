namespace Orblit.Sprite.Tests;

public class PngCodecTests
{
    private static byte[] Pattern(int w, int h, byte alphaAt = 255)
    {
        var output = new byte[w * h * 4];
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var i = ((y * w) + x) * 4;
                output[i] = (byte)(((x * 17) + 3) % 256);
                output[i + 1] = (byte)(((y * 53) + 11) % 256);
                output[i + 2] = (byte)((x + y) % 256);
                output[i + 3] = alphaAt;
            }
        }

        return output;
    }

    // the offline PNG codec

    [Fact]
    public void EncodingThenDecodingGivesBackTheSamePixels()
    {
        var pixels = Pattern(13, 9);
        var bytes = PngCodec.Encode(13, 9, pixels);
        var decoded = PngCodec.Decode(bytes);
        Assert.Equal(13, decoded.Width);
        Assert.Equal(9, decoded.Height);
        Assert.Equal(pixels, decoded.Pixels);
    }

    [Fact]
    public void PartialTransparencyRoundTripsExactly()
    {
        var pixels = Pattern(6, 6, alphaAt: 40);
        var decoded = PngCodec.Decode(PngCodec.Encode(6, 6, pixels));
        Assert.Equal(pixels, decoded.Pixels);
    }

    [Fact]
    public void AOneByOneImageRoundTrips()
    {
        byte[] pixels = [10, 20, 30, 40];
        var decoded = PngCodec.Decode(PngCodec.Encode(1, 1, pixels));
        Assert.Equal(pixels, decoded.Pixels);
    }

    [Fact]
    public void SomethingThatIsNotAPngIsRefusedWithAReason()
    {
        Assert.Throws<PngFormatException>(() => PngCodec.Decode("not a png"u8.ToArray()));
    }

    [Fact]
    public void MismatchedPixelDataIsRefusedRatherThanWritingABadFile()
    {
        Assert.ThrowsAny<ArgumentException>(() => PngCodec.Encode(4, 4, new byte[10]));
    }
}
