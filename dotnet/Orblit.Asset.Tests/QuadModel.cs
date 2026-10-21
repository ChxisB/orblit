using System.Runtime.InteropServices;
using static Orblit.Asset.Tests.GltfFixtures;

namespace Orblit.Asset.Tests;

/// <summary>A model of unit quads, each drawn with a material the test picks.</summary>
internal sealed class QuadModel
{
    private readonly List<byte> _bin = [];

    public GltfMap Json { get; } = Map("""
        {"asset": {"version": "2.0"},
         "materials": [],
         "meshes": [{"primitives": []}],
         "nodes": [{"mesh": 0}],
         "scenes": [{"nodes": [0]}],
         "scene": 0,
         "accessors": [],
         "bufferViews": []}
        """);

    public List<object?> Primitives =>
        (List<object?>)((GltfMap)((List<object?>)Json["meshes"]!)[0]!)["primitives"]!;

    /// <summary>Adds a material, returning its index.</summary>
    public int Material(GltfMap value)
    {
        var materials = (List<object?>)Json["materials"]!;
        materials.Add(value);
        return materials.Count - 1;
    }

    /// <summary>Adds a quad drawn with <paramref name="material"/> starting at <paramref name="x"/>.</summary>
    public void Quad(int material, double x = 0)
    {
        var position = Accessor(
            Bytes((float)x, 0, 0, (float)x + 1, 0, 0, (float)x + 1, 1, 0, (float)x, 1, 0),
            5126,
            4,
            "VEC3",
            new GltfMap { ["min"] = new List<object?> { x, 0.0, 0.0 }, ["max"] = new List<object?> { x + 1, 1.0, 0.0 } });
        var texCoord = Accessor(Bytes(0f, 0, 1, 0, 1, 1, 0, 1), 5126, 4, "VEC2");
        var index = Accessor(Bytes([0, 1, 2, 0, 2, 3]), 5123, 6, "SCALAR");

        Primitives.Add(new GltfMap
        {
            ["attributes"] = new GltfMap { ["POSITION"] = position, ["TEXCOORD_0"] = texCoord },
            ["indices"] = index,
            ["material"] = material,
            ["mode"] = 4,
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

    private int Accessor(byte[] bytes, int componentType, int count, string type, GltfMap? extra = null)
    {
        var views = (List<object?>)Json["bufferViews"]!;
        views.Add(new GltfMap { ["buffer"] = 0, ["byteOffset"] = _bin.Count, ["byteLength"] = bytes.Length });
        _bin.AddRange(bytes);
        _bin.AddRange(new byte[(4 - (_bin.Count % 4)) % 4]);

        var value = new GltfMap
        {
            ["bufferView"] = views.Count - 1,
            ["componentType"] = componentType,
            ["count"] = count,
            ["type"] = type,
        };
        foreach (var (key, entry) in extra ?? [])
        {
            value[key] = entry;
        }

        var accessors = (List<object?>)Json["accessors"]!;
        accessors.Add(value);
        return accessors.Count - 1;
    }
}
