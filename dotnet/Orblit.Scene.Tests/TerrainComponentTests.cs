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

    [Fact]
    public void SurvivesTheRoundTripThroughASceneFile()
    {
        var document = new SceneDocument("Scene", entities: [new SceneEntity(
            "ground",
            "Ground",
            components: new Dictionary<string, SceneComponent>
            {
                [SceneComponents.Transform] = new TransformComponent(),
                [SceneComponents.Terrain] = new TerrainComponent(file: "terrain/world.oterrain", castShadows: false),
            })]);

        var back = SceneDocument.Decode(document.Encode()).Document;
        var terrain = Assert.IsType<TerrainComponent>(back["ground"]![SceneComponents.Terrain]);
        Assert.Equal("terrain/world.oterrain", terrain.File);
        Assert.False(terrain.CastShadows);
        Assert.True(terrain.ReceiveShadows);
    }
}
