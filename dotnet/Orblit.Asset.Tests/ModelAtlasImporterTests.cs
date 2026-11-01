namespace Orblit.Asset.Tests;

public sealed class ModelAtlasImporterTests
{
    internal static readonly int[] Red = [220, 40, 40, 255];
    internal static readonly int[] Blue = [40, 40, 220, 255];

    private readonly GltfImporter _importer = new();

    /// <summary>A model the atlas has something to do with: two quads, two materials, two base-colour maps.</summary>
    internal static QuadModel TwoMaterials()
    {
        var model = new QuadModel();
        var a = model.Texture(Red);
        var b = model.Texture(Blue);
        model.Quad(model.Material(BaseColour(a)));
        model.Quad(model.Material(BaseColour(b)), x: 2);
        return model;
    }

    internal static GltfMap BaseColour(int texture) => new()
    {
        ["pbrMetallicRoughness"] = new GltfMap { ["baseColorTexture"] = new GltfMap { ["index"] = texture } },
    };

    internal static Dictionary<string, object?> AtlasSettings() => new()
    {
        ["atlas"] = true,
        ["maxPageSize"] = 512L,
        ["padding"] = 4L,
        ["extrude"] = 2L,
        ["maxCellSize"] = 0L,
        ["minMaterials"] = 2L,
        ["mergePrimitives"] = true,
        ["maxMergedVertices"] = 65536L,
    };

    private static ImportRequest RequestFor(byte[] bytes, IReadOnlyDictionary<string, object?> settings, CookTarget? target = null) =>
        new()
        {
            Id = AssetId.Parse("models/thing.glb"),
            Bytes = bytes,
            Settings = settings,
            Target = target ?? CookTargets.Macos,
            Source = new MemoryAssetSource(),
        };

    public static bool ToolBuilt =>
        new NativeTool("orblit_texture_cook", environmentVariable: "ORBLIT_TEXTURE_COOK").Locate() is not null;

    [Fact]
    public void IsOffUnlessTheAssetAsksForIt()
    {
        var off = new Dictionary<string, object?> { ["atlas"] = false, ["collision"] = false };
        Assert.Equal(off, _importer.ResolveSettings(new ImportSettings()));
        Assert.Equal(
            off,
            _importer.ResolveSettings(new ImportSettings(values: new Dictionary<string, object?> { ["atlas"] = false })));
    }

    [Fact]
    public void FillsInEveryKeyOnceItIsOn()
    {
        var settings = _importer.ResolveSettings(
            new ImportSettings(values: new Dictionary<string, object?> { ["atlas"] = true }));
        Assert.Equal(true, settings["atlas"]);
        Assert.Equal(2048L, settings["maxPageSize"]);
        Assert.Equal(4L, settings["padding"]);
        Assert.Equal(2L, settings["extrude"]);
        Assert.Equal(2L, settings["minMaterials"]);
        Assert.Equal(true, settings["mergePrimitives"]);
        Assert.Equal(65536L, settings["maxMergedVertices"]);
    }

    [Fact]
    public void SaysAValueIsWrongRatherThanQuietlyUsingAnother()
    {
        // A number outside the range is a mistake in the project file, and the one thing worse than a cook
        // that stops is a cook that pretends.
        (string Key, object Value)[] wrong =
        [
            ("maxPageSize", 1L << 20),
            ("padding", -3L),
            ("maxMergedVertices", 0L),
            ("minMaterials", 0L),
            ("extrude", "lots"),
        ];
        foreach (var (key, value) in wrong)
        {
            var values = new Dictionary<string, object?> { ["atlas"] = true, [key] = value };
            Assert.Throws<ArgumentException>(() => _importer.ResolveSettings(new ImportSettings(values: values)));
        }
    }

    [Fact]
    public void ChangedVersionBecauseACacheKeyedOnTheOldOneIsWrong()
    {
        // The settings gained `atlas`. A bundle cooked before that must not be served for an asset that now
        // asks to be packed.
        Assert.True(_importer.Version >= 2);
    }

    [Fact]
    public async Task HandsTheBytesStraightBackWhenAtlasingIsOff()
    {
        var bytes = TwoMaterials().ToGlb();
        var result = await _importer.ImportAsync(
            RequestFor(bytes, new Dictionary<string, object?> { ["atlas"] = false }));
        Assert.Equal(["glb"], result.Outputs.Keys);
        Assert.Same(bytes, result.Outputs["glb"]);
    }

    [Fact]
    public async Task HandsTheBytesBackUnchangedWhenThereIsNothingToPack()
    {
        // One material: packing it would cost a recook and save nothing.
        var model = new QuadModel();
        model.Quad(model.Material(BaseColour(model.Texture(Red))));
        var bytes = model.ToGlb();

        var result = await _importer.ImportAsync(RequestFor(bytes, AtlasSettings()));

        // A model that comes back byte for byte is a cache hit next time, not a needless miss.
        Assert.Same(bytes, result.Outputs["glb"]);
        Assert.Contains("1 of", string.Join(' ', result.Notes), StringComparison.Ordinal);
    }

    [Fact(Skip = "orblit_texture_cook is not built", SkipUnless = nameof(ToolBuilt))]
    public async Task CooksThePagesAndRewritesTheModelToNameThem()
    {
        var result = await new GltfImporter().ImportAsync(RequestFor(TwoMaterials().ToGlb(), AtlasSettings()));

        // The GLB, the page in the portable format, and a sibling per family the target can read.
        Assert.Contains("glb", result.Outputs.Keys);
        Assert.Contains("atlas0.basecolour.ktx2", result.Outputs.Keys);
        Assert.Contains("atlas0.basecolour.bc.ktx2", result.Outputs.Keys);

        var document = await GltfDocument.ReadAsync(
            AssetId.Parse("models/thing.glb"), result.Outputs["glb"], new MemoryAssetSource());

        // Every image the model still names must be one of the outputs beside it, or the loader is being
        // sent to a file that was never written.
        var images = document.List("images");
        Assert.NotEmpty(images);
        foreach (var image in images)
        {
            Assert.False(image.ContainsKey("bufferView"), "a source PNG left embedded is one the GPU cannot read");
            Assert.Contains((string)image["uri"]!, result.Outputs.Keys);
        }

        // The two source PNGs are gone: nothing points at them, so nothing should be decoding and uploading them.
        Assert.Single(images);
    }
}
