using System.Buffers.Binary;
using static Orblit.Asset.Tests.GltfImporterTests;
using static Orblit.Asset.Tests.ModelAtlasImporterTests;

namespace Orblit.Asset.Tests;

public sealed class GltfImporterAddedTests
{
    private const uint BinChunk = 0x004E4942;

    private readonly GltfImporter _importer = new();

    private static ImportRequest Request(byte[] bytes, bool atlas, bool collision) =>
        new()
        {
            Id = Id("models/thing.glb"),
            Bytes = bytes,
            Settings = new Dictionary<string, object?> { ["atlas"] = atlas, ["collision"] = collision },
            Target = CookTarget.Any,
            Source = new MemoryAssetSource(),
        };

    private object? Resolved(string key, object? value) =>
        _importer.ResolveSettings(
            new ImportSettings(values: new Dictionary<string, object?> { ["atlas"] = true, [key] = value }))[key];

    private static byte[] Chunked(uint length, uint type, int total)
    {
        var bytes = new byte[total];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x46546C67);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), length);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), type);
        return bytes;
    }

    [Fact]
    public void AcceptsEachSettingAtBothEdgesOfItsRange()
    {
        (string Key, long Low, long High)[] ranges =
        [
            ("maxPageSize", 64, 16384),
            ("padding", 0, 64),
            ("extrude", 0, 64),
            ("maxCellSize", 0, 16384),
            ("minMaterials", 1, 4096),
            ("maxMergedVertices", 3, 1 << 24),
        ];
        foreach (var (key, low, high) in ranges)
        {
            Assert.Equal(low, Resolved(key, low));
            Assert.Equal(high, Resolved(key, high));
        }
    }

    [Fact]
    public void RefusesEachSettingOneStepOutsideItsRangeAndWhenItIsNotWhole()
    {
        (string Key, long Low, long High)[] ranges =
        [
            ("maxPageSize", 64, 16384),
            ("padding", 0, 64),
            ("extrude", 0, 64),
            ("maxCellSize", 0, 16384),
            ("minMaterials", 1, 4096),
            ("maxMergedVertices", 3, 1 << 24),
        ];
        foreach (var (key, low, high) in ranges)
        {
            Assert.Throws<ArgumentException>(() => Resolved(key, low - 1));
            Assert.Throws<ArgumentException>(() => Resolved(key, high + 1));
            Assert.Throws<ArgumentException>(() => Resolved(key, (double)low));
        }
    }

    [Fact]
    public void NamesTheSettingAndItsRangeWhenItRefuses()
    {
        var error = Assert.Throws<ArgumentException>(() => Resolved("padding", 65L));
        Assert.Contains("padding", error.Message, StringComparison.Ordinal);
        Assert.Contains("between 0 and 64", error.Message, StringComparison.Ordinal);
        Assert.Contains("65", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyAnExplicitFalseTurnsMergingOff()
    {
        Assert.Equal(false, Resolved("mergePrimitives", false));
        Assert.Equal(true, Resolved("mergePrimitives", true));
        Assert.Equal(true, Resolved("mergePrimitives", "no"));
    }

    [Fact]
    public void OnlyAnExplicitTrueTurnsTheAtlasOrCollisionOn()
    {
        var odd = _importer.ResolveSettings(new ImportSettings(
            values: new Dictionary<string, object?> { ["atlas"] = "yes", ["collision"] = 1L }));
        Assert.Equal(false, odd["atlas"]);
        Assert.Equal(false, odd["collision"]);

        var on = _importer.ResolveSettings(new ImportSettings(
            values: new Dictionary<string, object?> { ["atlas"] = false, ["collision"] = true }));
        Assert.Equal(true, on["collision"]);
        Assert.False(on.ContainsKey("padding"));

        var both = _importer.ResolveSettings(new ImportSettings(
            values: new Dictionary<string, object?> { ["atlas"] = true, ["collision"] = true }));
        Assert.Equal(true, both["collision"]);
    }

    [Fact]
    public async Task AddsACollisionSidecarBesideThePassedThroughModel()
    {
        var bytes = TwoMaterials().ToGlb();

        var result = await _importer.ImportAsync(Request(bytes, atlas: false, collision: true));

        Assert.Equal(["collision.json", "glb"], result.Outputs.Keys.Order());
        Assert.Same(bytes, result.Outputs["glb"]);
        var expected = await CollisionMesh.FromGltfAsync(Id("models/thing.glb"), bytes, new MemoryAssetSource());
        Assert.Equal(expected.Encode(), result.Outputs["collision.json"]);
    }

    [Fact]
    public async Task AddsNoSidecarUnlessCollisionIsAsked()
    {
        var result = await _importer.ImportAsync(Request(TwoMaterials().ToGlb(), atlas: false, collision: false));

        Assert.Equal(["glb"], result.Outputs.Keys);
    }

    [Fact]
    public async Task KeepsTheAtlasNotesWhenTheSidecarIsAddedToo()
    {
        var model = new QuadModel();
        model.Quad(model.Material(BaseColour(model.Texture(Red))));
        var bytes = model.ToGlb();
        var request = Request(bytes, atlas: true, collision: true);
        var settings = AtlasSettings();
        settings["collision"] = true;

        var result = await _importer.ImportAsync(new ImportRequest
        {
            Id = request.Id,
            Bytes = bytes,
            Settings = settings,
            Target = request.Target,
            Source = request.Source,
        });

        Assert.Equal(["collision.json", "glb"], result.Outputs.Keys.Order());
        Assert.Contains("left as it was", result.Notes);
    }

    [Fact]
    public async Task ReadsAGlbWhoseJsonChunkEndsExactlyAtTheEndOfTheFile()
    {
        var bytes = JsonGlb("""{"buffers": [{"uri": "a.bin"}]}""");
        AssertSet(await DepsOf(_importer, "m.glb", bytes), "a.bin");
    }

    [Fact]
    public async Task ASizedChunkOneByteLongerThanTheFileIsTruncated()
    {
        var bytes = JsonGlb("""{"images": []}""");
        var length = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), length + 1);

        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => DepsOf(_importer, "m.glb", bytes));

        Assert.Contains("truncated", failure.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AChunkLengthTooBigForAnIntIsTruncatedRatherThanWrappedAround()
    {
        var failure = await Assert.ThrowsAsync<ImportFailureException>(
            () => DepsOf(_importer, "m.glb", Chunked(uint.MaxValue, 0x4E4F534A, 32)));

        Assert.Contains("truncated", failure.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARunOfBytesOneShortOfAChunkHeaderIsTooShort()
    {
        var failure = await Assert.ThrowsAsync<ImportFailureException>(
            () => DepsOf(_importer, "m.glb", new byte[19]));

        Assert.Contains("too short", failure.Reason, StringComparison.Ordinal);
        Assert.Contains("19 bytes", failure.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AGlbWithAnEmptyJsonChunkIsUnreadableNotTooShort()
    {
        var failure = await Assert.ThrowsAsync<ImportFailureException>(
            () => DepsOf(_importer, "m.glb", Chunked(0, 0x4E4F534A, 20)));

        Assert.Contains("not readable", failure.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AGlbWhoseFirstChunkIsNotJsonIsAnError()
    {
        var failure = await Assert.ThrowsAsync<ImportFailureException>(
            () => DepsOf(_importer, "m.glb", Chunked(0, BinChunk, 32)));

        Assert.Contains("first chunk", failure.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ADocumentThatIsNotAnObjectIsAnError()
    {
        var failure = Assert.Throws<ImportFailureException>(() => GltfImporter.UrisIn(Id("m.gltf"), Gltf("[]")));

        Assert.Contains("not an object", failure.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void SkipsEntriesThatAreNotAUriToFollow()
    {
        var json = Gltf("""
            {"buffers": "none",
             "images": [1, {"uri": 5}, {"uri": ""}, {}, {"uri": "a.png"}, {"uri": "data:image/png;base64,AAAA"}]}
            """);

        AssertSet(GltfImporter.UrisIn(Id("m.gltf"), json), "a.png");
    }

    [Fact]
    public void IsVersionThreeWithNoCellLimitByDefault()
    {
        Assert.Equal(3, _importer.Version);
        Assert.Equal("gltf", _importer.Name);
        Assert.Equal(0L, Resolved("maxCellSize", null));
    }

    [Fact]
    public void FaultsTheTaskRatherThanThrowingAtTheCall()
    {
        var thrown = Record.Exception(() => { _ = DepsOf(_importer, "m.gltf", Gltf("{nope")); });

        Assert.Null(thrown);
    }

    [Fact]
    public async Task DecodesAPercentEscapeInAUri()
    {
        var json = Gltf("""{"images": [{"uri": "tex%2Fa.png"}]}""");

        AssertSet(await DepsOf(_importer, "m.gltf", json), "tex/a.png");
    }

    [Fact]
    public async Task RefusesAUriWithABadEscapeAsANameOutsideTheProject()
    {
        var json = Gltf("""{"images": [{"uri": "bad%zz.png"}]}""");

        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => DepsOf(_importer, "m.gltf", json));

        Assert.Contains("bad%zz.png", failure.Message, StringComparison.Ordinal);
    }
}
