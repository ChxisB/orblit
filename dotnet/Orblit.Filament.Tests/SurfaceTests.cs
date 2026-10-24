namespace Orblit.Filament.Tests;

/// <summary>Ported from surface_test.dart.</summary>
public sealed class SurfaceTests
{
    private static readonly Texture Picture = new("/art/noise.png", Srgb: false);

    private static RenderScene SceneOf(params RenderMaterial[] materials) =>
        SceneBuilders.Of([], SceneBuilders.CameraAt(0, 2, 6)) with { Materials = materials };

    /// <summary>The numbers of a surface, in the order they are written.</summary>
    private static OrderedDictionary<string, IReadOnlyList<double>> Values(params (string Name, double[] Numbers)[] entries)
    {
        var values = new OrderedDictionary<string, IReadOnlyList<double>>();
        foreach (var (name, numbers) in entries)
        {
            values.Add(name, numbers);
        }

        return values;
    }

    [Fact]
    public void ASurfaceSetsTheCustomFlagAndAPlainMaterialDoesNot()
    {
        var worn = new RenderMaterial { Key = 1, Surface = new CustomSurface("/cook/pulse.filamat") };

        Assert.NotEqual(0, worn.Flags & (1 << 15));
        Assert.Equal(0, new RenderMaterial { Key = 2 }.Flags & (1 << 15));
        Assert.NotNull((worn with { Roughness = 0.1 }).Surface);
    }

    [Fact]
    public void TheMessageLaysSurfacesOutAsTheEntryPointReadsThem()
    {
        var textures = new Dictionary<string, Texture> { ["noise"] = Picture };
        var message = SceneOf(
            new RenderMaterial { Key = 7 },
            new RenderMaterial
            {
                Key = 8,
                Surface = new CustomSurface(
                    "/cook/pulse.filamat",
                    Values(("speed", [2.5]), ("tint", [1, 0.5, 0.25, 1])),
                    textures),
            },
            new RenderMaterial
            {
                Key = 9,
                Surface = new CustomSurface("/cook/pulse.filamat", Values(("speed", [4])), textures),
            }).ToMessage();

        Assert.Equal([8L, 9L], SceneBuilders.Read<long[]>(message, "surfaceKeys"));
        Assert.Equal(["/cook/pulse.filamat", "/cook/pulse.filamat"], SceneBuilders.Read<string[]>(message, "surfacePackages"));
        Assert.Equal([3, 2], SceneBuilders.Read<int[]>(message, "surfaceEntryCounts"));
        Assert.Equal(["speed", "tint", "noise", "speed", "noise"], SceneBuilders.Read<string[]>(message, "surfaceNames"));
        Assert.Equal([1, 4, 0, 1, 0], SceneBuilders.Read<int[]>(message, "surfaceWidths"));
        // The same picture is sent once and pointed at twice.
        Assert.Equal([-1, -1, 0, -1, 0], SceneBuilders.Read<int[]>(message, "surfaceMaps"));
        Assert.Equal(["/art/noise.png"], SceneBuilders.Read<string[]>(message, "surfaceTexturePaths"));
        Assert.Equal([0], SceneBuilders.Read<int[]>(message, "surfaceTextureSrgb"));
        Assert.Equal([2.5f, 1f, 0.5f, 0.25f, 1f, 4f], SceneBuilders.Read<float[]>(message, "surfaceValues"));
        Assert.Equal(
            [0, 1 << 15, 1 << 15],
            SceneBuilders.Read<int[]>(message, "materialFlags").Select(flags => flags & (1 << 15)));
    }

    [Fact]
    public void ASceneWithNoSurfaceSendsNothingAboutThem()
    {
        var message = SceneOf(new RenderMaterial { Key = 1 }).ToMessage();

        Assert.DoesNotContain(message.Keys, key => key.StartsWith("surface", StringComparison.Ordinal));
    }

    [Fact]
    public void ASurfaceRefusesAValueThatNoShaderCouldTake()
    {
        Assert.ThrowsAny<ArgumentException>(() => new CustomSurface("p", Values(("a", []))));
        Assert.ThrowsAny<ArgumentException>(() => new CustomSurface("p", Values(("a", [1, 2, 3, 4, 5]))));
        Assert.ThrowsAny<ArgumentException>(
            () => new CustomSurface("p", Values(("a", [1])), new Dictionary<string, Texture> { ["a"] = Picture }));
    }
}
