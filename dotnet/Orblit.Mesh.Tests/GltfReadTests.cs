using System.Buffers.Binary;
using System.Text;
using Orblit.Json;

namespace Orblit.Mesh.Tests;

public class GltfReadTests
{
    private static Dictionary<string, object?> Json(params (string Key, object? Value)[] entries) =>
        entries.ToDictionary(entry => entry.Key, entry => entry.Value);

    // reading accessors

    [Fact]
    public void ScalesANormalisedShortIntoMinusOneToOne()
    {
        var bytes = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(0), 32767);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(2), -32768);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(4), 0);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(6), 16384);
        var read = new GltfAccessors(
            Json(
                ("bufferViews", new List<object?> { Json(("buffer", 0L), ("byteLength", 8L)) }),
                (
                    "accessors",
                    new List<object?>
                    {
                        Json(
                            ("bufferView", 0L),
                            ("componentType", 5122L),
                            ("normalized", true),
                            ("count", 1L),
                            ("type", "VEC4")),
                    })),
            bytes);
        var numbers = read.Floats(0, 4);
        Assert.Equal(1.0, numbers[0]);

        // clamped, as the format says
        Assert.Equal(-1.0, numbers[1]);
        Assert.Equal(0.0, numbers[2]);
        Assert.Equal(0.5, numbers[3], 1e-4);
        Assert.Empty(read.Problems);
    }

    [Fact]
    public void MindsTheStrideOfAnInterleavedView()
    {
        var bytes = new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(0), 1);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(4), 99);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(8), 2);
        BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(12), 99);
        var read = new GltfAccessors(
            Json(
                (
                    "bufferViews",
                    new List<object?> { Json(("buffer", 0L), ("byteLength", 16L), ("byteStride", 8L)) }),
                (
                    "accessors",
                    new List<object?>
                    {
                        Json(
                            ("bufferView", 0L),
                            ("componentType", 5126L),
                            ("count", 2L),
                            ("type", "SCALAR")),
                    })),
            bytes);
        Assert.Equal(new[] { 1.0, 2.0 }, read.Floats(0, 1));
        Assert.Equal(2L, read.Count(0));
    }

    [Fact]
    public void NotesWhatItCannotReadRatherThanThrowing()
    {
        var read = new GltfAccessors(
            Json(
                (
                    "accessors",
                    new List<object?>
                    {
                        Json(("bufferView", 0L), ("componentType", 5126L), ("count", 3L)),
                    })),
            null);
        Assert.Empty(read.Floats(0, 3));

        // no such accessor
        Assert.Empty(read.Floats(7, 3));
        Assert.Single(read.Problems);
    }

    // the parts of a document

    [Fact]
    public void ComeOutOfAGlb()
    {
        var glb = GltfFiles.GlbBytes(Json(("asset", Json(("version", "2.0")))), new byte[4]);
        var parts = GltfFiles.GltfParts(glb);
        Assert.Equal("2.0", GlbJson.Map(parts.Json["asset"])["version"]);
        Assert.Equal(4, parts.Binary!.Length);
    }

    [Fact]
    public void ComeOutOfJsonWithItsBufferInADataUri()
    {
        var json = Json(
            ("asset", Json(("version", "2.0"))),
            (
                "buffers",
                new List<object?>
                {
                    Json(
                        ("byteLength", 3L),
                        ("uri", "data:application/octet-stream;base64," + Convert.ToBase64String([1, 2, 3]))),
                }));
        var parts = GltfFiles.GltfParts(Encoding.UTF8.GetBytes(DartJson.Encode(json)));
        Assert.Equal(new byte[] { 1, 2, 3 }, parts.Binary);
    }

    [Fact]
    public void ComeOutOfJsonWithItsBufferBesideIt()
    {
        var json = Json(("buffers", new List<object?> { Json(("byteLength", 2L), ("uri", "old%20town.bin")) }));
        var parts = GltfFiles.GltfParts(
            Encoding.UTF8.GetBytes(DartJson.Encode(json)),
            new Dictionary<string, byte[]> { ["old town.bin"] = [7, 8] });
        Assert.Equal(new byte[] { 7, 8 }, parts.Binary);
    }

    [Fact]
    public void AreRefusedWhenTheBytesAreNeither()
    {
        Assert.ThrowsAny<FormatException>(() => GltfFiles.GltfParts(Encoding.UTF8.GetBytes("not a model")));
    }
}
