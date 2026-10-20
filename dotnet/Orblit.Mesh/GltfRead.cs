using System.Buffers.Binary;
using Orblit.Json;

namespace Orblit.Mesh;

/// <summary>
/// Reading the numbers out of a glTF document somebody else wrote. The other half of
/// <see cref="GltfBuffer"/>, kept beside it so the two agree about what an accessor is. Two
/// readers of one format drift, and the day they do, one of them reads a stride the other
/// ignores and a model comes in subtly wrong.
/// </summary>
/// <remarks>
/// Lenient in the way every reader in Orblit is: an accessor that cannot be read comes back empty
/// with a note in <see cref="Problems"/>, because a document with one bad accessor still has a
/// hundred good ones.
/// </remarks>
public sealed class GltfAccessors
{
    private const int SignedByte = 5120;
    private const int SignedShort = 5122;

    private readonly List<IReadOnlyDictionary<string, object?>> _accessors;
    private readonly List<IReadOnlyDictionary<string, object?>> _views;

    /// <param name="json">The document.</param>
    /// <param name="binary">
    /// The bytes the accessors point into: a GLB's BIN chunk, or the <c>.bin</c> beside a
    /// <c>.gltf</c>.
    /// </param>
    /// <param name="problems">Where to note what could not be read. Kept as given, so a caller can share one list.</param>
    public GltfAccessors(IReadOnlyDictionary<string, object?> json, byte[]? binary, List<string>? problems = null)
    {
        Binary = binary;
        Problems = problems ?? [];
        _accessors = MapsAt(json, "accessors");
        _views = MapsAt(json, "bufferViews");
    }

    public byte[]? Binary { get; }

    /// <summary>What could not be read, in the order it was met.</summary>
    public List<string> Problems { get; }

    /// <summary>How many elements accessor <paramref name="at"/> has, or nought when there is no such accessor.</summary>
    public long Count(int? at) => Has(at) ? WholeOf(_accessors[at!.Value], "count") ?? 0 : 0;

    /// <summary>
    /// Accessor <paramref name="at"/>, <paramref name="components"/> numbers to an element,
    /// flattened. Floating point is read as it is. Whole numbers are read too, because glTF lets a
    /// model store a rotation or a position in fewer bytes: one marked <c>normalized</c> is scaled
    /// into nought to one, or minus one to one, and one that is not is read as the number it is.
    /// </summary>
    public List<double> Floats(int? at, int components)
    {
        if (!Has(at))
        {
            return [];
        }

        var accessor = _accessors[at!.Value];
        var type = WholeOf(accessor, "componentType");
        var size = SizeOf(type);
        if (size == 0)
        {
            Problems.Add($"Accessor {at} is not a numeric type; it was not read.");
            return [];
        }

        var normalized = accessor.GetValueOrDefault("normalized") is true;
        var output = new List<double>();
        Walk(at.Value, components, size, (data, offset) =>
        {
            for (var c = 0; c < components; c++)
            {
                var raw = ReadFloat(type, data, offset + (c * size));
                output.Add(normalized ? Normalise(type, raw) : raw);
            }
        });
        return output;
    }

    /// <summary>Accessor <paramref name="at"/> as whole numbers, which is what indices are.</summary>
    public List<long> Indices(int at)
    {
        if (!Has(at))
        {
            return [];
        }

        var size = WholeOf(_accessors[at], "componentType") switch
        {
            GltfComponent.UnsignedByte => 1,
            GltfComponent.UnsignedShort => 2,
            GltfComponent.UnsignedInt => 4,
            _ => 0,
        };
        if (size == 0)
        {
            Problems.Add($"Accessor {at} is not an index type; it was not read.");
            return [];
        }

        var output = new List<long>();
        Walk(at, 1, size, (data, offset) => output.Add(ReadWhole(size, data, offset)));
        return output;
    }

    /// <summary>
    /// Steps through an accessor's elements, minding the view's stride. The stride is not a detail
    /// that can be skipped: interleaved attributes are what an optimising exporter writes, and
    /// reading one as though it were tightly packed gives numbers that are wrong rather than
    /// missing, which is far harder to notice.
    /// </summary>
    private void Walk(int at, int components, int size, Action<byte[], int> each)
    {
        var accessor = _accessors[at];
        var count = WholeOf(accessor, "count") ?? 0;
        if (count == 0)
        {
            return;
        }

        if (accessor.GetValueOrDefault("sparse") is not null)
        {
            Problems.Add($"Accessor {at} is sparse, which is not read yet.");
        }

        if (RunOf(accessor, size * components) is not { } run)
        {
            return;
        }

        for (long i = 0; i < count; i++)
        {
            var offset = run.Start + (i * run.Stride);
            if (offset < 0 || offset + (size * components) > run.End)
            {
                Problems.Add("An accessor reaches past the end of its buffer view.");
                return;
            }

            each(Binary!, (int)offset);
        }
    }

    /// <summary>Where an accessor's elements sit, or null when there is nothing to read.</summary>
    private Run? RunOf(IReadOnlyDictionary<string, object?> accessor, int elementSize)
    {
        if (Binary is null)
        {
            Problems.Add("The document has data but no buffer was supplied.");
            return null;
        }

        if (WholeOf(accessor, "bufferView") is not { } viewAt || viewAt >= _views.Count)
        {
            return null;
        }

        var view = _views[(int)viewAt];
        var origin = WholeOf(view, "byteOffset") ?? 0;
        return new Run(
            origin + (WholeOf(accessor, "byteOffset") ?? 0),
            WholeOf(view, "byteStride") ?? elementSize,
            Math.Min(Binary.Length, origin + (WholeOf(view, "byteLength") ?? 0)));
    }

    private bool Has(int? at) => at is { } n && n >= 0 && n < _accessors.Count;

    private static double ReadFloat(long? type, byte[] data, int offset) => type switch
    {
        // A double first, or the switch settles on float and rounds a uint32 on the way.
        GltfComponent.Float => (double)BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(offset)),
        SignedByte => (sbyte)data[offset],
        GltfComponent.UnsignedByte => data[offset],
        SignedShort => BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(offset)),
        GltfComponent.UnsignedShort => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset)),
        _ => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset)),
    };

    private static double Normalise(long? type, double raw) => type switch
    {
        SignedByte => Math.Max(raw / 127, -1),
        GltfComponent.UnsignedByte => raw / 255,
        SignedShort => Math.Max(raw / 32767, -1),
        GltfComponent.UnsignedShort => raw / 65535,
        _ => raw,
    };

    private static long ReadWhole(int size, byte[] data, int offset) => size switch
    {
        1 => data[offset],
        2 => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset)),
        _ => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset)),
    };

    private static int SizeOf(long? type) => type switch
    {
        SignedByte or GltfComponent.UnsignedByte => 1,
        SignedShort or GltfComponent.UnsignedShort => 2,
        GltfComponent.UnsignedInt or GltfComponent.Float => 4,
        _ => 0,
    };

    private static List<IReadOnlyDictionary<string, object?>> MapsAt(IReadOnlyDictionary<string, object?> json, string key) =>
        [.. (JsonValues.List(json.GetValueOrDefault(key)) ?? []).Select(JsonValues.Map).OfType<IReadOnlyDictionary<string, object?>>()];

    /// <summary>A whole number that is not negative, as Dart's <c>raw is int &amp;&amp; raw &gt;= 0</c> says.</summary>
    private static long? WholeOf(IReadOnlyDictionary<string, object?> map, string key) => map.GetValueOrDefault(key) switch
    {
        long whole and >= 0 => whole,
        int whole and >= 0 => whole,
        _ => null,
    };

    private readonly record struct Run(long Start, long Stride, long End);
}

public static partial class GltfFiles
{
    /// <summary>
    /// A glTF document's JSON and the bytes its accessors read, whichever of the two forms it came
    /// in. Which one comes from the bytes rather than from a name: a <c>.gltf</c> that is really a
    /// GLB is an ordinary thing to be handed. <paramref name="files"/> supplies whatever the
    /// document names by URI, the <c>.bin</c> beside a <c>.gltf</c> being the one that matters.
    /// Throws <see cref="FormatException"/> when the bytes are neither a GLB nor a JSON document.
    /// </summary>
    public static (IReadOnlyDictionary<string, object?> Json, byte[]? Binary) GltfParts(
        byte[] bytes,
        IReadOnlyDictionary<string, byte[]>? files = null)
    {
        if (GlbChunks(bytes) is { } chunks)
        {
            return (chunks.Json, chunks.Binary);
        }

        object? decoded;
        try
        {
            decoded = DartJson.Decode(DartText.Decode(bytes));
        }
        catch (FormatException error)
        {
            throw new FormatException($"This is neither a GLB nor glTF JSON: {error.Message}", error);
        }

        if (decoded is not IReadOnlyDictionary<string, object?> json)
        {
            throw new FormatException("A glTF document must be a JSON object.");
        }

        return (json, BufferOf(json, files ?? new Dictionary<string, byte[]>()));
    }

    /// <summary>
    /// The bytes the document's first buffer names, wherever they are. Only the first, because
    /// that is the only one anything here writes and the only one an accessor in a document we
    /// produced ever points into.
    /// </summary>
    private static byte[]? BufferOf(IReadOnlyDictionary<string, object?> json, IReadOnlyDictionary<string, byte[]> files)
    {
        if (JsonValues.List(json.GetValueOrDefault("buffers")) is not { Count: > 0 } buffers
            || JsonValues.Map(buffers[0]) is not { } first
            || first.GetValueOrDefault("uri") is not string { Length: > 0 } uri)
        {
            return null;
        }

        if (uri.StartsWith("data:", StringComparison.Ordinal))
        {
            return DataUri(uri);
        }

        // Named twice, because a URI is escaped and a filename is not: a buffer written beside a
        // scene called `old town.gltf` arrives as `old%20town.bin` and is on disk under the name
        // with the space in it.
        return files.GetValueOrDefault(uri) ?? files.GetValueOrDefault(DartText.DecodeUriComponent(uri));
    }

    /// <summary>
    /// Only base64 is worth reading: a percent-encoded binary buffer is legal and is also several
    /// times the size, so nothing writes one.
    /// </summary>
    private static byte[]? DataUri(string uri)
    {
        var comma = uri.IndexOf(',', StringComparison.Ordinal);
        return comma < 0 || !uri[..comma].EndsWith(";base64", StringComparison.Ordinal)
            ? null
            : DartText.DecodeBase64(uri[(comma + 1)..]);
    }
}
