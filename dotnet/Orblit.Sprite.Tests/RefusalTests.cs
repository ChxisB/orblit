using System.IO.Compression;

namespace Orblit.Sprite.Tests;

/// <summary>
/// What the port refuses that the fixture exporter only asserted. An assert is off outside tests, and the port
/// checks every time, so none of these have a fixture twin.
/// </summary>
public class RefusalTests
{
    private static AtlasSprite Opaque(string name, long size) => new(name, size, size, Enumerable.Repeat((byte)255, (int)(size * size * 4)).ToArray());

    [Fact]
    public void AnUndefinedHeuristicIsRefusedRatherThanGuessed()
    {
        var options = new AtlasPackOptions(heuristic: (MaxRectsHeuristic)99);
        Assert.ThrowsAny<InvalidOperationException>(() => AtlasPacker.PackAtlas([Opaque("a", 4)], options));
    }

    [Theory]
    [InlineData("maxPageSize")]
    [InlineData("minPageSize")]
    [InlineData("padding")]
    [InlineData("border")]
    [InlineData("extrude")]
    public void OptionsRefuseASizeThatCannotHoldAPage(string which)
    {
        var bad = which is "maxPageSize" or "minPageSize" ? 0 : -1;
        var made = () => new AtlasPackOptions(
            maxPageSize: which == "maxPageSize" ? bad : 2048,
            minPageSize: which == "minPageSize" ? bad : 64,
            padding: which == "padding" ? bad : 2,
            border: which == "border" ? bad : 0,
            extrude: which == "extrude" ? bad : 1);
        Assert.ThrowsAny<ArgumentOutOfRangeException>(made);
    }

    [Fact]
    public void ASpriteWhosePixelsDoNotMatchItsSizeIsRefused()
    {
        Assert.ThrowsAny<ArgumentException>(() => new AtlasSprite("a", 2, 2, new byte[15]));
    }

    [Fact]
    public void ASpriteWithANegativeSideIsRefusedEvenWhenTheByteCountAddsUp()
    {
        var error = Record.Exception(() => new AtlasSprite("a", -3, -4, new byte[48]));
        Assert.True(error is ArgumentOutOfRangeException, $"got {error?.GetType().Name ?? "nothing"}");
    }

    [Fact]
    public void APngThatClaimsAnEnormousSizeIsRefusedWithAFormatError()
    {
        var header = new byte[13];
        WriteBigEndian(header, 0, int.MaxValue);
        WriteBigEndian(header, 4, int.MaxValue);
        header[8] = 8;
        header[9] = 6;
        using var memory = new MemoryStream();
        using (var zlib = new ZLibStream(memory, CompressionMode.Compress, leaveOpen: true))
        {
            zlib.WriteByte(0);
        }

        var png = Concat(
            [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            Chunk("IHDR", header),
            Chunk("IDAT", memory.ToArray()),
            Chunk("IEND", []));

        var error = Record.Exception(() => PngCodec.Decode(png));
        Assert.True(error is PngFormatException, $"got {error?.GetType().Name ?? "nothing"}");
    }

    private static void WriteBigEndian(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }

    // The decoder does not read the checksum, so a zero one is enough here.
    private static byte[] Chunk(string type, byte[] data)
    {
        var chunk = new byte[12 + data.Length];
        WriteBigEndian(chunk, 0, data.Length);
        System.Text.Encoding.ASCII.GetBytes(type, 0, 4, chunk, 4);
        data.CopyTo(chunk, 8);
        return chunk;
    }

    private static byte[] Concat(params byte[][] parts) => [.. parts.SelectMany(part => part)];
}
