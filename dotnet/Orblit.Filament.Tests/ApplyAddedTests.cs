using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>
/// Applies a scene to a live headless renderer. What these check is that the renderer accepts
/// every array, including a texture, a video, a shader and an environment whose files are not
/// there. They do not read the picture back.
/// </summary>
public sealed class ApplyAddedTests
{
    private static RenderScene Everything() =>
        SceneBuilders.Row() with
        {
            Lights =
            [
                new RenderLight { Key = 1, Kind = RenderLightKind.Directional, Intensity = 100000 },
                new RenderLight { Key = 2, Kind = RenderLightKind.Point, Intensity = 800, Position = new Vector3(1, 2, 3) },
            ],
            Sky = new Sky { Clouds = Clouds.Cumulus },
            Fog = new Fog(),
            Precipitation = new Precipitation { Amount = 0.5 },
            Materials =
            [
                new RenderMaterial { Key = 1, Roughness = 0.3, BaseColourMap = new Texture("/nowhere/base.png") },
                new RenderMaterial
                {
                    Key = 2,
                    Surface = new CustomSurface("/nowhere/pulse.filamat", new Dictionary<string, IReadOnlyList<double>> { ["speed"] = [2.5] }),
                },
                new RenderMaterial { Key = 3, Shading = MaterialShading.Video, Video = 9 },
            ],
            Videos = [new Video { Key = 9, Path = "/nowhere/clip.mp4" }],
            Decals = [new Decal { Key = 4, Position = Vector3.Zero(), Size = Vector3.All(1), Texture = new Texture("/nowhere/mark.png") }],
            Outline = new Outline { Keys = [100, 101], Primary = 100 },
            GodRays = new GodRays { Strength = 0.5 },
            Distortions = [Distortion.Shockwave(Vector3.Zero(), 2), Distortion.Lens(0.01)],
            Post = new PostProcess { Bloom = new Bloom { Enabled = true }, Vignette = new Vignette { Enabled = true } },
            Pipeline = Pipeline.At(Detail.High),
            Environment = RenderEnvironment.FromImage("/nowhere/sky.hdr"),
            Probes = [new Probe { Key = 5, Position = new Vector3(0, 1, 0), Resolution = 32 }],
            Field = new IrradianceField { Enabled = true, From = "lit" },
        };

    [RendererFact]
    public void ASceneIsAcceptedAndDrawnAgainWithFewerObjects()
    {
        using var renderer = new Renderer(64, 48);
        var scene = Everything();

        renderer.Apply(scene);
        renderer.Draw(0);
        renderer.Apply(scene with { Objects = [.. scene.Objects.Take(2)], Lights = [] }, at: 1.0 / 60);
        renderer.Draw(1.0 / 60);

        Assert.True(renderer.RenderedFrames >= 2);
    }

    [RendererFact]
    public void AnAnimatedObjectIsPosedAndThenReleased()
    {
        using var renderer = new Renderer(64, 48);
        var animated = SceneBuilders.Plain(7) with { Animation = new ModelAnimation { Clip = 1, Seconds = 0.5 } };

        renderer.Apply(SceneBuilders.Of([animated]), at: 0.5);
        renderer.Draw(0.5);
        renderer.Apply(SceneBuilders.Of([SceneBuilders.Plain(7)]), at: 1);
        renderer.Draw(1);

        Assert.True(renderer.RenderedFrames >= 2);
    }

    [RendererFact]
    public void ACustomGraphIsAcceptedAndDrawn()
    {
        using var renderer = new Renderer(64, 48);
        var graph = new RenderGraph
        {
            Targets = [new RenderTarget { Name = "mirror", Scale = 0.5 }, new RenderTarget { Name = "frame" }],
            Passes =
            [
                new RenderPass { Name = "water", Kind = PassKind.Reflection, Into = "mirror", Plane = [0, 1, 0, 0] },
                new RenderPass { Name = "world", Into = "frame", Reads = ["mirror"] },
                new MotionBlur().Pass(reads: "frame"),
            ],
        };

        renderer.Apply(SceneBuilders.Row() with { Graph = graph });
        renderer.Draw(0);

        Assert.True(renderer.RenderedFrames >= 1);
    }

    [RendererFact]
    public void AGraphWithProblemsIsAcceptedAsNoPassesAtAll()
    {
        using var renderer = new Renderer(64, 48);
        var broken = new RenderGraph { Passes = [new RenderPass { Name = "one" }, new RenderPass { Name = "two" }] };

        renderer.Apply(SceneBuilders.Row() with { Graph = broken });
        renderer.Draw(0);
        renderer.Apply(SceneBuilders.Row());
        renderer.Draw(1.0 / 60);

        Assert.True(renderer.RenderedFrames >= 2);
    }

    [RendererFact]
    public void APopulationIsAcceptedAndThenClearedWhenTheLastOneGoes()
    {
        using var renderer = new Renderer(64, 48);
        var forest = new Population(1, new float[16 * 3], new float[3 * 3], new Bounds(Vector3.Zero(), Vector3.All(4)))
        {
            Range = 30,
            Fade = Fade.Shrink,
        };

        renderer.Apply(SceneBuilders.Row() with { Populations = [forest] });
        renderer.Draw(0);
        renderer.Apply(SceneBuilders.Row() with { Populations = [forest] }, at: 1.0 / 60);
        renderer.Draw(1.0 / 60);
        renderer.Apply(SceneBuilders.Row(), at: 2.0 / 60);
        renderer.Draw(2.0 / 60);

        Assert.True(renderer.RenderedFrames >= 3);
    }

    [RendererFact]
    public void ASplatCloudIsAcceptedFromMemoryAndByPathAndClearedWhenTheLastOneGoes()
    {
        using var renderer = new Renderer(64, 48);
        var records = SplatCloud.Pack([0, 0, 0, 1, 1, 1], [1, 1, 1, 1, 1, 1], new float[8]);
        var memory = new SplatCloud(1, data: records);
        var file = new SplatCloud(2, "/no/such/garden.ply");

        renderer.Apply(SceneBuilders.Row() with { Splats = [memory, file] });
        renderer.Draw(0);
        renderer.Apply(SceneBuilders.Row() with { Splats = [new SplatCloud(1, data: records) { Revision = 1 }] }, at: 1.0 / 60);
        renderer.Draw(1.0 / 60);
        renderer.Apply(SceneBuilders.Row(), at: 2.0 / 60);
        renderer.Draw(2.0 / 60);

        Assert.True(renderer.RenderedFrames >= 3);
    }

    [RendererFact]
    public void ASpriteLayerIsAcceptedMovedAndClearedWhenTheLastOneGoes()
    {
        using var renderer = new Renderer(64, 48);
        var sprites = SpriteLayer.Pack([new LayerSprite { X = 0, Y = 0, Width = 2, Height = 2 }]);
        var layer = new SpriteLayer(1, sprites) { Order = 1 };
        var scene = SceneBuilders.Row() with { Camera = SceneBuilders.CameraAt(0, 0, 10) with { Orthographic = true } };

        renderer.Apply(scene with { Sprites = [layer] });
        renderer.Draw(0);
        renderer.Apply(scene with { Sprites = [layer] }, at: 1.0 / 60);
        renderer.Draw(1.0 / 60);
        renderer.Apply(scene with { Sprites = [new SpriteLayer(1, sprites) { Order = 1, Revision = 1 }] }, at: 2.0 / 60);
        renderer.Draw(2.0 / 60);
        renderer.Apply(scene, at: 3.0 / 60);
        renderer.Draw(3.0 / 60);

        Assert.True(renderer.RenderedFrames >= 4);
    }

    [RendererFact]
    public void ATerrainIsAcceptedMovedAndTakenAway()
    {
        using var renderer = new Renderer(64, 48);
        var maps = new[] { new TerrainRegion(0, 0, new float[256], new uint[256], new byte[1024]) };
        var land = new RenderTerrain(3, maps, options: new TerrainOptions { RegionSize = 16, MeshSize = 16, Levels = 2 });

        renderer.Apply(SceneBuilders.Row() with { Terrain = [land] });
        renderer.Draw(0);
        var moved = new RenderTerrain(
            3,
            [new TerrainRegion(0, 0, new float[256], new uint[256], new byte[1024]) { Revision = 2 }],
            options: new TerrainOptions { RegionSize = 16, MeshSize = 16, Levels = 2 });
        renderer.Apply(SceneBuilders.Row() with { Terrain = [moved] }, at: 1.0 / 60);
        renderer.Draw(1.0 / 60);
        renderer.Apply(SceneBuilders.Row(), at: 2.0 / 60);
        renderer.Draw(2.0 / 60);

        Assert.True(renderer.RenderedFrames >= 3);
    }

    [RendererFact]
    public void ASceneWithAVolumeIsDrawnAsTheVolumeResolvesIt()
    {
        using var renderer = new Renderer(64, 48);
        var hall = EnvironmentVolume.Box(
            1,
            Vector3.Zero(),
            Vector3.All(10),
            new EnvironmentOverrides { FogDensity = 0.1, Ambient = 2000, ExposureCompensation = -1 });

        renderer.Apply(SceneBuilders.Row() with { Volumes = [hall], Fog = new Fog() });
        renderer.Draw(0);

        Assert.True(renderer.RenderedFrames >= 1);
    }

    [RendererFact]
    public void EveryNamedDetailIsAccepted()
    {
        using var renderer = new Renderer(64, 48);

        foreach (var detail in Enum.GetValues<Detail>())
        {
            renderer.Apply(SceneBuilders.Row() with { Pipeline = Pipeline.ForDisplay(new Display(64, 48, detail)) });
            renderer.Draw(0);
        }

        Assert.True(renderer.RenderedFrames >= 4);
    }
}
