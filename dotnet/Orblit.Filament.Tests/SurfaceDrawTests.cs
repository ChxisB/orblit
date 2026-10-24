using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>
/// Surfaces somebody wrote, drawn: one cooked package worn by objects that differ by their values,
/// a package replaced on disk, a package that is cut short or missing, a texture on a surface, and
/// surface sources that are translated and then drawn beside the built-in material they replace.
/// </summary>
public sealed class SurfaceDrawTests(SurfaceWorkshop workshop) : IClassFixture<SurfaceWorkshop>
{
    private const uint Width = 64;
    private const uint Height = 48;

    private const double LeftCube = 0.255;
    private const double RightCube = 0.745;

    private static readonly double[] Red = [1, 0, 0, 1];
    private static readonly double[] Green = [0, 1, 0, 1];
    private static readonly double[] Blue = [0, 0, 1, 1];

    /// <summary>One cube a material, side by side and <paramref name="size"/> across, on black and seen straight through.</summary>
    private static RenderScene Show(double size, params RenderMaterial[] materials)
    {
        var middle = (materials.Length - 1) * 1.5;
        return new RenderScene
        {
            Objects =
            [
                .. materials.Select(
                    (material, place) => new RenderObject
                    {
                        Key = 100 + place,
                        Transform = Matrix4.Compose(new Vector3((place * 3.0) - middle, 0, 0), Quaternion.Identity(), Vector3.All(size)),
                        Colour = new Vector3(1, 1, 1),
                        Material = material.Key,
                    }),
            ],
            Materials = materials,
            Camera = new RenderCamera
            {
                Position = new Vector3(0, 0, 6),
                Target = Vector3.Zero(),
                FieldOfView = 42,
                Aperture = 1,
                ShutterSpeed = 1,
                Sensitivity = 100,
            },
            Sky = new Sky { Colour = Vector3.Zero(), Ambient = 0, ShowBody = false, Drawn = false },
            Post = new PostProcess
            {
                AntiAliasing = AntiAliasing.Off,
                Dithering = false,
                Grading = new Grading { ToneMapping = ToneMapping.Linear },
            },
        };
    }

    /// <summary>A material that wears a tint, a strength and, when given, a picture, through the package at <paramref name="package"/>.</summary>
    private static RenderMaterial Worn(long key, string package, double[] tint, Texture? picture = null) =>
        new()
        {
            Key = key,
            Surface = new CustomSurface(
                package,
                new Dictionary<string, IReadOnlyList<double>> { ["tint"] = tint, ["strength"] = [1] },
                picture is null ? null : new Dictionary<string, Texture> { ["albedo_map"] = picture }),
        };

    private static string Rgb(double[] colour) => string.Join('/', colour.Select(channel => channel.ToString("0")));

    [MatcFact]
    public void OnePackageWornByTwoObjectsDrawsEachInItsOwnValuesAndAValueCanBeRestated()
    {
        var package = workshop.Place("pair.filamat", workshop.Good);
        using var renderer = new Renderer(Width, Height);

        renderer.Apply(Show(1, Worn(1, package, Red), Worn(2, package, Green)), at: 1.0);
        var left = Pictures.ColourAt(renderer, 1.0, LeftCube, 0.5, 1);
        var right = Pictures.ColourAt(renderer, 2.0, RightCube, 0.5, 1);
        Assert.True(Pictures.Mostly(left, 0) && Pictures.Mostly(right, 1), $"left {Rgb(left)} right {Rgb(right)}");

        renderer.Apply(Show(1, Worn(1, package, Blue), Worn(2, package, Green)), at: 3.0);
        var restated = Pictures.ColourAt(renderer, 3.0, LeftCube, 0.5, 1);
        Assert.True(Pictures.Mostly(restated, 2), $"restated {Rgb(restated)}");
    }

    [MatcFact]
    public void APackageWrittenAgainOnDiskDrawsInPlaceOfTheOldOneOnceTheSurfaceIsRestated()
    {
        var package = workshop.Place("recooked.filamat", workshop.Good);
        using var renderer = new Renderer(Width, Height);
        var scene = Show(1, Worn(1, package, Blue));
        renderer.Apply(scene, at: 1.0);
        var blue = Pictures.ColourAt(renderer, 1.0, 0.5, 0.5, 1)[2];

        workshop.Place("recooked.filamat", workshop.Dim);
        renderer.Apply(scene, at: 2.0);
        var dimmed = Pictures.ColourAt(renderer, 2.0, 0.5, 0.5, 1)[2];

        Assert.True(dimmed > 10 && dimmed < blue - 15, $"blue {blue:0}, then {dimmed:0} after a recook");
    }

    [MatcFact]
    public void ARecookCutShortLeavesTheLastGoodSurfaceDrawingAndSaysWhichPackageItWas()
    {
        var package = workshop.Place("short.filamat", workshop.Good);
        using var renderer = new Renderer(Width, Height);
        var scene = Show(1, Worn(1, package, Blue));
        renderer.Apply(scene, at: 1.0);
        var before = Pictures.ColourAt(renderer, 1.0, 0.5, 0.5, 1)[2];

        workshop.Place("short.filamat", workshop.Good[..(workshop.Good.Length / 2)]);
        renderer.Apply(scene, at: 2.0);
        var after = Pictures.ColourAt(renderer, 2.0, 0.5, 0.5, 1)[2];

        Assert.True(Math.Abs(after - before) < 3, $"{before:0} before, {after:0} after");
        Assert.True(Pictures.Noted(renderer, "short.filamat"));
    }

    [MatcFact]
    public void ASurfaceNamingAPackageThatIsNotThereIsReportedAndTheFrameStillDraws()
    {
        using var renderer = new Renderer(Width, Height);

        renderer.Apply(Show(1, Worn(9, "/nowhere/missing_surface.filamat", [1, 1, 1, 1])), at: 1.0);
        renderer.Draw(2.0);

        Assert.True(Pictures.Noted(renderer, "missing_surface.filamat"));
    }

    [MatcFact]
    public void ASamplerOnASurfaceReadsThePictureItWasGivenTheRightWayUp()
    {
        var package = workshop.Place("textured.filamat", workshop.Good);
        var picture = RenderResources.NameFor("test/redgreen.png");
        RenderResources.Provide(picture, File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "fixtures", "orblit_filament", "redgreen.png")));
        try
        {
            using var renderer = new Renderer(Width, Height);
            renderer.Apply(Show(2, Worn(1, package, [1, 1, 1, 1], new Texture(picture))), at: 1.0);

            var upper = Pictures.ColourAt(renderer, 1.0, 0.5, 0.35, 1);
            var lower = Pictures.ColourAt(renderer, 2.0, 0.5, 0.65, 1);

            // The picture is two texels across and a linear filter blends them, so the halves differ
            // by a good deal without either being pure. A blank texture would give the same colour.
            var red = upper[0] - lower[0];
            var green = upper[1] - lower[1];
            Assert.True(Math.Abs(red) > 20 && Math.Abs(green) > 20 && red * green < 0, $"upper {Rgb(upper)} lower {Rgb(lower)}");
        }
        finally
        {
            Assert.True(RenderResources.Release(picture));
        }
    }

    [MatcFact]
    public void ASurfaceSourceThatRestatesTheBuiltInUnlitMaterialDrawsTheSameFrame()
    {
        var package = workshop.Place("unlit.filamat", workshop.Unlit);
        var builtIn = new RenderMaterial
        {
            Key = 1,
            Shading = MaterialShading.Unlit,
            DoubleSided = true,
            DepthWrite = true,
            BaseColour = new Vector4(0.2, 0.6, 0.9, 1),
        };
        var translated = new RenderMaterial
        {
            Key = 1,
            Surface = new CustomSurface(
                package,
                new Dictionary<string, IReadOnlyList<double>>
                {
                    ["baseColor"] = [0.2, 0.6, 0.9, 1],
                    ["emissive"] = [0, 0, 0],
                    ["emissiveIntensity"] = [1],
                    ["uvTransform"] = [1, 1, 0, 0],
                    ["hasBaseColorMap"] = [0],
                }),
        };
        using var renderer = new Renderer(Width, Height);

        renderer.Apply(Show(2, builtIn), at: 1.0);
        var plain = Pictures.Frame(renderer, 1.0).Rgba.ToArray();
        renderer.Apply(Show(2, translated), at: 1.0);
        var rewritten = Pictures.Frame(renderer, 1.0).Rgba.ToArray();

        Assert.Equal(plain.Length, rewritten.Length);
        var worst = plain.Zip(rewritten, (one, other) => Math.Abs(one - other)).Max();
        var brightest = plain.Where((_, place) => place % 4 != 3).Max();
        Assert.True(brightest > 100, "The cube is in the frame.");
        Assert.True(worst <= 2, $"worst channel difference {worst}");
    }

    [MatcFact]
    public void OneSurfaceSourceWithTwoTintsDrawsTwoColoursAndMovesWithTheRenderersOwnClock()
    {
        var package = workshop.Place("pulse.filamat", workshop.Pulse);
        RenderMaterial Pulsing(long key, double[] tint) =>
            new()
            {
                Key = key,
                Surface = new CustomSurface(
                    package,
                    new Dictionary<string, IReadOnlyList<double>> { ["tint"] = tint, ["speed"] = [2] }),
            };
        using var renderer = new Renderer(Width, Height);

        renderer.Apply(Show(1, Pulsing(1, Red), Pulsing(2, Green)), at: 1.0);
        var left = Pictures.ColourAt(renderer, 1.0, LeftCube, 0.5, 1);
        var right = Pictures.ColourAt(renderer, 1.0, RightCube, 0.5, 1);
        Assert.True(Pictures.Mostly(left, 0) && Pictures.Mostly(right, 1), $"left {Rgb(left)} right {Rgb(right)}");

        // TIME is Filament's own clock, seconds since the renderer was made, and not the seconds
        // passed to Draw. Speed 2 gives a period of 3 seconds, so the cube is read until it has both
        // brightened and dimmed.
        var low = left[0];
        var high = left[0];
        for (var reads = 0; reads < 60 && high - low < 60; reads++)
        {
            Thread.Sleep(100);
            var now = Pictures.ColourAt(renderer, 2.0 + reads, LeftCube, 0.5, 1)[0];
            low = Math.Min(low, now);
            high = Math.Max(high, now);
        }

        Assert.True(high - low >= 60, $"red between {low:0} and {high:0}");
    }
}
