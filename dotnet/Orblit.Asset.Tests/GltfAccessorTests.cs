using static Orblit.Asset.Tests.GltfFixtures;

namespace Orblit.Asset.Tests;

// Ported from the accessors group of gltf_document_test.dart.
public sealed class GltfAccessorTests
{
    private static readonly AssetId Id = AssetId.Parse("models/thing.glb");

    private static Task<GltfDocument> Of(GltfMap json, byte[] binary) =>
        GltfDocument.ReadAsync(Id, Glb(json, binary), new MemoryAssetSource());

    [Fact]
    public async Task ReadsTextureCoordinatesHoweverTheyAreStored()
    {
        byte[] bytes = [.. Bytes(0.0f, 0.5f, 1.0f, 0.25f), .. Bytes([0, 32768, 65535, 16384])];

        var document = await Of(
            Map("""
                {"asset": {"version": "2.0"},
                 "buffers": [{"byteLength": 24}],
                 "bufferViews": [
                   {"buffer": 0, "byteOffset": 0, "byteLength": 16},
                   {"buffer": 0, "byteOffset": 16, "byteLength": 8}],
                 "accessors": [
                   {"bufferView": 0, "componentType": 5126, "count": 2, "type": "VEC2"},
                   {"bufferView": 1, "componentType": 5123, "normalized": true, "count": 2, "type": "VEC2"}]}
                """),
            bytes);

        Assert.Equal(new[] { 0.0f, 0.5f, 1.0f, 0.25f }, document.ReadVec2(0));
        var quantized = document.ReadVec2(1);
        Assert.Equal(0.0f, quantized[0]);
        Assert.Equal(0.5f, quantized[1], 0.001f);
        Assert.Equal(1.0f, quantized[2]);
        Assert.Equal(0.25f, quantized[3], 0.001f);
    }

    [Fact]
    public async Task ReadsAnInterleavedViewAtItsStride()
    {
        // Position and UV in one buffer, thirty-two bytes apart: the layout a well-optimised exporter writes
        // and the one a naive reader gets wrong.
        var bytes = Bytes(
            1, 2, 3, 0.25f, // vertex 0: xyz, then u,v starts
            0.75f, 0, 0, 0,
            4, 5, 6, 0.5f,
            0.125f, 0, 0, 0);
        var document = await Of(
            Map($$"""
                {"asset": {"version": "2.0"},
                 "buffers": [{"byteLength": {{bytes.Length}}}],
                 "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": {{bytes.Length}}, "byteStride": 32}],
                 "accessors": [
                   {"bufferView": 0, "byteOffset": 0, "componentType": 5126, "count": 2, "type": "VEC3"},
                   {"bufferView": 0, "byteOffset": 12, "componentType": 5126, "count": 2, "type": "VEC2"}]}
                """),
            bytes);

        Assert.Equal(new float[] { 1, 2, 3, 4, 5, 6 }, document.ReadVec3(0));
        Assert.Equal(new[] { 0.25f, 0.75f, 0.5f, 0.125f }, document.ReadVec2(1));
        // Tightly packed on the way out, because it is about to be joined to another accessor's bytes and two
        // strides cannot be concatenated.
        Assert.Equal(16, document.AccessorBytes(1).Length);
        Assert.Equal(new[] { 0.25f, 0.75f, 0.5f, 0.125f }, Floats(document.AccessorBytes(1)));
    }

    [Fact]
    public async Task NarrowsNewIndicesToTheSmallestWidthThatHoldsThem()
    {
        var document = await Of(Map("""{"asset": {"version": "2.0"}}"""), []);

        Assert.Equal(5121, document.Accessor(document.AddIndices([0, 1, 2]))["componentType"]);
        Assert.Equal(5123, document.Accessor(document.AddIndices([0, 300]))["componentType"]);
        Assert.Equal(5125, document.Accessor(document.AddIndices([0, 70000]))["componentType"]);
    }

    [Fact]
    public async Task RefusesASparseAccessorInsteadOfReadingItsBase()
    {
        var document = await Of(
            Map("""
                {"asset": {"version": "2.0"},
                 "accessors": [{"componentType": 5126, "count": 1, "type": "VEC2", "sparse": {"count": 1}}]}
                """),
            []);

        var error = Assert.Throws<FormatException>(() => document.ReadVec2(0));

        Assert.Contains("sparse", error.Message, StringComparison.Ordinal);
    }
}
