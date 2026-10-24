using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>
/// Ported from screen_test.dart: the god rays and the distortion as they are packed. The groups
/// that read the pass list follow with the render graph.
/// </summary>
public sealed class ScreenTests
{
    private static RenderScene SceneWith(GodRays? godRays = null, Sky? sky = null)
    {
        var scene = SceneBuilders.Of([], SceneBuilders.CameraAt(0, 0, 5)) with
        {
            Lights =
            [
                new RenderLight
                {
                    Key = 1,
                    Kind = RenderLightKind.Directional,
                    Intensity = 1000,
                    Direction = new Vector3(0, -0.2, -1).Normalized(),
                    Colour = new Vector3(1, 0.8, 0.6),
                },
            ],
            GodRays = godRays ?? GodRays.Off,
        };
        return sky is null ? scene : scene with { Sky = sky };
    }

    private static float[] RaysOf(RenderScene scene) => SceneBuilders.Read<float[]>(scene.ToMessage(), "godRayParams");

    [Fact]
    public void GodRaysComeFromTheScenesOwnLightInItsColour()
    {
        var packed = RaysOf(SceneWith(new GodRays { Strength = 0.5, Samples = 32 }));

        SceneBuilders.Near(0.5, packed[0]);
        Assert.Equal(32f, packed[3]);
        // The light's colour, because none was asked for.
        SceneBuilders.NearAll(packed[4..7], 1, 0.8, 0.6);
        // Towards the light, which is the way it does not travel.
        var toward = new Vector3(packed[7], packed[8], packed[9]);
        Assert.Equal(1.0, toward.Length, 1e-5);
        Assert.True(toward.Y > 0);
        Assert.True(toward.Z > 0);
        Assert.Equal(1f, packed[11]);
    }

    [Fact]
    public void ATintOfTheirOwnWinsOverTheLight()
    {
        var packed = RaysOf(SceneWith(new GodRays { Strength = 1, Tint = new Vector3(0.2, 0.4, 1) }));

        SceneBuilders.NearAll(packed[4..7], 0.2, 0.4, 1);
    }

    [Fact]
    public void CloudOnlyThinsGodRaysWhenTheSkyThatCarriesItIsDrawn()
    {
        var cloudy = new Sky { Clouds = Clouds.Cumulus with { Cover = 0.7 } };
        SceneBuilders.Near(0.7, RaysOf(SceneWith(new GodRays { Strength = 1 }, cloudy))[10]);

        var hidden = new Sky { Drawn = false, Clouds = Clouds.Cumulus with { Cover = 0.7 } };
        Assert.Equal(0f, RaysOf(SceneWith(new GodRays { Strength = 1 }, hidden))[10]);
    }

    [Fact]
    public void AWaveGrowsWithTheClockAndWeakensAsItGoes()
    {
        var early = Distortion.Expanding(Vector3.Zero(), 0.5, speed: 4, lifetime: 2, strength: 0.04);
        var late = Distortion.Expanding(Vector3.Zero(), 1.5, speed: 4, lifetime: 2, strength: 0.04);

        Assert.Equal(2.0, early.Radius, 1e-9);
        Assert.Equal(6.0, late.Radius, 1e-9);
        Assert.Equal(0.03, early.Strength, 1e-9);
        Assert.Equal(0.01, late.Strength, 1e-9);
    }

    [Fact]
    public void EachKindPacksIntoTheSlotsTheRendererReads()
    {
        var packed = Distortion.PackAll(
        [
            Distortion.Shockwave(new Vector3(1, 2, 3), 4, thickness: 0.5, strength: 0.02, chromatic: 0.3),
            Distortion.Haze(new Vector3(-1, 0, 2), new Vector3(0.5, 1, 0.5), scale: 0.2, speed: 2, seconds: 3),
            Distortion.Lens(-0.1),
        ]);
        const int s = Distortion.Stride;

        Assert.Equal(3 * s, packed.Length);
        SceneBuilders.NearAll(packed[0..8], 1, 0.02, 0.3, 1, 2, 3, 4, 0.5);
        Assert.Equal(2f, packed[s]);
        SceneBuilders.NearAll(packed[(s + 6)..(s + 11)], 0.5, 1, 0.5, 0.2, 6);
        Assert.Equal(3f, packed[2 * s]);
        SceneBuilders.Near(-0.1, packed[(2 * s) + 1]);
    }

    [Fact]
    public void NoMoreThanTheRendererCanDrawAreSent()
    {
        var many = Enumerable.Range(0, 12).Select(_ => Distortion.Lens(0.01)).ToList();

        Assert.Equal(Distortion.Capacity * Distortion.Stride, Distortion.PackAll(many).Length);
    }
}
