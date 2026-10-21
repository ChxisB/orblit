using System.Text;
using Orblit.Json;

namespace Orblit.Asset.Tests;

public class ImportSettingsTests
{
    private static AssetId Id(string text) => AssetId.Parse(text);

    private static MemoryAssetSource Project(Dictionary<string, string> files) =>
        new(files.ToDictionary(file => Id(file.Key), file => Encoding.UTF8.GetBytes(file.Value)));

    private static string Settings(string? importer = null, Dictionary<string, object?>? values = null)
    {
        var json = new OrderedDictionary<string, object?> { ["formatVersion"] = 1L };
        if (importer is not null)
        {
            json["importer"] = importer;
        }

        json["settings"] = values ?? [];
        return DartJson.Encode(json);
    }

    private static void Same(IReadOnlyDictionary<string, object?> actual, Dictionary<string, object?> expected)
    {
        Assert.Equal(expected.Count, actual.Count);
        foreach (var (key, value) in expected)
        {
            Assert.Equal(value, actual[key]);
        }
    }

    // which files can have a say

    [Fact]
    public void EveryFolderAboveTheAssetThenTheAssetItself() =>
        Assert.Equal(
            [".import.json", "textures/.import.json", "textures/wood/.import.json", "textures/wood/wall.png.import.json"],
            ImportSettingsReader.FilesFor(Id("textures/wood/wall.png")).Select(id => id.ToString()));

    [Fact]
    public void AnAssetAtTheRootHasTheProjectFileAndItsOwn() =>
        Assert.Equal(
            [".import.json", "wall.png.import.json"],
            ImportSettingsReader.FilesFor(Id("wall.png")).Select(id => id.ToString()));

    [Fact]
    public void TheSuffixGoesOnTheWholeNameSoTwoKindsDoNotCollide()
    {
        // wall.png and wall.jpg in one folder want settings of their own.
        Assert.Equal("t/wall.png.import.json", ImportSettings.FileFor(Id("t/wall.png")).ToString());
        Assert.Equal("t/wall.jpg.import.json", ImportSettings.FileFor(Id("t/wall.jpg")).ToString());
    }

    [Fact]
    public void AFolderFileIsNamedForItsFolder()
    {
        Assert.Equal(".import.json", ImportSettings.FolderFileFor("").ToString());
        Assert.Equal("textures/wood/.import.json", ImportSettings.FolderFileFor("textures/wood").ToString());
    }

    // telling settings files from assets

    [Fact]
    public void ASettingsFileIsNotSomethingToCook()
    {
        Assert.True(ImportSettings.IsSettingsFile(Id(".import.json")));
        Assert.True(ImportSettings.IsSettingsFile(Id("t/.import.json")));
        Assert.True(ImportSettings.IsSettingsFile(Id("t/wall.png.import.json")));
        Assert.False(ImportSettings.IsSettingsFile(Id("t/wall.png")));
        Assert.False(ImportSettings.IsSettingsFile(Id("t/import.json")));
    }

    [Fact]
    public void APerAssetFileSaysWhichAssetItBelongsTo() =>
        Assert.Equal("t/wall.png", ImportSettings.AssetFor(Id("t/wall.png.import.json"))!.ToString());

    [Fact]
    public void AFolderFileBelongsToNoOneAsset()
    {
        Assert.Null(ImportSettings.AssetFor(Id(".import.json")));
        Assert.Null(ImportSettings.AssetFor(Id("t/.import.json")));
    }

    [Fact]
    public void SomethingThatIsNotASettingsFileBelongsToNothing() => Assert.Null(ImportSettings.AssetFor(Id("t/wall.png")));

    // what applies to an asset

    [Fact]
    public async Task NothingAtAllWhenTheProjectSaysNothing()
    {
        var read = new ImportSettingsReader(Project([]));
        var settings = await read.ForAssetAsync(Id("t/wall.png"));
        Assert.Null(settings.Importer);
        Assert.Empty(settings.Values);
    }

    [Fact]
    public async Task AFolderSpeaksForEverythingUnderIt()
    {
        // A folder of sprites is all pixel art, and saying so once is the difference between
        // settings that stay true and two hundred files nobody maintains.
        var read = new ImportSettingsReader(Project(new()
        {
            ["sprites/.import.json"] = Settings(values: new() { ["lossless"] = true }),
        }));
        Same((await read.ForAssetAsync(Id("sprites/hero/idle.png"))).Values, new() { ["lossless"] = true });
    }

    [Fact]
    public async Task TheNearerFileWinsKeyByKey()
    {
        var read = new ImportSettingsReader(Project(new()
        {
            [".import.json"] = Settings(values: new() { ["maxSize"] = 2048L, ["srgb"] = true }),
            ["textures/.import.json"] = Settings(values: new() { ["maxSize"] = 1024L }),
        }));
        Same((await read.ForAssetAsync(Id("textures/wall.png"))).Values, new() { ["maxSize"] = 1024L, ["srgb"] = true });
    }

    [Fact]
    public async Task TheAssetHasTheLastWord()
    {
        var read = new ImportSettingsReader(Project(new()
        {
            ["textures/.import.json"] = Settings(values: new() { ["role"] = "colour" }),
            ["textures/wall.png.import.json"] = Settings(values: new() { ["role"] = "normal" }),
        }));
        Assert.Equal("normal", (await read.ForAssetAsync(Id("textures/wall.png"))).Values["role"]);
    }

    [Fact]
    public async Task AnImporterNamedHigherUpStillApplies()
    {
        var read = new ImportSettingsReader(Project(new() { ["t/.import.json"] = Settings(importer: "raw") }));
        Assert.Equal("raw", (await read.ForAssetAsync(Id("t/table.png"))).Importer);
    }

    [Fact]
    public async Task ANearerFileCanChangeTheImporter()
    {
        var read = new ImportSettingsReader(Project(new()
        {
            ["t/.import.json"] = Settings(importer: "raw"),
            ["t/table.png.import.json"] = Settings(importer: "texture"),
        }));
        Assert.Equal("texture", (await read.ForAssetAsync(Id("t/table.png"))).Importer);
    }

    [Fact]
    public async Task OneAssetDoesNotPickUpAnotherAssetsSettings()
    {
        var read = new ImportSettingsReader(Project(new()
        {
            ["t/wall.png.import.json"] = Settings(values: new() { ["role"] = "normal" }),
        }));
        Assert.Empty((await read.ForAssetAsync(Id("t/floor.png"))).Values);
    }

    [Fact]
    public async Task ASettingsFileIsReadOnceHoweverManyAssetsAsk()
    {
        var reads = 0;
        var source = new CallbackAssetSource(id =>
        {
            if (id.ToString() != "t/.import.json")
            {
                return Task.FromResult<byte[]?>(null);
            }

            reads++;
            return Task.FromResult<byte[]?>(Encoding.UTF8.GetBytes(Settings(values: new() { ["maxSize"] = 512L })));
        });
        var read = new ImportSettingsReader(source);
        foreach (var name in new[] { "a", "b", "c" })
        {
            Assert.Equal(512L, (await read.ForAssetAsync(Id($"t/{name}.png"))).Values["maxSize"]);
        }

        Assert.Equal(1, reads);
    }

    // merging

    [Fact]
    public void AKeyInTheLaterSettingsReplacesTheEarlierOneOutright()
    {
        var outer = new ImportSettings(values: new Dictionary<string, object?>
        {
            ["mips"] = new Dictionary<string, object?> { ["filter"] = "box", ["renormalise"] = true },
        });
        var inner = new ImportSettings(values: new Dictionary<string, object?>
        {
            ["mips"] = new Dictionary<string, object?> { ["filter"] = "kaiser" },
        });
        // Shallow on purpose: renormalise is gone, not merged back in.
        Same(
            outer.MergedWith(inner).Values,
            new() { ["mips"] = new Dictionary<string, object?> { ["filter"] = "kaiser" } });
    }

    [Fact]
    public void AnImporterTheLaterSettingsDoNotNameIsKept()
    {
        var outer = new ImportSettings(importer: "raw");
        var inner = new ImportSettings(values: new Dictionary<string, object?> { ["maxSize"] = 8L });
        Assert.Equal("raw", outer.MergedWith(inner).Importer);
    }

    // reading a file

    [Fact]
    public void WhatEncodeWritesParseReads()
    {
        var settings = new ImportSettings(
            importer: "texture",
            values: new Dictionary<string, object?> { ["role"] = "normal", ["maxSize"] = 1024L });
        var read = ImportSettings.Parse(settings.Encode(), from: Id(".import.json"));
        Assert.Equal("texture", read.Importer);
        Same(read.Values, new() { ["role"] = "normal", ["maxSize"] = 1024L });
    }

    [Fact]
    public void SettingsAreWrittenInKeyOrderSoADiffIsReviewable()
    {
        var settings = new ImportSettings(values: new Dictionary<string, object?> { ["b"] = 1L, ["a"] = 2L });
        Assert.True(settings.Encode().IndexOf("\"a\"", StringComparison.Ordinal) < settings.Encode().IndexOf("\"b\"", StringComparison.Ordinal));
    }

    [Fact]
    public void AFileWithNoFormatVersionIsReadAsTheCurrentOne()
    {
        var read = ImportSettings.Parse("""{"settings": {"maxSize": 4}}""", from: Id(".import.json"));
        Same(read.Values, new() { ["maxSize"] = 4L });
    }

    [Fact]
    public void ANewerFormatVersionIsRefusedRatherThanHalfRead()
    {
        var error = Assert.Throws<AssetFormatException>(
            () => ImportSettings.Parse("""{"formatVersion": 99, "settings": {}}""", from: Id(".import.json")));
        Assert.Contains(".import.json", error.Message);
        Assert.Contains("99", error.Message);
    }

    [Theory]
    [InlineData("not JSON at all", "{oh dear")]
    [InlineData("not an object", "[1, 2, 3]")]
    [InlineData("an importer that is not a name", """{"importer": 7}""")]
    [InlineData("an importer named as nothing", """{"importer": ""}""")]
    [InlineData("settings that are not an object", """{"settings": [1]}""")]
    [InlineData("a format version that is not a number", """{"formatVersion": "1"}""")]
    public void IsRefusedNamingTheFile(string what, string text)
    {
        var error = Assert.Throws<AssetFormatException>(() => ImportSettings.Parse(text, from: Id("t/wall.png.import.json")));
        Assert.True(error.Message.Contains("t/wall.png.import.json", StringComparison.Ordinal), what);
    }
}
