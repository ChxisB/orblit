using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Text;
using Orblit.Json;

namespace Orblit.Asset;

/// <summary>The constants of the pack format, and the one sum that places a blob.</summary>
internal static class PackFormat
{
    /// <summary>The format this code writes, and the newest it reads.</summary>
    public const int Version = 1;

    /// <summary>Blobs start on a multiple of this, so a view of one is aligned for any element up to 16 bytes wide.</summary>
    public const int Alignment = 16;

    /// <summary>The <c>kind</c> marker in a pack's directory.</summary>
    public const string Kind = "orblit.pack";

    /// <summary>The four bytes every pack starts with: <c>OPAK</c>.</summary>
    public static ReadOnlySpan<byte> Marker => "OPAK"u8;

    /// <summary>
    /// The next multiple of <see cref="Alignment"/> at or after <paramref name="offset"/>. Wraps at 64 bits
    /// as the Dart twin does, so a size too large to place gives the same answer in both.
    /// </summary>
    public static long AlignedOffset(long offset) => unchecked(((offset + Alignment - 1) / Alignment) * Alignment);
}

/// <summary>The 32 fixed bytes at the start of a pack.</summary>
internal sealed record PackHeader(long DirectoryAt, long DirectoryBytes)
{
    public const int Size = 32;

    public byte[] Encode()
    {
        var bytes = new byte[Size];
        PackFormat.Marker.CopyTo(bytes);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), PackFormat.Version);
        SetUint64(bytes, 8, DirectoryAt);
        SetUint64(bytes, 16, DirectoryBytes);
        return bytes;
    }

    /// <summary>Reads the header, refusing a file that is not a pack of a format this code reads.</summary>
    public static PackHeader Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < Size)
        {
            throw new FormatException(
                $"This is not an Orblit pack. It is {bytes.Length} bytes long and a pack header alone is {Size}.");
        }

        if (!bytes[..4].SequenceEqual(PackFormat.Marker))
        {
            throw new FormatException("This is not an Orblit pack. It does not start with OPAK.");
        }

        var format = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        if (format > PackFormat.Version)
        {
            throw new FormatException(
                $"This pack was written by a newer Orblit (format {format}; this one reads up to {PackFormat.Version}).");
        }

        return format < 1
            ? throw new FormatException($"This pack says it is format {format}, which no Orblit has written.")
            : new PackHeader(GetUint64(bytes, 8), GetUint64(bytes, 16));
    }

    // Two 32-bit halves, the way the Dart twin writes them. A value past 63 bits reads back negative in both.
    private static void SetUint64(Span<byte> bytes, int at, long value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[at..], unchecked((uint)value));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes[(at + 4)..], unchecked((uint)(value / 0x100000000)));
    }

    private static long GetUint64(ReadOnlySpan<byte> bytes, int at) =>
        unchecked(BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]) + ((long)BinaryPrimitives.ReadUInt32LittleEndian(bytes[(at + 4)..]) * 0x100000000));
}

/// <summary>Where one blob sits in a pack.</summary>
internal readonly record struct PackBlob(long At, long Bytes)
{
    public long End => unchecked(At + Bytes);
}

/// <summary>
/// What a pack holds and where. The last thing in the file. A pack is written by a tool, so a bad one
/// is a bad file and not a hand edit. Reading one that is wrong in any way throws, whole, where a
/// manifest carries on past a broken entry.
/// </summary>
internal sealed class PackDirectory
{
    /// <summary>
    /// Builds a directory, throwing a <see cref="FormatException"/> for a layout no reader would accept.
    /// Everything <paramref name="directoryAt"/> and the header leave unsaid is checked here, so the
    /// writer cannot produce what the reader refuses.
    /// </summary>
    // F2: six parameters, called by name from the writer and the reader.
    public PackDirectory(
        string target,
        ContentHash? @base,
        IReadOnlyList<AssetId> removed,
        IReadOnlyDictionary<AssetId, ContentHash> assets,
        IReadOnlyDictionary<ContentHash, PackBlob> blobs,
        long directoryAt)
    {
        CheckAssets(assets, blobs);
        CheckRemoved(@base, removed, assets);
        CheckBlobs(blobs, directoryAt);
        Target = target;
        Base = @base;
        Removed = [.. removed];
        Assets = new ReadOnlyDictionary<AssetId, ContentHash>(Ordered.Copy(assets));
        Blobs = new ReadOnlyDictionary<ContentHash, PackBlob>(Ordered.Copy(blobs));
    }

    /// <summary>The cook target the textures in this pack were made for.</summary>
    public string Target { get; }

    /// <summary>The hash of the pack a patch was built against, or null.</summary>
    public ContentHash? Base { get; }

    /// <summary>Ids the base had and this pack takes away.</summary>
    public IReadOnlyList<AssetId> Removed { get; }

    /// <summary>Which blob each asset is.</summary>
    public IReadOnlyDictionary<AssetId, ContentHash> Assets { get; }

    /// <summary>Where each blob is.</summary>
    public IReadOnlyDictionary<ContentHash, PackBlob> Blobs { get; }

    /// <summary>
    /// Reads what <see cref="Encode"/> writes, for a directory that starts at <paramref name="directoryAt"/>.
    /// </summary>
    public static PackDirectory Decode(ReadOnlySpan<byte> bytes, long directoryAt)
    {
        var map = Parse(bytes);
        var kind = map.GetValueOrDefault("kind");
        if (kind is not string text || text != PackFormat.Kind)
        {
            throw new FormatException(
                $"The directory of this pack says it is \"{DartText.Print(kind)}\", not \"{PackFormat.Kind}\".");
        }

        var version = map.GetValueOrDefault("formatVersion");
        if (!IsTheVersion(version))
        {
            throw new FormatException(
                $"The directory of this pack says format {DartText.Print(version)}, and its header says {PackFormat.Version}.");
        }

        var @base = map.GetValueOrDefault("base");
        return new PackDirectory(
            target: Text(map, "target"),
            @base: @base is null ? null : Hash(@base, "base"),
            removed: [.. List(map, "removed").Select(id => Id(id, "removed"))],
            assets: Ordered.Copy(Object(map, "assets").Select(e => KeyValuePair.Create(Id(e.Key, "assets"), Hash(e.Value, "assets")))),
            blobs: Ordered.Copy(Object(map, "blobs").Select(e => KeyValuePair.Create(Hash(e.Key, "blobs"), Blob(e.Key, e.Value)))),
            directoryAt: directoryAt);
    }

    /// <summary>
    /// The directory as JSON, the same bytes every time for the same content. Keys are in alphabetical
    /// order and there are no timestamps, so two packs of one bundle are one file.
    /// </summary>
    public byte[] Encode()
    {
        var assets = new OrderedDictionary<string, object?>();
        foreach (var id in Ordered.ById(Assets.Keys))
        {
            assets[id.ToString()] = Assets[id].Hex;
        }

        var blobs = new OrderedDictionary<string, object?>();
        foreach (var hash in Blobs.Keys.OrderBy(h => h.Hex, StringComparer.Ordinal))
        {
            blobs[hash.Hex] = new OrderedDictionary<string, object?> { ["at"] = Blobs[hash].At, ["bytes"] = Blobs[hash].Bytes };
        }

        var json = new OrderedDictionary<string, object?>
        {
            ["assets"] = assets,
            ["base"] = Base?.Hex,
            ["blobs"] = blobs,
            ["formatVersion"] = (long)PackFormat.Version,
            ["kind"] = PackFormat.Kind,
            ["removed"] = Ordered.ById(Removed).Select(id => (object?)id.ToString()).ToList(),
            ["target"] = Target,
        };
        return Encoding.UTF8.GetBytes(DartJson.Encode(json));
    }

    private static IReadOnlyDictionary<string, object?> Parse(ReadOnlySpan<byte> bytes)
    {
        object? parsed;
        try
        {
            parsed = DartJson.Decode(DartText.Decode(bytes));
        }
        catch (FormatException error)
        {
            throw new FormatException($"The directory of this pack is not JSON: {error.Message}");
        }

        return parsed as IReadOnlyDictionary<string, object?>
            ?? throw new FormatException("The directory of this pack is not a JSON object.");
    }

    // Dart compares a number to 1 by value, so a directory that says 1.0 is read as format 1.
    private static bool IsTheVersion(object? version) => version switch
    {
        long whole => whole == PackFormat.Version,
        double fraction => fraction == PackFormat.Version,
        _ => false,
    };

    private static void CheckAssets(IReadOnlyDictionary<AssetId, ContentHash> assets, IReadOnlyDictionary<ContentHash, PackBlob> blobs)
    {
        foreach (var (id, hash) in assets)
        {
            if (!blobs.ContainsKey(hash))
            {
                throw new FormatException($"The asset \"{id}\" names a blob, {hash}, that this pack does not have.");
            }
        }
    }

    private static void CheckRemoved(ContentHash? @base, IReadOnlyList<AssetId> removed, IReadOnlyDictionary<AssetId, ContentHash> assets)
    {
        if (removed.Count > 0 && @base is null)
        {
            throw new FormatException("This pack removes assets and has no base to remove them from.");
        }

        var seen = new HashSet<AssetId>();
        foreach (var id in removed)
        {
            if (!seen.Add(id))
            {
                throw new FormatException($"The asset \"{id}\" is removed twice.");
            }

            if (assets.ContainsKey(id))
            {
                throw new FormatException($"The asset \"{id}\" is both removed and held.");
            }
        }
    }

    private static void CheckBlobs(IReadOnlyDictionary<ContentHash, PackBlob> blobs, long directoryAt)
    {
        // An empty blob shares its start with the blob after it, so it sorts first.
        var byPlace = blobs.OrderBy(b => b.Value.At).ThenBy(b => b.Value.Bytes);
        long previousEnd = PackHeader.Size;
        ContentHash? previous = null;
        foreach (var (hash, blob) in byPlace)
        {
            if (blob.At < PackHeader.Size)
            {
                throw new FormatException($"The blob {hash} starts at byte {blob.At}, inside the header.");
            }

            if (blob.At % PackFormat.Alignment != 0)
            {
                throw new FormatException(
                    $"The blob {hash} starts at byte {blob.At}, which is not a multiple of {PackFormat.Alignment}.");
            }

            if (blob.At < previousEnd)
            {
                throw new FormatException($"The blobs {previous} and {hash} overlap.");
            }

            if (blob.End > directoryAt)
            {
                throw new FormatException(
                    $"The blob {hash} ends at byte {blob.End}, inside the directory that starts at {directoryAt}.");
            }

            previousEnd = blob.End;
            previous = hash;
        }
    }

    private static IReadOnlyDictionary<string, object?> Object(IReadOnlyDictionary<string, object?> from, string key) =>
        from.GetValueOrDefault(key) as IReadOnlyDictionary<string, object?>
            ?? throw new FormatException($"The directory of this pack has no \"{key}\" object.");

    private static IReadOnlyList<object?> List(IReadOnlyDictionary<string, object?> from, string key) =>
        from.GetValueOrDefault(key) as IReadOnlyList<object?>
            ?? throw new FormatException($"The directory of this pack has no \"{key}\" list.");

    private static string Text(IReadOnlyDictionary<string, object?> from, string key) =>
        from.GetValueOrDefault(key) as string
            ?? throw new FormatException($"The directory of this pack has no \"{key}\" text.");

    private static AssetId Id(object? value, string where) => value is string text
        ? AssetId.Parse(text)
        : throw new FormatException($"\"{where}\" in this pack holds {DartText.Print(value)}, not an id.");

    private static ContentHash Hash(object? value, string where) => value is string text
        ? ContentHash.Parse(text)
        : throw new FormatException($"\"{where}\" in this pack holds {DartText.Print(value)}, not a hash.");

    private static PackBlob Blob(string hash, object? value) =>
        value is IReadOnlyDictionary<string, object?> map
        && map.GetValueOrDefault("at") is long at
        && map.GetValueOrDefault("bytes") is long bytes
        && at >= 0
        && bytes >= 0
            ? new PackBlob(at, bytes)
            : throw new FormatException($"The blob {hash} does not give \"at\" and \"bytes\" as whole numbers.");
}
