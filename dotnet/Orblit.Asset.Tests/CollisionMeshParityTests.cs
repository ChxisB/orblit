using System.Text;
using System.Text.Json.Nodes;
using Orblit.Parity;
using static Orblit.Asset.Tests.AssetCases;
using static Orblit.Asset.Tests.PackCases;

namespace Orblit.Asset.Tests;

public class CollisionMeshParityTests
{
    private static readonly Fixture Dart = Fixture.Load("orblit_asset", "collision");

    [Fact]
    public void DecodeMatchesDart() =>
        Dart.Check("decode", c => Dump.Attempt(() => Out(CollisionMesh.Decode(Encoding.UTF8.GetBytes(Text(c, 0))))));

    [Fact]
    public void ObjMatchesDart() =>
        Dart.Check("obj", c => Dump.Attempt(() => Out(CollisionMesh.FromObj(Encoding.UTF8.GetBytes(Text(c, 0))))));

    [Fact]
    public Task GltfMatchesDart() =>
        CheckAsync(Dart, "gltf", async c => Out(await CollisionMesh.FromGltfAsync(
            AssetId.Parse("floor.gltf"), Encoding.UTF8.GetBytes(Text(c, 0)), new MemoryAssetSource())));

    private static JsonObject Out(CollisionMesh mesh) => new()
    {
        ["vertices"] = Dump.Arr(mesh.Vertices, Dump.Num),
        ["indices"] = Dump.Arr(mesh.Indices, i => (JsonNode?)i),
        ["encoded"] = Encoding.UTF8.GetString(mesh.Encode()),
    };
}
