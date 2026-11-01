using System.Text;
using Orblit.Json;
using static Orblit.Asset.Tests.GltfFixtures;

namespace Orblit.Asset.Tests;

// The model the collision tests build: the unit quad at y = 0 as one mesh of one primitive.
internal static class CollisionModels
{
    public static readonly double[] Positions = [-1, 0, -1, 1, 0, -1, 1, 0, 1, -1, 0, 1];

    public static readonly ushort[] Quad = [0, 2, 1, 0, 3, 2];

    public static GltfMap Model(string? nodes = null, int mode = 4, ushort[]? indices = null, bool skin = false)
    {
        indices ??= Quad;
        var binary = Bytes([.. Positions.Select(p => (float)p)]).Concat(Bytes(indices)).ToArray();
        var node = skin ? """{"mesh":0,"skin":0}""" : """{"mesh":0}""";
        return Map($$"""
            {
              "asset": {"version": "2.0"},
              "buffers": [{"byteLength": {{binary.Length}}, "uri": "data:application/octet-stream;base64,{{Convert.ToBase64String(binary)}}"}],
              "bufferViews": [
                {"buffer": 0, "byteLength": {{Positions.Length * 4}}},
                {"buffer": 0, "byteOffset": {{Positions.Length * 4}}, "byteLength": {{indices.Length * 2}}}
              ],
              "accessors": [
                {"bufferView": 0, "componentType": 5126, "count": 4, "type": "VEC3"},
                {"bufferView": 1, "componentType": 5123, "count": {{indices.Length}}, "type": "SCALAR"}
              ],
              "meshes": [{"primitives": [{"attributes": {"POSITION": 0}, "indices": 1, "mode": {{mode}}}]}],
              "nodes": {{nodes ?? $"[{node}]"}},
              "scenes": [{"nodes": [0]}],
              "scene": 0
            }
            """);
    }

    public static byte[] Write(GltfMap model) => Encoding.UTF8.GetBytes(JsonText.Encode(model));

    public static Task<CollisionMesh> ReadAsync(byte[] bytes) =>
        CollisionMesh.FromGltfAsync(AssetId.Parse("floor.gltf"), bytes, new MemoryAssetSource());

    public static Task<CollisionMesh> ReadAsync(GltfMap model) => ReadAsync(Write(model));
}
