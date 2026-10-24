using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

public sealed class DiagnosticsAddedTests
{
    private static RenderObject Thing(long key, long? material = null) =>
        new() { Key = key, Transform = Matrix4.Identity(), Colour = new Vector3(1, 0, 0), Material = material };

    private static Population Crowd(int key, int count = 2) =>
        new(key, new float[count * 16], new float[count * 3], Unit());

    private static Bounds Unit() => new(Vector3.Zero(), Vector3.All(1));

    private static OrderedDictionary<string, object?> Part(OrderedDictionary<string, object?> row, string key) =>
        (OrderedDictionary<string, object?>)row[key]!;

    [Fact]
    public void ASceneWithNothingInItHasNoRows() =>
        Assert.Empty(SceneBuilders.Of([]).DrawRows());

    [Fact]
    public void AnObjectAndAPopulationOfTheSameKeyHaveRowsOfTheirOwn() =>
        Assert.Equal(
            ["object:1", "population:1"],
            (SceneBuilders.Of([Thing(1)]) with { Populations = [Crowd(1)] }).DrawRows().Select(row => row["id"]));

    [Fact]
    public void AnObjectRowKeepsItsKeyColourAndShadows()
    {
        var row = SceneBuilders.Of([Thing(5) with { CastShadows = false }]).DrawRows().Single();

        Assert.Equal(5L, row["key"]);
        Assert.Equal([1.0, 0.0, 0.0], (double[])row["colour"]!);
        Assert.Equal(false, row["castShadows"]);
        Assert.Equal(true, row["receiveShadows"]);
        Assert.Equal(16, ((double[])row["transform"]!).Length);
    }

    [Fact]
    public void AnObjectRowCarriesItsWholeTransformInColumnOrder()
    {
        var at = Matrix4.TranslationValues(4, 5, 6);
        var row = SceneBuilders.Of([Thing(1) with { Transform = at }]).DrawRows().Single();

        Assert.Equal(Enumerable.Range(0, 16).Select(i => at[i]), (double[])row["transform"]!);
    }

    [Fact]
    public void ALayerBelowNoughtReadsAsNought()
    {
        var scene = SceneBuilders.Of([Thing(1) with { Layer = -3 }]) with { Populations = [new Population(1, new float[32], new float[6], Unit()) { Layer = -3 }] };

        Assert.All(scene.DrawRows(), row => Assert.Equal(0, row["layer"]));
    }

    [Fact]
    public void ALayerInRangeIsKeptAndOnePastItReadsAsTheLast()
    {
        var scene = SceneBuilders.Of([Thing(1) with { Layer = 3 }]) with { Populations = [new Population(1, new float[32], new float[6], Unit()) { Layer = 9 }] };

        Assert.Equal([3, RenderScene.MaxLayer], scene.DrawRows().Select(row => row["layer"]));
    }

    [Fact]
    public void APopulationRowSaysHowItFadesAndWhetherItCastsShadows()
    {
        var scene = SceneBuilders.Of([]) with
        {
            Populations =
            [
                new Population(1, new float[32], new float[6], Unit()) { Fade = Fade.Shrink, CastShadows = true, Mesh = "/models/grass.glb" },
                new Population(2, new float[32], new float[6], Unit()) { Fade = Fade.None, ReceiveShadows = false },
            ],
        };

        var rows = scene.DrawRows();

        Assert.Equal(["shrink", "none"], rows.Select(row => row["fade"]));
        Assert.Equal([true, false], rows.Select(row => row["castShadows"]));
        Assert.Equal([true, false], rows.Select(row => row["receiveShadows"]));
        Assert.Equal("/models/grass.glb", rows[0]["mesh"]);
        Assert.Null(rows[1]["mesh"]);
        Assert.Null(rows[0]["material"]);
    }

    [Fact]
    public void APopulationOfNoMembersIsListedAndHidden()
    {
        var row = (SceneBuilders.Of([]) with { Populations = [Crowd(1, count: 0)] }).DrawRows().Single();

        Assert.Equal(0, row["instances"]);
        Assert.Equal(false, row["visible"]);
    }

    [Fact]
    public void ADefaultMaterialIsNamedInPlainWords()
    {
        var row = (SceneBuilders.Of([]) with { Materials = [new RenderMaterial { Key = 1 }] }).MaterialRow(1)!;

        Assert.Equal(["lit", "opaque", "back", "repeat", "smooth", "none"], new[] { "shading", "blend", "culling", "wrap", "filter", "blendMode" }.Select(name => row[name]));
        Assert.Equal(false, row["doubleSided"]);
        Assert.Equal(true, row["depthWrite"]);
        Assert.Equal(false, row["screenMapped"]);
        Assert.Equal(false, row["litScreen"]);
        Assert.Null(row["video"]);
        Assert.Empty(Part(row, "maps"));
        Assert.Empty((string[])row["usedBy"]!);
    }

    [Fact]
    public void AMaterialWithEveryChoiceMadeSpellsEachMultiWordNameInCamelCase()
    {
        var material = new RenderMaterial
        {
            Key = 1,
            Shading = MaterialShading.ShadowCatcher,
            Blend = MaterialBlend.Masked,
            Culling = MaterialCulling.None,
            DoubleSided = true,
            DepthWrite = false,
            Wrap = MaterialWrap.Mirror,
            Filter = MaterialFilter.Sharp,
            ScreenMapped = true,
            LitScreen = true,
            Video = 9,
            BlendMode = MaterialBlendMode.MaskedDepth,
        };

        var row = material.ToJson();

        Assert.Equal(
            ["shadowCatcher", "masked", "none", "mirror", "sharp", "maskedDepth"],
            new[] { "shading", "blend", "culling", "wrap", "filter", "blendMode" }.Select(name => row[name]));
        Assert.Equal([true, false, true, true], new[] { "doubleSided", "depthWrite", "screenMapped", "litScreen" }.Select(name => row[name]));
        Assert.Equal(9L, row["video"]);
        Assert.Equal(3.0, Part(row, "values")["blendMode"]);
    }

    [Fact]
    public void EveryMapIsListedUnderItsSlotName()
    {
        var material = new RenderMaterial
        {
            Key = 1,
            BaseColourMap = new Texture("a.png"),
            NormalMap = new Texture("b.png", Srgb: false),
            MetallicRoughnessMap = new Texture("c.png"),
            OcclusionMap = new Texture("d.png"),
            EmissiveMap = new Texture("e.png"),
            BlendBaseColourMap = new Texture("f.png"),
            BlendMaskMap = new Texture("g.png", Srgb: false),
        };

        var maps = Part(material.ToJson(), "maps");

        Assert.Equal(RenderMaterial.MapNames, maps.Keys);
        Assert.Equal(["a.png", "b.png", "c.png", "d.png", "e.png", "f.png", "g.png"], maps.Values.Select(map => ((OrderedDictionary<string, object?>)map!)["path"]));
        Assert.Equal([true, false, true, true, true, true, false], maps.Values.Select(map => ((OrderedDictionary<string, object?>)map!)["srgb"]));
    }

    [Fact]
    public void AMapLeftOutIsLeftOutOfTheRow()
    {
        var material = new RenderMaterial { Key = 1, OcclusionMap = new Texture("d.png") };

        Assert.Equal(["occlusion"], Part(material.ToJson(), "maps").Keys);
    }

    [Fact]
    public void ValuesAreTheThirtyTwoBitFloatsTheMessageHolds()
    {
        var values = Part(new RenderMaterial { Key = 1, Roughness = 0.3 }.ToJson(), "values");

        Assert.Equal(RenderMaterial.PackedNames, values.Keys);
        Assert.Equal((double)0.3f, values["roughness"]);
    }

    [Fact]
    public void AWindThatMovesIsSentAndOneThatDoesNotIsZero()
    {
        var moving = Part(new RenderMaterial { Key = 1, Wind = new Wind { Speed = 3, Strength = 0.5 } }.ToJson(), "values");
        var still = Part(new RenderMaterial { Key = 1, Wind = new Wind { Speed = 0, Strength = 0.5 } }.ToJson(), "values");

        Assert.Equal([3.0, 0.5], new[] { moving["windSpeed"], moving["windStrength"] });
        Assert.Equal([0.0, 0.0], new[] { still["windSpeed"], still["windStrength"] });
    }

    [Fact]
    public void AMaterialRowListsOnlyTheObjectsMadeOfIt()
    {
        var scene = SceneBuilders.Of([Thing(1, material: 7), Thing(2), Thing(3, material: 8)]) with
        {
            Materials = [new RenderMaterial { Key = 7 }],
        };

        Assert.Equal(["object:1"], (string[])scene.MaterialRow(7)!["usedBy"]!);
    }

    [Fact]
    public void AMaterialNoObjectNamesHasNoUsers()
    {
        var scene = SceneBuilders.Of([Thing(1)]) with { Materials = [new RenderMaterial { Key = 7 }] };

        Assert.Empty((string[])scene.MaterialRow(7)!["usedBy"]!);
    }

    [Fact]
    public void ARowForAnotherKeyThanTheLastListedIsStillFoundWhenOthersFollow()
    {
        var scene = SceneBuilders.Of([]) with
        {
            Materials = [new RenderMaterial { Key = 7, Metallic = 1 }, new RenderMaterial { Key = 8 }],
        };

        Assert.Equal(1.0, Part(scene.MaterialRow(7)!, "values")["metallic"]);
    }
}
