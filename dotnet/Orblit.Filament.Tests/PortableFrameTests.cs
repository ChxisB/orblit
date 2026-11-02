using System.Runtime.InteropServices;
using Orblit.Sprite;
using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>
/// One frame drawn on the backend the platform picks, and read back. The Avalonia viewport shows
/// this same readback on Windows and Linux, so a frame that measures right here is the frame a
/// visitor sees there.
/// </summary>
public sealed class PortableFrameTests
{
    private const uint Width = 96;
    private const uint Height = 72;
    private const string FrameFolderVariable = "ORBLIT_FRAME_OUT";

    // The sum of the three channel differences between the cube and the sky behind it.
    private const double SeparatedBy = 60;

    [RendererFact]
    public void ThePlatformPicksItsOwnBackend()
    {
        using var renderer = new Renderer(Width, Height);

        if (OperatingSystem.IsMacOS())
        {
            Assert.Equal(Backend.Metal, renderer.Backend);
        }
        else
        {
            Assert.Contains(renderer.Backend, new[] { Backend.Vulkan, Backend.OpenGl });
        }
    }

    [RendererFact]
    public void ACubeIsDrawnInFrontOfTheSkyAtTheSizeAskedFor()
    {
        using var renderer = new Renderer(Width, Height);
        renderer.Apply(RedCubeOn(new Vector3(0.1, 0.2, 0.8)));

        var capture = Pictures.Frame(renderer, 3.0);
        Save(capture, renderer.Backend);

        Assert.Equal((Width, Height), (capture.Width, capture.Height));
        var cube = Pictures.ColourIn(capture, 0.5, 0.5, 2);
        var sky = Pictures.ColourIn(capture, 0.06, 0.06, 2);
        Assert.True(cube[0] > cube[2], $"the cube is red, and the middle of the frame is {Describe(cube)}");
        Assert.True(Distance(cube, sky) > SeparatedBy, $"the cube {Describe(cube)} stands out from the sky {Describe(sky)}");
    }

    private static RenderScene RedCubeOn(Vector3 sky) =>
        SceneBuilders.Of(
            [SceneBuilders.Plain(1) with { Colour = new Vector3(1, 0.02, 0.01) }],
            new RenderCamera { Position = new Vector3(0, 0, 4), Target = Vector3.Zero(), FieldOfView = 42 }) with
        {
            Sky = new Sky { Colour = sky, Ambient = 20000, Drawn = false },
        };

    private static double Distance(double[] first, double[] second) =>
        first.Zip(second, (a, b) => Math.Abs(a - b)).Sum();

    private static string Describe(double[] rgb) => $"rgb({rgb[0]:0}, {rgb[1]:0}, {rgb[2]:0})";

    /// <summary>
    /// Writes the frame as a PNG when a leg asks for it, so a person can look at what the
    /// measurement saw. The name carries the backend and the platform, because one folder
    /// collects the frames of every leg.
    /// </summary>
    private static void Save(Capture capture, Backend backend)
    {
        if (Environment.GetEnvironmentVariable(FrameFolderVariable) is not { Length: > 0 } folder)
        {
            return;
        }

        Directory.CreateDirectory(folder);
        var system = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        var architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        var png = PngCodec.Encode(capture.Width, capture.Height, capture.Rgba.ToArray());
        File.WriteAllBytes(Path.Combine(folder, $"frame-{backend.ToString().ToLowerInvariant()}-{system}-{architecture}.png"), png);
    }
}
