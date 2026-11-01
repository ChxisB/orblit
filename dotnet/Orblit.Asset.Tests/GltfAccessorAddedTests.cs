using Orblit.Json;
using static Orblit.Asset.Tests.GltfFixtures;

namespace Orblit.Asset.Tests;

// Tests with no ported original: the component types and accessor shapes the ported tests leave alone.
public sealed class GltfAccessorAddedTests
{
    private static readonly AssetId Id = AssetId.Parse("models/thing.glb");

    private static Task<GltfDocument> Of(string accessors, int[]? views, byte[] binary)
    {
        var viewList = views is null
            ? ""
            : $$"""
                ,"bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": {{binary.Length}}}], "buffers": [{"byteLength": {{binary.Length}}}]
                """;
        return GltfDocument.ReadAsync(
            Id,
            Glb(Map($$"""{"asset": {"version": "2.0"}, "accessors": [{{accessors}}]{{viewList}}}"""), binary),
            new MemoryAssetSource());
    }

    [Theory]
    [InlineData(ComponentType.Byte, 1)]
    [InlineData(ComponentType.UnsignedByte, 1)]
    [InlineData(ComponentType.Short, 2)]
    [InlineData(ComponentType.UnsignedShort, 2)]
    [InlineData(ComponentType.UnsignedInt, 4)]
    [InlineData(ComponentType.Float, 4)]
    public void KnowsHowWideEachComponentTypeIs(int type, int width) =>
        Assert.Equal(width, ComponentType.SizeOf(type));

    [Fact]
    public void RefusesAComponentTypeItDoesNotKnow() =>
        Assert.Equal("5124 is not a glTF component type", Assert.Throws<FormatException>(() => ComponentType.SizeOf(5124)).Message);

    [Theory]
    [InlineData("SCALAR", 1)]
    [InlineData("VEC2", 2)]
    [InlineData("VEC3", 3)]
    [InlineData("VEC4", 4)]
    [InlineData("MAT2", 4)]
    [InlineData("MAT3", 9)]
    [InlineData("MAT4", 16)]
    public void KnowsHowManyComponentsEachAccessorTypeHolds(string type, int count) =>
        Assert.Equal(count, ComponentType.ComponentsOf(type));

    [Fact]
    public void RefusesAnAccessorTypeItDoesNotKnow() =>
        Assert.Equal(
            "\"VEC5\" is not a glTF accessor type",
            Assert.Throws<FormatException>(() => ComponentType.ComponentsOf("VEC5")).Message);

    [Fact]
    public async Task ReadsSignedAndNormalisedBytesAsTheSpecSays()
    {
        // The spec's formula for a signed byte is max(c / 127, -1), so -128 and -127 are both -1.
        var document = await Of(
            """{"bufferView": 0, "componentType": 5120, "normalized": true, "count": 2, "type": "VEC2"}""",
            [0],
            [0x80, 0x81, 127, 0]);

        var read = document.ReadVec2(0);

        Assert.Equal(-1f, read[0]);
        Assert.Equal(-1f, read[1]);
        Assert.Equal(1f, read[2]);
        Assert.Equal(0f, read[3]);
    }

    [Fact]
    public async Task ReadsUnnormalisedIntegersAsTheirValue()
    {
        var document = await Of(
            """{"bufferView": 0, "componentType": 5123, "count": 1, "type": "VEC2"}""",
            [0],
            Bytes([7, 65535]));

        Assert.Equal(new[] { 7f, 65535f }, document.ReadVec2(0));
    }

    [Theory]
    [InlineData(5120, true, new byte[] { 0x80, 0x81 }, new[] { -1f, -1f })]
    [InlineData(5120, false, new byte[] { 0x80, 0x7F }, new[] { -128f, 127f })]
    [InlineData(5121, true, new byte[] { 0, 255 }, new[] { 0f, 1f })]
    [InlineData(5121, false, new byte[] { 0, 255 }, new[] { 0f, 255f })]
    [InlineData(5122, true, new byte[] { 0x00, 0x80, 0xFF, 0x7F }, new[] { -1f, 1f })]
    [InlineData(5122, false, new byte[] { 0x00, 0x80, 0xFF, 0x7F }, new[] { -32768f, 32767f })]
    [InlineData(5123, true, new byte[] { 0, 0, 0xFF, 0xFF }, new[] { 0f, 1f })]
    [InlineData(5123, false, new byte[] { 0, 0, 0xFF, 0xFF }, new[] { 0f, 65535f })]
    [InlineData(5125, false, new byte[] { 1, 0, 0, 0, 0, 1, 0, 0 }, new[] { 1f, 256f })]
    public async Task ReadsEachComponentTypeNormalisedOrNot(int type, bool normalized, byte[] bytes, float[] expected)
    {
        var document = await Of(
            $$"""{"bufferView": 0, "componentType": {{type}}, "normalized": {{(normalized ? "true" : "false")}}, "count": 1, "type": "VEC2"}""",
            [0],
            bytes);

        Assert.Equal(expected, document.ReadVec2(0));
    }

    [Fact]
    public async Task RefusesAComponentTypeItCannotRead()
    {
        var document = await Of(
            """{"bufferView": 0, "componentType": 5124, "count": 1, "type": "VEC2"}""",
            [0],
            new byte[8]);

        Assert.Throws<FormatException>(() => document.ReadVec2(0));
    }

    [Fact]
    public async Task ReadsAnAccessorWithNoViewAsZeroes()
    {
        var document = await Of("""{"componentType": 5126, "count": 2, "type": "VEC3"}""", null, []);

        Assert.Equal(new float[6], document.ReadVec3(0));
    }

    [Theory]
    [InlineData(255, 5121)]
    [InlineData(256, 5123)]
    [InlineData(65535, 5123)]
    [InlineData(65536, 5125)]
    public async Task ChoosesTheIndexWidthAtItsBoundaries(int highest, int type)
    {
        var document = await Of("", null, []);

        var added = document.AddIndices([0, highest]);

        Assert.Equal(type, document.Accessor(added)["componentType"]);
        Assert.Equal(new uint[] { 0, (uint)highest }, document.ReadIndices(added));
    }

    [Fact]
    public async Task ReadsIndicesOfEveryWidthBackAsWritten()
    {
        var document = await Of("", null, []);

        var small = document.AddIndices([1, 2, 3]);
        var medium = document.AddIndices([1, 300, 3]);
        var large = document.AddIndices([1, 70000, 3]);
        var again = await GltfDocument.ReadAsync(Id, document.ToGlb(), new MemoryAssetSource());

        Assert.Equal(new uint[] { 1, 2, 3 }, again.ReadIndices(small));
        Assert.Equal(new uint[] { 1, 300, 3 }, again.ReadIndices(medium));
        Assert.Equal(new uint[] { 1, 70000, 3 }, again.ReadIndices(large));
    }

    [Fact]
    public async Task ReadsIndicesFromTheOffsetTheAccessorGives()
    {
        var document = await Of(
            """{"bufferView": 0, "byteOffset": 2, "componentType": 5121, "count": 2, "type": "SCALAR"}""",
            [0],
            [9, 9, 4, 5]);

        Assert.Equal(new uint[] { 4, 5 }, document.ReadIndices(0));
    }

    [Fact]
    public async Task RefusesIndicesThatAreNotScalar()
    {
        var document = await Of("""{"bufferView": 0, "componentType": 5123, "count": 1, "type": "VEC2"}""", [0], new byte[4]);

        Assert.Equal(
            "index accessor 0 is VEC2, not SCALAR",
            Assert.Throws<FormatException>(() => document.ReadIndices(0)).Message);
    }

    [Fact]
    public async Task RefusesIndicesOfAFloatType()
    {
        var document = await Of("""{"bufferView": 0, "componentType": 5126, "count": 1, "type": "SCALAR"}""", [0], new byte[4]);

        Assert.Equal(
            "indices cannot be component type 5126",
            Assert.Throws<FormatException>(() => document.ReadIndices(0)).Message);
    }

    [Fact]
    public async Task AddLikeKeepsANormalisedFlagAndDropsABoundsItWasNotGiven()
    {
        var document = await Of(
            """{"bufferView": 0, "componentType": 5123, "normalized": true, "count": 1, "type": "VEC2", "min": [0, 0], "max": [1, 1]}""",
            [0],
            new byte[4]);

        var added = document.AddLike(0, new byte[8], 2);

        var accessor = document.Accessor(added);
        Assert.Equal(true, accessor["normalized"]);
        Assert.False(accessor.ContainsKey("min"));
        Assert.False(accessor.ContainsKey("max"));
    }

    [Fact]
    public async Task AddLikeLeavesOutANormalisedFlagTheTemplateDidNotHave()
    {
        var document = await Of("""{"bufferView": 0, "componentType": 5126, "count": 1, "type": "VEC2"}""", [0], new byte[8]);

        Assert.False(document.Accessor(document.AddLike(0, new byte[8], 1)).ContainsKey("normalized"));
    }

    [Fact]
    public async Task ReadsIndicesOfEveryWidth()
    {
        var document = await Of(
            """
            {"bufferView": 0, "componentType": 5121, "count": 2, "type": "SCALAR"}
            """,
            [0],
            [3, 250, 0, 0]);

        Assert.Equal(new uint[] { 3, 250 }, document.ReadIndices(0));
    }

    [Fact]
    public async Task RefusesAVectorOfTheWrongType()
    {
        var document = await Of(
            """{"bufferView": 0, "componentType": 5126, "count": 1, "type": "VEC3"}""",
            [0],
            Bytes(1f, 2f, 3f));

        var error = Assert.Throws<FormatException>(() => document.ReadVec2(0));

        Assert.Contains("VEC2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefusesAnAccessorThatDoesNotExist()
    {
        var document = await Of("", null, []);

        Assert.Throws<FormatException>(() => document.Accessor(0));
    }

    [Fact]
    public async Task AddLikeCopiesTheTemplateAndReplacesTheBytes()
    {
        var document = await Of(
            """{"bufferView": 0, "componentType": 5126, "count": 1, "type": "VEC3"}""",
            [0],
            Bytes(1f, 2f, 3f));

        var added = document.AddLike(0, Bytes(4f, 5f, 6f, 7f, 8f, 9f), 2, [4, 5, 6], [7, 8, 9]);

        Assert.Equal(2, document.AccessorCount(added));
        Assert.Equal(new float[] { 4, 5, 6, 7, 8, 9 }, document.ReadVec3(added));
        Assert.Equal(24, document.AccessorBytes(added).Length);
        var accessor = document.Accessor(added);
        Assert.Equal("[4,5,6]", JsonText.Encode(accessor["min"]).Replace(".0", "", StringComparison.Ordinal));
        Assert.Equal("[7,8,9]", JsonText.Encode(accessor["max"]).Replace(".0", "", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AddVec2StoresFloatsTightlyPacked()
    {
        var document = await Of("", null, []);

        var added = document.AddVec2([0.25f, 0.5f, 0.75f, 1f]);

        Assert.Equal(new[] { 0.25f, 0.5f, 0.75f, 1f }, document.ReadVec2(added));
        Assert.Equal("VEC2", document.Accessor(added)["type"]);
    }

    [Fact]
    public async Task ElementBytesCopiesOneElementWholeAtTheViewStride()
    {
        // Two VEC2 shorts of four bytes each, eight bytes apart.
        byte[] bytes = [1, 0, 2, 0, 9, 9, 9, 9, 3, 0, 4, 0, 9, 9, 9, 9];
        var document = await GltfDocument.ReadAsync(
            Id,
            Glb(
                Map($$"""
                    {"asset": {"version": "2.0"},
                     "buffers": [{"byteLength": {{bytes.Length}}}],
                     "bufferViews": [{"buffer": 0, "byteOffset": 0, "byteLength": {{bytes.Length}}, "byteStride": 8}],
                     "accessors": [{"bufferView": 0, "byteOffset": 0, "componentType": 5123, "count": 2, "type": "VEC2"}]}
                    """),
                bytes),
            new MemoryAssetSource());

        Assert.Equal(new byte[] { 1, 0, 2, 0 }, document.ElementBytes(0, 0).ToArray());
        Assert.Equal(new byte[] { 3, 0, 4, 0 }, document.ElementBytes(0, 1).ToArray());
    }

    [Fact]
    public async Task ElementBytesStartsAtTheAccessorOffsetWhenTheViewHasNoStride()
    {
        var document = await Of(
            """{"bufferView": 0, "byteOffset": 2, "componentType": 5121, "count": 2, "type": "VEC2"}""",
            [0],
            [9, 9, 1, 2, 3, 4]);

        Assert.Equal(new byte[] { 3, 4 }, document.ElementBytes(0, 1).ToArray());
    }

    [Fact]
    public async Task ElementBytesIsZeroForAnAccessorWithNoViewAndRefusesOneThatReadsPastIt()
    {
        var empty = await Of("""{"componentType": 5126, "count": 1, "type": "VEC3"}""", null, []);
        var truncated = await Of(
            """{"bufferView": 0, "componentType": 5121, "count": 2, "type": "VEC2"}""", [0], [1, 2, 3]);

        Assert.Equal(new byte[12], empty.ElementBytes(0, 0).ToArray());
        Assert.Equal(new byte[] { 1, 2 }, truncated.ElementBytes(0, 0).ToArray());
        Assert.Equal(
            "accessor 0 reads past its buffer view",
            Assert.Throws<FormatException>(() => truncated.ElementBytes(0, 1)).Message);
    }
}
