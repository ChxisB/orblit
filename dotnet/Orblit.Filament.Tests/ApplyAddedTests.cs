using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>
/// Applies a scene to a live headless renderer. A scene core has no render graph yet, so nothing
/// it describes shows. What these check is that the renderer accepts every array.
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
