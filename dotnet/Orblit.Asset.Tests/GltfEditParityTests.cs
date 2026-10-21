using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Json;
using Orblit.Parity;
using Orblit.VectorMath;
using static Orblit.Asset.Tests.AssetCases;
using static Orblit.Asset.Tests.PackCases;

namespace Orblit.Asset.Tests;

public class GltfEditParityTests
{
    private static readonly Fixture Dart = Fixture.Load("orblit_asset", "gltf_edit");

    [Fact]
    public Task MergeMaterialsMatchesDart() => CheckAsync(Dart, "mergeMaterials", async c =>
    {
        var document = await Open(c);
        return new JsonObject
        {
            ["count"] = GltfMerge.MergeMaterials(document),
            ["json"] = DartJson.Encode(document.Json),
        };
    });

    [Fact]
    public Task MergePrimitivesMatchesDart() => CheckAsync(Dart, "mergePrimitives", async c =>
    {
        var document = await Open(c);
        var count = GltfMerge.MergePrimitives(document, In(c, 1).GetInt32());
        var parts = Parts(document.ToGlb());
        parts["count"] = count;
        return parts;
    });

    [Fact]
    public Task GroupKeyMatchesDart() => CheckAsync(Dart, "groupKey", async c =>
    {
        var document = await Open(c);
        var keys = document.List("meshes")
            .SelectMany(mesh => GltfJson.Get(mesh, "primitives") is List<object?> list ? list.OfType<GltfMap>() : [])
            .Select(primitive => GltfMerge.PrimitiveGroupKey(document, primitive));
        return new JsonObject { ["keys"] = Dump.Arr(keys, key => key is null ? null : Dump.Text(key)) };
    });

    [Fact]
    public Task BakeMatchesDart() => CheckAsync(Dart, "bake", async c =>
    {
        var placements = In(c, 1).EnumerateArray().Select(Placement).ToList();
        var baked = await GltfBake.BakeInstancesAsync(
            Convert.FromBase64String(Text(c, 0)), placements, In(c, 2).GetInt32());
        return baked is null ? null : Parts(baked);
    });

    private static Task<GltfDocument> Open(JsonElement c) => GltfDocument.ReadAsync(
        AssetId.Parse("models/thing.glb"), Convert.FromBase64String(Text(c, 0)), new MemoryAssetSource());

    private static Matrix4 Placement(JsonElement values)
    {
        var matrix = Matrix4.Zero();
        for (var i = 0; i < 16; i++)
        {
            matrix[i] = Fixture.Number(values[i]);
        }

        return matrix;
    }

    // A glb as its JSON text and its binary chunk, the way the Dart exporter wrote it.
    private static JsonObject Parts(byte[] glb)
    {
        var at = 20 + (int)BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(12));
        var end = at + 8 + (int)BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(at));
        return new JsonObject
        {
            ["json"] = Encoding.UTF8.GetString(glb, 20, at - 20),
            ["bin"] = Convert.ToBase64String(glb, at + 8, end - at - 8),
        };
    }
}
