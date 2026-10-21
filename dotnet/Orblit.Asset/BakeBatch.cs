using System.Buffers;
using System.Runtime.InteropServices;
using Orblit.VectorMath;

namespace Orblit.Asset;

/// <summary>One primitive of the model, read once and placed many times.</summary>
internal sealed class BakeSource
{
    public BakeSource(GltfDocument document, GltfMap primitive)
    {
        Primitive = primitive;
        foreach (var (name, value) in (GltfMap)primitive["attributes"]!)
        {
            var index = GltfJson.Saturated(value);
            switch (name)
            {
                case "POSITION":
                    Positions = document.ReadVec3(index);
                    break;
                case "NORMAL":
                    Normals = document.ReadVec3(index);
                    break;
                case "TANGENT":
                    Tangents = MemoryMarshal.Cast<byte, float>(document.AccessorBytes(index)).ToArray();
                    break;
                default:
                    Raw[name] = document.AccessorBytes(index);
                    break;
            }
        }

        Indices = GltfJson.Get(primitive, "indices") is { } indexed && GltfJson.IsInt(indexed)
            ? document.ReadIndices(GltfJson.Saturated(indexed))
            : [.. Enumerable.Range(0, Positions.Length / 3).Select(i => (uint)i)];
    }

    public GltfMap Primitive { get; }

    public float[] Positions { get; } = [];

    public float[]? Normals { get; }

    public float[]? Tangents { get; }

    public OrderedDictionary<string, byte[]> Raw { get; } = [];

    public uint[] Indices { get; }

    public int Vertices => Positions.Length / 3;
}

/// <summary>
/// Primitives that share everything that makes a draw, filled one placement at a time and closed before the
/// vertex count would widen the indices.
/// </summary>
internal sealed class BakeBatch(GltfDocument document, int maxVertices)
{
    private readonly List<GltfMap> _done = [];
    private readonly Dictionary<string, ArrayBufferWriter<byte>> _bytes = [];
    private readonly List<int> _indices = [];
    private GltfMap? _template;
    private int _vertices;
    private double[]? _low;
    private double[]? _high;

    public void Add(BakeSource source, Matrix4 matrix)
    {
        if (_vertices > 0 && (long)_vertices + source.Vertices > maxVertices)
        {
            Close();
        }

        _template ??= source.Primitive;
        var positions = Points(source.Positions, matrix);
        _low ??= [double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity];
        _high ??= [double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity];
        GltfMerge.Widen(positions, _low, _high);
        Append("POSITION", positions);
        // The renderer turns a tangent with the normal matrix and leaves its sign alone, mirrored or not.
        // Doing the same keeps a baked group looking like the objects it replaced.
        var turn = new Matrix3();
        turn.CopyNormalMatrix(matrix);
        if (source.Normals is { } normals)
        {
            Append("NORMAL", Directions(normals, 3, turn));
        }

        if (source.Tangents is { } tangents)
        {
            Append("TANGENT", Directions(tangents, 4, turn));
        }

        foreach (var (name, bytes) in source.Raw)
        {
            Write(name, bytes);
        }

        AddIndices(source.Indices, matrix.Determinant() < 0);
        _vertices += source.Vertices;
    }

    public List<GltfMap> Finish()
    {
        Close();
        return _done;
    }

    private static float[] Points(float[] from, Matrix4 m)
    {
        var result = new float[from.Length];
        for (var i = 0; i < from.Length; i += 3)
        {
            double x = from[i];
            double y = from[i + 1];
            double z = from[i + 2];
            result[i] = (float)((m[0] * x) + (m[4] * y) + (m[8] * z) + m[12]);
            result[i + 1] = (float)((m[1] * x) + (m[5] * y) + (m[9] * z) + m[13]);
            result[i + 2] = (float)((m[2] * x) + (m[6] * y) + (m[10] * z) + m[14]);
        }

        return result;
    }

    // Unit directions turned by s. An element is stride floats: the first three are the direction, and a
    // fourth is a sign that is left as it is.
    private static float[] Directions(float[] from, int stride, Matrix3 s)
    {
        var result = new float[from.Length];
        for (var i = 0; i < from.Length; i += stride)
        {
            double x = from[i];
            double y = from[i + 1];
            double z = from[i + 2];
            var a = (s[0] * x) + (s[3] * y) + (s[6] * z);
            var b = (s[1] * x) + (s[4] * y) + (s[7] * z);
            var c = (s[2] * x) + (s[5] * y) + (s[8] * z);
            var length = new Vector3(a, b, c).Length;
            var scale = length == 0 ? 1.0 : 1 / length;
            result[i] = (float)(a * scale);
            result[i + 1] = (float)(b * scale);
            result[i + 2] = (float)(c * scale);
            if (stride == 4)
            {
                result[i + 3] = from[i + 3];
            }
        }

        return result;
    }

    private void Append(string name, float[] values) => Write(name, MemoryMarshal.AsBytes(values.AsSpan()));

    private void Write(string name, ReadOnlySpan<byte> bytes)
    {
        if (!_bytes.TryGetValue(name, out var writer))
        {
            _bytes[name] = writer = new ArrayBufferWriter<byte>();
        }

        writer.Write(bytes);
    }

    // A mirror turns every triangle inside out, so two of its corners swap to keep the front face out.
    private void AddIndices(uint[] indices, bool mirrored)
    {
        for (var i = 0; i + 2 < indices.Length; i += 3)
        {
            _indices.Add(Offset(indices[i]));
            _indices.Add(Offset(indices[mirrored ? i + 2 : i + 1]));
            _indices.Add(Offset(indices[mirrored ? i + 1 : i + 2]));
        }
    }

    // Dart's int is 64 bits and wraps nothing. An index at or past 2^31 throws here instead.
    private int Offset(uint index) => checked((int)(_vertices + index));

    private void Close()
    {
        if (_template is not { } template || _vertices == 0)
        {
            return;
        }

        var attributes = new GltfMap();
        foreach (var (name, value) in (GltfMap)template["attributes"]!)
        {
            var position = name == "POSITION";
            attributes[name] = document.AddLike(
                GltfJson.Saturated(value),
                _bytes[name].WrittenSpan,
                _vertices,
                position ? _low : null,
                position ? _high : null);
        }

        var primitive = new GltfMap { ["attributes"] = attributes, ["indices"] = document.AddIndices(_indices) };
        if (GltfJson.Get(template, "material") is { } material)
        {
            primitive["material"] = material;
        }

        if (GltfJson.Get(template, "extras") is { } extras)
        {
            primitive["extras"] = extras;
        }

        _done.Add(primitive);
        _bytes.Clear();
        _indices.Clear();
        _low = null;
        _high = null;
        _vertices = 0;
    }
}
