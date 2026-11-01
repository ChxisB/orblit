using System.Runtime.InteropServices;
using static Orblit.Asset.Tests.GltfFixtures;

namespace Orblit.Asset.Tests;

/// <summary>
/// A model of triangles. Each has a normal along +z and a tangent along +x with sign +1, so a placement
/// that turns or mirrors it is easy to read.
/// </summary>
internal sealed class TriangleModel
{
    private readonly List<byte> _bin = [];

    public GltfMap Json { get; } = Map("""
        {"asset": {"version": "2.0"},
         "materials": [{}],
         "meshes": [{"primitives": []}],
         "nodes": [{"mesh": 0}],
         "scenes": [{"nodes": [0]}],
         "scene": 0,
         "accessors": [],
         "bufferViews": []}
        """);

    public List<object?> Primitives =>
        (List<object?>)((GltfMap)((List<object?>)Json["meshes"]!)[0]!)["primitives"]!;

    public int Material(GltfMap value)
    {
        var materials = (List<object?>)Json["materials"]!;
        materials.Add(value);
        return materials.Count - 1;
    }

    /// <summary>A triangle whose corners are (0,0,0), (1,0,0) and (0,1,0).</summary>
    public void Triangle(int material = 0, double[]? normal = null)
    {
        float[] lean = normal is null ? [0, 0, 1] : [.. normal.Select(v => (float)v)];
        Primitives.Add(new GltfMap
        {
            ["attributes"] = new GltfMap
            {
                ["POSITION"] = Accessor(Bytes(0f, 0, 0, 1, 0, 0, 0, 1, 0), 5126, 3, "VEC3"),
                ["NORMAL"] = Accessor(Bytes([.. lean, .. lean, .. lean]), 5126, 3, "VEC3"),
                ["TANGENT"] = Accessor(Bytes(1f, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1), 5126, 3, "VEC4"),
                ["TEXCOORD_0"] = Accessor(Bytes(0f, 0, 1, 0, 0, 1), 5126, 3, "VEC2"),
            },
            ["indices"] = Accessor(Bytes([0, 1, 2]), 5123, 3, "SCALAR"),
            ["material"] = material,
        });
    }

    /// <summary>Points the last primitive at 32-bit indices.</summary>
    public void IndexWith(params uint[] values) =>
        ((GltfMap)Primitives[^1]!)["indices"] =
            Accessor(MemoryMarshal.AsBytes(values.AsSpan()).ToArray(), 5125, values.Length, "SCALAR");

    public byte[] ToGlb()
    {
        Json["buffers"] = new List<object?> { new GltfMap { ["byteLength"] = _bin.Count } };
        return Glb(Json, [.. _bin]);
    }

    private int Accessor(byte[] bytes, int componentType, int count, string type)
    {
        var views = (List<object?>)Json["bufferViews"]!;
        views.Add(new GltfMap { ["buffer"] = 0, ["byteOffset"] = _bin.Count, ["byteLength"] = bytes.Length });
        _bin.AddRange(bytes);
        _bin.AddRange(new byte[(4 - (_bin.Count % 4)) % 4]);

        var accessors = (List<object?>)Json["accessors"]!;
        accessors.Add(new GltfMap
        {
            ["bufferView"] = views.Count - 1,
            ["componentType"] = componentType,
            ["count"] = count,
            ["type"] = type,
        });
        return accessors.Count - 1;
    }
}
