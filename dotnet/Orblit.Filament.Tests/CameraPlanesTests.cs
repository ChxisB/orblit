using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>What the camera's near and far planes cut out of a drawn frame.</summary>
public sealed class CameraPlanesTests
{
    private const uint Width = 64;
    private const uint Height = 48;

    // A red block 60 m wide, 400 m from the eye, in front of blue sky.
    private static RenderScene Block(RenderCamera camera) =>
        SceneBuilders.Of(
            [
                SceneBuilders.Plain(1) with
                {
                    Transform = Matrix4.Diagonal3Values(60, 60, 60),
                    Colour = new Vector3(1, 0.02, 0.01),
                },
            ],
            camera) with
        {
            Sky = new Sky { Colour = new Vector3(0.1, 0.2, 0.8), Ambient = 20000, Drawn = false },
        };

    private static RenderCamera Eye() =>
        new() { Position = new Vector3(0, 0, 400), Target = Vector3.Zero(), FieldOfView = 42 };

    private static bool BlockIsDrawn(RenderCamera camera)
    {
        using var renderer = new Renderer(Width, Height);
        renderer.Apply(Block(camera));
        return Pictures.Mostly(Pictures.Middle(renderer, 3.0), 0);
    }

    [RendererFact]
    public void ABlockInsideTheDefaultPlanesIsDrawn() => Assert.True(BlockIsDrawn(Eye()));

    [RendererFact]
    public void AFarPlaneBeforeTheBlockCutsItOut() => Assert.False(BlockIsDrawn(Eye() with { Far = 100 }));

    [RendererFact]
    public void ANearPlanePastTheBlockCutsItOut() => Assert.False(BlockIsDrawn(Eye() with { Near = 500 }));

    [RendererFact]
    public void AFarPlaneNotPastTheNearPlaneIsRefused()
    {
        using var renderer = new Renderer(Width, Height);

        Assert.Throws<RendererException>(() => renderer.Apply(Block(Eye() with { Near = 50, Far = 10 })));
    }

    [RendererFact]
    public void AFarPlaneNotPastTheBuiltInNearPlaneIsRefused()
    {
        using var renderer = new Renderer(Width, Height);

        Assert.Throws<RendererException>(() => renderer.Apply(Block(Eye() with { Far = 0.05 })));
    }
}
