using System.IO.Compression;
using System.Text;

namespace Orblit.Sprite;

/// <summary>
/// A decoded picture: RGBA8, straight alpha, row nought at the top, the same shape
/// <see cref="AtlasSprite"/> and the rest of the packer expect.
/// </summary>
public sealed record DecodedPng(long Width, long Height, byte[] Pixels);

/// <summary>A PNG this codec could not read, with why rather than a stack trace through somebody else's parser.</summary>
public sealed class PngFormatException(string message) : Exception(message);

/// <summary>
/// A small, offline PNG codec for the atlas cooker. It does not do Adam7 interlacing, or write
/// anything smaller than 8-bit truecolour with alpha. Real art tools export straightforward PNGs
/// almost always, and a cooker that refuses the rare interlaced file with a clear reason is a
/// better trade than several hundred more lines nothing packs by is likely to exercise.
/// </summary>
public static class PngCodec
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    private static ReadOnlySpan<byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];

    public static DecodedPng Decode(byte[] bytes)
    {
        if (bytes.Length < 8 || !bytes.AsSpan(0, 8).SequenceEqual(Signature))
        {
            throw new PngFormatException("not a PNG: missing signature");
        }

        var parts = ReadChunks(bytes);
        Check(parts);

        var raw = Inflate(parts.Compressed.ToArray());
        var rows = Unfilter(raw, parts);
        return new DecodedPng(parts.Width, parts.Height, ToRgba(rows, parts));
    }

    /// <summary>
    /// <paramref name="rgba"/> as an 8-bit truecolour-with-alpha PNG: one IDAT, filter type
    /// None throughout. Not the smallest a page could be, a real filter heuristic and multiple
    /// IDAT chunks would compress better, but simple enough to be sure it round-trips, which for
    /// a page an atlas depends on matters more than a few percent of file size. The compressed
    /// bytes are zlib's and not Dart's, so two encoders agree on what they decode to and not on
    /// the file.
    /// </summary>
    public static byte[] Encode(long width, long height, byte[] rgba)
    {
        if (rgba.Length != width * height * 4)
        {
            throw new ArgumentException($"rgba must hold {width} x {height} RGBA8 texels", nameof(rgba));
        }

        using var raw = new MemoryStream();
        for (long y = 0; y < height; y++)
        {
            raw.WriteByte(0); // filter type None
            raw.Write(rgba, checked((int)(y * width * 4)), checked((int)(width * 4)));
        }

        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw.ToArray());
        }

        using var output = new MemoryStream();
        output.Write(Signature);
        var header = new byte[13];
        WriteUint32(header, 0, width);
        WriteUint32(header, 4, height);
        header[8] = 8; // bit depth
        header[9] = 6; // colour type: truecolour with alpha
        WriteChunk(output, "IHDR", header);
        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static Parts ReadChunks(byte[] bytes)
    {
        var parts = new Parts();
        long offset = 8;
        while (offset + 8 <= bytes.Length)
        {
            var start = (int)offset;
            var length = ReadUint32(bytes, start);
            var type = Encoding.Latin1.GetString(bytes, start + 4, 4);
            var dataStart = start + 8;
            var dataEnd = dataStart + length;
            if (dataEnd + 4 > bytes.Length)
            {
                throw new PngFormatException("a chunk runs past the end of the file");
            }

            if (type == "IEND")
            {
                break;
            }

            var data = bytes.AsSpan(dataStart, (int)length);
            switch (type)
            {
                case "IHDR":
                    parts.ReadHeader(data);
                    break;
                case "PLTE":
                    parts.Palette = data.ToArray();
                    break;
                case "tRNS":
                    parts.Transparency = data.ToArray();
                    break;
                case "IDAT":
                    parts.Compressed.Write(data);
                    break;
            }

            offset = dataEnd + 4;
        }

        return parts;
    }

    private static void Check(Parts parts)
    {
        if (!parts.HasHeader)
        {
            throw new PngFormatException("no IHDR chunk");
        }

        if (parts.BitDepth is not (1 or 2 or 4 or 8 or 16))
        {
            throw new PngFormatException($"unsupported bit depth {parts.BitDepth}");
        }

        if (parts.ColorType is not (0 or 2 or 3 or 4 or 6))
        {
            throw new PngFormatException($"unsupported colour type {parts.ColorType}");
        }

        if (parts.ColorType == 3 && parts.Palette is null)
        {
            throw new PngFormatException("a palette image has no PLTE chunk");
        }
    }

    private static byte[] Inflate(byte[] compressed)
    {
        using var zlib = new ZLibStream(new MemoryStream(compressed), CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        return raw.ToArray();
    }

    private static byte[] Unfilter(byte[] raw, Parts parts)
    {
        var bytesPerScanline = SizeOf(((parts.Width * parts.BitsPerPixel) + 7) / 8, "scanline");
        var bpp = (parts.BitsPerPixel + 7) / 8;

        var unfiltered = new byte[SizeOf(bytesPerScanline * parts.Height, "picture")];
        var previous = new byte[bytesPerScanline];
        var read = 0;
        for (long y = 0; y < parts.Height; y++)
        {
            if (read >= raw.Length)
            {
                throw new PngFormatException("ran out of pixel data early");
            }

            var filterType = raw[read];
            read++;
            var row = new byte[bytesPerScanline];
            for (var x = 0; x < bytesPerScanline; x++)
            {
                var a = x >= bpp ? row[x - bpp] : 0;
                var c = x >= bpp ? previous[x - bpp] : 0;
                row[x] = (byte)(Filtered(filterType, raw[read + x], a, previous[x], c) & 0xff);
            }

            read += bytesPerScanline;
            row.CopyTo(unfiltered, (int)y * bytesPerScanline);
            previous = row;
        }

        return unfiltered;
    }

    // The unfiltering stays one switch, whose arms are the five filters the format defines.
    private static int Filtered(byte filterType, int raw, int a, int b, int c) => filterType switch
    {
        0 => raw,
        1 => raw + a,
        2 => raw + b,
        3 => raw + ((a + b) >> 1),
        4 => raw + Paeth(a, b, c),
        _ => throw new PngFormatException($"unsupported scanline filter {filterType}"),
    };

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc)
        {
            return a;
        }

        return pb <= pc ? b : c;
    }

    private static byte[] ToRgba(byte[] rows, Parts parts)
    {
        var pixels = new byte[SizeOf(parts.Width * parts.Height * 4, "decoded picture")];
        var bytesPerScanline = (int)(((parts.Width * parts.BitsPerPixel) + 7) / 8);
        for (var y = 0; y < parts.Height; y++)
        {
            var rowStart = y * bytesPerScanline;
            for (var x = 0; x < parts.Width; x++)
            {
                var texel = new Texel(rows, rowStart, x * parts.BitsPerPixel, parts);
                texel.WriteTo(pixels, (int)(((y * parts.Width) + x) * 4));
            }
        }

        return pixels;
    }

    private static int SizeOf(long count, string what) => count <= Array.MaxLength
        ? (int)count
        : throw new PngFormatException($"the {what} is too large to hold");

    private static uint ReadUint32(byte[] bytes, int offset) =>
        ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];

    private static void WriteUint32(byte[] bytes, int offset, long value)
    {
        bytes[offset] = (byte)((value >> 24) & 0xff);
        bytes[offset + 1] = (byte)((value >> 16) & 0xff);
        bytes[offset + 2] = (byte)((value >> 8) & 0xff);
        bytes[offset + 3] = (byte)(value & 0xff);
    }

    private static void WriteChunk(MemoryStream output, string type, byte[] data)
    {
        var length = new byte[4];
        WriteUint32(length, 0, data.Length);
        output.Write(length);

        var typeAndData = new byte[4 + data.Length];
        Encoding.Latin1.GetBytes(type, typeAndData);
        data.CopyTo(typeAndData, 4);
        output.Write(typeAndData);

        var crc = new byte[4];
        WriteUint32(crc, 0, Crc32(typeAndData));
        output.Write(crc);
    }

    // The CRC-32 used by every PNG chunk (ISO/IEC 15948, RFC 2083 Annex D), built once.
    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xedb88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    private static uint Crc32(byte[] bytes)
    {
        var crc = 0xffffffff;
        foreach (var value in bytes)
        {
            crc = CrcTable[(crc ^ value) & 0xff] ^ (crc >> 8);
        }

        return crc ^ 0xffffffff;
    }

    /// <summary>What the chunks said, gathered before any of it is checked.</summary>
    private sealed class Parts
    {
        public long Width { get; private set; }

        public long Height { get; private set; }

        public int BitDepth { get; private set; }

        public int ColorType { get; private set; }

        public bool HasHeader { get; private set; }

        public byte[]? Palette { get; set; }

        public byte[]? Transparency { get; set; }

        public MemoryStream Compressed { get; } = new();

        public int Channels => ColorType switch
        {
            0 => 1,
            2 => 3,
            3 => 1,
            4 => 2,
            _ => 4,
        };

        public int BitsPerPixel => Channels * BitDepth;

        public void ReadHeader(ReadOnlySpan<byte> data)
        {
            if (data.Length != 13)
            {
                throw new PngFormatException("IHDR is the wrong size");
            }

            Width = ReadUint32(data);
            Height = ReadUint32(data[4..]);
            BitDepth = data[8];
            ColorType = data[9];
            HasHeader = true;
            if (data[12] != 0)
            {
                throw new PngFormatException("interlaced PNGs are not supported by this cooker");
            }
        }

        private static uint ReadUint32(ReadOnlySpan<byte> data) =>
            ((uint)data[0] << 24) | ((uint)data[1] << 16) | ((uint)data[2] << 8) | data[3];
    }

    /// <summary>One texel's samples, read at a bit offset from a decoded row and widened to RGBA8.</summary>
    private readonly struct Texel(byte[] rows, int rowStart, long bitOffset, Parts parts)
    {
        public void WriteTo(byte[] pixels, int at)
        {
            var (red, green, blue, alpha) = Read();
            pixels[at] = red;
            pixels[at + 1] = green;
            pixels[at + 2] = blue;
            pixels[at + 3] = alpha;
        }

        private (byte Red, byte Green, byte Blue, byte Alpha) Read()
        {
            var depth = parts.BitDepth;
            switch (parts.ColorType)
            {
                case 0:
                    var gray = Sample(0);
                    var keyed = parts.Transparency is { Length: >= 2 } trns && gray == Key(trns, 0);
                    return (To8(gray), To8(gray), To8(gray), keyed ? (byte)0 : (byte)255);
                case 2:
                    var (r, g, b) = (Sample(0), Sample(depth), Sample(depth * 2));
                    return (To8(r), To8(g), To8(b), Keyed(r, g, b));
                case 3:
                    return Indexed(Sample(0));
                case 4:
                    var (value, a) = (Sample(0), Sample(depth));
                    return (To8(value), To8(value), To8(value), To8(a));
                default:
                    return (To8(Sample(0)), To8(Sample(depth)), To8(Sample(depth * 2)), To8(Sample(depth * 3)));
            }
        }

        private (byte Red, byte Green, byte Blue, byte Alpha) Indexed(int index)
        {
            var palette = parts.Palette!; // Check refuses a palette picture with no PLTE.
            var alpha = parts.Transparency is { } keys && index < keys.Length ? keys[index] : (byte)255;
            if (index * 3 + 2 >= palette.Length)
            {
                return (0, 0, 0, alpha);
            }

            return (palette[index * 3], palette[(index * 3) + 1], palette[(index * 3) + 2], alpha);
        }

        private byte Keyed(int r, int g, int b) =>
            parts.Transparency is { Length: >= 6 } trns && r == Key(trns, 0) && g == Key(trns, 2) && b == Key(trns, 4)
                ? (byte)0
                : (byte)255;

        private static int Key(byte[] trns, int at) => (trns[at] << 8) | trns[at + 1];

        private int Sample(int extraBits)
        {
            var bit = bitOffset + extraBits;
            var byteIndex = rowStart + (int)(bit / 8);
            var depth = parts.BitDepth;
            if (depth == 16)
            {
                return (rows[byteIndex] << 8) | rows[byteIndex + 1];
            }

            if (depth == 8)
            {
                return rows[byteIndex];
            }

            var shift = 8 - (int)(bit % 8) - depth;
            return (rows[byteIndex] >> shift) & ((1 << depth) - 1);
        }

        private byte To8(int value) => parts.BitDepth switch
        {
            8 => (byte)value,
            16 => (byte)(value >> 8),
            _ => (byte)(value * 255 / ((1 << parts.BitDepth) - 1)),
        };
    }
}
