using System.Text;
using static Orblit.Asset.Tests.GltfImporterTests;

namespace Orblit.Asset.Tests;

public sealed class SceneImporterAddedTests
{
    private readonly SceneImporter _scene = new();

    [Fact]
    public void ClaimsOsceneAtVersionOne()
    {
        Assert.Equal("scene", _scene.Name);
        Assert.Equal(1, _scene.Version);
        Assert.Equal(["oscene"], _scene.Extensions);
    }

    [Theory]
    [InlineData("asset")]
    [InlineData("Asset")]
    [InlineData("model")]
    [InlineData("Model")]
    [InlineData("texture")]
    [InlineData("Texture")]
    [InlineData("mesh")]
    [InlineData("Mesh")]
    [InlineData("splats")]
    [InlineData("Splats")]
    [InlineData("environment")]
    [InlineData("Environment")]
    [InlineData("material")]
    [InlineData("Material")]
    [InlineData("sprite")]
    [InlineData("Sprite")]
    [InlineData("atlas")]
    [InlineData("Atlas")]
    public async Task FindsAKeyThatEndsInEachSuffix(string suffix)
    {
        var json = $$"""{"my{{suffix}}": "things/a.bin"}""";

        AssertSet(await DepsOf(_scene, "level.oscene", Encoding.UTF8.GetBytes(json)), "things/a.bin");
    }

    [Fact]
    public void FaultsTheTaskRatherThanThrowingAtTheCall()
    {
        var thrown = Record.Exception(() => { _ = DepsOf(_scene, "level.oscene", "{nope"u8.ToArray()); });

        Assert.Null(thrown);
    }
}
