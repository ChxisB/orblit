using System.Buffers.Binary;

namespace Orblit.Asset.Tests;

// KTX2 files small enough to build in a test.
internal static class Ktx2Files
{
    private static readonly byte[] Identifier = [0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>A header and an empty level index, enough to say how big the picture is.</summary>
    public static byte[] Plain(int width, int height, int levels = 1, int faces = 1, int layers = 1)
    {
        var bytes = new byte[80 + ((levels == 0 ? 1 : levels) * 24)];
        Identifier.CopyTo(bytes, 0);
        Put32(bytes, 12, 1); // vkFormat
        Put32(bytes, 16, 1); // typeSize
        Put32(bytes, 20, width);
        Put32(bytes, 24, height);
        Put32(bytes, 28, 0); // pixelDepth
        Put32(bytes, 32, layers);
        Put32(bytes, 36, faces);
        Put32(bytes, 40, levels);
        return bytes;
    }

    /// <summary>
    /// A file with a mip chain, laid out the way the cooker lays one out: the smallest level at the front of
    /// the data, the largest at the back, the format description and key-value data ahead of both.
    /// </summary>
    // F2: a file is described by its shape, and every part is named at the call.
    public static byte[] Mipped(
        int width = 256,
        int height = 256,
        int levels = 9,
        int supercompression = 2,
        int describe = 44,
        int keyValues = 20,
        bool metadataLast = false)
    {
        var indexEnd = 80 + (levels * 24);
        var kvLength = metadataLast ? 0 : keyValues;

        // Smallest first, which is what makes the front of the file worth fetching.
        var offsets = new int[levels];
        var lengths = new int[levels];
        var at = metadataLast ? indexEnd : indexEnd + describe + kvLength;
        for (var level = levels - 1; level >= 0; level--)
        {
            lengths[level] = LengthOf(width, height, level);
            offsets[level] = at;
            at += lengths[level];
        }

        var dfdAt = metadataLast ? at : indexEnd;
        var bytes = new byte[metadataLast ? at + describe : at];
        Identifier.CopyTo(bytes, 0);
        Put32(bytes, 12, 0); // vkFormat: Basis Universal.
        Put32(bytes, 16, 1); // typeSize.
        Put32(bytes, 20, width);
        Put32(bytes, 24, height);
        Put32(bytes, 28, 0); // Not a 3D texture.
        Put32(bytes, 32, 0); // Not an array.
        Put32(bytes, 36, 1); // One face.
        Put32(bytes, 40, levels);
        Put32(bytes, 44, supercompression);
        Put32(bytes, 48, dfdAt);
        Put32(bytes, 52, describe);
        Put32(bytes, 56, metadataLast ? 0 : indexEnd + describe);
        Put32(bytes, 60, kvLength);
        WriteLevels(bytes, offsets, lengths);

        bytes.AsSpan(dfdAt, describe).Fill(0xD0);
        bytes.AsSpan(indexEnd + describe, kvLength).Fill(0xCE);
        return bytes;
    }

    private static void WriteLevels(byte[] bytes, int[] offsets, int[] lengths)
    {
        for (var level = 0; level < offsets.Length; level++)
        {
            var entry = 80 + (level * 24);
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(entry), offsets[level]);
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(entry + 8), lengths[level]);
            BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(entry + 16), lengths[level] * 4L);

            // One byte value per level, so a prefix can be checked byte for byte.
            bytes.AsSpan(offsets[level], lengths[level]).Fill((byte)(level + 1));
        }
    }

    private static int LengthOf(int width, int height, int level) =>
        Math.Max((Math.Max(width >> level, 1) * Math.Max(height >> level, 1)) / 8, 8);

    private static void Put32(byte[] bytes, int at, int value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), (uint)value);
}
