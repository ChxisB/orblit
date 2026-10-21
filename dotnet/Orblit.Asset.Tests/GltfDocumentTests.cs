using System.Buffers.Binary;
using System.Text;
using Orblit.Json;
using static Orblit.Asset.Tests.GltfFixtures;

namespace Orblit.Asset.Tests;

// Ported from gltf_document_test.dart.
public sealed class GltfDocumentTests
{
    private static readonly AssetId Id = AssetId.Parse("models/thing.glb");
    private static readonly MemoryAssetSource Empty = new();

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public async Task ReadsAGlbAndGivesBackTheBytesItsViewsCover()
    {
        float[] values = [1, 2, 3, 4, 5, 6];
        var document = await GltfDocument.ReadAsync(
            Id,
            Glb(
                Map("""
                    {"asset": {"version": "2.0"},
                     "buffers": [{"byteLength": 24}],
                     "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": 24}]}
                    """),
                Bytes(values)),
            Empty);

        Assert.Equal(values, Floats(document.ViewBytes(0)));
    }

    [Fact]
    public async Task ADocumentWithNoGeometryNamesNoBuffer()
    {
        // A buffer of nought bytes is an error in the format, and a scene of lights and cameras really has
        // nothing to put in one. Writing the empty buffer anyway made a file the validator refused.
        var document = await GltfDocument.ReadAsync(
            AssetId.Parse("models/lights.gltf"),
            Utf8("""{"asset": {"version": "2.0"}, "nodes": [{"name": "sun"}]}"""),
            Empty);

        var glb = document.ToGlb();
        var jsonLength = (int)BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(12));

        Assert.Equal(20 + jsonLength, glb.Length);
        var written = (GltfMap)DartJson.Decode(Encoding.UTF8.GetString(glb, 20, jsonLength).TrimEnd())!;
        Assert.False(written.ContainsKey("buffers"));
    }

    [Fact]
    public async Task ReadsAGltfItsDataUrisAndTheFilesBesideIt()
    {
        var beside = new byte[] { 9, 9, 9, 9 };
        var document = await GltfDocument.ReadAsync(
            AssetId.Parse("models/thing.gltf"),
            Utf8($$"""
                {"asset": {"version": "2.0"},
                 "buffers": [
                   {"uri": "geometry.bin", "byteLength": 4},
                   {"uri": "data:application/octet-stream;base64,{{Convert.ToBase64String([1, 2, 3, 4])}}", "byteLength": 4}],
                 "bufferViews": [
                   {"buffer": 0, "byteOffset": 0, "byteLength": 4},
                   {"buffer": 1, "byteOffset": 0, "byteLength": 4}]}
                """),
            new MemoryAssetSource(new Dictionary<AssetId, byte[]> { [AssetId.Parse("models/geometry.bin")] = beside }));

        Assert.Equal(beside, document.ViewBytes(0).ToArray());
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, document.ViewBytes(1).ToArray());
    }

    [Fact]
    public async Task FlattensEveryBufferIntoOneWhenItWritesAGlb()
    {
        // Two buffers of a length that is not a multiple of four, so that a rewrite which forgot to align the
        // second would be read at the wrong offset rather than merely being untidy.
        var document = await GltfDocument.ReadAsync(
            AssetId.Parse("models/thing.gltf"),
            Utf8("""
                {"asset": {"version": "2.0"},
                 "buffers": [{"uri": "a.bin", "byteLength": 3}, {"uri": "b.bin", "byteLength": 3}],
                 "bufferViews": [
                   {"buffer": 0, "byteOffset": 0, "byteLength": 3},
                   {"buffer": 1, "byteOffset": 0, "byteLength": 3}]}
                """),
            new MemoryAssetSource(new Dictionary<AssetId, byte[]>
            {
                [AssetId.Parse("models/a.bin")] = [1, 2, 3],
                [AssetId.Parse("models/b.bin")] = [4, 5, 6],
            }));
        var added = document.AddView(new byte[] { 7, 7 });

        var again = await GltfDocument.ReadAsync(Id, document.ToGlb(), Empty);
        Assert.Single((List<object?>)again.Json["buffers"]!);
        Assert.Equal(new byte[] { 1, 2, 3 }, again.ViewBytes(0).ToArray());
        Assert.Equal(new byte[] { 4, 5, 6 }, again.ViewBytes(1).ToArray());
        Assert.Equal(new byte[] { 7, 7 }, again.ViewBytes(added).ToArray());
        foreach (var view in again.List("bufferViews"))
        {
            // A view that starts off a four-byte boundary cannot hold a float accessor.
            Assert.Equal(0, GltfJson.RequiredInt(view, "byteOffset") % 4);
        }
    }

    [Fact]
    public async Task KeepsEveryKeyItWasNotAskedAbout()
    {
        var document = await GltfDocument.ReadAsync(
            Id,
            Glb(
                Map("""
                    {"asset": {"version": "2.0", "generator": "somebody else"},
                     "extensionsUsed": ["KHR_materials_variants"],
                     "extensions": {"KHR_materials_variants": {"variants": [{"name": "red"}]}},
                     "extras": {"anything": 42},
                     "nodes": [{"name": "root", "extras": {"mine": true}}]}
                    """),
                []),
            Empty);

        var again = await GltfDocument.ReadAsync(Id, document.ToGlb(), Empty);
        Assert.Equal("""{"anything":42}""", DartJson.Encode(again.Json["extras"]));
        Assert.Equal("""["KHR_materials_variants"]""", DartJson.Encode(again.Json["extensionsUsed"]));
        Assert.NotNull(again.Json["extensions"]);
        Assert.Equal(true, ((GltfMap)again.List("nodes")[0]["extras"]!)["mine"]);
        Assert.Equal("somebody else", ((GltfMap)again.Json["asset"]!)["generator"]);
    }

    [Fact]
    public async Task SaysWhichFileIsWrongRatherThanReadingPastTheEnd()
    {
        var bytes = Glb(Map("""{"asset": {"version": "2.0"}}"""), []);
        var truncated = bytes[..^4];

        var error = await Assert.ThrowsAsync<ImportFailureException>(() => GltfDocument.ReadAsync(Id, truncated, Empty));

        Assert.Contains("truncated", error.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefusesAGlbThatIsNotVersion2()
    {
        var bytes = Glb(Map("""{"asset": {"version": "2.0"}}"""), []);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 1);

        var error = await Assert.ThrowsAsync<ImportFailureException>(() => GltfDocument.ReadAsync(Id, bytes, Empty));

        Assert.Contains("version 1", error.Reason, StringComparison.Ordinal);
    }
}
