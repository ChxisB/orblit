using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>
/// Applies a scene to a live headless renderer. A scene core has no render graph yet, so nothing
/// it describes shows. What these check is that the renderer accepts every array, including a
/// texture, a video and a shader whose files are not there.
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
}
