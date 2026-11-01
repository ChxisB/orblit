using System.Text;
using System.Text.Json;
using Orblit.Sprite;

namespace Orblit.Asset.Tests;

public sealed class AtlasImporterTests
{
    private readonly AtlasImporter _importer = new();

    private MemoryCookCache _cache = new();
    private Cook _cook = null!;

    internal static AssetId Id(string text) => AssetId.Parse(text);

    /// <summary>
    /// A solid rectangle, as PNG bytes: enough for the packer to place and for a test to tell two sprites
    /// apart by colour.
    /// </summary>
    internal static byte[] Block(int width, int height, int red)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            pixels[i * 4] = (byte)red;
            pixels[i * 4 + 3] = 255;
        }

        return PngCodec.Encode(width, height, pixels);
    }

    internal static byte[] Document(object value) =>
        Encoding.UTF8.GetBytes(value as string ?? JsonSerializer.Serialize(value));

    internal static Dictionary<AssetId, byte[]> Assets(object document, int count = 3)
    {
        var files = new Dictionary<AssetId, byte[]> { [Id("ui.atlas.json")] = Document(document) };
        for (var i = 0; i < count; i++)
        {
            files[Id($"ui/s{i}.png")] = Block(16, 16, i * 40);
        }

        return files;
    }

    private Cook CookOver(Dictionary<AssetId, byte[]> files) =>
        new(new MemoryAssetSource(files), _cache, new ImporterRegistry([_importer]), CookTargets.Macos);

    private void Give(Dictionary<AssetId, byte[]> files)
    {
        _cache = new MemoryCookCache();
        _cook = CookOver(files);
    }

    [Fact]
    public void ClaimsADocumentThatSaysItIsAnAtlas()
    {
        Assert.True(_importer.CanHandle(Id("ui.atlas.json")));
        Assert.True(_importer.CanHandle(Id("menus/ui.ATLAS.JSON")));
    }

    [Fact]
    public void ClaimsNoOtherJsonBecauseJsonIsEverybodysExtension()
    {
        Assert.False(_importer.CanHandle(Id("level.json")));
        Assert.False(_importer.CanHandle(Id("wall.png.import.json")));
        Assert.False(_importer.CanHandle(Id("atlas.json")));
    }

    [Fact]
    public async Task DependsOnTheSpritesTheDocumentNamesBeforeAnythingIsPacked()
    {
        var found = await _importer.DependenciesOfAsync(
            Id("ui.atlas.json"),
            Document(new { sprites = new[] { "ui/a.png", "ui/b.png" } }),
            new Dictionary<string, object?>());
        Assert.Equal([Id("ui/a.png"), Id("ui/b.png")], found.ToHashSet());
    }

    [Fact]
    public async Task ReadsTheSpritesAsProjectRelativeWithDotSlashMeaningBesideMe()
    {
        var found = await _importer.DependenciesOfAsync(
            Id("menus/ui.atlas.json"),
            Document(new { sprites = new[] { "./play.png", "shared/stop.png" } }),
            new Dictionary<string, object?>());
        Assert.Equal([Id("menus/play.png"), Id("shared/stop.png")], found.ToHashSet());
    }

    [Fact]
    public async Task SaysADocumentThatIsNotJsonIsNotJson()
    {
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => _importer.DependenciesOfAsync(
            Id("ui.atlas.json"), Document("{ not json"), new Dictionary<string, object?>()));
        Assert.Contains("not JSON", failure.Reason);
    }

    [Fact]
    public async Task SaysWhichKeyIsMissingFromADocumentWithNoSpritesList()
    {
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => _importer.DependenciesOfAsync(
            Id("ui.atlas.json"), Document(new { padding = 2 }), new Dictionary<string, object?>()));
        Assert.Contains("\"sprites\"", failure.Reason);
    }

    [Fact]
    public async Task NamesASpriteThatIsNotAnAssetNameInTheFailure()
    {
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => _importer.DependenciesOfAsync(
            Id("ui.atlas.json"),
            Document(new { sprites = new[] { "https://example.com/a.png" } }),
            new Dictionary<string, object?>()));
        Assert.Contains("example.com", failure.Reason);
    }

    [Fact]
    public async Task ProducesAPageAndADescriptorForIt()
    {
        Give(Assets(new { sprites = new[] { "ui/s0.png", "ui/s1.png", "ui/s2.png" } }));
        var result = await _cook.CookOneAsync(Id("ui.atlas.json"));
        Assert.Equal(CookStatus.Cooked, result.Status);
        var names = result.Asset!.Outputs.Select(output => output.Name).ToList();
        Assert.Contains("page0.png", names);
        Assert.Contains("page0.json", names);
    }

    [Fact]
    public async Task NamesEverySpriteInTheDescriptorByItsAssetId()
    {
        Give(Assets(new { sprites = new[] { "ui/s0.png", "ui/s1.png", "ui/s2.png" } }));
        var result = await _cook.CookOneAsync(Id("ui.atlas.json"));
        var atlas = Atlas.Read(Encoding.UTF8.GetString((await _cache.ReadAsync(result.Asset!["page0.json"]!.Hash))!));
        Assert.Contains("ui/s0.png", atlas!.Regions.Keys);
        Assert.Contains("ui/s2.png", atlas.Regions.Keys);
    }

    [Fact]
    public async Task WritesAPageThatReadsBackAtTheSizeItSays()
    {
        Give(Assets(new { sprites = new[] { "ui/s0.png" }, powerOfTwo = false, padding = 0 }));
        var result = await _cook.CookOneAsync(Id("ui.atlas.json"));
        var page = PngCodec.Decode((await _cache.ReadAsync(result.Asset!["page0.png"]!.Hash))!);
        Assert.True(page.Width >= 16);
        Assert.Equal(page.Width * page.Height * 4, page.Pixels.Length);
    }

    [Fact]
    public async Task SaysWhatItDidBecauseAPackIsAResultWorthReading()
    {
        Give(Assets(new { sprites = new[] { "ui/s0.png", "ui/s1.png" } }));
        var result = await _cook.CookOneAsync(Id("ui.atlas.json"));
        Assert.Contains("2 sprites", result.Notes[0]);
        Assert.Contains("% full", string.Join(' ', result.Notes));
    }

    [Fact]
    public async Task TreatsAnEmptyAtlasAsAMistakeNotAnEmptyPage()
    {
        Give(new Dictionary<AssetId, byte[]> { [Id("ui.atlas.json")] = Document(new { sprites = Array.Empty<string>() }) });
        var result = await _cook.CookOneAsync(Id("ui.atlas.json"));
        Assert.Equal(CookStatus.Failed, result.Status);
        Assert.Contains("at least one sprite", result.Error!.Message);
    }

    [Fact]
    public async Task FailsTheAtlasWhenASpriteIsTooLargeToFitRatherThanLosingIt()
    {
        Give(new Dictionary<AssetId, byte[]>
        {
            [Id("ui.atlas.json")] = Document(new { sprites = new[] { "ui/huge.png" }, maxPageSize = 32 }),
            [Id("ui/huge.png")] = Block(64, 64, 255),
        });
        var result = await _cook.CookOneAsync(Id("ui.atlas.json"));
        Assert.Equal(CookStatus.Failed, result.Status);
        Assert.Contains("do not fit", result.Error!.Message);
        Assert.Contains("ui/huge.png", result.Error.Message);
    }

    [Fact]
    public async Task SaysWhichHeuristicsThereAreForAnUnknownOne()
    {
        Give(Assets(new { sprites = new[] { "ui/s0.png" }, heuristic = "sideways" }));
        var result = await _cook.CookOneAsync(Id("ui.atlas.json"));
        Assert.Equal(CookStatus.Failed, result.Status);
        Assert.Contains("contact", result.Error!.Message);
    }

    [Fact]
    public async Task NamesTheSpriteNotTheAtlasWhenASpriteIsNotAPng()
    {
        Give(new Dictionary<AssetId, byte[]>
        {
            [Id("ui.atlas.json")] = Document(new { sprites = new[] { "ui/broken.png" } }),
            [Id("ui/broken.png")] = Encoding.UTF8.GetBytes("this is not a png"),
        });
        var result = await _cook.CookOneAsync(Id("ui.atlas.json"));
        Assert.Equal(CookStatus.Failed, result.Status);
        Assert.Contains("ui/broken.png", result.Error!.Message);
    }

    [Fact]
    public async Task CostsNothingToRepackWhenNeitherTheDocumentNorASpriteChanged()
    {
        var files = Assets(new { sprites = new[] { "ui/s0.png", "ui/s1.png" } });
        Give(files);
        await _cook.CookOneAsync(Id("ui.atlas.json"));

        var again = await CookOver(files).CookOneAsync(Id("ui.atlas.json"));
        Assert.Equal(CookStatus.Cached, again.Status);
    }

    [Fact]
    public async Task RepacksWhenOneSpriteInItChanged()
    {
        var files = Assets(new { sprites = new[] { "ui/s0.png", "ui/s1.png" } });
        Give(files);
        await _cook.CookOneAsync(Id("ui.atlas.json"));

        files[Id("ui/s1.png")] = Block(16, 16, 200);
        var again = await CookOver(files).CookOneAsync(Id("ui.atlas.json"));
        Assert.Equal(CookStatus.Cooked, again.Status);
    }

    [Fact]
    public void AProjectCooksAtlasesWithoutBeingToldTo()
    {
        Assert.NotNull(CookProject.DefaultImporters().ByName("atlas"));
        Assert.NotNull(RuntimeImport.DefaultImporters().ByName("atlas"));
    }
}
