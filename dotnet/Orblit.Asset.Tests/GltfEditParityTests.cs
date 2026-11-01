using System.Text.Json;
using System.Text.Json.Nodes;
using Orblit.Json;
using Orblit.Parity;
using Orblit.VectorMath;
using static Orblit.Asset.Tests.AssetCases;
using static Orblit.Asset.Tests.GltfFixtures;
using static Orblit.Asset.Tests.PackCases;

namespace Orblit.Asset.Tests;

public class GltfEditParityTests
{
    private static readonly Fixture Golden = Fixture.Load("orblit_asset", "gltf_edit");

    [Fact]
    public Task MergeMaterialsMatchesTheFixture() => CheckAsync(Golden, "mergeMaterials", async c =>
    {
        var document = await Open(c);
        return new JsonObject
        {
            ["count"] = GltfMerge.MergeMaterials(document),
            ["json"] = JsonText.Encode(document.Json),
        };
    });

    [Fact]
    public Task MergePrimitivesMatchesTheFixture() => CheckAsync(Golden, "mergePrimitives", async c =>
    {
        var document = await Open(c);
        var count = GltfMerge.MergePrimitives(document, In(c, 1).GetInt32());
        var parts = GlbParts(document.ToGlb());
        parts["count"] = count;
        return parts;
    });

    [Fact]
    public Task GroupKeyMatchesTheFixture() => CheckAsync(Golden, "groupKey", async c =>
    {
        var document = await Open(c);
        var keys = document.List("meshes")
            .SelectMany(mesh => GltfJson.Get(mesh, "primitives") is List<object?> list ? list.OfType<GltfMap>() : [])
            .Select(primitive => GltfMerge.PrimitiveGroupKey(document, primitive));
        return new JsonObject { ["keys"] = Dump.Arr(keys, key => key is null ? null : Dump.Text(key)) };
    });

    [Fact]
    public Task BakeMatchesTheFixture() => CheckAsync(Golden, "bake", async c =>
    {
        var placements = In(c, 1).EnumerateArray().Select(Placement).ToList();
        var baked = await GltfBake.BakeInstancesAsync(
            Convert.FromBase64String(Text(c, 0)), placements, In(c, 2).GetInt32());
        return baked is null ? null : GlbParts(baked);
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
}
