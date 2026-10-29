using System.Runtime.InteropServices;
using System.Text;
using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples.Tests;

/// <summary>
/// The imported models example with its files there. The fixture ran without
/// them, so its scenes hold only the floor.
/// </summary>
public sealed class ImportedTests
{
    private const string Folder = "/samples";

    private static readonly RenderCamera Camera = new ViewPoint().ToRenderCamera();

    private static readonly string Fox = RenderResources.NameFor("samples/Fox.glb");

    private static readonly SkinInfo Hip = new()
    {
        Name = "Skin",
        Joints = ["Hip"],
        Parents = [-1],
        Rest = [Matrix4.Identity()],
        Local = [Matrix4.Identity()],
    };

    [Fact]
    public void TheEnvironmentPicksTheFolderAndTheModel()
    {
        var files = new Files(_ => [0]);
        var example = new ImportedExample(Given(("ORBLIT_SAMPLES", Folder), ("ORBLIT_MODEL", "Cesium Man")), files);

        example.Scene(Camera, 0);

        Assert.Equal("Cesium Man", example.Control<Choice>("Model").Selected);
        Assert.Equal([Path.Combine(Folder, "CesiumMan.glb")], files.Asked);
    }

    [Fact]
    public void AMissingFileSaysWhereToFetchIt()
    {
        var files = new Files(_ => null);
        var example = new ImportedExample(Given(("ORBLIT_MODEL", "Teapot")), files);
        example.Models[Fox] = Info() with { Lights = [Bulb()] };

        var scene = example.Scene(Camera, 0);

        Assert.Equal("Fox", example.Control<Choice>("Model").Selected);
        Assert.Equal([Path.Combine("../orblit/assets/samples", "Fox.glb")], files.Asked);
        Assert.Equal("No Fox.glb in ../orblit/assets/samples. Fetch it with tool/fetch_import_samples.sh.", example.Note);
        Assert.Equal([2L], scene.Objects.Select(one => one.Key));
        Assert.Equal([3L], scene.Lights.Select(light => light.Key));
    }

    /// <summary>A missing file is asked for again, so a fetch made while the gallery is open is picked up.</summary>
    [Fact]
    public void AFileIsAskedForUntilItIsThereAndThenNoMore()
    {
        byte[]? fox = null;
        var files = new Files(_ => fox);
        var example = new ImportedExample(_ => null, files);

        example.Scene(Camera, 0);
        fox = [0];
        var scene = example.Scene(Camera, 0.1);
        example.Scene(Camera, 0.2);

        Assert.Equal(2, files.Asked.Count);
        Assert.Equal([Fox], files.Provided);
        Assert.Equal(Fox, scene.Objects[0].Mesh);
    }

    [Fact]
    public void TheCompanionsAreHandedOverBeforeTheFile()
    {
        var files = new Files(_ => [0]);
        var example = new ImportedExample(Given(("ORBLIT_SAMPLES", Folder), ("ORBLIT_MODEL", "Man")), files);

        example.Scene(Camera, 0);

        string[] order =
        [
            "male02/male02.mtl",
            "male02/01_-_Default1noCulling.JPG",
            "male02/male-02-1noCulling.JPG",
            "male02/orig_02_-_Defaul1noCulling.JPG",
            "male02/male02.obj",
        ];
        Assert.Equal(order.Select(file => Path.Combine(Folder, file)), files.Asked);
        Assert.Equal(order.Select(file => RenderResources.NameFor($"samples/{file}")), files.Provided);
    }

    [Fact]
    public void AFileThatCannotBeReadSaysWhy()
    {
        var example = new ImportedExample(_ => null, new Files(_ => throw new IOException("locked")));

        example.Scene(Camera, 0);

        Assert.Equal("Fox.glb could not be read: locked", example.Note);
    }

    /// <summary>A file still being read is not asked for again every frame.</summary>
    [Fact]
    public void AFileBeingReadIsNotAskedForAgain()
    {
        var files = new Unanswered();
        var example = new ImportedExample(_ => null, files);

        example.Scene(Camera, 0);
        example.Scene(Camera, 0.1);

        Assert.Equal(1, files.Asked);
    }

    [Fact]
    public void TheModelIsDrawnInTheExamplesOrange() =>
        Assert.Equal(Tint.Hex(0xD9634F).Linear, Model(Ready(Info()), 0).Colour);

    /// <summary>Stood on the floor, centred and 1.6 high, whatever size the file made it.</summary>
    [Fact]
    public void TheModelStandsOnTheFloorAsTallAsAPerson()
    {
        var example = Ready(Info() with { BoundsMin = new Vector3(-1, 2, -3), BoundsMax = new Vector3(3, 6, 1) });

        var placement = Model(example, 0).Transform;

        Near(new Vector3(0, 0, 0), placement.Transformed3(new Vector3(1, 2, -1)));
        Near(new Vector3(0, 1.6, 0), placement.Transformed3(new Vector3(1, 6, -1)));
        Near(new Vector3(0.8, 0, -0.8), placement.Transformed3(new Vector3(3, 2, -3)));
    }

    /// <summary>A model longer than it is tall is scaled by its length.</summary>
    [Fact]
    public void ALongModelIsScaledByItsLength()
    {
        var example = Ready(Info() with { BoundsMin = new Vector3(0, 0, 0), BoundsMax = new Vector3(1, 1, 4) });

        Near(new Vector3(0, 0, 0.8), Model(example, 0).Transform.Transformed3(new Vector3(0.5, 0, 4)));
    }

    /// <summary>Bounds with no size, or none yet, leave the file's own placement.</summary>
    [Fact]
    public void AModelWithNoSizeIsLeftWhereItIs()
    {
        var flat = Ready(Info() with { BoundsMin = new Vector3(1, 1, 1), BoundsMax = new Vector3(1, 1, 1) });
        var unknown = new ImportedExample(_ => null, new Files(_ => [0]));

        Near(new Vector3(1, 2, 3), Model(flat, 0).Transform.Transformed3(new Vector3(1, 2, 3)));
        Near(new Vector3(1, 2, 3), Model(unknown, 0).Transform.Transformed3(new Vector3(1, 2, 3)));
    }

    [Fact]
    public void TheClipsAreListedByTheFilesOwnNames()
    {
        var example = Ready(Info() with { Clips = [new("Idle", 1), new("", 1)] });
        var none = Ready(Info());

        Assert.Equal(["Idle", "Clip 2"], example.Control<Choice>("Clip").Options);
        Assert.Equal("Idle", example.Control<Choice>("Clip").Selected);
        Assert.Equal(["None"], none.Control<Choice>("Clip").Options);
        Assert.Null(none.Control<Choice>("Clip").Select);
        Assert.Null(none.Control<Choice>("Played by").Select);
        Assert.Null(Model(none, 0).Animation);
    }

    /// <summary>
    /// A new clip fades in from the one playing, at the same speed, over 0.4
    /// seconds, then plays on its own.
    /// </summary>
    [Fact]
    public void AChangeOfClipFadesFromTheOnePlaying()
    {
        var example = Ready(Info() with { Clips = [new("Survey", 3), new("Walk", 1), new("Run", 1)] });
        example.Control<Setting>("Speed").Change(1.5);
        example.Scene(Camera, 1.0);

        Pick(example, "Clip", "Survey");
        Assert.Null(Model(example, 1.0).Animation?.From);
        Pick(example, "Clip", "Walk");
        var first = Model(example, 1.0).Animation?.From;
        Pick(example, "Clip", "Run");
        var halfway = Model(example, 1.2).Animation;
        var done = Model(example, 1.5).Animation;

        Assert.Equal(0, first?.Clip);
        Assert.Equal(1.5, first?.Speed);
        Assert.NotNull(halfway);
        Assert.Equal(2, halfway.Clip);
        Assert.Equal(1, halfway.From?.Clip);
        Assert.Equal(0.5, halfway.Fade, 9);
        Assert.Equal("Run", example.Control<Choice>("Clip").Selected);
        Assert.NotNull(done);
        Assert.Null(done.From);
        Assert.Equal(1, done.Fade);
        // The clock starts again when the example is shown again. A fade that ended stays ended.
        Assert.Null(Model(example, 0.1).Animation?.From);
    }

    [Fact]
    public void PlayingAndSpeedReachTheRenderer()
    {
        var example = Ready(Info() with { Clips = [new("Walk", 1)] });

        var speed = example.Control<Setting>("Speed");
        speed.Change(1.5);
        var playing = Model(example, 2).Animation;
        example.Control<Toggle>("Playing").Change(false);
        var paused = Model(example, 2).Animation;

        Assert.Equal((0.1, 2.0, 1), (speed.Min, speed.Max, speed.Decimals));
        Assert.Equal(3.0, playing?.Seconds);
        Assert.Equal(1.5, playing?.Speed);
        Assert.Equal(0.0, paused?.Seconds);
        Assert.Equal(0.0, paused?.Speed);
    }

    [Fact]
    public void ALookIsPickedByTheFilesOwnName()
    {
        var example = Ready(Info() with { Variants = ["midnight", "beach", "street"] });
        var plain = Ready(Info());

        Assert.Equal(["As made", "midnight", "beach", "street"], example.Control<Choice>("Look").Options);
        Pick(example, "Look", "beach");
        Assert.Equal(1, Model(example, 0).Variant);
        Assert.Equal("beach", example.Control<Choice>("Look").Selected);
        Pick(example, "Look", "As made");
        Assert.Null(Model(example, 0).Variant);
        Assert.Equal("As made", example.Control<Choice>("Look").Selected);
        Assert.Null(plain.Control<Choice>("Look").Select);
    }

    [Fact]
    public void AnotherModelStartsFromItsFirstClipAndOwnLook()
    {
        var example = Ready(Info() with { Clips = [new("Walk", 1), new("Run", 1)], Variants = ["beach"] });
        Pick(example, "Clip", "Run");
        Pick(example, "Look", "beach");

        example.Note = "stale";
        Pick(example, "Model", "Fox");
        Assert.Null(example.Note);
        Assert.Equal(1, Model(example, 0).Animation?.Clip);

        Pick(example, "Model", "Shoe");
        Pick(example, "Model", "Fox");
        var model = Model(example, 0);

        Assert.Equal("Fox", example.Control<Choice>("Model").Selected);
        Assert.Equal("Three clips on one skin", example.Settings().OfType<Caption>().First().Label);
        Assert.Equal(0, model.Animation?.Clip);
        Assert.Null(model.Animation?.From);
        Assert.Null(model.Variant);
    }

    /// <summary>
    /// The renderer may report a file again with fewer clips and looks. The
    /// last clip then plays, and the look reads as made.
    /// </summary>
    [Fact]
    public void AShorterReportKeepsTheChoicesInRange()
    {
        var example = Ready(Info() with { Clips = [new("Survey", 1), new("Walk", 1), new("Run", 1)], Variants = ["midnight", "beach"] });
        Pick(example, "Clip", "Run");
        Pick(example, "Look", "beach");

        example.Models[Fox] = Info() with { Clips = [new("Survey", 1), new("Walk", 1)], Variants = ["midnight"] };

        Assert.Equal("Walk", example.Control<Choice>("Clip").Selected);
        Assert.Equal("As made", example.Control<Choice>("Look").Selected);
        Assert.Equal(1, Model(example, 0).Animation?.Clip);
    }

    [Fact]
    public void TheFilesLightsAreSceneLightsKeyedFromAHundred()
    {
        var example = Ready(Info() with { Lights = [Bulb(), Bulb()] });

        Assert.Equal([3L, 100L, 101L], example.Scene(Camera, 0).Lights.Select(light => light.Key));
        example.Control<Toggle>("The file's lights").Change(false);
        Assert.Equal([3L], example.Scene(Camera, 0).Lights.Select(light => light.Key));
    }

    /// <summary>At night the sun is down and the camera opens up for a twenty-lumen bulb.</summary>
    [Fact]
    public void NightIsDarkAndTheCameraOpensUp()
    {
        var example = Ready(Info() with { Lights = [Bulb()] });
        example.Control<Toggle>("Daylight").Change(false);

        var scene = example.Scene(Camera, 0);

        Assert.Equal([100L], scene.Lights.Select(light => light.Key));
        Assert.Equal(2.0, scene.Sky.Ambient);
        Assert.Equal((Camera.Position, Camera.Target, Camera.FieldOfView), (scene.Camera.Position, scene.Camera.Target, scene.Camera.FieldOfView));
        Assert.Equal((1.4, 1.0 / 30, 3200.0), (scene.Camera.Aperture, scene.Camera.ShutterSpeed, scene.Camera.Sensitivity));
    }

    [Fact]
    public void WhatTheRendererCannotDrawIsSaid()
    {
        var example = Ready(Info() with { Unsupported = ["KHR_materials_anisotropy", "KHR_texture_basisu"] });

        Assert.Equal("Not drawn: KHR_materials_anisotropy, KHR_texture_basisu", Captions(example).Last());
        var one = Ready(Info() with { Unsupported = ["KHR_texture_basisu"] });
        Assert.Equal("Not drawn: KHR_texture_basisu", Captions(one).Last());
    }

    /// <summary>Bytes Orblit cannot read as glTF say so, and the renderer goes on playing the clip.</summary>
    [Fact]
    public void ClipsOrblitCannotReadAreLeftToTheRenderer()
    {
        var example = Ready(Info() with { Clips = [new("Walk", 1)], Skins = [Hip] }, "not glTF"u8.ToArray());
        Pick(example, "Played by", "Orblit");

        var model = Model(example, 0);

        Assert.StartsWith("Orblit could not read the clips: ", example.Note);
        Assert.NotNull(model.Animation);
        Assert.Null(model.Joints);
    }

    /// <summary>A file with clips but no skin has no joints for Orblit to set, so the renderer plays it.</summary>
    [Fact]
    public void AModelWithNoSkinIsLeftToTheRenderer()
    {
        var example = Ready(Info() with { Clips = [new("Rise", 1)] }, Rising());
        Pick(example, "Played by", "Orblit");

        var model = Model(example, 0);

        Assert.NotNull(model.Animation);
        Assert.Null(model.Joints);
    }

    [Fact]
    public void OrblitPlaysAGltfClipJointByJoint()
    {
        var example = Ready(Info() with { Clips = [new("Rise", 1)], Skins = [Hip] }, Rising());
        Assert.Null(Model(example, 0).Joints);
        Pick(example, "Played by", "Orblit");

        example.Scene(Camera, 2);
        var rising = Model(example, 2.5);
        example.Control<Toggle>("Playing").Change(false);
        var paused = Model(example, 3.2);

        Assert.Null(rising.Animation);
        Assert.NotNull(rising.Joints);
        var joint = Assert.Single(rising.Joints);
        Assert.Equal((0, 0), (joint.Skin, joint.Joint));
        Near(new Vector3(0, 0.5, 0), joint.Transform.GetTranslation());
        Assert.NotNull(paused.Joints);
        Near(new Vector3(0, 0.5, 0), Assert.Single(paused.Joints).Transform.GetTranslation());
        Assert.Contains(
            "Imported as clips, sampled by Orblit and set on the skin joint by joint. A change of clip cuts rather than fades. "
            + "Left out: Morph target weights in \"Rise\" were not carried across; clips do not move them yet.",
            Captions(example));
    }

    [Fact]
    public void OrblitPlaysAtTheChosenSpeed()
    {
        var example = Ready(Info() with { Clips = [new("Rise", 1)], Skins = [Hip] }, Rising());
        Pick(example, "Played by", "Orblit");
        example.Control<Setting>("Speed").Change(0.5);

        example.Scene(Camera, 2);
        var joints = Model(example, 2.5).Joints;

        Assert.NotNull(joints);
        Near(new Vector3(0, 0.25, 0), Assert.Single(joints).Transform.GetTranslation());
    }

    /// <summary>Orblit cuts to another clip and starts it from its beginning.</summary>
    [Fact]
    public void OrblitCutsToAnotherClip()
    {
        var example = Ready(Info() with { Clips = [new("Rise", 1), new("Grow", 1)], Skins = [Hip] }, Rising());
        Pick(example, "Played by", "Orblit");
        example.Scene(Camera, 2);
        example.Scene(Camera, 2.5);

        Pick(example, "Clip", "Grow");
        var joints = Model(example, 2.75).Joints;

        Assert.NotNull(joints);
        Near(new Vector3(0, 0, 0), Assert.Single(joints).Transform.GetTranslation()); // still rising, it is 0.75
    }

    /// <summary>A model chosen again starts Orblit's clip from its beginning.</summary>
    [Fact]
    public void AModelChosenAgainStartsOrblitsClipFromTheBeginning()
    {
        var example = Ready(Info() with { Clips = [new("Rise", 1)], Skins = [Hip] }, Rising());
        Pick(example, "Played by", "Orblit");
        example.Scene(Camera, 2);
        example.Scene(Camera, 2.5);

        Pick(example, "Model", "Shoe");
        Pick(example, "Model", "Fox");
        var joints = Model(example, 3.2).Joints;

        Assert.NotNull(joints);
        Near(new Vector3(0, 0, 0), Assert.Single(joints).Transform.GetTranslation()); // played on, it is 0.2
    }

    /// <summary>Each model's joints come from its own skins.</summary>
    [Fact]
    public void AnotherModelIsBoundToItsOwnSkins()
    {
        var man = RenderResources.NameFor("samples/CesiumMan.glb");
        var example = Ready(Info() with { Clips = [new("Rise", 1)], Skins = [Hip] }, Rising());
        example.Models[man] = Info() with { Path = man, Clips = [new("Rise", 1)], Skins = [Hip, Hip] };
        Pick(example, "Played by", "Orblit");
        example.Scene(Camera, 0);

        Pick(example, "Model", "Cesium Man");
        var joints = Model(example, 0.5).Joints;

        Assert.NotNull(joints);
        Assert.Equal([0, 1], joints.Select(joint => joint.Skin));
    }

    /// <summary>Only glTF clips are imported, so a converted FBX is always played by the renderer.</summary>
    [Fact]
    public void OnlyGltfCanBePlayedByOrblit()
    {
        var dancer = RenderResources.NameFor("samples/Samba Dancing.fbx");
        var example = Ready(Info() with { Clips = [new("Walk", 1)] });
        example.Models[dancer] = Info() with { Path = dancer, Clips = [new("Samba", 1)] };
        Pick(example, "Played by", "Orblit");

        Pick(example, "Model", "Dancer");

        Assert.Null(example.Control<Choice>("Played by").Select);
        Assert.NotNull(Model(example, 0).Animation);
        Assert.DoesNotContain(Captions(example), caption => caption.StartsWith("Imported as clips", StringComparison.Ordinal));
    }

    /// <summary>The fox, handed over as <paramref name="bytes"/> and reported on by the renderer as <paramref name="info"/>.</summary>
    private static ImportedExample Ready(AssetInfo info, byte[]? bytes = null)
    {
        var example = new ImportedExample(_ => null, new Files(_ => bytes ?? [0]));
        example.Models[Fox] = info;
        return example;
    }

    private static AssetInfo Info() => new() { Path = Fox, BoundsMin = new Vector3(-1, 0, -1), BoundsMax = new Vector3(1, 2, 1) };

    private static FileLight Bulb() =>
        new("Bulb", RenderLightKind.Point, new Vector3(1, 0.9, 0.8), 20, 0, 0, 0, Matrix4.Identity());

    private static RenderObject Model(ImportedExample example, double seconds) =>
        example.Scene(Camera, seconds).Objects.Single(one => one.Key == 1);

    private static List<string> Captions(Example example) => [.. example.Settings().OfType<Caption>().Select(one => one.Label)];

    private static void Pick(Example example, string label, string option)
    {
        var select = example.Control<Choice>(label).Select;
        Assert.NotNull(select);
        select(option);
    }

    private static Func<string, string?> Given(params (string Name, string Value)[] set) =>
        name => set.FirstOrDefault(one => one.Name == name).Value;

    private static void Near(Vector3 expected, Vector3 actual)
    {
        Assert.Equal(expected.X, actual.X, 9);
        Assert.Equal(expected.Y, actual.Y, 9);
        Assert.Equal(expected.Z, actual.Z, 9);
    }

    /// <summary>
    /// A glTF file whose one joint, Hip, rises a metre over a second, with a
    /// morph channel Orblit leaves out, and a second clip that only grows it.
    /// Its data is carried in the file.
    /// </summary>
    private static byte[] Rising()
    {
        float[] numbers = [0, 1, 0, 0, 0, 0, 1, 0, 1, 1, 1, 1, 2, 1];
        var data = Convert.ToBase64String(MemoryMarshal.AsBytes<float>(numbers).ToArray());
        var json = $$"""
            {
              "asset": { "version": "2.0" },
              "nodes": [{ "name": "Hip" }],
              "skins": [{ "joints": [0] }],
              "animations": [{
                "name": "Rise",
                "samplers": [{ "input": 0, "output": 1 }],
                "channels": [
                  { "sampler": 0, "target": { "node": 0, "path": "translation" } },
                  { "sampler": 0, "target": { "node": 0, "path": "weights" } }
                ]
              }, {
                "name": "Grow",
                "samplers": [{ "input": 0, "output": 2 }],
                "channels": [{ "sampler": 0, "target": { "node": 0, "path": "scale" } }]
              }],
              "accessors": [
                { "bufferView": 0, "componentType": 5126, "type": "SCALAR", "count": 2 },
                { "bufferView": 1, "componentType": 5126, "type": "VEC3", "count": 2 },
                { "bufferView": 2, "componentType": 5126, "type": "VEC3", "count": 2 }
              ],
              "bufferViews": [
                { "buffer": 0, "byteOffset": 0, "byteLength": 8 },
                { "buffer": 0, "byteOffset": 8, "byteLength": 24 },
                { "buffer": 0, "byteOffset": 32, "byteLength": 24 }
              ],
              "buffers": [{ "byteLength": 56, "uri": "data:application/octet-stream;base64,{{data}}" }]
            }
            """;
        return Encoding.UTF8.GetBytes(json);
    }

    /// <summary>Files from <paramref name="read"/>, already read, with every ask and hand-over written down in order.</summary>
    private sealed class Files(Func<string, byte[]?> read) : ISampleFiles
    {
        public List<string> Asked { get; } = [];

        public List<string> Provided { get; } = [];

        public Task<byte[]?> ReadAsync(string path)
        {
            Asked.Add(path);
            return Task.FromResult(read(path));
        }

        public void Provide(string name, byte[] bytes) => Provided.Add(name);
    }

    /// <summary>Files whose reads never finish, with the asks counted.</summary>
    private sealed class Unanswered : ISampleFiles
    {
        public int Asked { get; private set; }

        public Task<byte[]?> ReadAsync(string path)
        {
            Asked++;
            return new TaskCompletionSource<byte[]?>().Task;
        }

        public void Provide(string name, byte[] bytes)
        {
        }
    }
}
