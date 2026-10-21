namespace Orblit.Asset.Tests;

public sealed class EnvironmentImporterTests : NativeToolWork
{
    private static IReadOnlyDictionary<string, object?> Resolve(Dictionary<string, object?>? values = null) =>
        new EnvironmentImporter().ResolveSettings(new ImportSettings(values: values));

    [ShellFact]
    public async Task BakesTheReflectionsAndTheBackdropAndKeepsTheHarmonics()
    {
        var importer = new EnvironmentImporter(Fake(
            "writeAfter=--deploy:environment_ibl.ktx;writeAfter=--deploy:sh.txt;writeAfter=--extract:environment_skybox.ktx"));
        var result = await importer.ImportAsync(RequestFor("sky/dusk.hdr", Resolve()));
        Assert.Equal(["ibl.ktx", "sh.txt", "skybox.ktx"], result.Outputs.Keys.Order());
    }

    [Fact]
    public void TheBackdropIsFourTimesTheReflectionsHeldTo1024()
    {
        var plain = Resolve();
        Assert.Equal(256L, Assert.IsType<long>(plain["size"]));
        Assert.Equal(1024L, Assert.IsType<long>(plain["skyboxSize"]));
        var small = Resolve(new Dictionary<string, object?> { ["size"] = 64L });
        Assert.Equal(64L, Assert.IsType<long>(small["size"]));
        Assert.Equal(256L, Assert.IsType<long>(small["skyboxSize"]));
    }

    [Fact]
    public void RefusesAFaceSizeThatIsNotAPowerOfTwo() =>
        Assert.Throws<FormatException>(() => Resolve(new Dictionary<string, object?> { ["size"] = 300L }));

    [ShellFact]
    public async Task ACmgenThatNamesItsOutputsDifferentlyIsAFailureThatSaysSo()
    {
        var importer = new EnvironmentImporter(Fake(""));
        var failure = await Assert.ThrowsAsync<ImportFailureException>(
            () => importer.ImportAsync(RequestFor("sky.hdr", Resolve())));
        Assert.Contains("different Filament version", failure.Reason);
    }
}
