using System.Buffers.Binary;
using System.Text;

namespace Orblit.Asset.Tests;

public class NetPictureSizeTests
{
    // what a picture says it is
    [Fact]
    public void APNGFromItsIHDR()
    {
        var size = PictureSize.Of(Png(1920, 1080))!;
        Assert.Equal(1920, size.Width);
        Assert.Equal(1080, size.Height);
        Assert.Equal(1920 * 1080, size.Pixels);
    }

    // Two segments to walk past first, because a reader that assumes the frame comes straight after the marker
    // works on the file it was written against and nothing else.
    [Fact]
    public void AJPEGFromTheFrameHeaderAfterTheSegmentsBeforeIt()
    {
        var size = PictureSize.Of(Jpeg(800, 600, before: 2))!;
        Assert.Equal(800, size.Width);
        Assert.Equal(600, size.Height);
    }

    [Fact]
    public void AProgressiveJPEGWhoseFrameMarkerIsNotC0()
    {
        var size = PictureSize.Of(Jpeg(640, 480, frame: 0xc2))!;
        Assert.Equal(640, size.Width);
        Assert.Equal(480, size.Height);
    }

    [Fact]
    public void ALossyWebP()
    {
        var size = PictureSize.Of(WebpLossy(1254, 1254))!;
        Assert.Equal(1254, size.Width);
        Assert.Equal(1254, size.Height);
    }

    [Fact]
    public void ALosslessWebPWhoseSizeIsPackedAcrossBytes()
    {
        var size = PictureSize.Of(WebpLossless(1254, 1254))!;
        Assert.Equal(1254, size.Width);
        Assert.Equal(1254, size.Height);
    }

    [Fact]
    public void AnExtendedWebPFromTheCanvas()
    {
        var size = PictureSize.Of(WebpExtended(1254, 1254))!;
        Assert.Equal(1254, size.Width);
        Assert.Equal(1254, size.Height);
    }

    [Fact]
    public void AKTX2()
    {
        var size = PictureSize.Of(Ktx2Files.Plain(2048, 2048))!;
        Assert.Equal(2048, size.Width);
        Assert.Equal(2048, size.Height);
        Assert.Equal(1, size.Levels);
        Assert.Equal(2048 * 2048, size.Pixels);
    }

    // what it would cost to decode

    // The reason this is not width × height: a mipped texture is about a third larger again, and a limit that
    // ignores the chain is a limit that lets a third more through than it says.
    [Fact]
    public void CountsEveryMipLevelNotJustTheBiggest()
    {
        var size = PictureSize.Of(Ktx2Files.Plain(256, 256, levels: 9))!;
        Assert.Equal(9, size.Levels);
        var expected = 0;
        for (var at = 0; at < 9; at++)
        {
            expected += (256 >> at) * (256 >> at);
        }

        Assert.Equal(expected, size.Pixels);
    }

    [Fact]
    public void CountsAllSixFacesOfACubeMap()
    {
        var size = PictureSize.Of(Ktx2Files.Plain(512, 512, faces: 6))!;
        Assert.Equal(6, size.Faces);
        Assert.Equal(512 * 512 * 6, size.Pixels);
    }

    [Fact]
    public void CountsEverySliceOfAnArray()
    {
        var size = PictureSize.Of(Ktx2Files.Plain(64, 64, layers: 4))!;
        Assert.Equal(4, size.Layers);
        Assert.Equal(64 * 64 * 4, size.Pixels);
    }

    // KTX2 writes nought for "not an array" and "not a cube map", which is a fine way to say it and a terrible
    // number to multiply by.
    [Fact]
    public void AZeroInTheHeaderMeansOne()
    {
        var size = PictureSize.Of(Ktx2Files.Plain(32, 32, layers: 0, faces: 0, levels: 0))!;
        Assert.Equal(32 * 32, size.Pixels);
    }

    // what it cannot tell
    [Fact]
    public void SaysNothingRatherThanGuessingAtAnUnknownFormat() =>
        Assert.Null(PictureSize.Of(Encoding.UTF8.GetBytes("glTF not a picture")));

    [Fact]
    public void SaysNothingForAHeaderThatStopsPartway() => Assert.Null(PictureSize.Of(Png(1024, 1024).AsSpan(0, 18)));

    [Fact]
    public void SaysNothingForAnEmptyFile() => Assert.Null(PictureSize.Of([]));

    [Fact]
    public void SaysNothingForAKTX1WhichIsADifferentFormat()
    {
        var ktx1 = new byte[64];
        new byte[] { 0xab, 0x4b, 0x54, 0x58, 0x20, 0x31, 0x31, 0xbb }.CopyTo(ktx1, 0);
        Assert.Null(PictureSize.Of(ktx1));
    }

    private static byte[] Png(int width, int height)
    {
        var bytes = new byte[64];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), 13);
        Encoding.UTF8.GetBytes("IHDR").CopyTo(bytes, 12);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), (uint)height);
        return bytes;
    }

    // A JPEG with `before` segments ahead of the frame, which uses `frame` as its marker.
    private static byte[] Jpeg(int width, int height, int before = 0, int frame = 0xc0)
    {
        var bytes = new List<byte> { 0xff, 0xd8 };
        for (var at = 0; at < before; at++)
        {
            bytes.AddRange([0xff, 0xe0, 0x00, 0x10]);
            bytes.AddRange(new byte[14]);
        }

        bytes.AddRange([0xff, (byte)frame, 0x00, 0x11, 0x08, (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width]);
        bytes.AddRange([0xff, 0xda]);
        return [.. bytes];
    }

    private static byte[] Riff(byte[] payload)
    {
        var bytes = new byte[12 + payload.Length];
        Encoding.UTF8.GetBytes("RIFF").CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), (uint)(4 + payload.Length));
        Encoding.UTF8.GetBytes("WEBP").CopyTo(bytes, 8);
        payload.CopyTo(bytes, 12);
        return bytes;
    }

    private static byte[] WebpLossy(int width, int height)
    {
        var chunk = new byte[32];
        Encoding.UTF8.GetBytes("VP8 ").CopyTo(chunk, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(4), 20);

        // Three bytes of frame tag, then the sync code, then the sizes.
        new byte[] { 0x9d, 0x01, 0x2a }.CopyTo(chunk, 11);
        BinaryPrimitives.WriteUInt16LittleEndian(chunk.AsSpan(14), (ushort)width);
        BinaryPrimitives.WriteUInt16LittleEndian(chunk.AsSpan(16), (ushort)height);
        return Riff(chunk);
    }

    private static byte[] WebpLossless(int width, int height)
    {
        var chunk = new byte[24];
        Encoding.UTF8.GetBytes("VP8L").CopyTo(chunk, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(4), 16);
        chunk[8] = 0x2f;

        // Fourteen bits each, one less than the size, packed little-endian.
        BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(9), (uint)((width - 1) | ((height - 1) << 14)));
        return Riff(chunk);
    }

    private static byte[] WebpExtended(int width, int height)
    {
        var chunk = new byte[24];
        Encoding.UTF8.GetBytes("VP8X").CopyTo(chunk, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(4), 10);

        // Twenty-four bits each, one less than the size.
        for (var at = 0; at < 3; at++)
        {
            chunk[12 + at] = (byte)((width - 1) >> (8 * at));
            chunk[15 + at] = (byte)((height - 1) >> (8 * at));
        }

        return Riff(chunk);
    }
}
