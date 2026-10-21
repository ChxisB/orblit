using static Orblit.Scene.Tests.Tree;

namespace Orblit.Scene.Tests;

public class TerrainComponentTests
{
    [Fact]
    public void NamesNoFileAndCastsShadowsWhenTheFileSaysNothing()
    {
        var terrain = TerrainComponent.FromJson(Map());

        Assert.Null(terrain.File);
        Assert.True(terrain.CastShadows);
        Assert.True(terrain.ReceiveShadows);
    }

    [Fact]
    public void WritesOnlyWhatIsNotTheDefault()
    {
        Same(Map(("file", "a.oterrain")), new TerrainComponent(file: "a.oterrain").ToJson());
        Assert.Empty(new TerrainComponent().ToJson());
    }
}
