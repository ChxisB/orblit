namespace Orblit.Asset.Tests;

public class ImporterRegistryTests
{
    private static AssetId Id(string text) => AssetId.Parse(text);

    [Fact]
    public void PicksByExtensionInRegistrationOrder()
    {
        var first = new RecordingImporter("first", extension: "png");
        var second = new RecordingImporter("second", extension: "png");
        var registry = new ImporterRegistry([first, second]);
        Assert.Same(first, registry.ForAsset(Id("a.png")));
    }

    [Fact]
    public void ANamedImporterBeatsTheExtension()
    {
        var byExtension = new RecordingImporter("by-extension", extension: "png");
        var named = new RecordingImporter("named", extension: "exr");
        var registry = new ImporterRegistry([byExtension, named]);
        Assert.Same(named, registry.ForAsset(Id("a.png"), new ImportSettings(importer: "named")));
    }

    [Fact]
    public void NamingAnImporterThatIsNotRegisteredIsAnErrorThatListsTheOnesThatAre()
    {
        var registry = new ImporterRegistry([new RecordingImporter("texture")]);
        var error = Assert.Throws<ArgumentException>(
            () => registry.ForAsset(Id("a.txt"), new ImportSettings(importer: "textrue")));
        Assert.Contains("texture", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoImportersMayNotShareANameBecauseTheyWouldShareCacheEntries()
    {
        var registry = new ImporterRegistry([new RecordingImporter()]);
        Assert.Throws<ArgumentException>(() => registry.Add(new RecordingImporter()));
    }

    [Fact]
    public void AnUnclaimedAssetIsNobodyNotAnError()
    {
        var registry = new ImporterRegistry([new RecordingImporter()]);
        Assert.Null(registry.ForAsset(Id("README.md")));
    }
}
