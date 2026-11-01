namespace Orblit.Asset.Tests;

public sealed class ModelImporterTests : NativeToolWork
{
    [ShellFact]
    public async Task ConvertsToAGlb()
    {
        var importer = new ModelImporter(Fake("writeArg=1"));
        var result = await importer.ImportAsync(RequestFor("chair.fbx"));
        Assert.Equal(["glb"], result.Outputs.Keys);
        Assert.EndsWith("in.fbx", OnlyRun()[0]);
        Assert.EndsWith("out.glb", OnlyRun()[^1]);
    }

    [Fact]
    public void ClaimsTheFormatsArtistsActuallyHandOver()
    {
        var importer = new ModelImporter();
        Assert.True(importer.CanHandle(AssetId.Parse("a.fbx")));
        Assert.True(importer.CanHandle(AssetId.Parse("a.obj")));
        Assert.False(importer.CanHandle(AssetId.Parse("a.glb")));
    }

    [ShellFact]
    public async Task AConversionThatWritesNothingIsAFailureThatSaysWhyThatHappens()
    {
        var importer = new ModelImporter(Fake(""));
        var failure = await Assert.ThrowsAsync<ImportFailureException>(() => importer.ImportAsync(RequestFor("empty.fbx")));
        Assert.Contains("no GLB", failure.Reason);
    }
}
