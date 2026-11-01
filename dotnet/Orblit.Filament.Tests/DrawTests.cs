using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>
/// Scenes drawn by a headless renderer and read back out of the frame. What these measure is what
/// reached the pixels, which the tests that only apply a scene cannot say.
/// </summary>
public sealed class DrawTests
{
    private const uint Width = 64;
    private const uint Height = 48;

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", "orblit_filament", name);

    private static RenderCamera Straight(double eyeZ) =>
        new() { Position = new Vector3(0, 0, eyeZ), Target = Vector3.Zero(), FieldOfView = 42 };

    [RendererFact]
    public void ACaptureHasItsTopRowFirst()
    {
        using var renderer = new Renderer(Width, Height);
        // A red cube below the camera's aim leaves blue sky above it. A centred cube cannot tell
        // a flipped row order from the right one.
        var cube = SceneBuilders.Plain(1) with
        {
            Transform = Matrix4.TranslationValues(0, -1, 0),
            Colour = new Vector3(1, 0.02, 0.01),
        };
        renderer.Apply(
            SceneBuilders.Of([cube], Straight(6)) with
            {
                Sky = new Sky { Colour = new Vector3(0.1, 0.2, 0.8), Ambient = 20000, Drawn = false },
            });

        var capture = Pictures.Frame(renderer, 3.0);

        Assert.Equal((Width, Height), (capture.Width, capture.Height));
        var rgba = capture.Rgba.Span;
        var top = rgba.Slice((((12 * (int)Width) + 32) * 4), 3);
        var bottom = rgba.Slice((((36 * (int)Width) + 32) * 4), 3);
        Assert.True(top[2] > top[0] + 10, "blue sky is above the cube");
        Assert.True(bottom[0] > bottom[2] + 10, "the red cube is below the sky");
    }

    [RendererFact]
    public void AMeshNamedBeforeItsBytesExistIsReportedMissingAndLoadsOnceTheyAreProvided()
    {
        var name = RenderResources.NameFor("test/cube.glb");
        using var renderer = new Renderer(Width, Height);
        var scene = SceneBuilders.Of(
            [SceneBuilders.Plain(1) with { Colour = Vector3.All(0.8), Mesh = name }],
            SceneBuilders.CameraAt(0, 2, 8));
        try
        {
            renderer.Apply(scene);
            for (var frame = 0; frame < 3; frame++)
            {
                renderer.Draw(frame / 60.0);
            }

            Assert.True(Pictures.Noted(renderer, "cube.glb"), "the missing mesh is reported");

            RenderResources.Provide(name, File.ReadAllBytes(Fixture("cube.glb")));
            renderer.Apply(scene, at: 3 / 60.0);
            for (var frame = 3; frame < 8; frame++)
            {
                renderer.Draw(frame / 60.0);
            }

            Assert.False(Pictures.Noted(renderer, "cube.glb"), "the same scene finds the mesh once it is provided");
        }
        finally
        {
            RenderResources.Release(name);
        }
    }

    // How many times the graph is replaced. Several make it plain that what leaked scaled with the
    // changes and was not one stray object.
    private const int GraphChanges = 8;

    [RendererFact]
    public void AGraphChangedManyTimesIsDrawnEachTimeAndTheRendererGoesDownCleanly()
    {
        var renderer = new Renderer(Width, Height);
        for (var change = 0; change < GraphChanges; change++)
        {
            // The dial moves every time round, because a graph identical to the one already set is
            // ignored and a loop that moved nothing would set one graph and test nothing.
            var graph = new RenderGraph
            {
                Targets = [new RenderTarget { Name = "scene" }],
                Passes =
                [
                    new RenderPass { Name = "world", Into = "scene" },
                    new RenderPass
                    {
                        Name = "sharpen",
                        Kind = PassKind.Effect,
                        Effect = PassEffect.Sharpen,
                        Reads = ["scene"],
                        Plane = [0.2 + (0.05 * change)],
                    },
                ],
            };
            Assert.True(graph.IsRunnable);

            renderer.Apply(SceneBuilders.Row() with { Graph = graph }, at: change / 60.0);
            renderer.Draw(change / 60.0);
        }

        Assert.True(renderer.RenderedFrames >= GraphChanges);

        // Filament ends the process here if anything the graph built outlived the graph.
        renderer.Dispose();
    }
}
