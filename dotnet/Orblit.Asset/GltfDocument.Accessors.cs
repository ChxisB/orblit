using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using Orblit.VectorMath;

namespace Orblit.Asset;

// Reading and writing the accessors a rewrite touches. Only what a rewrite needs is here: texture
// coordinates, indices, positions, and a raw per-element copy for everything else. An accessor this
// does not understand is left exactly where it is, which is the point of editing a document rather
// than rebuilding one.
public sealed partial class GltfDocument
{
    private const int ArrayBuffer = 34962;
    private const int ElementArrayBuffer = 34963;

    /// <summary>One accessor's definition.</summary>
    public GltfMap Accessor(int index)
    {
        if (GltfJson.Get(Json, "accessors") is not List<object?> accessors || index < 0 || index >= accessors.Count)
        {
            throw new FormatException($"there is no accessor {index}");
        }

        var found = accessors[index] as GltfMap ?? throw new FormatException($"accessor {index} is not an object");
        // A sparse accessor stores overrides in a second pair of views. Nothing in this rewrite reads one,
        // and silently reading only the base would move the wrong vertices.
        return found.ContainsKey("sparse")
            ? throw new FormatException($"accessor {index} is sparse, which this cook does not rewrite")
            : found;
    }

    /// <summary>How many elements an accessor holds.</summary>
    public int AccessorCount(int index) => GltfJson.IntOr(Accessor(index), "count", 0);

    /// <summary>
    /// The bytes of element <paramref name="element"/> of an accessor, laid out as glTF has them. Used when
    /// merging primitives: an attribute whose meaning this rewrite has no opinion on still has to end up in
    /// the merged buffer, and copying the element whole is both exact and format-agnostic.
    /// </summary>
    public ReadOnlyMemory<byte> ElementBytes(int index, int element)
    {
        var size = ElementSize(index);
        // No view means every element is zero, which is legal and does happen in documents built by tools
        // that pad an attribute set.
        if (GltfJson.IntAt(Accessor(index), "bufferView") is not { } view)
        {
            return new byte[size];
        }

        var bytes = ViewBytes(view);
        var stride = ViewStride(view) ?? size;
        var at = GltfJson.IntOr(Accessor(index), "byteOffset", 0) + (element * stride);
        return at + size > bytes.Length
            ? throw new FormatException($"accessor {index} reads past its buffer view")
            : bytes.Slice(at, size);
    }

    /// <summary>
    /// Every element of an accessor, tightly packed, whatever stride the view it came from used. Tightly
    /// packed rather than as found because the result is about to be concatenated with another accessor's,
    /// and two interleaved buffers cannot be joined end to end and still be read at either stride.
    /// </summary>
    public byte[] AccessorBytes(int index)
    {
        var it = Accessor(index);
        var count = GltfJson.IntOr(it, "count", 0);
        var size = ElementSize(index);
        if (GltfJson.IntAt(it, "bufferView") is not { } view)
        {
            return new byte[count * size];
        }

        var bytes = ViewBytes(view).Span;
        var stride = ViewStride(view) ?? size;
        var offset = GltfJson.IntOr(it, "byteOffset", 0);
        // The common case by a wide margin: one accessor over its own view, or a run of one inside a shared
        // one. Copying it whole is one memory move rather than one per vertex, which on a million-vertex mesh
        // is the difference between a cook you wait for and one you notice.
        if (stride == size && offset + (count * size) <= bytes.Length)
        {
            return bytes.Slice(offset, count * size).ToArray();
        }

        var result = new byte[count * size];
        for (var i = 0; i < count; i++)
        {
            var at = offset + (i * stride);
            if (at + size > bytes.Length)
            {
                throw new FormatException($"accessor {index} reads past its buffer view");
            }

            bytes.Slice(at, size).CopyTo(result.AsSpan(i * size));
        }

        return result;
    }

    /// <summary>How many bytes one element of an accessor takes, tightly packed.</summary>
    public int ElementSize(int index)
    {
        var it = Accessor(index);
        // glTF pads the columns of a MAT2 or MAT3 with one or two byte components to four bytes. This does
        // not, so such an accessor is read as if it were packed.
        return ComponentType.ComponentsOf(AsType(it)) * ComponentType.SizeOf(GltfJson.RequiredInt(it, "componentType"));
    }

    /// <summary>
    /// A VEC2 accessor as pairs of floats, whatever it is stored as. <c>KHR_mesh_quantization</c> lets texture
    /// coordinates be bytes or shorts, and a normalized integer means what its normalized value means, so
    /// reading gives floats in every case and the caller never asks how they were kept.
    /// </summary>
    public float[] ReadVec2(int index) => ReadVector(index, "VEC2", 2);

    /// <summary>A VEC3 accessor as triples of floats, whatever it is stored as.</summary>
    public float[] ReadVec3(int index) => ReadVector(index, "VEC3", 3);

    /// <summary>An index accessor as a list of ints, whatever width it is stored at.</summary>
    public uint[] ReadIndices(int index)
    {
        var it = Accessor(index);
        if (!Equals(GltfJson.Get(it, "type"), "SCALAR"))
        {
            throw new FormatException($"index accessor {index} is {Describe(it, "type")}, not SCALAR");
        }

        var count = GltfJson.IntOr(it, "count", 0);
        var type = GltfJson.RequiredInt(it, "componentType");
        var result = new uint[count];
        if (GltfJson.IntAt(it, "bufferView") is not { } view)
        {
            return result;
        }

        var data = ViewBytes(view).Span;
        var width = ComponentType.SizeOf(type);
        var stride = ViewStride(view) ?? width;
        var offset = GltfJson.IntOr(it, "byteOffset", 0);
        for (var i = 0; i < count; i++)
        {
            var at = offset + (i * stride);
            result[i] = type switch
            {
                ComponentType.UnsignedByte => data[at],
                ComponentType.UnsignedShort => BinaryPrimitives.ReadUInt16LittleEndian(data[at..]),
                ComponentType.UnsignedInt => BinaryPrimitives.ReadUInt32LittleEndian(data[at..]),
                _ => throw new FormatException($"indices cannot be component type {type}"),
            };
        }

        return result;
    }

    /// <summary>Adds a VEC2 float accessor over <paramref name="values"/>, returning its index.</summary>
    public int AddVec2(float[] values)
    {
        var view = AddView(System.Runtime.InteropServices.MemoryMarshal.AsBytes(values.AsSpan()), target: ArrayBuffer);
        return Add("accessors", new GltfMap
        {
            ["bufferView"] = view,
            ["componentType"] = ComponentType.Float,
            ["count"] = values.Length / 2,
            ["type"] = "VEC2",
        });
    }

    /// <summary>
    /// Adds an index accessor over <paramref name="values"/>, narrowed to the smallest width that holds them.
    /// Width is not cosmetic here: a merged primitive that stays under 65,536 vertices keeps 16-bit indices,
    /// which is half the bandwidth per draw and is what the merge was for.
    /// </summary>
    public int AddIndices(IReadOnlyList<int> values)
    {
        var highest = 0;
        foreach (var value in values)
        {
            if (value > highest)
            {
                highest = value;
            }
        }

        var (type, bytes) = highest switch
        {
            < 256 => (ComponentType.UnsignedByte, [.. values.Select(value => (byte)value)]),
            < 65536 => (ComponentType.UnsignedShort, Pack(values, 2)),
            _ => (ComponentType.UnsignedInt, Pack(values, 4)),
        };
        var view = AddView(bytes, target: ElementArrayBuffer);
        return Add("accessors", new GltfMap
        {
            ["bufferView"] = view,
            ["componentType"] = type,
            ["count"] = values.Count,
            ["type"] = "SCALAR",
        });
    }

    /// <summary>
    /// Adds an accessor over raw element bytes copied from another accessor, keeping its type, component type
    /// and normalization. <c>min</c> and <c>max</c> are glTF-required on <c>POSITION</c>, so they are carried
    /// when the caller has recomputed them and dropped otherwise: a stale bounding box is worse than none,
    /// because culling believes it.
    /// </summary>
    public int AddLike(int template, ReadOnlySpan<byte> bytes, int count, IReadOnlyList<double>? min = null, IReadOnlyList<double>? max = null)
    {
        var it = Accessor(template);
        var view = AddView(bytes, target: ArrayBuffer);
        var like = new GltfMap
        {
            ["bufferView"] = view,
            ["componentType"] = GltfJson.Get(it, "componentType"),
            ["count"] = count,
            ["type"] = GltfJson.Get(it, "type"),
        };
        if (GltfJson.Get(it, "normalized") is true)
        {
            like["normalized"] = true;
        }

        if (min is not null)
        {
            like["min"] = new List<object?>(min.Select(value => (object?)value));
        }

        if (max is not null)
        {
            like["max"] = new List<object?>(max.Select(value => (object?)value));
        }

        return Add("accessors", like);
    }

    private float[] ReadVector(int index, string type, int components)
    {
        var it = Accessor(index);
        if (!Equals(GltfJson.Get(it, "type"), type))
        {
            throw new FormatException($"accessor {index} is {Describe(it, "type")}, not {type}");
        }

        var count = GltfJson.IntOr(it, "count", 0);
        var componentType = GltfJson.RequiredInt(it, "componentType");
        var normalized = GltfJson.Get(it, "normalized") is true;
        var result = new float[count * components];
        if (GltfJson.IntAt(it, "bufferView") is not { } view)
        {
            return result;
        }

        var data = ViewBytes(view).Span;
        var width = ComponentType.SizeOf(componentType);
        var stride = ViewStride(view) ?? (width * components);
        var offset = GltfJson.IntOr(it, "byteOffset", 0);
        for (var i = 0; i < count; i++)
        {
            var at = offset + (i * stride);
            for (var c = 0; c < components; c++)
            {
                result[(i * components) + c] = (float)ReadComponent(data, at + (c * width), componentType, normalized);
            }
        }

        return result;
    }

    private static string AsType(GltfMap accessor) =>
        GltfJson.Get(accessor, "type") as string ?? throw new InvalidCastException("An accessor's type is not a string.");

    // What Dart prints for the value, which is the word itself and null when there is none.
    private static string Describe(GltfMap map, string key) => GltfJson.Get(map, key)?.ToString() ?? "null";

    private static byte[] Pack(IReadOnlyList<int> values, int width)
    {
        var bytes = new byte[values.Count * width];
        for (var i = 0; i < values.Count; i++)
        {
            if (width == 2)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(i * 2), (ushort)values[i]);
            }
            else
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i * 4), (uint)values[i]);
            }
        }

        return bytes;
    }

    [SuppressMessage("Maintainability", "CA1502:Avoid excessive complexity", Justification = "F2: a list of cases is data.")]
    private static double ReadComponent(ReadOnlySpan<byte> data, int at, int type, bool normalized) => type switch
    {
        ComponentType.Float => BinaryPrimitives.ReadSingleLittleEndian(data[at..]),
        ComponentType.UnsignedByte => normalized ? data[at] / 255.0 : data[at],
        ComponentType.Byte => normalized ? Doubles.Clamp((sbyte)data[at] / 127.0, -1.0, 1.0) : (sbyte)data[at],
        ComponentType.UnsignedShort =>
            normalized ? BinaryPrimitives.ReadUInt16LittleEndian(data[at..]) / 65535.0 : BinaryPrimitives.ReadUInt16LittleEndian(data[at..]),
        ComponentType.Short =>
            normalized
                ? Doubles.Clamp(BinaryPrimitives.ReadInt16LittleEndian(data[at..]) / 32767.0, -1.0, 1.0)
                : BinaryPrimitives.ReadInt16LittleEndian(data[at..]),
        ComponentType.UnsignedInt => BinaryPrimitives.ReadUInt32LittleEndian(data[at..]),
        _ => throw new FormatException($"{type} is not a glTF component type"),
    };
}
