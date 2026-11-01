using System.Text;
using static Orblit.Asset.Tests.GltfImporterTests;

namespace Orblit.Asset.Tests;

public sealed class SceneImporterTests
{
    private readonly SceneImporter _scene = new();

    private async Task<IReadOnlySet<AssetId>> DepsIn(string json) =>
        await DepsOf(_scene, "scenes/level.oscene", Encoding.UTF8.GetBytes(json));

    [Fact]
    public async Task FindsWhatASceneHoldsHoweverDeep()
    {
        AssertSet(
            await DepsIn("""
                {"nodes": [{"name": "floor", "model": "models/floor.glb",
                            "children": [{"name": "lamp", "model": "../props/lamp.glb"}]}],
                 "environment": "sky/dusk.hdr"}
                """),
            "models/floor.glb",
            "props/lamp.glb",
            "sky/dusk.hdr");
    }

    [Fact]
    public async Task FindsAFieldNobodyThoughtToNameHereBecauseItMatchesTheShapeRatherThanAList()
    {
        AssertSet(await DepsIn("""{"nodes": [{"emissiveTexture": "lights/glow.png"}]}"""), "lights/glow.png");
    }

    [Fact]
    public async Task AReferenceIsAnAssetIdSoMovingTheSceneDoesNotBreakIt()
    {
        // Not scenes/models/floor.glb.
        AssertSet(await DepsIn("""{"nodes": [{"model": "models/floor.glb"}]}"""), "models/floor.glb");
    }

    [Fact]
    public async Task AReferenceSpelledDotSlashIsReadAgainstTheScenesOwnFolder()
    {
        AssertSet(await DepsIn("""{"nodes": [{"model": "./beside.glb"}]}"""), "scenes/beside.glb");
    }

    [Fact]
    public async Task AReferenceKeyHoldingSomethingThatIsNotANameAtAllIsNotADependency()
    {
        Assert.Empty(await DepsIn("""{"nodes": [{"material": "matte white"}]}"""));
    }

    [Fact]
    public async Task AStringUnderAKeyThatNamesNothingIsLeftAlone()
    {
        Assert.Empty(await DepsIn("""{"name": "level one", "author": "someone"}"""));
    }

    [Fact]
    public async Task MalformedJsonIsAnError()
    {
        await Assert.ThrowsAsync<ImportFailureException>(() => DepsOf(_scene, "a.oscene", Encoding.UTF8.GetBytes("nope")));
    }
}
