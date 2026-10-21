using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using Orblit.Json;

namespace Orblit.Asset;

/// <summary>
/// A glTF document held as the JSON it is, plus the bytes its buffers hold.
/// </summary>
/// <remarks>
/// This is deliberately not a glTF library. Nothing here turns the document into typed objects,
/// because the moment it does, every part of glTF the types do not model is lost on the way back
/// out. Orblit's loader is gltfio, so what the cook hands back has to still be a glTF, with its
/// extensions, <c>extras</c>, variants and skins. So the rule is: read the JSON into a map, edit
/// the handful of keys a rewrite owns, and let every other key travel through untouched because
/// it was never looked at.
/// <para>
/// Buffers are resolved on the way in (a GLB's BIN chunk, a data URI, a file beside a
/// <c>.gltf</c>) and flattened to one buffer on the way out, so whatever came in leaves as a
/// self-contained GLB.
/// </para>
/// <para>
/// A map is an <c>OrderedDictionary&lt;string, object?&gt;</c> and a list is a
/// <c>List&lt;object?&gt;</c>, as <see cref="DartJson"/> reads them. A map of another type is not
/// seen as an object.
/// </para>
/// </remarks>
public sealed partial class GltfDocument
{
    private const uint GlbMagic = 0x46546C67; // 'glTF'
    private const uint JsonChunk = 0x4E4F534A; // 'JSON'
    private const uint BinChunk = 0x004E4942; // 'BIN\0'

    private readonly List<ReadOnlyMemory<byte>> _buffers;

    // Bytes appended by AddView, in the order the views were added.
    private readonly ArrayBufferWriter<byte> _appended = new();

    // Which views AddView made, and where in _appended each one starts.
    private readonly Dictionary<int, int> _appendedAt = [];

    private GltfDocument(GltfMap json, List<ReadOnlyMemory<byte>> buffers)
    {
        Json = json;
        _buffers = buffers;
    }

    /// <summary>The document. Mutable, and meant to be: this is the thing a rewrite edits.</summary>
    public GltfMap Json { get; }

    private ReadOnlyMemory<byte> Appended => _appended.WrittenMemory;

    /// <summary>
    /// Reads <paramref name="bytes"/>, a <c>.glb</c> or a <c>.gltf</c>, resolving every buffer it names.
    /// <paramref name="source"/> is asked only for ids the caller has already declared as dependencies.
    /// A buffer that cannot be resolved is a failure rather than an empty one: a rewrite over missing
    /// geometry would write a valid file with nothing in it.
    /// </summary>
    public static async Task<GltfDocument> ReadAsync(AssetId id, byte[] bytes, IAssetSource source)
    {
        GltfMap json;
        ReadOnlyMemory<byte> binary = default;
        if (LooksLikeGlb(bytes))
        {
            (json, binary) = ReadGlb(id, bytes);
        }
        else
        {
            json = ReadJson(id, bytes);
        }

        var buffers = new List<ReadOnlyMemory<byte>>();
        if (GltfJson.Get(json, "buffers") is List<object?> declared)
        {
            var reader = new BufferReader(id, binary, source);
            for (var i = 0; i < declared.Count; i++)
            {
                if (declared[i] is not GltfMap entry)
                {
                    throw new ImportFailureException(id, $"buffer {i} is not an object");
                }

                buffers.Add(await reader.ReadAsync(entry, i).ConfigureAwait(false));
            }
        }

        return new GltfDocument(json, buffers);
    }

    /// <summary>
    /// A list of objects under <paramref name="key"/> (<c>meshes</c>, <c>materials</c>, <c>accessors</c>), or
    /// an empty list when the document has none. The maps it holds are the document's own, so writing into
    /// them edits it. A document missing the key gets an empty list that is not attached, since adding an
    /// empty <c>materials</c> to a document that had none would be a change for nothing.
    /// </summary>
    public List<GltfMap> List(string key) =>
        GltfJson.Get(Json, key) is List<object?> found ? [.. found.OfType<GltfMap>()] : [];

    /// <summary>The bytes a buffer view covers.</summary>
    public ReadOnlyMemory<byte> ViewBytes(int index)
    {
        var view = ViewAt(index);
        if (_appendedAt.TryGetValue(index, out var appendedAt))
        {
            return Appended.Slice(appendedAt, GltfJson.RequiredInt(view, "byteLength"));
        }

        var buffer = _buffers[GltfJson.IntOr(view, "buffer", 0)];
        var offset = GltfJson.IntOr(view, "byteOffset", 0);
        var length = GltfJson.RequiredInt(view, "byteLength");
        return offset + length > buffer.Length
            ? throw new FormatException("a buffer view runs past its buffer")
            : buffer.Slice(offset, length);
    }

    /// <summary>The stride a view reads at, or null when its data is tightly packed.</summary>
    public int? ViewStride(int index) => GltfJson.IntAt(ViewAt(index), "byteStride");

    /// <summary>
    /// Adds a buffer view over <paramref name="bytes"/>, returning its index. The bytes are kept aside and
    /// written into the output buffer at the end, after everything that came in. Nothing already in the
    /// document moves, so an accessor this rewrite did not touch still points where it did.
    /// </summary>
    public int AddView(ReadOnlySpan<byte> bytes, int? byteStride = null, int? target = null)
    {
        var views = ListFor("bufferViews");
        var at = _appended.WrittenCount;
        _appended.Write(bytes);
        // Four-byte alignment between views, because an accessor's offset into a view has to satisfy its
        // component type's alignment and the view's own start is what that offset is measured from.
        var padding = (4 - (bytes.Length % 4)) % 4;
        if (padding > 0)
        {
            _appended.Write(new byte[padding]);
        }

        var index = views.Count;
        var view = new GltfMap
        {
            ["buffer"] = 0,
            ["byteOffset"] = 0, // Filled in by ToGlb, from _appendedAt.
            ["byteLength"] = bytes.Length,
        };
        if (byteStride is { } stride)
        {
            view["byteStride"] = stride;
        }

        if (target is { } kind)
        {
            view["target"] = kind;
        }

        views.Add(view);
        _appendedAt[index] = at;
        return index;
    }

    /// <summary>Adds an object to a top-level list, returning its index.</summary>
    public int Add(string key, GltfMap value)
    {
        var entries = ListFor(key);
        entries.Add(value);
        return entries.Count - 1;
    }

    /// <summary>
    /// The document as a self-contained GLB: one buffer, no external files but the images it deliberately
    /// still names by URI. Every input buffer is laid down in order and every view rebased onto the result,
    /// so a <c>.gltf</c> with its geometry in a <c>.bin</c> beside it comes out as one file, which is what
    /// the cache stores and what the loader is handed.
    /// </summary>
    public byte[] ToGlb()
    {
        var bases = new List<int>();
        var at = 0;
        foreach (var buffer in _buffers)
        {
            bases.Add(at);
            at = Aligned(at + buffer.Length);
        }

        var appendBase = at;
        var appended = Appended;
        var total = appendBase + appended.Length;

        RebaseViews(bases, appendBase);
        // A buffer of nought length is an error in the format, and so is a view or an accessor over one. A
        // document with no geometry at all, a scene of lights and cameras, therefore names no buffer rather
        // than naming an empty one.
        if (total == 0)
        {
            Json.Remove("buffers");
        }
        else
        {
            Json["buffers"] = new List<object?> { new GltfMap { ["byteLength"] = total } };
        }

        var binary = new byte[Aligned(total)];
        for (var i = 0; i < _buffers.Count; i++)
        {
            _buffers[i].CopyTo(binary.AsMemory(bases[i]));
        }

        appended.CopyTo(binary.AsMemory(appendBase));
        return Container(DartJson.Encode(Json), binary);
    }

    private GltfMap ViewAt(int index)
    {
        if (GltfJson.Get(Json, "bufferViews") is not List<object?> views || index < 0 || index >= views.Count)
        {
            throw new FormatException($"there is no buffer view {index}");
        }

        return views[index] as GltfMap ?? throw new FormatException($"buffer view {index} is not an object");
    }

    private List<object?> ListFor(string key)
    {
        if (!Json.TryGetValue(key, out var found))
        {
            found = new List<object?>();
            Json[key] = found;
        }

        return found as List<object?> ?? throw new InvalidCastException($"\"{key}\" is not a list.");
    }

    private void RebaseViews(List<int> bases, int appendBase)
    {
        if (GltfJson.Get(Json, "bufferViews") is not List<object?> views)
        {
            return;
        }

        for (var i = 0; i < views.Count; i++)
        {
            if (views[i] is not GltfMap view)
            {
                continue;
            }

            // Read where the view was before saying where it is now: the two fields are the same two keys,
            // and writing first loses the answer.
            var was = GltfJson.IntOr(view, "buffer", 0);
            var wasAt = GltfJson.IntOr(view, "byteOffset", 0);
            view["buffer"] = 0;
            view["byteOffset"] = _appendedAt.TryGetValue(i, out var appendedAt) ? appendBase + appendedAt : bases[was] + wasAt;
        }
    }

    // A GLB's JSON chunk is padded with spaces and its binary chunk with zeroes. The spec says so, and a
    // viewer that trusts the padding is not wrong to.
    private static byte[] Container(string json, byte[] binary)
    {
        var text = Encoding.UTF8.GetBytes(json);
        var jsonLength = Aligned(text.Length);
        var length = 12 + 8 + jsonLength + (binary.Length == 0 ? 0 : 8 + binary.Length);
        var glb = new byte[length];
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(0), GlbMagic);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(8), (uint)length);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(12), (uint)jsonLength);
        BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(16), JsonChunk);
        glb.AsSpan(20, jsonLength).Fill(0x20);
        text.CopyTo(glb, 20);
        if (binary.Length > 0)
        {
            var binAt = 20 + jsonLength;
            BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(binAt), (uint)binary.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(glb.AsSpan(binAt + 4), BinChunk);
            binary.CopyTo(glb, binAt + 8);
        }

        return glb;
    }

    private static int Aligned(int length) => length + ((4 - (length % 4)) % 4);

    private static bool LooksLikeGlb(byte[] bytes) =>
        bytes.Length >= 12 && BinaryPrimitives.ReadUInt32LittleEndian(bytes) == GlbMagic;

    private static GltfMap ReadJson(AssetId id, byte[] bytes)
    {
        object? parsed;
        try
        {
            parsed = DartJson.Decode(DartText.Decode(bytes));
        }
        catch (FormatException error)
        {
            throw new ImportFailureException(id, "is not readable as glTF JSON", error.Message);
        }

        return parsed as GltfMap
            ?? throw new ImportFailureException(id, "is not a glTF document: its JSON is not an object");
    }

    private static (GltfMap Json, ReadOnlyMemory<byte> Binary) ReadGlb(AssetId id, byte[] bytes)
    {
        if (bytes.Length < 20)
        {
            throw new ImportFailureException(id, $"is too short to be a GLB ({bytes.Length} bytes)");
        }

        var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4));
        if (version != 2)
        {
            throw new ImportFailureException(id, $"is a GLB of version {version}, and only version 2 exists");
        }

        var total = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8));
        if (total > bytes.Length)
        {
            throw new ImportFailureException(id, $"says it is {total} bytes and is only {bytes.Length}. It is truncated.");
        }

        var (json, binary) = ReadChunks(id, bytes, (int)total);
        return (json ?? throw new ImportFailureException(id, "has no JSON chunk, and a GLB must have one"), binary);
    }

    private static (GltfMap? Json, ReadOnlyMemory<byte> Binary) ReadChunks(AssetId id, byte[] bytes, int total)
    {
        GltfMap? json = null;
        ReadOnlyMemory<byte>? binary = null;
        var at = 12;
        while (at + 8 <= total)
        {
            var length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
            var type = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at + 4));
            var start = at + 8;
            var end = start + (long)length;
            if (end > total)
            {
                throw new ImportFailureException(id, "has a chunk running past the end of the file");
            }

            var chunk = bytes.AsMemory(start, (int)length);
            switch (type)
            {
                case JsonChunk:
                    json = json is null
                        ? ReadJsonChunk(id, chunk)
                        : throw new ImportFailureException(id, "has two JSON chunks, and a GLB has one");
                    break;
                case BinChunk:
                    binary = binary is null
                        ? chunk
                        : throw new ImportFailureException(id, "has two BIN chunks, and a GLB has one");
                    break;
                default:
                    break; // The spec says to ignore a chunk you do not know.
            }

            at = (int)end;
        }

        return (json, binary ?? default);
    }

    private static GltfMap ReadJsonChunk(AssetId id, ReadOnlyMemory<byte> chunk)
    {
        object? parsed;
        try
        {
            parsed = DartJson.Decode(DartText.Decode(chunk.Span));
        }
        catch (FormatException error)
        {
            throw new ImportFailureException(id, "'s JSON chunk is not JSON", error.Message);
        }

        return parsed as GltfMap ?? throw new ImportFailureException(id, "'s JSON chunk is not an object");
    }

    // One buffer's bytes: the BIN chunk, a data URI, or a file beside the document.
    private sealed class BufferReader(AssetId id, ReadOnlyMemory<byte> binary, IAssetSource source)
    {
        public async Task<ReadOnlyMemory<byte>> ReadAsync(GltfMap buffer, int index)
        {
            var uri = GltfJson.Get(buffer, "uri");
            if (uri is null)
            {
                // No URI means the GLB's own BIN chunk, and only buffer 0 may do that.
                return index != 0
                    ? throw new ImportFailureException(
                        id, $"buffer {index} has no URI, and only the first buffer of a GLB may leave it out")
                    : binary;
            }

            if (uri is not string text)
            {
                throw new ImportFailureException(id, $"buffer {index}'s URI is not a string");
            }

            if (text.StartsWith("data:", StringComparison.Ordinal))
            {
                return DataUri(text);
            }

            var at = text.Contains(':', StringComparison.Ordinal) ? null : id.Resolve(DartText.PercentDecoded(text));
            return at is null
                ? throw new ImportFailureException(id, $"buffer {index} is at \"{text}\", which is not a name inside the project")
                : await source.ReadAsync(at).ConfigureAwait(false);
        }

        private ReadOnlyMemory<byte> DataUri(string uri)
        {
            var comma = uri.IndexOf(',', StringComparison.Ordinal);
            if (comma < 0)
            {
                throw new ImportFailureException(id, "has a data URI with no comma in it");
            }

            // Percent-encoded data URIs are legal and vanishingly rare. Refusing one by name beats
            // guessing at it.
            return !uri[..comma].EndsWith(";base64", StringComparison.Ordinal)
                ? throw new ImportFailureException(id, "has a data URI that is not base64, which this cook does not read")
                : DartText.DecodeBase64(uri[(comma + 1)..])
                    ?? throw new ImportFailureException(id, "has a data URI that is not valid base64");
        }
    }
}
