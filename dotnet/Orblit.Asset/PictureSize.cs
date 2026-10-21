using System.Text;

namespace Orblit.Asset;

/// <summary>How big a picture is, read from its header without decoding it.</summary>
/// <param name="Width">Pixels across.</param>
/// <param name="Height">Pixels down.</param>
/// <param name="Depth">Slices, for a 3D texture. One for everything else.</param>
/// <param name="Layers">Array slices, for a texture array. One for everything else.</param>
/// <param name="Faces">Six for a cube map, one otherwise.</param>
/// <param name="Levels">Mip levels stored in the file. One when the file holds only the full-size picture.</param>
public sealed record PictureSize(long Width, long Height, long Depth = 1, long Layers = 1, long Faces = 1, long Levels = 1)
{
    // Past this many levels every level is one pixel, which is the same sum added in one step.
    private const int LevelsWorthWalking = 64;

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Riff = [0x52, 0x49, 0x46, 0x46];
    private static readonly byte[] Webp = [0x57, 0x45, 0x42, 0x50];
    private static readonly byte[] Ktx2 = [0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Every pixel the file will decode to, mips, layers and faces included.
    /// </summary>
    /// <remarks>
    /// The number that matters for memory. A 4096 cube map with mips is fifty times the pixels its width and
    /// height suggest, and a limit that looked at width and height alone would wave it through.
    /// </remarks>
    public long Pixels
    {
        get
        {
            long total = 0;
            var walked = Math.Min(Levels, LevelsWorthWalking);
            for (var level = 0; level < walked; level++)
            {
                total += OrOne(Width >> level) * OrOne(Height >> level) * OrOne(Depth >> level);
            }

            if (Levels > LevelsWorthWalking)
            {
                total += Levels - LevelsWorthWalking;
            }

            return total * Layers * Faces;
        }
    }

    public override string ToString()
    {
        var size = Depth > 1 ? $"{Width}×{Height}×{Depth}" : $"{Width}×{Height}";
        var extra = new List<string>();
        if (Levels > 1)
        {
            extra.Add($"{Levels} levels");
        }

        if (Layers > 1)
        {
            extra.Add($"{Layers} layers");
        }

        if (Faces > 1)
        {
            extra.Add($"{Faces} faces");
        }

        return extra.Count == 0 ? size : $"{size}, {string.Join(", ", extra)}";
    }

    /// <summary>
    /// How big the picture in <paramref name="bytes"/> is, or null when this is not a picture in a format that
    /// can be read this way.
    /// </summary>
    /// <remarks>
    /// <b>Header only.</b> Nothing here allocates in proportion to the picture, walks the pixel data or hands
    /// anything to a decoder, which is the whole point: it is the check that runs <i>before</i> a downloaded
    /// file is decoded, on bytes nobody has vouched for yet. A picture whose dimensions would fill memory is a
    /// few dozen bytes on the wire, and by the time a decoder has been asked how big it is, it has usually
    /// already tried to allocate it.
    /// <para>
    /// Null means "this cannot be checked", not "this is safe". A caller enforcing a limit has to decide what
    /// to do about a format it cannot read, and saying so with null rather than a reassuring zero is how it
    /// gets that choice.
    /// </para>
    /// </remarks>
    public static PictureSize? Of(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(Png))
        {
            return PngSize(bytes);
        }

        if (bytes.StartsWith(Ktx2))
        {
            return Ktx2Size(bytes);
        }

        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            return JpegSize(bytes);
        }

        return bytes.StartsWith(Riff) && bytes.Length >= 12 && bytes[8..].StartsWith(Webp) ? WebpSize(bytes) : null;
    }

    // IHDR is required to be the first chunk, so its place is fixed: eight bytes of signature, four of length,
    // four of type, then the two dimensions.
    private static PictureSize? PngSize(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 24 || !bytes[12..16].SequenceEqual("IHDR"u8))
        {
            return null;
        }

        var width = Big32(bytes, 16);
        var height = Big32(bytes, 20);
        return width <= 0 || height <= 0 ? null : new PictureSize(width, height);
    }

    // KTX2 puts everything needed in a fixed header, little-endian, and is the only format here that can hold
    // mips, layers and faces, so it is the one where reading past width and height matters.
    private static PictureSize? Ktx2Size(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 48)
        {
            return null;
        }

        var width = Little32(bytes, 20);
        var height = Little32(bytes, 24);
        return width <= 0
            ? null
            : new PictureSize(
                width,

                // A 1D texture writes nought for height and depth, meaning "not that many, none at all":
                // one row, and one slice.
                OrOne(height),
                OrOne(Little32(bytes, 28)),
                OrOne(Little32(bytes, 32)),
                OrOne(Little32(bytes, 36)),
                OrOne(Little32(bytes, 40)));
    }

    // Walks JPEG's segments to the frame header, which is the only segment that states the size and can sit
    // behind any amount of metadata. A phone's EXIF thumbnail alone is tens of kilobytes.
    private static PictureSize? JpegSize(ReadOnlySpan<byte> bytes)
    {
        var at = 2;
        while (at + 4 <= bytes.Length)
        {
            if (bytes[at] != 0xFF)
            {
                return null;
            }

            var (marker, next) = SkipFill(bytes, at);

            // Standalone markers carry no length: restarts, and start-of-image.
            if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD9))
            {
                at = next;
                continue;
            }

            if (next + 2 > bytes.Length)
            {
                return null;
            }

            var length = Big16(bytes, next);
            if (length < 2)
            {
                return null;
            }

            if (IsFrame(marker))
            {
                return FrameSize(bytes, next);
            }

            // Start of scan: the pixel data begins, and there is no frame header behind it. Stop rather than
            // walk into the entropy-coded bytes, where anything can look like a marker.
            if (marker == 0xDA)
            {
                return null;
            }

            at = next + length;
        }

        return null;
    }

    // Fill bytes: any number of 0xFF may pad the gap before a marker.
    private static (int Marker, int Next) SkipFill(ReadOnlySpan<byte> bytes, int at)
    {
        int marker = bytes[at + 1];
        var next = at + 2;
        while (marker == 0xFF && next < bytes.Length)
        {
            marker = bytes[next];
            next++;
        }

        return (marker, next);
    }

    // Every frame marker states the size the same way. C4, C8 and CC are the three in this range that are not
    // frames, a Huffman table, an extension and an arithmetic-coding table, and reading a size out of one
    // would give a number that looks plausible.
    private static bool IsFrame(int marker) => marker is >= 0xC0 and <= 0xCF && marker is not (0xC4 or 0xC8 or 0xCC);

    private static PictureSize? FrameSize(ReadOnlySpan<byte> bytes, int next)
    {
        if (next + 7 > bytes.Length)
        {
            return null;
        }

        var height = Big16(bytes, next + 3);
        var width = Big16(bytes, next + 5);
        return width <= 0 || height <= 0 ? null : new PictureSize(width, height);
    }

    // WebP is three formats behind one signature, and each states its size differently and in bit fields
    // rather than bytes.
    private static PictureSize? WebpSize(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 16)
        {
            return null;
        }

        return Encoding.Latin1.GetString(bytes[12..16]) switch
        {
            "VP8 " => LossyWebpSize(bytes),
            "VP8L" => LosslessWebpSize(bytes),
            "VP8X" => ExtendedWebpSize(bytes),
            _ => null,
        };
    }

    // A lossy frame: three bytes of frame tag, a three-byte sync code, then fourteen bits each of width and
    // height with a two-bit scale above.
    private static PictureSize? LossyWebpSize(ReadOnlySpan<byte> bytes) =>
        bytes.Length < 30 || bytes[23] != 0x9D || bytes[24] != 0x01 || bytes[25] != 0x2A
            ? null
            : new PictureSize(Little16(bytes, 26) & 0x3FFF, Little16(bytes, 28) & 0x3FFF);

    // Lossless: a one-byte signature, then fourteen bits of width minus one and fourteen of height minus one,
    // packed across four bytes.
    private static PictureSize? LosslessWebpSize(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 25 || bytes[20] != 0x2F)
        {
            return null;
        }

        var packed = Little32(bytes, 21);
        return new PictureSize((packed & 0x3FFF) + 1, ((packed >> 14) & 0x3FFF) + 1);
    }

    // Extended: the canvas size, three bytes each, minus one. This is the one that matters for a limit, since
    // an animation or an alpha-plus-colour file states its real size only here.
    private static PictureSize? ExtendedWebpSize(ReadOnlySpan<byte> bytes) =>
        bytes.Length < 30 ? null : new PictureSize(Little24(bytes, 24) + 1, Little24(bytes, 27) + 1);

    private static long OrOne(long value) => value < 1 ? 1 : value;

    private static int Big16(ReadOnlySpan<byte> b, int at) => (b[at] << 8) | b[at + 1];

    private static long Big32(ReadOnlySpan<byte> b, int at) =>
        ((long)b[at] << 24) | ((long)b[at + 1] << 16) | ((long)b[at + 2] << 8) | b[at + 3];

    private static int Little16(ReadOnlySpan<byte> b, int at) => b[at] | (b[at + 1] << 8);

    private static int Little24(ReadOnlySpan<byte> b, int at) => b[at] | (b[at + 1] << 8) | (b[at + 2] << 16);

    private static long Little32(ReadOnlySpan<byte> b, int at) =>
        b[at] | ((long)b[at + 1] << 8) | ((long)b[at + 2] << 16) | ((long)b[at + 3] << 24);
}
