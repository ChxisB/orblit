using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.InteropServices;
using Orblit.Json;

namespace Orblit.Mesh;

/// <summary>
/// The component types anything here writes. glTF's own numbers rather than an enum of ours,
/// because they go into the document as they are and somebody reading the JSON beside this file
/// should find the same value in both.
/// </summary>
public static class GltfComponent
{
    public const int UnsignedByte = 5121;
    public const int UnsignedShort = 5123;
    public const int UnsignedInt = 5125;

    /// <summary>glTF's name for a 32-bit float, kept as the format spells it.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "The glTF spelling.")]
    public const int Float = 5126;
}

/// <summary>
/// What a buffer view is for, when it is for one of the two things glTF names. Optional in the
/// format and worth writing: a loader that knows a view holds indices can put it in the right
/// kind of GPU buffer without reading the accessors first.
/// </summary>
public static class GltfTarget
{
    public const int ArrayBuffer = 34962;
    public const int ElementArrayBuffer = 34963;
}

/// <summary>
/// One glTF buffer, its views and its accessors, built up in order. The bytes are accumulated
/// here rather than in each writer so the padding rule is enforced in one place: a byte offset
/// that forgot its padding is where most of what goes wrong in writing a glTF goes wrong.
/// </summary>
public sealed class GltfBuffer
{
    private readonly List<byte> _bytes = [];

    /// <summary>
    /// The views, in the order they were added. A view's index is its name in the document, which
    /// is why nothing is ever removed from this.
    /// </summary>
    public List<OrderedDictionary<string, object?>> Views { get; } = [];

    /// <summary>The accessors, likewise.</summary>
    public List<OrderedDictionary<string, object?>> Accessors { get; } = [];

    /// <summary>How many bytes are in the buffer so far, padding included.</summary>
    public int Length => _bytes.Count;

    /// <summary>
    /// A view over <paramref name="data"/>, four-byte aligned, returning its index. Every view
    /// starts on a multiple of four, so an accessor over it meets glTF's offset rule for every
    /// component size written here.
    /// </summary>
    public int AddView<T>(ReadOnlySpan<T> data, int? target = null)
        where T : unmanaged
    {
        var bytes = MemoryMarshal.AsBytes(data);
        var at = AddBytes(bytes);
        var view = new OrderedDictionary<string, object?>
        {
            ["buffer"] = 0L,
            ["byteOffset"] = (long)at,
            ["byteLength"] = (long)bytes.Length,
        };
        if (target is { } kind)
        {
            view["target"] = (long)kind;
        }

        Views.Add(view);
        return Views.Count - 1;
    }

    /// <summary>
    /// Bytes with nothing pointing at them yet, returning where they start. For grafting one
    /// document into another: the views that arrive with it need somewhere to point, aligned like
    /// any other so they keep whatever alignment they had.
    /// </summary>
    public int AddBytes(ReadOnlySpan<byte> data)
    {
        Pad();
        var at = _bytes.Count;
        _bytes.AddRange(data);
        return at;
    }

    /// <summary>An accessor over <paramref name="view"/>, returning its index.</summary>
    public int AddAccessor(
        int view,
        int componentType,
        int count,
        string type,
        int byteOffset = 0,
        IReadOnlyList<double>? min = null,
        IReadOnlyList<double>? max = null,
        bool normalized = false)
    {
        var accessor = new OrderedDictionary<string, object?> { ["bufferView"] = (long)view };
        if (byteOffset != 0)
        {
            accessor["byteOffset"] = (long)byteOffset;
        }

        accessor["componentType"] = (long)componentType;
        accessor["count"] = (long)count;
        accessor["type"] = type;
        if (normalized)
        {
            accessor["normalized"] = true;
        }

        if (min is not null)
        {
            accessor["min"] = Boxed(min);
        }

        if (max is not null)
        {
            accessor["max"] = Boxed(max);
        }

        Accessors.Add(accessor);
        return Accessors.Count - 1;
    }

    /// <summary>
    /// Positions: a view, an accessor, and the bounds glTF requires on them. A loader frames,
    /// culls and picks a model it has not decoded from those bounds, so they are worked out here
    /// rather than asked for, because a caller that has to supply them can supply the wrong ones.
    /// </summary>
    public int AddPositions(float[] values)
    {
        var view = AddView<float>(Float32.Quiet(values), GltfTarget.ArrayBuffer);
        var count = values.Length / 3;
        double[] min = [double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity];
        double[] max = [double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity];
        for (var i = 0; i < count; i++)
        {
            for (var axis = 0; axis < 3; axis++)
            {
                double one = values[(i * 3) + axis];
                min[axis] = one < min[axis] ? one : min[axis];
                max[axis] = one > max[axis] ? one : max[axis];
            }
        }

        // An empty mesh is still a valid file, and infinity is not a number glTF will take.
        if (count == 0)
        {
            Array.Clear(min);
            Array.Clear(max);
        }

        return AddAccessor(view, GltfComponent.Float, count, "VEC3", min: min, max: max);
    }

    /// <summary>Floats as <paramref name="type"/>: SCALAR, VEC2, VEC3, VEC4 or MAT4.</summary>
    public int AddFloats(float[] values, string type, int? target = null)
    {
        var view = AddView<float>(Float32.Quiet(values), target);
        return AddAccessor(view, GltfComponent.Float, values.Length / ComponentsIn(type), type);
    }

    /// <summary>
    /// Indices, in the smallest component type that can hold the largest one. Four bytes an index
    /// is what a mesh of any size needs and what most of them do not: a shape under sixty-five
    /// thousand vertices halves its index buffer by saying so, and every loader reads all three
    /// widths. <see cref="Triangles"/> deliberately holds thirty-two bits whatever it contains,
    /// because it picks its width before it knows what it will hold. This picks from the numbers
    /// in hand, at the moment of writing.
    /// </summary>
    public int AddIndices<T>(IReadOnlyList<T> indices)
        where T : IBinaryInteger<T>
    {
        var narrowed = AddIndexView(indices);
        return AddAccessor(narrowed.View, narrowed.ComponentType, indices.Count, "SCALAR");
    }

    /// <summary>
    /// The same view, without an accessor over it, and how to read it. For a mesh drawn in more
    /// than one material: each material's run is an accessor into a stretch of one buffer, so the
    /// caller needs the width the narrowing chose and not a second copy of the rule.
    /// </summary>
    public (int View, int ComponentType, int Width) AddIndexView<T>(IReadOnlyList<T> indices)
        where T : IBinaryInteger<T>
    {
        var wide = new long[indices.Count];
        long largest = 0;
        for (var i = 0; i < wide.Length; i++)
        {
            wide[i] = long.CreateTruncating(indices[i]);
            largest = Math.Max(largest, wide[i]);
        }

        if (largest < 256)
        {
            return (AddView<byte>([.. wide.Select(one => unchecked((byte)one))], GltfTarget.ElementArrayBuffer), GltfComponent.UnsignedByte, 1);
        }

        if (largest < 65536)
        {
            return (AddView<ushort>([.. wide.Select(one => unchecked((ushort)one))], GltfTarget.ElementArrayBuffer), GltfComponent.UnsignedShort, 2);
        }

        return (AddView<uint>([.. wide.Select(one => unchecked((uint)one))], GltfTarget.ElementArrayBuffer), GltfComponent.UnsignedInt, 4);
    }

    /// <summary>
    /// The buffer, padded to four bytes so whatever follows starts aligned. F5: the padding is
    /// part of <see cref="Length"/> from here on, which is how Dart's getter behaves.
    /// </summary>
    public byte[] Bytes
    {
        get
        {
            Pad();
            return [.. _bytes];
        }
    }

    /// <summary>
    /// The <c>buffers</c> array for a document that keeps its bytes in a GLB chunk. Empty when
    /// nothing was written, because a buffer of no length is one of the few things the validator
    /// does call an error.
    /// </summary>
    public List<object?> Buffers => Length == 0
        ? []
        : [new OrderedDictionary<string, object?> { ["byteLength"] = (long)Length }];

    /// <summary>How many components one element of <paramref name="type"/> has.</summary>
    public static int ComponentsIn(string type) => type switch
    {
        "SCALAR" => 1,
        "VEC2" => 2,
        "VEC3" => 3,
        "VEC4" => 4,
        "MAT2" => 4,
        "MAT3" => 9,
        "MAT4" => 16,
        _ => 1,
    };

    private static List<object?> Boxed(IReadOnlyList<double> values) => [.. values.Select(one => (object?)one)];

    private void Pad()
    {
        var over = _bytes.Count % 4;
        if (over != 0)
        {
            _bytes.AddRange(new byte[4 - over]);
        }
    }
}

/// <summary>
/// The container half of glTF: a <c>.glb</c> and a <c>.gltf</c>, written and read. Rewriting a
/// document that already exists is a different job, with buffers it did not write and views it
/// must not move, and shares this file's container code rather than keeping its own.
/// </summary>
public static partial class GltfFiles
{
    private const uint Magic = 0x46546C67; // 'glTF'
    private const uint JsonChunk = 0x4E4F534A; // 'JSON'
    private const uint BinChunk = 0x004E4942; // 'BIN\0'

    /// <summary>
    /// A <c>.glb</c>: the document and its bytes in one container. The chunk padding is not
    /// decoration. A GLB's JSON chunk is padded with spaces and its binary chunk with zeroes,
    /// because the format says a reader may take a chunk at its stated length and find valid JSON
    /// there.
    /// </summary>
    public static byte[] GlbBytes(IReadOnlyDictionary<string, object?> json, ReadOnlySpan<byte> binary)
    {
        var text = System.Text.Encoding.UTF8.GetBytes(DartJson.Encode(Tidy(json)));
        var jsonLength = Aligned(text.Length);
        var binaryLength = Aligned(binary.Length);
        var total = 12 + 8 + jsonLength + (binary.IsEmpty ? 0 : 8 + binaryLength);

        var output = new byte[total];
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(0), Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(8), (uint)total);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(12), (uint)jsonLength);
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(16), JsonChunk);
        output.AsSpan(20, jsonLength).Fill(0x20);
        text.CopyTo(output, 20);

        if (!binary.IsEmpty)
        {
            var at = 20 + jsonLength;
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(at), (uint)binaryLength);
            BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(at + 4), BinChunk);
            binary.CopyTo(output.AsSpan(at + 8));
        }

        return output;
    }

    /// <summary>
    /// The two chunks of a <c>.glb</c>, or null if that is not what these bytes are. Lenient about
    /// the binary chunk and strict about the header. A file with no geometry is a real file, while
    /// a file with the wrong magic is something else entirely.
    /// </summary>
    public static (IReadOnlyDictionary<string, object?> Json, byte[] Binary)? GlbChunks(byte[] bytes)
    {
        ReadOnlySpan<byte> span = bytes;
        if (span.Length < 20
            || BinaryPrimitives.ReadUInt32LittleEndian(span) != Magic
            || BinaryPrimitives.ReadUInt32LittleEndian(span[4..]) != 2
            || BinaryPrimitives.ReadUInt32LittleEndian(span[16..]) != JsonChunk)
        {
            return null;
        }

        long jsonLength = BinaryPrimitives.ReadUInt32LittleEndian(span[12..]);
        if (20 + jsonLength > span.Length || JsonObject(span.Slice(20, (int)jsonLength)) is not { } parsed)
        {
            return null;
        }

        return (parsed, BinaryAfter(bytes, 20 + (int)jsonLength));
    }

    private static IReadOnlyDictionary<string, object?>? JsonObject(ReadOnlySpan<byte> chunk)
    {
        try
        {
            return DartJson.Decode(DartText.Decode(chunk)) as IReadOnlyDictionary<string, object?>;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static byte[] BinaryAfter(byte[] bytes, int at)
    {
        ReadOnlySpan<byte> span = bytes;
        if (at + 8L > span.Length)
        {
            return [];
        }

        long length = BinaryPrimitives.ReadUInt32LittleEndian(span[at..]);
        var kind = BinaryPrimitives.ReadUInt32LittleEndian(span[(at + 4)..]);
        return kind == BinChunk && at + 8 + length <= span.Length ? span.Slice(at + 8, (int)length).ToArray() : [];
    }

    /// <summary>
    /// A <c>.gltf</c>'s JSON, with its buffer named as the file beside it. A sidecar rather than a
    /// base64 data URI, because a data URI makes the document a third larger than the bytes it
    /// carries. Indented for the same reason. F5: the document gains or loses its <c>buffers</c>
    /// entry, as the Dart one does.
    /// </summary>
    public static string GltfText(OrderedDictionary<string, object?> json, string? buffer = null, int? byteLength = null)
    {
        if (buffer is not null && byteLength is > 0)
        {
            json["buffers"] = new List<object?>
            {
                new OrderedDictionary<string, object?> { ["uri"] = buffer, ["byteLength"] = (long)byteLength },
            };
        }
        else
        {
            json.Remove("buffers");
        }

        return DartJson.Encode(Tidy(json), "  ");
    }

    /// <summary>
    /// The same document with every whole number written as one. glTF is fussy about which of its
    /// fields are integers, and a count that arrives as <c>12.0</c> is a file some loaders refuse.
    /// A number that is not finite is left exactly as it is, and the encoder refuses it: a file
    /// carrying NaN is one no loader will open, and better said here, where the mesh that produced
    /// it is still in hand.
    /// </summary>
    internal static object? Tidy(object? value)
    {
        switch (value)
        {
            case IReadOnlyDictionary<string, object?> map:
                var copy = new OrderedDictionary<string, object?>();
                foreach (var (key, one) in map)
                {
                    copy[key] = Tidy(one);
                }

                return copy;
            case IReadOnlyList<object?> list:
                return list.Select(Tidy).ToList();
            case double number when double.IsFinite(number) && Math.Round(number) == number && Math.Abs(number) < 1e15:
                return (long)number;
            default:
                return value;
        }
    }

    private static int Aligned(int length) => length + ((4 - (length % 4)) % 4);
}
