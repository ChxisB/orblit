using Orblit.Json;

namespace Orblit.Asset.Tests;

public class CookTargetTests
{
    [Fact]
    public void PutsItsNameInTheRecipeSoTwoTargetsNeverShareAKey() =>
        Assert.Equal("""{"name":"macos"}""", DartJson.Encode(new CookTarget("macos").Recipe));

    [Fact]
    public void CarriesItsPropertiesAlongside()
    {
        var target = new CookTarget("android", new Dictionary<string, object?>
        {
            ["textureFamilies"] = new List<object?> { "astc" },
            ["maxTextureSize"] = 2048L,
        });
        Assert.Equal(
            """{"name":"android","textureFamilies":["astc"],"maxTextureSize":2048}""",
            DartJson.Encode(target.Recipe));
    }

    [Fact]
    public void ReadsThePropertiesTheBuiltInImportersAgreeOn()
    {
        var target = new CookTarget("ios", new Dictionary<string, object?>
        {
            ["textureFamilies"] = new List<object?> { "astc", "basis" },
            ["maxTextureSize"] = 4096L,
            ["halfFloatTextures"] = true,
        });
        Assert.Equal(["astc", "basis"], target.TextureFamilies());
        Assert.Equal(4096L, target.MaxTextureSize());
        Assert.True(target.HalfFloatTextures());
    }

    [Fact]
    public void AnAbsentPropertyIsAbsentNotAWrongDefault()
    {
        Assert.Empty(CookTarget.Any.TextureFamilies());
        Assert.Null(CookTarget.Any.MaxTextureSize());
        Assert.False(CookTarget.Any.HalfFloatTextures());
    }

    [Fact]
    public void RefusesAPropertyOfTheWrongShapeNamingIt()
    {
        var target = new CookTarget("bad", new Dictionary<string, object?>
        {
            ["maxTextureSize"] = -1L,
            ["textureFamilies"] = "astc",
        });
        Assert.Equal("maxTextureSize", Assert.Throws<ArgumentException>(() => target.MaxTextureSize()).ParamName);
        Assert.Equal("textureFamilies", Assert.Throws<ArgumentException>(() => target.TextureFamilies()).ParamName);
    }
}
