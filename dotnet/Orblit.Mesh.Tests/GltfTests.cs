using System.Buffers.Binary;
using System.Text;
using Orblit.Json;

namespace Orblit.Mesh.Tests;

public class GltfTests
{
    private static OrderedDictionary<string, object?> Doc(params (string Key, object? Value)[] entries)
    {
        var map = new OrderedDictionary<string, object?>();
        foreach (var (key, value) in entries)
        {
            map[key] = value;
        }

        return map;
    }

    /// <summary>The JSON chunk of a glb as text, padding and all.</summary>
    private static string JsonTextOf(byte[] glb)
    {
        var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(12));
        return Encoding.UTF8.GetString(glb, 20, length);
    }

    // the buffer it builds

    [Fact]
    public void EveryViewStartsOnAFourByteBoundary()
    {
        var buffer = new GltfBuffer();

        // Three bytes, so whatever follows has to be pushed along.
        buffer.AddView<byte>([1, 2, 3]);
        buffer.AddFloats([1, 2, 3], "VEC3");

        Assert.Equal(0L, buffer.Views[0]["byteOffset"]);
        Assert.Equal(4L, buffer.Views[1]["byteOffset"]);
        Assert.Equal(0, buffer.Bytes.Length % 4);
    }

    [Fact]
    public void AViewSaysHowLongItIsNotHowLongItWasPaddedTo()
    {
        var buffer = new GltfBuffer();
        buffer.AddView<byte>([1, 2, 3]);
        Assert.Equal(3L, Assert.Single(buffer.Views)["byteLength"]);
    }

    [Fact]
    public void PositionsCarryTheBoundsGltfRequiresOfThem()
    {
        var buffer = new GltfBuffer();
        buffer.AddPositions([-1, 0, 2, 3, -4, 0, 0, 5, -6]);

        var accessor = Assert.Single(buffer.Accessors);
        Assert.Equal(3L, accessor["count"]);
        Assert.Equal(new[] { -1.0, -4.0, -6.0 }, GlbJson.List(accessor["min"]).Select(GlbJson.Number));
        Assert.Equal(new[] { 3.0, 5.0, 2.0 }, GlbJson.List(accessor["max"]).Select(GlbJson.Number));
    }

    [Fact]
    public void NoPositionsMeansNoughtRatherThanInfinity()
    {
        // Whichever way round the sweep starts, an empty one leaves the bounds at the extremes it
        // started from, and glTF takes neither.
        var buffer = new GltfBuffer();
        buffer.AddPositions([]);
        var accessor = Assert.Single(buffer.Accessors);
        Assert.Equal(new[] { 0.0, 0.0, 0.0 }, GlbJson.List(accessor["min"]).Select(GlbJson.Number));
        Assert.Equal(new[] { 0.0, 0.0, 0.0 }, GlbJson.List(accessor["max"]).Select(GlbJson.Number));
    }

    [Fact]
    public void IndicesTakeTheNarrowestWidthThatHoldsThem()
    {
        static long WidthOf(List<int> indices)
        {
            var buffer = new GltfBuffer();
            buffer.AddIndices(indices);
            return (long)Assert.Single(buffer.Accessors)["componentType"]!;
        }

        Assert.Equal(GltfComponent.UnsignedByte, WidthOf([0, 1, 255]));
        Assert.Equal(GltfComponent.UnsignedShort, WidthOf([0, 1, 256]));
        Assert.Equal(GltfComponent.UnsignedShort, WidthOf([0, 1, 65535]));
        Assert.Equal(GltfComponent.UnsignedInt, WidthOf([0, 1, 65536]));
    }

    [Fact]
    public void ANarrowerIndexIsASmallerBuffer()
    {
        var small = new GltfBuffer();
        small.AddIndices(Enumerable.Repeat(1, 300).ToList());
        var large = new GltfBuffer();
        large.AddIndices(Enumerable.Repeat(70000, 300).ToList());

        Assert.Equal(300, small.Length);
        Assert.Equal(1200, large.Length);
    }

    [Fact]
    public void AnOffsetOfNoughtIsLeftOutSinceThatIsWhatItMeans()
    {
        var buffer = new GltfBuffer();
        buffer.AddView<float>([1, 2, 3, 4]);
        buffer.AddAccessor(view: 0, componentType: GltfComponent.Float, count: 2, type: "VEC2");
        Assert.False(Assert.Single(buffer.Accessors).ContainsKey("byteOffset"));
    }

    [Fact]
    public void ABufferNobodyWroteToIsNoBufferAtAll()
    {
        // A buffer of nought length is one of the few things the validator calls an error outright.
        Assert.Empty(new GltfBuffer().Buffers);
    }

    // the glb container

    [Fact]
    public void ReadsBackWhatItWrote()
    {
        var bytes = GltfFiles.GlbBytes(Doc(("asset", Doc(("version", "2.0")))), new byte[] { 1, 2, 3, 4, 5 });

        var chunks = GltfFiles.GlbChunks(bytes)!.Value;
        Assert.Equal("2.0", GlbJson.Map(chunks.Json["asset"])["version"]);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, chunks.Binary[..5]);
    }

    [Fact]
    public void EveryChunkIsAWholeNumberOfWords()
    {
        var bytes = GltfFiles.GlbBytes(Doc(("asset", Doc(("version", "2.0")))), new byte[] { 1, 2, 3 });

        Assert.Equal(0, bytes.Length % 4);
        Assert.Equal((uint)bytes.Length, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8)));
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12)) % 4);
    }

    [Fact]
    public void TheJsonChunkIsPaddedWithSpacesSoItStaysJson()
    {
        // The specification asks for spaces rather than zeros precisely so a reader taking the
        // chunk at its stated length still finds json.
        var bytes = GltfFiles.GlbBytes(Doc(("asset", Doc())), []);
        var text = JsonTextOf(bytes);

        Assert.Equal("{\"asset\":{}}", text.TrimEnd());
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(DartJson.Decode(text));
    }

    [Fact]
    public void ADocumentWithNoBytesWritesNoBinaryChunk()
    {
        var bytes = GltfFiles.GlbBytes(Doc(("asset", Doc())), []);
        var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12));
        Assert.Equal(20 + length, bytes.Length);
        Assert.Empty(GltfFiles.GlbChunks(bytes)!.Value.Binary);
    }

    [Fact]
    public void AnythingThatIsNotAGlbReadsAsNothing()
    {
        Assert.Null(GltfFiles.GlbChunks([]));

        // no magic word
        Assert.Null(GltfFiles.GlbChunks(new byte[64]));

        // too short to hold a header
        Assert.Null(GltfFiles.GlbChunks([1, 2, 3, 4, 5]));

        // The magic word and then nothing that follows it.
        var cut = GltfFiles.GlbBytes(Doc(("asset", Doc())), [])[..24];
        Assert.Null(GltfFiles.GlbChunks(cut));
    }

    [Fact]
    public void ANameWithAnAccentInItSurvivesTheTrip()
    {
        // The length in the header counts bytes, and utf-8 says a character is not one of them.
        // Writing the length in characters is a file that is truncated by exactly as many accents
        // as the name has.
        var bytes = GltfFiles.GlbBytes(
            Doc(("nodes", new List<object?> { Doc(("name", "Fa\u00e7ade \u2014 \u00e9tage")) })),
            []);

        var nodes = GlbJson.List(GltfFiles.GlbChunks(bytes)!.Value.Json["nodes"]);
        Assert.Equal("Fa\u00e7ade \u2014 \u00e9tage", GlbJson.Map(Assert.Single(nodes))["name"]);
    }

    // the numbers it writes

    [Fact]
    public void AWholeNumberGoesOutWithoutItsPoint()
    {
        // A count written as 12.0 is a file some loaders refuse.
        var bytes = GltfFiles.GlbBytes(
            Doc(("metallicFactor", 1.0), ("nested", new List<object?> { Doc(("count", 12.0)) })),
            []);
        var text = JsonTextOf(bytes);

        Assert.Contains("\"metallicFactor\":1,", text);
        Assert.Contains("\"count\":12", text);
        Assert.DoesNotContain(".0", text);
    }

    [Fact]
    public void ANumberThatIsNotWholeKeepsEveryBitOfItself()
    {
        var bytes = GltfFiles.GlbBytes(Doc(("roughness", 0.05)), []);
        Assert.Equal(0.05, GlbJson.Number(GltfFiles.GlbChunks(bytes)!.Value.Json["roughness"]));
    }

    // the gltf text it writes

    [Fact]
    public void NamesTheBufferBesideIt()
    {
        var json = GltfFiles.GltfText(
            Doc(("asset", Doc(("version", "2.0")))),
            buffer: "model.bin",
            byteLength: 64);

        var buffers = GlbJson.List(GlbJson.Map(DartJson.Decode(json))["buffers"]);
        var only = GlbJson.Map(Assert.Single(buffers));
        Assert.Equal("model.bin", only["uri"]);
        Assert.Equal(64.0, GlbJson.Number(only["byteLength"]));
    }

    [Fact]
    public void ADocumentWithNoBytesNamesNoBuffer()
    {
        var json = GltfFiles.GltfText(
            Doc(
                ("asset", Doc(("version", "2.0"))),
                ("buffers", new List<object?> { Doc(("byteLength", 8L)) })),
            byteLength: 0);
        Assert.False(GlbJson.Map(DartJson.Decode(json)).ContainsKey("buffers"));
    }
}
