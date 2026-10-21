namespace Orblit.Asset.Tests;

public sealed class NativeImportersTests
{
    [Fact]
    public void TheNativeImportersAreRegisteredUnderDistinctNames()
    {
        var native = NativeImporters.Create();
        var registry = new ImporterRegistry(native);
        Assert.Equal(native.Count, registry.Importers.Count);
        Assert.IsType<TextureImporter>(registry.ForAsset(AssetId.Parse("a.png")));
        Assert.IsType<ModelImporter>(registry.ForAsset(AssetId.Parse("a.fbx")));
        Assert.IsType<SplatImporter>(registry.ForAsset(AssetId.Parse("a.ply")));
        Assert.IsType<EnvironmentImporter>(registry.ForAsset(AssetId.Parse("a.hdr")));
    }
}
