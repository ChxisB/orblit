using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>
/// Ported from diagnostics_test.dart. The rows that say what a scene submitted: one for each draw,
/// one for each material, and one for each pass of a frame.
/// </summary>
public sealed class DiagnosticsTests
{
    private static readonly RenderMaterial Brass = new()
    {
        Key = 7,
        Metallic = 1,
        Roughness = 0.25,
        BaseColour = new Vector4(0.5, 0.25, 0.125, 1),
        BaseColourMap = new Texture("/tex/brass.png"),
    };

    private static RenderObject Thing(long key, long? material = null, string? mesh = null, bool visible = true, Matrix4? at = null) =>
        new()
        {
            Key = key,
            Transform = at ?? Matrix4.Identity(),
            Colour = new Vector3(1, 0, 0),
            Material = material,
            Mesh = mesh,
            Visible = visible,
        };

    private static RenderScene Scene() =>
        new()
        {
            Objects =
            [
                Thing(1, material: 7, at: Matrix4.TranslationValues(1, 2, 3)),
                Thing(2, material: 7, mesh: "/models/rock.glb"),
                Thing(3, material: 99),
                Thing(4, visible: false),
            ],
            Populations =
            [
                new Population(1, new float[3 * 16], new float[3 * 3], new Bounds(new Vector3(-10, 0, -10), new Vector3(10, 4, 10)))
                {
                    Range = 50,
                    Revision = 5,
                },
            ],
            Materials = [Brass],
            Camera = SceneBuilders.CameraAt(0, 2, 8),
        };

    [Fact]
    public void HasARowForEachObjectThenEachPopulation() =>
        Assert.Equal(
            ["object:1", "object:2", "object:3", "object:4", "population:1"],
            Scene().DrawRows().Select(row => row["id"]));

    [Fact]
    public void AnObjectRowSaysWhatItIsMadeOfAndWhereItIs()
    {
        var rows = Scene().DrawRows();

        Assert.Equal("object", rows[0]["kind"]);
        Assert.Null(rows[0]["mesh"]);
        Assert.Equal(7L, rows[0]["material"]);
        Assert.Equal(true, rows[0]["materialListed"]);
        Assert.Equal(1, rows[0]["instances"]);
        Assert.Equal([1.0, 2.0, 3.0], ((double[])rows[0]["transform"]!)[12..15]);
        Assert.Equal("/models/rock.glb", rows[1]["mesh"]);
    }

    [Fact]
    public void SaysWhenAnObjectNamesAMaterialTheSceneDoesNotList()
    {
        var rows = Scene().DrawRows();

        Assert.Equal(99L, rows[2]["material"]);
        Assert.Equal(false, rows[2]["materialListed"]);
    }

    [Fact]
    public void ARowWithNoMaterialHasNoAnswerToGiveAboutOne()
    {
        var rows = Scene().DrawRows();

        Assert.Null(rows[3]["material"]);
        Assert.Null(rows[3]["materialListed"]);
        Assert.Null(rows[^1]["materialListed"]);
    }

    [Fact]
    public void ReportsTheLayerTheRendererIsSent()
    {
        var scene = SceneBuilders.Of([Thing(1) with { Layer = 9 }]);

        Assert.Equal(RenderScene.MaxLayer, scene.DrawRows().Single()["layer"]);
    }

    [Fact]
    public void AHiddenObjectIsListedAndSaysItIsHidden()
    {
        Assert.Equal(false, Scene().DrawRows()[3]["visible"]);
        Assert.Equal(true, Scene().DrawRows()[0]["visible"]);
    }

    [Fact]
    public void APopulationRowCountsItsMembersAndKeepsItsRange()
    {
        var row = Scene().DrawRows()[^1];

        Assert.Equal("population", row["kind"]);
        Assert.Equal(3, row["instances"]);
        Assert.Equal(50.0, row["range"]);
        Assert.Equal("sink", row["fade"]);
        Assert.Equal(5, row["revision"]);
        Assert.Equal([-10.0, 0.0, -10.0], (double[])row["minimum"]!);
        Assert.Equal([10.0, 4.0, 10.0], (double[])row["maximum"]!);
    }

    [Fact]
    public void ListsTheValuesTheRendererIsSentAndTheDrawsOnIt()
    {
        var row = Scene().MaterialRow(7)!;

        Assert.Equal(7L, row["key"]);
        Assert.Equal(["object:1", "object:2"], (string[])row["usedBy"]!);
        var values = (OrderedDictionary<string, object?>)row["values"]!;
        Assert.Equal(1.0, values["metallic"]);
        Assert.Equal(0.25, values["roughness"]);
        Assert.Equal(0.25, values["baseColour.g"]);
        var maps = (OrderedDictionary<string, object?>)row["maps"]!;
        var map = (OrderedDictionary<string, object?>)Assert.Single(maps).Value!;
        Assert.Equal("baseColour", Assert.Single(maps).Key);
        Assert.Equal("/tex/brass.png", map["path"]);
        Assert.Equal(true, map["srgb"]);
        Assert.Equal("lit", row["shading"]);
        Assert.Equal("none", row["blendMode"]);
    }

    [Fact]
    public void AnswersTheLastMaterialWhenAKeyIsListedTwice()
    {
        var twice = SceneBuilders.Of([]) with
        {
            Materials = [new RenderMaterial { Key = 7 }, new RenderMaterial { Key = 7, Metallic = 1 }],
        };

        var values = (OrderedDictionary<string, object?>)twice.MaterialRow(7)!["values"]!;

        Assert.Equal(1.0, values["metallic"]);
    }

    [Fact]
    public void IsNullForAKeyTheSceneDoesNotHold() => Assert.Null(Scene().MaterialRow(99));

    [Fact]
    public void NamesEveryFloatOfThePackedMaterial()
    {
        // A float added to Pack without a name would go unread here.
        Assert.Equal(RenderMaterial.Stride, RenderMaterial.PackedNames.Count);
        Assert.Equal(RenderMaterial.MapCount, RenderMaterial.MapNames.Count);
    }

    [Fact]
    public void EachNameIsTheFieldPackWritesAtItsPlace()
    {
        // Every float gets its own value, so a name out of step with Pack shows.
        var distinct = RenderMaterial.PackedNames
            .Select((name, at) => (name, value: (at + 1) / 64.0))
            .ToDictionary(pair => pair.name, pair => pair.value);
        double V(string name) => distinct[name];
        var material = new RenderMaterial
        {
            Key = 1,
            BaseColour = new Vector4(V("baseColour.r"), V("baseColour.g"), V("baseColour.b"), V("baseColour.a")),
            Metallic = V("metallic"),
            Roughness = V("roughness"),
            Reflectance = V("reflectance"),
            Emissive = new Vector3(V("emissive.r"), V("emissive.g"), V("emissive.b")),
            EmissiveIntensity = V("emissiveIntensity"),
            AmbientOcclusion = V("ambientOcclusion"),
            NormalScale = V("normalScale"),
            Tiling = new Vector2(V("tiling.x"), V("tiling.y")),
            Offset = new Vector2(V("offset.x"), V("offset.y")),
            MaskThreshold = V("maskThreshold"),
            DepthBias = V("depthBias"),
            BlendAmount = V("blendAmount"),
            BlendSharpness = V("blendSharpness"),
            BlendTiling = new Vector2(V("blendTiling.x"), V("blendTiling.y")),
            BlendOffset = new Vector2(V("blendOffset.x"), V("blendOffset.y")),
            ClearCoat = V("clearCoat"),
            ClearCoatRoughness = V("clearCoatRoughness"),
            Anisotropy = V("anisotropy"),
            SheenColour = new Vector3(V("sheenColour.r"), V("sheenColour.g"), V("sheenColour.b")),
            SheenRoughness = V("sheenRoughness"),
        };

        var values = (OrderedDictionary<string, object?>)material.ToJson()["values"]!;

        // The blend mode and the wind are not plain fields, so they have their own checks.
        foreach (var (name, expected) in distinct.Where(pair => pair.Key != "blendMode" && !pair.Key.StartsWith("wind", StringComparison.Ordinal)))
        {
            Assert.True(expected == (double)values[name]!, name);
        }
    }

    [Fact]
    public void APassTimingIsARowOfItsNameTimeAndDraws()
    {
        var row = new NamedPassTiming("colour", 1.5, 12).ToJson();

        Assert.Equal(["name", "milliseconds", "draws"], row.Keys);
        Assert.Equal("colour", row["name"]);
        Assert.Equal(1.5, row["milliseconds"]);
        Assert.Equal(12, row["draws"]);
    }
}
