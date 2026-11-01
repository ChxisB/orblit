using Orblit.Filament;
using Orblit.Light;
using Orblit.VectorMath;

namespace Orblit.Examples.Tests;

/// <summary>
/// The Bistro, from a folder of tables. The fixture ran without the scene
/// fetched, so it never saw a fixture, a photographed sky or the still.
/// </summary>
public sealed class BistroTests
{
    private const string Lights = """
        [
          { "kind": "street", "at": [1.0, 2.0, 3.0] },
          { "kind": "orange", "at": [4, 5, 6] },
          { "kind": "candle", "at": [7.0, 8.0, 9.0] },
          { "kind": "sign", "at": [10.0, 11.0, 12.0] }
        ]
        """;

    private static readonly RenderCamera Camera = new ViewPoint().ToRenderCamera();

    [Fact]
    public void AtNightEachKnownFixtureIsALampAndTheMoonCastsTheShadows()
    {
        var example = Exterior(Folder.Fetched());
        example.Control<Toggle>("Night").Change(true);

        var lights = example.Scene(Camera, 0).Lights;

        Assert.Equal([100L, 101L, 102L, 1L], lights.Select(light => light.Key));
        var (street, orange, moon) = (lights[0], lights[1], lights[3]);
        Assert.Equal((1.0, 2.0, 3.0), (street.Position.X, street.Position.Y, street.Position.Z));
        Assert.Equal((5200.0, 28.0, false), (street.Intensity, street.FalloffRadius, street.CastShadows));
        Assert.Equal(Tint.Hex(0xFFB870).Linear, street.Colour);
        Assert.Equal((4.0, 5.0, 6.0), (orange.Position.X, orange.Position.Y, orange.Position.Z));
        Assert.Equal((70.0, 4.0, false), (orange.Intensity, orange.FalloffRadius, orange.CastShadows));
        Assert.Equal((RenderLightKind.Directional, 4.0, true), (moon.Kind, moon.Intensity, moon.CastShadows));
        Assert.Equal("4 point lights", example.Control<Toggle>("Night").Note);
    }

    [Fact]
    public void WithoutTheFestoonOnlyTheBrightFittingsLight()
    {
        var example = Exterior(Folder.Fetched());
        Assert.False(example.Control<Toggle>("Festoon").Enabled);
        example.Control<Toggle>("Night").Change(true);
        Assert.True(example.Control<Toggle>("Festoon").Enabled);

        example.Control<Toggle>("Festoon").Change(false);

        // The sign is the dimmest fitting that stays, and the brightest bulb
        // is a tenth of it.
        Assert.Equal([(100L, 5200.0), (101L, 900.0), (1L, 4.0)], example.Scene(Camera, 0).Lights.Select(light => (light.Key, light.Intensity)));
    }

    /// <summary>The Dart dropped a fitting under the threshold, so one at it stays.</summary>
    [Fact]
    public void AFittingAtTheThresholdStays()
    {
        Assert.Single(BistroFiles.Lamps([new BistroFixture("sign", Vector3.Zero())], 900));
    }

    [Fact]
    public void EachSwitchSaysWhatItShows()
    {
        var example = Exterior(Folder.Fetched());

        Assert.Equal(
            ("On foot, looking around", "One sun at 100,000 lux", "The small coloured bulbs"),
            (example.Control<Toggle>("Walk").Note, example.Control<Toggle>("Night").Note, example.Control<Toggle>("Festoon").Note));
    }

    [Fact]
    public void ByDayOneSunLightsTheStreetAndThePhotographReplacesTheSky()
    {
        var bare = Exterior(Folder.Fetched()).Scene(Camera, 0);
        var photographed = Exterior(Folder.Fetched("bistro_ibl.ktx", "bistro_skybox.ktx")).Scene(Camera, 0);

        var sun = Assert.Single(bare.Lights);
        Assert.Equal((1L, 100000.0, true), (sun.Key, sun.Intensity, sun.CastShadows));
        Assert.Same(RenderEnvironment.None, bare.Environment);
        Assert.Equal((22000.0, true), (bare.Sky.Ambient, bare.Sky.Drawn));
        Assert.Equal(Folder.Whole("bistro_ibl.ktx"), photographed.Environment.Radiance);
        Assert.Equal(Folder.Whole("bistro_skybox.ktx"), photographed.Environment.Skybox);
        Assert.Equal(30000, photographed.Environment.Intensity);
        Assert.Equal((0.0, false), (photographed.Sky.Ambient, photographed.Sky.Drawn));
    }

    /// <summary>The photograph is a bridge at noon, so the night never uses it.</summary>
    [Fact]
    public void TheNightLeavesThePhotographOut()
    {
        var example = Exterior(Folder.Fetched("bistro_ibl.ktx", "bistro_skybox.ktx"));
        example.Control<Toggle>("Night").Change(true);

        var scene = example.Scene(Camera, 0);

        Assert.Same(RenderEnvironment.None, scene.Environment);
        Assert.Equal((12.0, false), (scene.Sky.Ambient, scene.Sky.ShowBody));
    }

    /// <summary>The film speed only shows at night, and only the night camera reads it.</summary>
    [Fact]
    public void TheFilmSpeedIsANightDial()
    {
        var example = Exterior(Folder.Fetched());
        Assert.DoesNotContain(example.Settings(), setting => setting.Label == "Film speed");
        example.Control<Toggle>("Night").Change(true);

        var speed = example.Control<Setting>("Film speed");
        Assert.Equal((1600.0, 100.0, 6400.0, " ISO"), (speed.Value, speed.Min, speed.Max, speed.Unit));
        Assert.Equal("At ISO 100 this street is black. The lamps have not changed. The camera has.", example.Settings().OfType<Caption>().Single().Label);
        speed.Change(6400);

        Assert.Equal((2.0, 1.0 / 30, 6400.0), Exposure(example.Scene(Camera, 1).Camera));
        example.Control<Toggle>("Night").Change(false);
        Assert.Equal((16.0, 1.0 / 125, 100.0), Exposure(example.Scene(Camera, 1).Camera));
    }

    /// <summary>
    /// Still, the camera stands six metres off the Vespa at knee height, on a
    /// long lens, and focuses on it.
    /// </summary>
    [Fact]
    public void TheStillIsAPhotographOfTheVespa()
    {
        var example = Exterior(Folder.Fetched());
        example.Control<Toggle>("Walk").Change(false);

        var scene = example.Scene(Camera, 3);

        Assert.Equal("Still, by the Vespa", example.Control<Toggle>("Walk").Note);
        Assert.Equal((34.0, 2.8, 1.0 / 4000), (scene.Camera.FieldOfView, scene.Camera.Aperture, scene.Camera.ShutterSpeed));
        Assert.Equal(1.05, scene.Camera.Position.Y);
        Assert.Equal(-7.4 + (6 * Math.Cos(-168 * Math.PI / 180)), scene.Camera.Position.X, 12);
        Assert.True(scene.Post.DepthOfField.Enabled);
        Assert.Equal(Math.Sqrt(36 + (0.2 * 0.2)), scene.Post.DepthOfField.FocusDistance, 12);
        Assert.Equal(scene.Camera.Position.X, example.Scene(Camera, 9).Camera.Position.X);
    }

    /// <summary>
    /// The walk goes out and back at a walking pace and never jumps. It only
    /// stands to turn. The fixture covers its first five seconds, which never
    /// reach a turn. Out, turn, back and turn is under a hundred seconds.
    /// </summary>
    [Fact]
    public void TheWalkNeverJumpsAndComesBackToWhereItSetOff()
    {
        var example = Exterior(Folder.Fetched());
        var start = example.Scene(Camera, 0).Camera.Position;
        var before = start;
        var looked = example.Scene(Camera, 0).Camera.Target;
        var farthest = 0.0;
        var still = 0.0;
        var home = false;
        for (var tick = 1; tick <= 100 * 20; tick++)
        {
            var camera = example.Scene(Camera, tick / 20.0).Camera;
            var at = camera.Position;
            Assert.True((at - before).Length < 1.3 / 20 * 1.01, $"A step at {tick / 20.0} s.");

            // Eight metres out, a half-turn eased over four and a half seconds
            // moves the target under half a metre a tick. A jump is sixteen.
            Assert.True((camera.Target - looked).Length < 0.5, $"A glance at {tick / 20.0} s.");
            still = (at - before).Length < 1e-12 ? still + (1 / 20.0) : 0;
            Assert.True(still < 5, $"Standing at {tick / 20.0} s.");
            farthest = Math.Max(farthest, (at - start).Length);
            home |= farthest > 20 && (at - start).Length < 1e-9;
            before = at;
            looked = camera.Target;
        }

        Assert.InRange(farthest, 20, 60);
        Assert.True(home);
        Assert.Equal(1.7, start.Y, 12);
    }

    /// <summary>
    /// The walk through its first cycle and into the second, from a one-off
    /// run of the Dart example with the mesh path left out. Out to the far
    /// turn is about forty seconds and home again about eighty-five.
    /// </summary>
    [Theory]
    [InlineData(20, "68ba2a2379a3aba1")]
    [InlineData(40, "da01858a1da866f5")]
    [InlineData(60, "f0fccff55d2a9b92")]
    [InlineData(82.5, "e9deccea41e76573")]
    [InlineData(100, "f5257487acb649d1")]
    public void TheWalkMatchesDartThroughACycle(double seconds, string rest)
    {
        var message = SceneDigest.Message(new BistroExteriorExample(_ => null, _ => false, _ => ""), seconds);
        message.Remove("meshPaths");

        Assert.Equal(rest, SceneDigest.Of(message));
    }

    [Fact]
    public void TheModelSaysItIsMissingUntilItIsFetched()
    {
        var fetched = Exterior(Folder.Fetched());
        var missing = new BistroExteriorExample(Folder.Environment, _ => false, _ => "");

        Assert.Null(fetched.Note);
        Assert.StartsWith("The Bistro is not here yet.", missing.Note);
        Assert.Equal(Folder.Whole("BistroExterior.gltf"), Assert.Single(missing.Scene(Camera, 0).Objects).Mesh);
        Assert.Equal(["tool/fetch_bistro.sh", "exterior"], missing.Needs?.Command);
        Assert.StartsWith("The Bistro is not here yet.", new BistroInteriorExample(Folder.Environment, _ => false, _ => "").Note);
    }

    /// <summary>
    /// The fetch script puts the scene in the engine checkout beside this one.
    /// Each path handed to the renderer is whole.
    /// </summary>
    [Fact]
    public void WithoutTheVariableTheSceneIsInTheEngineCheckout()
    {
        var example = new BistroExteriorExample(_ => null, path => path.EndsWith(".ktx", StringComparison.Ordinal), _ => "");

        var scene = example.Scene(Camera, 0);

        Assert.Equal(Path.GetFullPath("../orblit/assets/bistro/BistroExterior.gltf"), Assert.Single(scene.Objects).Mesh);
        Assert.Equal(Path.GetFullPath("../orblit/assets/bistro/bistro_ibl.ktx"), scene.Environment.Radiance);
    }

    /// <summary>A developer's own variables never reach the fixture checks.</summary>
    [Fact]
    public void TheListReadsTheEnvironmentItIsGiven()
    {
        var example = EngineExamples.All(new ExampleFixture.NoPictures(), name => name == "ORBLIT_BISTRO" ? "/elsewhere" : null, ExampleFixture.NoRenderer).OfType<BistroExteriorExample>().Single();

        Assert.Equal(Path.GetFullPath(Path.Combine("/elsewhere", "BistroExterior.gltf")), Assert.Single(example.Scene(Camera, 0).Objects).Mesh);
    }

    /// <summary>It is a hundred entries and the scene is rebuilt every frame.</summary>
    [Fact]
    public void TheFixturesAreReadOnce()
    {
        var reads = 0;
        var folder = Folder.Fetched();
        var example = new BistroExteriorExample(Folder.Environment, folder.Contains, path =>
        {
            reads++;
            return Folder.Read(path);
        });
        example.Control<Toggle>("Night").Change(true);

        example.Scene(Camera, 0);
        example.Scene(Camera, 1);
        example.Settings();

        Assert.Equal(1, reads);
    }

    [Fact]
    public void ARoomWithoutFixturesHangsSixPendants()
    {
        var lights = Interior(Folder.Fetched()).Scene(Camera, 0).Lights;

        Assert.Equal([100L, 101L, 102L, 103L, 104L, 105L], lights.Select(light => light.Key));
        Assert.Equal([true, false, false, false, false, false], lights.Select(light => light.CastShadows));
        Assert.Equal((-4.0, 2.6, -1.2), (lights[0].Position.X, lights[0].Position.Y, lights[0].Position.Z));
        Assert.Equal((-4.0 + 1.8, 1.4), (lights[1].Position.X, lights[1].Position.Z));
        Assert.All(lights, light => Assert.Equal((900.0, 6.0), (light.Intensity, light.FalloffRadius)));
    }

    [Fact]
    public void ARoomWithFixturesIsLitByThemFestoonAndAll()
    {
        var lights = Interior(Folder.Fetched("BistroInterior.lights.json")).Scene(Camera, 0).Lights;

        Assert.Equal([5200.0, 70.0, 900.0], lights.Select(light => light.Intensity));
    }

    [Fact]
    public void TheWineIsAnotherModelAndTheAmbientADial()
    {
        var example = Interior(Folder.Fetched());
        Assert.Null(example.Note);

        var ambient = example.Control<Setting>("Ambient");
        Assert.Equal((400.0, 0.0, 2000.0, " lux"), (ambient.Value, ambient.Min, ambient.Max, ambient.Unit));
        Assert.Equal("The variant with the bottles and glasses", example.Control<Toggle>("Wine").Note);
        example.Control<Toggle>("Wine").Change(true);
        ambient.Change(0);

        var scene = example.Scene(Camera, 0);
        Assert.Equal(Folder.Whole("BistroInterior_Wine.gltf"), Assert.Single(scene.Objects).Mesh);
        Assert.StartsWith("The Bistro is not here yet.", example.Note);
        Assert.Equal(0, scene.Sky.Ambient);
        Assert.Same(Camera, scene.Camera);
        Assert.Equal(["tool/fetch_bistro.sh", "interior"], example.Needs?.Command);
    }

    /// <summary>The fixture moved no camera, so these are the Dart's by hand.</summary>
    [Fact]
    public void EachOpensWhereTheDartOpened()
    {
        Assert.Equal(new ViewPoint { Distance = 22, Pitch = 0.20, Height = 3, Yaw = 2.2 }, Exterior(Folder.Fetched()).ViewPoint);
        Assert.Equal(new ViewPoint { Distance = 9, Pitch = 0.06, Height = 1.7, Yaw = 0.4 }, Interior(Folder.Fetched()).ViewPoint);
    }

    private static (double Aperture, double Shutter, double Sensitivity) Exposure(RenderCamera camera) =>
        (camera.Aperture, camera.ShutterSpeed, camera.Sensitivity);

    private static BistroExteriorExample Exterior(HashSet<string> folder) =>
        new(Folder.Environment, folder.Contains, Folder.Read);

    private static BistroInteriorExample Interior(HashSet<string> folder) =>
        new(Folder.Environment, folder.Contains, Folder.Read);

    /// <summary>A fetched Bistro at <c>/bistro</c>, with whatever else is named.</summary>
    private static class Folder
    {
        public static string? Environment(string name) => name == "ORBLIT_BISTRO" ? "/bistro" : null;

        /// <summary>The path the example asks about, joined the way the example joins it.</summary>
        public static string Under(string name) => Path.Combine("/bistro", name);

        /// <summary>The path the example hands the renderer, which is whole.</summary>
        public static string Whole(string name) => Path.GetFullPath(Under(name));

        public static HashSet<string> Fetched(params string[] also) =>
        [
            Under("BistroExterior.gltf"),
            Under("BistroExterior.lights.json"),
            Under("BistroInterior.gltf"),
            .. also.Select(Under),
        ];

        public static string Read(string path) => path.EndsWith(".lights.json", StringComparison.Ordinal)
            ? Lights
            : throw new FileNotFoundException(path);
    }
}
