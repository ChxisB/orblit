using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

public sealed class AssetInfoTests
{
    // What the renderer said about the fox and a lamp, shortened, in the shape it writes it.
    private const string Fox = """
        {"clips":[{"name":"Survey","seconds":3.41666675},
        {"name":"Walk","seconds":0.708333313},{"name":"Run","seconds":1.1583333}],
        "skins":[{"name":"","joints":["_rootJoint","b_Root_00","b_Hip_01"]}],
        "variants":[],"materials":["fox_material"],"lights":[],"cameras":[],
        "bounds":{"min":[-8,0,-60],"max":[8,80,70]},"unsupported":[]}
        """;

    private const string Lamp = """
        {"clips":[],"skins":[],"variants":["on","off"],"materials":[],
        "lights":[{"name":"Bulb","kind":1,"colour":[1,0.9,0.8],"intensity":800,
        "falloff":0,"inner":0,"outer":0,
        "transform":[1,0,0,0,0,1,0,0,0,0,1,0,0,2,0,1]},
        {"name":"Shade","kind":2,"colour":[1,1,1],"intensity":300,
        "falloff":5,"inner":0.3,"outer":0.5,
        "transform":[1,0,0,0,0,0,-1,0,0,1,0,0,0,1.5,0,1]}],
        "cameras":[{"name":"Front","orthographic":false,"fieldOfView":39.6,
        "viewHeight":0,"near":0.1,"far":null,
        "transform":[1,0,0,0,0,1,0,0,0,0,1,0,0,1,4,1]}],
        "bounds":{"min":[-0.5,0,-0.5],"max":[0.5,2.2,0.5]},
        "unsupported":["KHR_materials_anisotropy"]}
        """;

    private static AssetInfo Read(string json) =>
        AssetInfo.Split(new Dictionary<string, string> { ["orblit.model:model"] = json }).Models.Single();

    [Fact]
    public void ModelDescriptionsInTheNotesAreTakenOutAndTheProblemsLeftBehind()
    {
        var split = AssetInfo.Split(
            new Dictionary<string, string>
            {
                ["orblit.model:/models/fox.glb"] = Fox,
                ["/models/crate.glb"] = "The file could not be read.",
                ["pose 3"] = "Clip 7 was asked for, and the file has 3.",
            });

        Assert.Equal(["/models/crate.glb", "pose 3"], split.Notes.Keys);
        Assert.Equal("/models/fox.glb", split.Models.Single().Path);
    }

    [Fact]
    public void ModelDescriptionsThatDoNotParseAreDroppedNotThrown()
    {
        var split = AssetInfo.Split(
            new Dictionary<string, string>
            {
                ["orblit.model:/a.glb"] = "{not json",
                ["orblit.model:/b.glb"] = "[1, 2, 3]",
                ["orblit.model:/c.glb"] = """{"clips": "sideways"}""",
            });

        Assert.Empty(split.Notes);
        Assert.Empty(split.Models);
    }

    [Fact]
    public void ADescriptionNamesItsClipsWithHowLongEachIs()
    {
        var fox = Read(Fox);

        Assert.Equal(["Survey", "Walk", "Run"], fox.Clips.Select(clip => clip.Name));
        Assert.Equal(0.7083, fox.Clips[1].Seconds, 1e-4);
        Assert.Equal(2, fox.ClipNamed("Run"));
        Assert.Null(fox.ClipNamed("Swim"));
    }

    [Fact]
    public void ADescriptionFindsAJointByNameInWhicheverSkinHasIt()
    {
        var fox = Read(Fox);

        Assert.Equal((0, 2), fox.JointNamed("b_Hip_01"));
        Assert.Null(fox.JointNamed("tail"));
    }

    [Fact]
    public void ADescriptionReadsItsBoundsVariantsCamerasAndWhatItCannotDraw()
    {
        var fox = Read(Fox);
        var lamp = Read(Lamp);

        Assert.Equal(80, fox.BoundsMax.Y);
        Assert.Equal(["on", "off"], lamp.Variants);
        Assert.Equal(["KHR_materials_anisotropy"], lamp.Unsupported);
        var camera = lamp.Cameras.Single();
        Assert.Equal("Front", camera.Name);
        Assert.Equal(39.6, camera.FieldOfView, 1e-9);
        // No far plane in the file is a camera that sees forever.
        Assert.Equal(double.PositiveInfinity, camera.Far);
        Assert.Equal(new Vector3(0, 1, 4), camera.Transform.GetTranslation());
    }

    [Fact]
    public void AFilesLightsStandWhereTheModelDoesInItsUnitsAndKinds()
    {
        var lamp = Read(Lamp);
        var lights = lamp.LightsFor(Matrix4.TranslationValues(10, 0, 0), i => 500 + i);

        Assert.Equal([500L, 501L], lights.Select(light => light.Key));
        Assert.Equal(RenderLightKind.Point, lights[0].Kind);
        Assert.Equal(800, lights[0].Intensity);
        Assert.Equal(new Vector3(10, 2, 0), lights[0].Position);
        Assert.Equal(RenderLightKind.Spot, lights[1].Kind);
        Assert.Equal(0.5, lights[1].OuterConeAngle, 1e-9);
        Assert.Equal(5, lights[1].FalloffRadius);
    }

    [Fact]
    public void AFilesLightsShineAlongTheirOwnMinusZTurnedAsTheModelIs()
    {
        var lamp = Read(Lamp);
        var lights = lamp.LightsFor(Matrix4.Identity(), i => i);

        // The shade's node turns minus z to minus y: it points at the floor.
        var down = lights[1].Direction;
        Assert.Equal(0, down.X, 1e-6);
        Assert.Equal(-1, down.Y, 1e-6);
        Assert.Equal(0, down.Z, 1e-6);

        var turned = lamp.LightsFor(Matrix4.RotationY(Math.PI / 2), i => i);
        // The bulb has no turn of its own, so it faces the model's minus z, which a quarter turn about y makes minus x.
        Assert.Equal(-1, turned[0].Direction.X, 1e-6);
    }

    [Fact]
    public void AFilesLightsWithNoRangeReachAsFarAsATenthOfALux()
    {
        var lamp = Read(Lamp);
        var lights = lamp.LightsFor(Matrix4.Identity(), i => i);

        Assert.Equal(FileLight.ReachOf(800), lights[0].FalloffRadius);
        Assert.Equal(Math.Sqrt(800 / (4 * Math.PI) / 0.1), FileLight.ReachOf(800), 1e-9);
    }
}
