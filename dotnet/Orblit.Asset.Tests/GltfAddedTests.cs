using System.Buffers.Binary;
using System.Text;
using static Orblit.Asset.Tests.GltfFixtures;

namespace Orblit.Asset.Tests;

// Tests with no ported original: the refusals the ported tests do not reach, and the reads made by hand.
public sealed class GltfAddedTests
{
    private static readonly AssetId Id = AssetId.Parse("models/thing.gltf");
    private static readonly MemoryAssetSource Empty = new();

    private static async Task<string> ReasonOf(byte[] bytes, IAssetSource? source = null)
    {
        var error = await Assert.ThrowsAsync<ImportFailureException>(
            () => GltfDocument.ReadAsync(Id, bytes, source ?? Empty));
        return error.Reason;
    }

    private static byte[] Text(string text) => Encoding.UTF8.GetBytes(text);

    private static byte[] WithBuffer(string buffer) =>
        Text($$"""{"asset": {"version": "2.0"}, "buffers": [{{buffer}}]}""");

    [Fact]
    public async Task RefusesJsonThatIsNotJson() =>
        Assert.Equal("is not readable as glTF JSON", await ReasonOf(Text("{not json")));

    [Fact]
    public async Task RefusesJsonThatIsNotAnObject() =>
        Assert.Equal("is not a glTF document: its JSON is not an object", await ReasonOf(Text("[1, 2]")));

    [Fact]
    public async Task RefusesABufferThatIsNotAnObject() =>
        Assert.Equal("buffer 0 is not an object", await ReasonOf(Text("""{"buffers": [3]}""")));

    [Fact]
    public async Task OnlyTheFirstBufferMayLeaveItsUriOut() =>
        Assert.Equal(
            "buffer 1 has no URI, and only the first buffer of a GLB may leave it out",
            await ReasonOf(Text("""{"buffers": [{"byteLength": 0}, {"byteLength": 0}]}""")));

    [Fact]
    public async Task RefusesAUriThatIsNotAString() =>
        Assert.Equal("buffer 0's URI is not a string", await ReasonOf(WithBuffer("""{"uri": 7}""")));

    [Theory]
    [InlineData("https://example.com/a.bin")]
    [InlineData("../../outside.bin")]
    [InlineData("/etc/hosts")]
    [InlineData("100%.bin")]
    public async Task RefusesAUriThatLeavesTheProject(string uri) =>
        Assert.Equal(
            $"buffer 0 is at \"{uri}\", which is not a name inside the project",
            await ReasonOf(WithBuffer($$"""{"uri": "{{uri}}"}""")));

    [Fact]
    public async Task ReadsAFileNameThatIsPercentEncoded()
    {
        var source = new MemoryAssetSource(new Dictionary<AssetId, byte[]>
        {
            [AssetId.Parse("models/my geometry.bin")] = [5, 6, 7, 8],
        });
        var document = await GltfDocument.ReadAsync(
            Id,
            Text("""
                {"buffers": [{"uri": "my%20geometry.bin", "byteLength": 4}],
                 "bufferViews": [{"buffer": 0, "byteLength": 4}]}
                """),
            source);

        Assert.Equal(new byte[] { 5, 6, 7, 8 }, document.ViewBytes(0).ToArray());
    }

    [Fact]
    public async Task DecodesAFileNameTwice()
    {
        // The reader decodes the URI and AssetId.Resolve decodes it again, so %2520 reaches the source as a space
        // rather than as %20. Kept as it is: a file that relies on the first reading is rare, and a cook that
        // read it differently from other cooks would put different assets in a pack.
        var source = new MemoryAssetSource(new Dictionary<AssetId, byte[]>
        {
            [AssetId.Parse("models/my geometry.bin")] = [1, 2],
        });
        var document = await GltfDocument.ReadAsync(
            Id,
            Text("""
                {"buffers": [{"uri": "my%2520geometry.bin", "byteLength": 2}],
                 "bufferViews": [{"buffer": 0, "byteLength": 2}]}
                """),
            source);

        Assert.Equal(new byte[] { 1, 2 }, document.ViewBytes(0).ToArray());
    }

    [Fact]
    public async Task RefusesADataUriWithNoComma() =>
        Assert.Equal("has a data URI with no comma in it", await ReasonOf(WithBuffer("""{"uri": "data:;base64"}""")));

    [Fact]
    public async Task RefusesADataUriThatIsNotBase64() =>
        Assert.Equal(
            "has a data URI that is not base64, which this cook does not read",
            await ReasonOf(WithBuffer("""{"uri": "data:text/plain,hello"}""")));

    [Theory]
    [InlineData("AAA")]
    [InlineData("AA A=")]
    [InlineData("A===")]
    public async Task RefusesADataUriWhosePayloadIsNotBase64(string payload) =>
        Assert.Equal(
            "has a data URI that is not valid base64",
            await ReasonOf(WithBuffer($$"""{"uri": "data:application/octet-stream;base64,{{payload}}"}""")));

    [Fact]
    public async Task ReadsABase64PayloadInEitherAlphabet()
    {
        // 0xFB 0xFF is "+/8=" in the standard alphabet and "-_8=" in the URL one.
        foreach (var payload in new[] { "+/8=", "-_8=" })
        {
            var document = await GltfDocument.ReadAsync(
                Id,
                Text($$"""
                    {"buffers": [{"uri": "data:;base64,{{payload}}", "byteLength": 2}],
                     "bufferViews": [{"buffer": 0, "byteLength": 2}]}
                    """),
                Empty);

            Assert.Equal(new byte[] { 0xFB, 0xFF }, document.ViewBytes(0).ToArray());
        }
    }

    [Fact]
    public async Task RefusesAViewThatRunsPastItsBuffer()
    {
        var document = await GltfDocument.ReadAsync(
            Id,
            Text("""
                {"buffers": [{"uri": "data:;base64,AAAAAA==", "byteLength": 4}],
                 "bufferViews": [{"buffer": 0, "byteOffset": 1, "byteLength": 4}]}
                """),
            Empty);

        Assert.Equal("a buffer view runs past its buffer", Assert.Throws<FormatException>(() => document.ViewBytes(0)).Message);
    }

    [Fact]
    public async Task ReadsTheTinyestValidGlb()
    {
        var bytes = Glb(Map("""{"asset": {"version": "2.0"}}"""), []);

        var document = await GltfDocument.ReadAsync(Id, bytes, Empty);

        Assert.Equal("2.0", ((GltfMap)document.Json["asset"]!)["version"]);
    }

    [Fact]
    public async Task RefusesAGlbTooShortToHoldAChunkHeader() =>
        Assert.Equal(
            "is too short to be a GLB (16 bytes)",
            await ReasonOf(Glb(Map("""{"asset": {"version": "2.0"}}"""), [])[..16]));

    [Fact]
    public async Task ReadsADocumentTooSmallToBeMistakenForAGlb()
    {
        var document = await GltfDocument.ReadAsync(Id, Text("{}"), Empty);

        Assert.Empty(document.Json);
    }

    [Fact]
    public async Task RefusesAGlbOneByteShortOfAChunkHeader()
    {
        var bytes = new byte[19];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 19);

        Assert.Equal("is too short to be a GLB (19 bytes)", await ReasonOf(bytes));
    }

    [Fact]
    public async Task RefusesAGlbThatClaimsOneByteMoreThanItHas()
    {
        var bytes = Glb(Map("""{"asset": {"version": "2.0"}}"""), []);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), (uint)bytes.Length + 1);

        Assert.Contains("It is truncated.", await ReasonOf(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefusesAChunkThatEndsOneByteBeyondTheLengthTheHeaderGives()
    {
        // The file has the byte, so only the header's total says the chunk is too long.
        var bytes = new byte[29];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 28);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 9);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 0x4E4F534A);
        bytes.AsSpan(20).Fill(0x20);

        Assert.Equal("has a chunk running past the end of the file", await ReasonOf(bytes));
    }

    [Fact]
    public async Task PadsAViewOnlyWhenItIsNotAlreadyOnAFourByteBoundary()
    {
        var document = await GltfDocument.ReadAsync(Id, Text("""{"asset": {"version": "2.0"}}"""), Empty);
        document.AddView(new byte[4]);
        var second = document.AddView(new byte[4]);

        var again = await GltfDocument.ReadAsync(Id, document.ToGlb(), Empty);

        Assert.Equal(4, GltfJson.RequiredInt(again.List("bufferViews")[second], "byteOffset"));
        Assert.Equal(8, GltfJson.RequiredInt((GltfMap)((List<object?>)again.Json["buffers"]!)[0]!, "byteLength"));
    }

    [Fact]
    public async Task ReadsAViewThatLeavesItsBufferOutAsBufferZero()
    {
        var document = await GltfDocument.ReadAsync(
            Id,
            Text("""
                {"buffers": [{"uri": "data:;base64,AQIDBA==", "byteLength": 4}, {"uri": "data:;base64,BQYHCA==", "byteLength": 4}],
                 "bufferViews": [{"byteOffset": 1, "byteLength": 2}]}
                """),
            Empty);

        Assert.Equal(new byte[] { 2, 3 }, document.ViewBytes(0).ToArray());
    }

    [Fact]
    public async Task RefusesAChunkThatRunsPastTheEnd()
    {
        var bytes = Glb(Map("""{"asset": {"version": "2.0"}}"""), []);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), (uint)bytes.Length);

        Assert.Equal("has a chunk running past the end of the file", await ReasonOf(bytes));
    }

    [Fact]
    public async Task RefusesTwoJsonChunks()
    {
        var one = Glb(Map("""{"asset": {"version": "2.0"}}"""), []);
        var chunk = one[12..(20 + (int)BinaryPrimitives.ReadUInt32LittleEndian(one.AsSpan(12)))];
        byte[] two = [.. one[..12], .. chunk, .. chunk[0..]];
        BinaryPrimitives.WriteUInt32LittleEndian(two.AsSpan(8), (uint)two.Length);

        Assert.Equal("has two JSON chunks, and a GLB has one", await ReasonOf(two));
    }

    [Fact]
    public async Task RefusesTwoBinChunks()
    {
        var one = Glb(Map("""{"asset": {"version": "2.0"}}"""), [1, 2, 3, 4]);
        var bin = one[(20 + (int)BinaryPrimitives.ReadUInt32LittleEndian(one.AsSpan(12)))..];
        byte[] two = [.. one, .. bin];
        BinaryPrimitives.WriteUInt32LittleEndian(two.AsSpan(8), (uint)two.Length);

        Assert.Equal("has two BIN chunks, and a GLB has one", await ReasonOf(two));
    }

    [Fact]
    public async Task RefusesAGlbWithNoJsonChunk()
    {
        // Header, then one BIN chunk of nought bytes.
        var bytes = new byte[20];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0), 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 20);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 0x004E4942);

        Assert.Equal("has no JSON chunk, and a GLB must have one", await ReasonOf(bytes));
    }

    [Fact]
    public async Task IgnoresAChunkItDoesNotKnow()
    {
        var one = Glb(Map("""{"asset": {"version": "2.0"}}"""), []);
        byte[] unknown = [4, 0, 0, 0, 0x58, 0x58, 0x58, 0x58, 1, 2, 3, 4];
        byte[] with = [.. one, .. unknown];
        BinaryPrimitives.WriteUInt32LittleEndian(with.AsSpan(8), (uint)with.Length);

        var document = await GltfDocument.ReadAsync(Id, with, Empty);

        Assert.Equal("2.0", ((GltfMap)document.Json["asset"]!)["version"]);
    }

    [Fact]
    public async Task RefusesAJsonChunkThatIsNotJson()
    {
        var bytes = Glb(Map("""{"asset": {"version": "2.0"}}"""), []);
        bytes[20] = (byte)'#';

        Assert.Equal("'s JSON chunk is not JSON", await ReasonOf(bytes));
    }

    [Fact]
    public async Task RefusesAJsonChunkThatIsNotAnObject()
    {
        var bytes = Glb(Map("""{"asset": {"version": "2.0"}}"""), []);
        bytes.AsSpan(20, (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12))).Fill(0x20);
        bytes[20] = (byte)'3';

        Assert.Equal("'s JSON chunk is not an object", await ReasonOf(bytes));
    }

    [Fact]
    public async Task ListLeavesOutAnythingThatIsNotAnObject()
    {
        var document = await GltfDocument.ReadAsync(Id, Text("""{"nodes": [{"name": "a"}, 3, "b"]}"""), Empty);

        Assert.Single(document.List("nodes"));
        Assert.Empty(document.List("meshes"));
        Assert.False(document.Json.ContainsKey("meshes"));
    }

    [Fact]
    public async Task AddingAViewMakesTheListItNeeds()
    {
        var document = await GltfDocument.ReadAsync(Id, Text("""{"asset": {"version": "2.0"}}"""), Empty);

        var first = document.AddView(new byte[] { 1 }, byteStride: 4, target: 34962);

        Assert.Equal(0, first);
        Assert.Equal(4, document.ViewStride(first));
        var view = document.List("bufferViews")[0];
        Assert.Equal(34962, view["target"]);
    }

    [Fact]
    public async Task WritesTheViewsItAddedAfterTheOnesItReadAtFourByteBoundaries()
    {
        var document = await GltfDocument.ReadAsync(Id, Text("""{"asset": {"version": "2.0"}}"""), Empty);
        var a = document.AddView(new byte[] { 1 });
        var b = document.AddView(new byte[] { 2, 2 });

        var again = await GltfDocument.ReadAsync(Id, document.ToGlb(), Empty);

        Assert.Equal(new byte[] { 1 }, again.ViewBytes(a).ToArray());
        Assert.Equal(new byte[] { 2, 2 }, again.ViewBytes(b).ToArray());
        Assert.Equal(4, GltfJson.RequiredInt(again.List("bufferViews")[b], "byteOffset"));
    }
}
