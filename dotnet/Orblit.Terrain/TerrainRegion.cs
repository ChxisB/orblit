using System.Buffers.Binary;
using System.Text;
using Orblit.Json;

using CoverValue = Orblit.Terrain.Cover;

namespace Orblit.Terrain;

/// <summary>
/// One square of ground: <see cref="Size"/> texels a side, each with a height, a
/// <see cref="Cover"/> and a <see cref="GroundColour"/>.
/// Texel (<c>i</c>, <c>j</c>) sits <c>i</c> texels along +x and <c>j</c> along +z from the
/// region's corner, and is stored at <c>j * size + i</c> in every map. The maps are handed out as
/// they are, so a brush can write a thousand texels without a thousand calls; whoever writes into
/// them directly calls <see cref="Touch"/> afterwards, and the setters here do it themselves.
/// </summary>
public class TerrainRegion
{
    public const string Marker = "orblit.region";
    public const int FormatVersion = 1;
    public const int MinSize = 2;
    public const int MaxSize = 4096;

    private static readonly UTF8Encoding Strict = new(false, true);

    private static long _lastRevision;

    private (double Low, double High)? _range;

    /// <summary>
    /// A region of flat ground at height 0 under <see cref="Cover.Auto"/> and no colour, or of the
    /// maps given.
    /// </summary>
    public TerrainRegion(RegionKey key, int size, float[]? heights = null, uint[]? cover = null, byte[]? colour = null)
    {
        if (!ValidSize(size))
        {
            throw new ArgumentOutOfRangeException(
                nameof(size),
                size,
                $"A region is a power of two texels across, from {MinSize} to {MaxSize}.");
        }

        Key = key;
        Size = size;
        Heights = heights ?? new float[size * size];
        Cover = cover ?? FilledCover(size);
        Colour = colour ?? FilledColour(size);
        CheckLength(nameof(heights), Heights.Length, size * size);
        CheckLength(nameof(cover), Cover.Length, size * size);
        CheckLength(nameof(colour), Colour.Length, size * size * 4);
        Revision = Interlocked.Increment(ref _lastRevision);
    }

    /// <summary>
    /// The steps that bring an older region file up to <see cref="FormatVersion"/>, oldest first.
    /// Empty: this is the first format.
    /// </summary>
    public static IReadOnlyList<RegionMigration> Migrations { get; } = [];

    public RegionKey Key { get; }

    /// <summary>Texels a side.</summary>
    public int Size { get; }

    /// <summary>Heights in metres, one per texel.</summary>
    public float[] Heights { get; }

    /// <summary><see cref="CoverValue"/> words, one per texel.</summary>
    public uint[] Cover { get; }

    /// <summary><see cref="GroundColour"/> bytes, four per texel: red, green, blue, roughness.</summary>
    public byte[] Colour { get; }

    /// <summary>
    /// Changes whenever the maps do, and is never the same for two regions or two states of one. A
    /// renderer that remembers the revision it sent knows whether to send again, even for a region
    /// removed and put back.
    /// </summary>
    public long Revision { get; private set; }

    /// <summary>The lowest height in the region, holes and all.</summary>
    public double MinHeight => (_range ??= Measure()).Low;

    /// <summary>The highest height in the region, holes and all.</summary>
    public double MaxHeight => (_range ??= Measure()).High;

    /// <summary>
    /// Whether <paramref name="size"/> can be a region's: a power of two, <see cref="MinSize"/> to
    /// <see cref="MaxSize"/>.
    /// </summary>
    public static bool ValidSize(long size) =>
        size >= MinSize && size <= MaxSize && (size & (size - 1)) == 0;

    /// <summary>
    /// Says the maps have changed. Called by the setters here; call it after writing into
    /// <see cref="Heights"/>, <see cref="Cover"/> or <see cref="Colour"/> directly.
    /// </summary>
    public void Touch()
    {
        Revision = Interlocked.Increment(ref _lastRevision);
        _range = null;
    }

    public int IndexOf(int i, int j)
    {
        CheckInInterval(i, nameof(i));
        CheckInInterval(j, nameof(j));
        return (j * Size) + i;
    }

    public double HeightAt(int i, int j) => Heights[IndexOf(i, j)];

    public void SetHeight(int i, int j, double height)
    {
        Heights[IndexOf(i, j)] = (float)height;
        Touch();
    }

    public CoverValue CoverAt(int i, int j) => new(Cover[IndexOf(i, j)]);

    public void SetCover(int i, int j, CoverValue value)
    {
        Cover[IndexOf(i, j)] = value.Word;
        Touch();
    }

    public GroundColour ColourAt(int i, int j)
    {
        var at = IndexOf(i, j) * 4;
        return GroundColour.Bytes(Colour[at], Colour[at + 1], Colour[at + 2], Colour[at + 3]);
    }

    public void SetColour(int i, int j, GroundColour value)
    {
        var at = IndexOf(i, j) * 4;
        Colour[at] = (byte)value.Red;
        Colour[at + 1] = (byte)value.Green;
        Colour[at + 2] = (byte)value.Blue;
        Colour[at + 3] = (byte)value.RoughnessByte;
        Touch();
    }

    /// <summary>
    /// The region as a file: a line of JSON saying what follows, padded to four bytes, then each
    /// map that is not still at its default, in the order the line lists them, little-endian.
    /// Binary because the maps are: a 256-texel region is a quarter of a million heights, and a
    /// quarter of a million numbers written out as text would be six times the size and not one of
    /// them readable.
    /// </summary>
    public byte[] Encode()
    {
        var maps = new List<(string Name, byte[] Bytes)>();
        if (Heights.Any(height => height != 0))
        {
            maps.Add(("height", FloatBytes(Heights)));
        }

        if (Cover.Any(word => word != CoverValue.Auto.Word))
        {
            maps.Add(("cover", WordBytes(Cover)));
        }

        if (!IsDefaultColour(Colour))
        {
            maps.Add(("colour", (byte[])Colour.Clone()));
        }

        var listed = new List<object?>();
        foreach (var (name, bytes) in maps)
        {
            listed.Add(new OrderedDictionary<string, object?> { ["name"] = name, ["bytes"] = (long)bytes.Length });
        }

        var header = Encoding.UTF8.GetBytes(JsonText.Encode(new OrderedDictionary<string, object?>
        {
            ["kind"] = Marker,
            ["formatVersion"] = (long)FormatVersion,
            ["x"] = Key.X,
            ["z"] = Key.Z,
            ["size"] = (long)Size,
            ["maps"] = listed,
        }) + "\n");
        var start = Aligned(header.Length);
        var output = new byte[start + maps.Sum(map => map.Bytes.Length)];
        header.CopyTo(output, 0);
        var at = start;
        foreach (var (_, bytes) in maps)
        {
            bytes.CopyTo(output, at);
            at += bytes.Length;
        }

        return output;
    }

    /// <summary>
    /// A region out of a file's bytes, with whatever could not be read.
    /// Strict about the maps: a map whose length is not the region's is a damaged file, and half a
    /// height map is worse than none, so it throws <see cref="RegionFormatException"/> like a file
    /// that is not a region or is one from a newer Orblit. A map this version does not know is left
    /// out with a note.
    /// </summary>
    public static RegionLoad Decode(byte[] bytes)
    {
        var end = Array.IndexOf(bytes, (byte)0x0A);
        var (parsed, version) = ReadHeader(bytes, end);
        var maps = ReadMaps(bytes, end, parsed);

        var problems = new List<string>();
        var parts = new RegionParts(parsed, maps);
        foreach (var step in Migrations)
        {
            if (version is { } from && step.From >= from)
            {
                parts = step.Apply(parts, problems);
            }
        }

        var (x, z, size) = ReadPlace(parts.Header);
        var texels = (int)(size * size);
        var height = Take(parts, "height", texels * 4, size);
        var cover = Take(parts, "cover", texels * 4, size);
        var colour = Take(parts, "colour", texels * 4, size);
        foreach (var name in parts.Maps.Keys)
        {
            problems.Add($"A \"{name}\" map this Orblit does not know was left out.");
        }

        return new RegionLoad(
            new TerrainRegion(
                new RegionKey(x, z),
                (int)size,
                heights: height is null ? null : ReadFloats(height),
                cover: cover is null ? null : ReadWords(cover),
                colour: colour is null ? null : (byte[])colour.Clone()),
            problems);
    }

    private static (long X, long Z, long Size) ReadPlace(OrderedDictionary<string, object?> header)
    {
        if (header.GetValueOrDefault("x") is not long x ||
            header.GetValueOrDefault("z") is not long z ||
            header.GetValueOrDefault("size") is not long size ||
            !ValidSize(size))
        {
            throw new RegionFormatException("This region does not say where it is or how big.");
        }

        return (x, z, size);
    }

    private static (OrderedDictionary<string, object?> Header, long? Version) ReadHeader(byte[] bytes, int end)
    {
        if (end < 0)
        {
            throw new RegionFormatException("This is not a region file.");
        }

        object? parsed;
        try
        {
            // The twin's UTF-8 decoder drops a leading byte order mark, so a header an editor put
            // one on still reads.
            var text = Strict.GetString(bytes, 0, end);
            parsed = JsonText.Decode(text.StartsWith('﻿') ? text[1..] : text);
        }
        catch (Exception error) when (error is FormatException or DecoderFallbackException)
        {
            throw new RegionFormatException($"This is not a region file: {error.Message}");
        }

        if (parsed is not OrderedDictionary<string, object?> header ||
            !header.TryGetValue("kind", out var kind) ||
            kind as string != Marker)
        {
            throw new RegionFormatException("This is not a region file.");
        }

        var version = header.GetValueOrDefault("formatVersion") as long?;
        if (version > FormatVersion)
        {
            throw new RegionFormatException("This region was written by a newer Orblit.");
        }

        return (header, version);
    }

    private static OrderedDictionary<string, byte[]> ReadMaps(
        byte[] bytes,
        int end,
        OrderedDictionary<string, object?> header)
    {
        var maps = new OrderedDictionary<string, byte[]>();
        var at = (long)Aligned(end + 1);
        var listed = header.GetValueOrDefault("maps") as List<object?> ?? [];
        foreach (var entry in listed)
        {
            var box = entry as IReadOnlyDictionary<string, object?>;
            if (box?.GetValueOrDefault("name") is not string name ||
                box.GetValueOrDefault("bytes") is not long length ||
                length < 0)
            {
                throw new RegionFormatException("A map in this region is unnamed.");
            }

            // Compared as a remainder, so a length near 2^63 is cut short and does not wrap.
            if (length > bytes.Length - at)
            {
                throw new RegionFormatException($"The {name} map is cut short.");
            }

            maps[name] = bytes[(int)at..(int)(at + length)];
            at += length;
        }

        return maps;
    }

    private static byte[]? Take(RegionParts parts, string name, int length, long size)
    {
        if (!parts.Maps.Remove(name, out var bytes))
        {
            return null;
        }

        if (bytes.Length != length)
        {
            throw new RegionFormatException(
                $"The {name} map is {bytes.Length} bytes; a region {size} across needs {length}.");
        }

        return bytes;
    }

    private (double Low, double High) Measure()
    {
        var low = double.PositiveInfinity;
        var high = double.NegativeInfinity;
        foreach (var height in Heights)
        {
            if (height < low)
            {
                low = height;
            }

            if (height > high)
            {
                high = height;
            }
        }

        return (low, high);
    }

    private void CheckInInterval(int value, string name)
    {
        if (value < 0 || value > Size - 1)
        {
            throw new ArgumentOutOfRangeException(name, value, $"Invalid value: Not in inclusive range 0..{Size - 1}");
        }
    }

    private static uint[] FilledCover(int size)
    {
        var words = new uint[size * size];
        Array.Fill(words, CoverValue.Auto.Word);
        return words;
    }

    private static byte[] FilledColour(int size)
    {
        var none = GroundColour.None;
        var bytes = new byte[size * size * 4];
        for (var at = 0; at < bytes.Length; at += 4)
        {
            bytes[at] = (byte)none.Red;
            bytes[at + 1] = (byte)none.Green;
            bytes[at + 2] = (byte)none.Blue;
            bytes[at + 3] = (byte)none.RoughnessByte;
        }

        return bytes;
    }

    private static bool IsDefaultColour(byte[] bytes)
    {
        var none = GroundColour.None;
        for (var at = 0; at < bytes.Length; at += 4)
        {
            if (bytes[at] != none.Red ||
                bytes[at + 1] != none.Green ||
                bytes[at + 2] != none.Blue ||
                bytes[at + 3] != none.RoughnessByte)
            {
                return false;
            }
        }

        return true;
    }

    private static void CheckLength(string name, int length, int expected)
    {
        if (length != expected)
        {
            throw new ArgumentOutOfRangeException(name, length, $"Expected {expected} entries.");
        }
    }

    private static int Aligned(int length) => (length + 3) & ~3;

    private static byte[] FloatBytes(float[] values)
    {
        var bytes = new byte[values.Length * 4];
        for (var n = 0; n < values.Length; n++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(n * 4), values[n]);
        }

        return bytes;
    }

    private static byte[] WordBytes(uint[] values)
    {
        var bytes = new byte[values.Length * 4];
        for (var n = 0; n < values.Length; n++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(n * 4), values[n]);
        }

        return bytes;
    }

    private static float[] ReadFloats(byte[] bytes)
    {
        var values = new float[bytes.Length / 4];
        for (var n = 0; n < values.Length; n++)
        {
            values[n] = BinaryPrimitives.ReadSingleLittleEndian(bytes.AsSpan(n * 4));
        }

        return values;
    }

    private static uint[] ReadWords(byte[] bytes)
    {
        var values = new uint[bytes.Length / 4];
        for (var n = 0; n < values.Length; n++)
        {
            values[n] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(n * 4));
        }

        return values;
    }
}

/// <summary>What reading a region file gave: the region, and what had to be left out.</summary>
public sealed record RegionLoad(TerrainRegion Region, IReadOnlyList<string> Problems);

/// <summary>A region file that cannot be read at all.</summary>
public sealed class RegionFormatException(string message) : Exception(message);

/// <summary>
/// A region file taken apart: the header line, decoded, and each map's bytes by name. What a
/// <see cref="RegionMigration"/> works on, since a step may need to rewrite a map as well as the
/// line that describes it.
/// </summary>
public sealed class RegionParts(OrderedDictionary<string, object?> header, OrderedDictionary<string, byte[]> maps)
{
    public OrderedDictionary<string, object?> Header { get; } = header;

    public OrderedDictionary<string, byte[]> Maps { get; } = maps;
}

/// <summary>One step between region formats: parts at <see cref="From"/> in, at <see cref="To"/> out.</summary>
public abstract class RegionMigration
{
    public abstract int From { get; }

    public int To => From + 1;

    public abstract RegionParts Apply(RegionParts parts, List<string> notes);
}
