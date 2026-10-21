using System.Buffers.Binary;
using System.Text;

namespace Orblit.Asset.Tests;

public sealed class GltfImporterTests
{
    private readonly GltfImporter _importer = new();

    internal static AssetId Id(string text) => AssetId.Parse(text);

    internal static byte[] Gltf(string json) => Encoding.UTF8.GetBytes(json);

    /// <summary>
    /// A GLB whose JSON chunk holds <paramref name="json"/>: a 12-byte header, then a chunk header, then the
    /// JSON padded to four bytes as the format requires.
    /// </summary>
    internal static byte[] JsonGlb(string json)
    {
        var text = Encoding.UTF8.GetBytes(json);
        var chunk = new byte[text.Length + (4 - text.Length % 4) % 4];
        Array.Fill(chunk, (byte)0x20);
        text.CopyTo(chunk, 0);
        var bytes = new byte[20 + chunk.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), (uint)bytes.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), (uint)chunk.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 0x4E4F534A);
        chunk.CopyTo(bytes, 20);
        return bytes;
    }

    internal static Task<IReadOnlySet<AssetId>> DepsOf(Importer importer, string of, byte[] bytes) =>
        importer.DependenciesOfAsync(Id(of), bytes, new Dictionary<string, object?>());

    internal static void AssertSet(IReadOnlySet<AssetId> actual, params string[] expected) =>
        Assert.Equal(expected.Select(Id).ToHashSet(), actual.ToHashSet());

    [Fact]
    public void ClaimsBothContainerFormats()
    {
        Assert.True(_importer.CanHandle(Id("a.gltf")));
        Assert.True(_importer.CanHandle(Id("a.glb")));
        Assert.False(_importer.CanHandle(Id("a.fbx")));
    }

    [Fact]
    public async Task FindsTheBuffersAndImagesBesideAGltf()
    {
        var bytes = Gltf("""
            {"buffers": [{"uri": "helmet.bin"}],
             "images": [{"uri": "textures/albedo.png"}, {"uri": "textures/normal.png"}]}
            """);
        AssertSet(
            await DepsOf(_importer, "models/helmet.gltf", bytes),
            "models/helmet.bin",
            "models/textures/albedo.png",
            "models/textures/normal.png");
    }

    [Fact]
    public async Task ResolvesAUriThatClimbsOutOfTheModelsFolder()
    {
        var bytes = Gltf("""{"images": [{"uri": "../shared/wood.png"}]}""");
        AssertSet(await DepsOf(_importer, "models/chair/chair.gltf", bytes), "models/shared/wood.png");
    }

    [Fact]
    public async Task DecodesAPercentEncodedUriBecauseThatIsHowASpaceSurvivesAGltf()
    {
        var bytes = Gltf("""{"images": [{"uri": "base%20colour.png"}]}""");
        AssertSet(await DepsOf(_importer, "m.gltf", bytes), "base colour.png");
    }

    [Fact]
    public async Task BytesAlreadyInsideTheFileAreNotDependencies()
    {
        var bytes = Gltf("""
            {"buffers": [{"uri": "data:application/octet-stream;base64,AAAA"}, {"byteLength": 4}],
             "images": [{"bufferView": 0}]}
            """);
        Assert.Empty(await DepsOf(_importer, "m.gltf", bytes));
    }

    [Fact]
    public async Task AUriWithASchemeIsLeftAloneRatherThanGuessedAt()
    {
        var bytes = Gltf("""{"images": [{"uri": "https://example.invalid/a.png"}]}""");
        Assert.Empty(await DepsOf(_importer, "m.gltf", bytes));
    }

    [Fact]
    public async Task AUriClimbingOutOfTheProjectFailsNamingIt()
    {
        var bytes = Gltf("""{"images": [{"uri": "../../outside.png"}]}""");
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => DepsOf(_importer, "m.gltf", bytes));
        Assert.Contains("outside.png", failure.Reason);
    }

    [Fact]
    public async Task AGlbIsReadThroughItsJsonChunk()
    {
        var bytes = JsonGlb("""{"images": [{"uri": "beside.png"}]}""");
        AssertSet(await DepsOf(_importer, "m.glb", bytes), "beside.png");
    }

    [Fact]
    public async Task ASelfContainedGlbDependsOnNothing()
    {
        Assert.Empty(await DepsOf(_importer, "m.glb", JsonGlb("""{"images": []}""")));
    }

    [Fact]
    public async Task AGltfSavedUnderAGlbNameIsAnErrorNotAnEmptyDependencyList()
    {
        var bytes = Gltf("""{"images": [{"uri": "a.png"}, {"uri": "b.png"}]}""");
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => DepsOf(_importer, "m.glb", bytes));
        Assert.Contains("magic", failure.Reason);
    }

    [Fact]
    public async Task ATruncatedGlbIsAnError()
    {
        var bytes = JsonGlb("""{"images": []}""");
        await Assert.ThrowsAsync<ImportFailureException>(() => DepsOf(_importer, "m.glb", bytes[..^4]));
    }

    [Fact]
    public async Task AGlbTooShortToHaveAHeaderIsAnError()
    {
        await Assert.ThrowsAsync<ImportFailureException>(() => DepsOf(_importer, "m.glb", new byte[8]));
    }

    [Fact]
    public async Task MalformedJsonIsAnError()
    {
        await Assert.ThrowsAsync<ImportFailureException>(() => DepsOf(_importer, "m.gltf", Gltf("{nope")));
    }

    [Fact]
    public async Task PassesTheBytesThroughUnderTheContainersName()
    {
        var bytes = Gltf("""{"images": []}""");
        var result = await _importer.ImportAsync(new ImportRequest
        {
            Id = Id("m.gltf"),
            Bytes = bytes,
            Settings = new Dictionary<string, object?>(),
            Target = CookTarget.Any,
            Source = new MemoryAssetSource(),
        });
        Assert.Equal(["gltf"], result.Outputs.Keys);
        Assert.Equal(bytes, result.Outputs["gltf"]);
    }
}
