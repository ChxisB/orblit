using System.Buffers.Binary;

namespace Orblit.Asset;

/// <summary>A KTX2 file's mip chain, read from the front of the file.</summary>
/// <remarks>
/// The point of reading it is that <b>KTX2 stores levels smallest first</b>. Level 0, the full-size picture
/// and almost all of the bytes, sits at the far end of the file, and the 1×1 level sits at the front. A
/// prefix of the file is therefore a complete set of small mip levels, which is exactly what there is to draw
/// while the rest of it arrives. A 1254×1254 texture cooked here is 782 kB, and its first 216 kB hold every
/// level down to 627×627.
/// <para>
/// Nothing here decodes a pixel. It reads the header and the level index, the first few hundred bytes, and
/// rewrites them, so the cost is the same whatever the texture's size.
/// </para>
/// </remarks>
public sealed class Ktx2Chain
{
    // The header, the four offsets and two lengths after it, and then the level index.
    private const int IndexStart = 80;
    private const int EntryLength = 24;

    private static readonly byte[] Identifier = [0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A];

    private Ktx2Chain(long width, long height, long depth, Ktx2Level[] levels, long supercompression, long metadataEnd)
    {
        Width = width;
        Height = height;
        Depth = depth;
        Levels = levels;
        Supercompression = supercompression;
        MetadataEnd = metadataEnd;
    }

    /// <summary>Level 0's width.</summary>
    public long Width { get; }

    /// <summary>Level 0's height.</summary>
    public long Height { get; }

    /// <summary>Nought for a 2D texture, which is nearly all of them.</summary>
    public long Depth { get; }

    /// <summary>Largest first, the order the level index is written in.</summary>
    public IReadOnlyList<Ktx2Level> Levels { get; }

    public long Supercompression { get; }

    /// <summary>
    /// The byte after the last of the header, the level index, the format description and the key-value data,
    /// where the level bytes begin.
    /// </summary>
    public long MetadataEnd { get; }

    /// <summary>
    /// Reads the chain out of the front of a file, or answers null.
    /// </summary>
    /// <remarks>
    /// Null is the ordinary answer, not a failure: it means there is no shorter version of this picture worth
    /// waiting for. The bytes are not a KTX2, not enough of them have arrived yet to say, there is only one
    /// level, or the file is laid out in a way this cannot safely shorten.
    /// </remarks>
    public static Ktx2Chain? Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < IndexStart || !bytes.StartsWith(Identifier))
        {
            return null;
        }

        var count = U32(bytes, 40);
        var supercompression = U32(bytes, 44);

        // One level is the whole picture, and nought means the levels are built on the device: either way
        // there is nothing smaller in the file.
        if (count < 2)
        {
            return null;
        }

        // A file whose levels are stored raw must keep each one aligned to its texel block, and shortening
        // the index moves every level. Everything this project cooks is supercompressed, where the required
        // alignment is one byte, so the shift is always safe. A raw file is left alone rather than guessed at.
        if (supercompression == 0)
        {
            return null;
        }

        var indexEnd = IndexStart + (count * EntryLength);
        if (bytes.Length < indexEnd)
        {
            return null;
        }

        var levels = ReadLevels(bytes, count, indexEnd);
        var metadataEnd = levels is null ? null : MetadataEndOf(bytes, indexEnd);
        return metadataEnd is { } end && levels!.TrueForAll(level => level.Offset >= end)
            ? new Ktx2Chain(U32(bytes, 20), U32(bytes, 24), U32(bytes, 28), [.. levels], supercompression, end)
            : null;
    }

    /// <summary>
    /// The coarsest level whose longest side is still at least <paramref name="size"/>, or null if there is no
    /// level worth stopping at.
    /// </summary>
    /// <remarks>
    /// Counted from level 0, so 0 is the full-size picture and the answer is how many levels would be
    /// skipped. Never the last level, since a 1×1 stand-in is not worth a stage, and never level 0, which is
    /// the whole download.
    /// </remarks>
    public int? LevelAtLeast(long size)
    {
        for (var level = Levels.Count - 2; level >= 1; level--)
        {
            if (Shrink(Width, level) >= size || Shrink(Height, level) >= size)
            {
                return level;
            }
        }

        return null;
    }

    /// <summary>How many bytes of the file are needed to hold every level from <paramref name="level"/> down to the smallest.</summary>
    /// <remarks>
    /// The largest of them decides it. Taken as a maximum over the levels kept rather than from the level
    /// alone, so a file written in some other order gives an honest answer. A useless one, being most of the
    /// file, which is then simply never worth fetching early.
    /// </remarks>
    public long BytesFor(int level)
    {
        var end = MetadataEnd;
        for (var i = level; i < Levels.Count; i++)
        {
            end = Math.Max(end, Levels[i].End);
        }

        return end;
    }

    /// <summary>
    /// Builds a whole, valid KTX2 holding levels <paramref name="level"/> and smaller, out of the first
    /// <see cref="BytesFor"/> bytes of the file.
    /// </summary>
    /// <remarks>
    /// The prefix is not a KTX2 by itself: its header still claims levels that are not there. This writes the
    /// same bytes with a header and a level index that describe what actually arrived. Dropping the entries
    /// for the levels left behind moves everything after them, and every offset in the file moves by that same
    /// amount, which keeps the four-byte and eight-byte alignments the format requires, since an entry is
    /// twenty-four bytes.
    /// <para>Returns null if <paramref name="bytes"/> is shorter than the prefix needs to be.</para>
    /// </remarks>
    public byte[]? Prefix(ReadOnlySpan<byte> bytes, int level)
    {
        if (level < 1 || level >= Levels.Count)
        {
            return null;
        }

        var end = BytesFor(level);
        if (bytes.Length < end)
        {
            return null;
        }

        var shift = level * EntryLength;
        var kept = Levels.Count - level;
        var output = new byte[end - shift];

        // The header and the six numbers after it, then patched.
        bytes[..IndexStart].CopyTo(output);
        PatchHeader(bytes, output, level, kept, shift);
        for (var i = 0; i < kept; i++)
        {
            var was = Levels[level + i];
            var at = IndexStart + (i * EntryLength);
            BinaryPrimitives.WriteInt64LittleEndian(output.AsSpan(at), was.Offset - shift);
            BinaryPrimitives.WriteInt64LittleEndian(output.AsSpan(at + 8), was.Length);
            BinaryPrimitives.WriteInt64LittleEndian(output.AsSpan(at + 16), was.UncompressedLength);
        }

        // Everything from the old index's end to the prefix's end, verbatim: the format description, the
        // key-value data and the level bytes themselves, none of which cares where in the file it sits.
        var from = IndexStart + (kept * EntryLength);
        bytes.Slice(IndexStart + (Levels.Count * EntryLength), output.Length - from).CopyTo(output.AsSpan(from));
        return output;
    }

    private void PatchHeader(ReadOnlySpan<byte> bytes, Span<byte> output, int level, int kept, int shift)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(output[20..], (uint)Shrink(Width, level));
        BinaryPrimitives.WriteUInt32LittleEndian(output[24..], (uint)Shrink(Height, level));
        BinaryPrimitives.WriteUInt32LittleEndian(output[28..], (uint)Shrink(Depth, level));
        BinaryPrimitives.WriteUInt32LittleEndian(output[40..], (uint)kept);

        // Where nothing was, nothing is: an absent part keeps its nought rather than being shifted into
        // pointing at something.
        Move32(bytes, output, 48, shift); // The format description's offset.
        Move32(bytes, output, 56, shift); // The key-value data's.
        var global = U64(bytes, 64); // The supercompression global data's.
        BinaryPrimitives.WriteInt64LittleEndian(output[64..], global == 0 ? 0 : global - shift);
    }

    private static void Move32(ReadOnlySpan<byte> bytes, Span<byte> output, int at, int shift)
    {
        var was = U32(bytes, at);
        BinaryPrimitives.WriteUInt32LittleEndian(output[at..], (uint)(was == 0 ? 0 : was - shift));
    }

    private static List<Ktx2Level>? ReadLevels(ReadOnlySpan<byte> bytes, long count, long indexEnd)
    {
        var levels = new List<Ktx2Level>((int)count);
        for (var i = 0; i < count; i++)
        {
            var at = IndexStart + (i * EntryLength);
            var level = new Ktx2Level(U64(bytes, at), U64(bytes, at + 8), U64(bytes, at + 16));
            if (level.Offset < indexEnd || level.Length == 0)
            {
                return null;
            }

            levels.Add(level);
        }

        return levels;
    }

    // The format description, the key-value data and any supercompression global data all sit between the
    // index and the first level's bytes. They have to travel with the prefix, so the prefix is only useful if
    // they come first, which the format requires, and which is checked rather than assumed.
    private static long? MetadataEndOf(ReadOnlySpan<byte> bytes, long indexEnd)
    {
        var metadataEnd = indexEnd;
        foreach (var (offset, length) in new[]
        {
            (U32(bytes, 48), U32(bytes, 52)),
            (U32(bytes, 56), U32(bytes, 60)),
            (U64(bytes, 64), U64(bytes, 72)),
        })
        {
            if (length == 0)
            {
                continue;
            }

            if (offset < indexEnd)
            {
                return null;
            }

            metadataEnd = Math.Max(metadataEnd, offset + length);
        }

        return metadataEnd;
    }

    // A dimension at a level, where nought stays nought: a height of nought means a 1D texture and a depth of
    // nought means a 2D one, and neither becomes one-deep by being made smaller.
    private static long Shrink(long size, int level) => size == 0 ? 0 : Math.Max(size >> level, 1);

    private static long U32(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);

    // Signed, so a length with the top bit set reads as negative the way it does in Dart.
    private static long U64(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadInt64LittleEndian(bytes[at..]);
}
