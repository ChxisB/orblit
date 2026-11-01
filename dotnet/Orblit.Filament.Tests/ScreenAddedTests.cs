using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

public sealed class ScreenAddedTests
{
    private static float[] RaysOf(GodRays rays, params RenderLight[] lights) =>
        SceneBuilders.Read<float[]>(
            (SceneBuilders.Of([]) with { GodRays = rays, Lights = lights }).ToMessage(),
            "godRayParams");

    private static RenderLight Sun() =>
        new() { Key = 1, Kind = RenderLightKind.Directional, Intensity = 1000, Direction = new Vector3(0, -1, 0) };

    [Fact]
    public void GodRaysAreAllNoughtWhenOffWhenWeakAndWhenThereIsNoSun()
    {
        Assert.All(RaysOf(GodRays.Off, Sun()), value => Assert.Equal(0f, value));
        Assert.All(RaysOf(new GodRays { Strength = 0 }, Sun()), value => Assert.Equal(0f, value));
        Assert.All(RaysOf(new GodRays { Strength = 1 }), value => Assert.Equal(0f, value));
        Assert.NotEqual(0f, RaysOf(new GodRays { Strength = 1 }, Sun())[11]);
    }

    [Fact]
    public void AFinishedWaveIsNotActiveAndPacksToNothing()
    {
        var spent = Distortion.Expanding(Vector3.Zero(), 3, speed: 4, lifetime: 2, strength: 0.04);

        Assert.False(spent.IsActive);
        Assert.Empty(Distortion.PackAll([spent]));
    }
}
