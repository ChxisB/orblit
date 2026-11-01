using System.Text.RegularExpressions;

namespace Orblit.Filament.Tests;

/// <summary>The motion blur pass, its dials and its native contract.</summary>
public sealed partial class MotionBlurTests
{
    private static string ReadNative(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "native", name));

    [Fact]
    public void IsOffUnlessAGraphAsksForIt()
    {
        // Nothing about a scene with no graph mentions it, so the frame a host has always drawn
        // is the frame it still draws.
        Assert.Null(Assert.Single(RenderGraph.Standard().Passes).Effect);
    }

    [Fact]
    public void ItsGraphIsTheWorldIntoATargetAndTheBlurOntoTheScreen()
    {
        var graph = new MotionBlur().Graph();

        Assert.Empty(graph.Problems);
        var order = graph.Schedule;
        Assert.Equal(["world", "motion blur"], order.Select(pass => pass.Name));
        Assert.Equal(PassEffect.MotionBlur, order[^1].Effect);
        // The blur draws the frame.
        Assert.Null(order[^1].Into);
        Assert.Equal(["frame"], order[^1].Reads);
    }

    [Fact]
    public void ItsFourDialsCrossInTheOrderTheRendererReadsThem()
    {
        var packed = new MotionBlur { Shutter = 1.0 / 30, MaxPixels = 32, Objects = false, Samples = 9 }
            .Graph().PackedPasses;
        const int at = RenderGraph.PassStride;

        // The shutter, the clamp in pixels, camera only, and the taps.
        Assert.Equal(1.0 / 30, packed[at + 8], 1e-6);
        Assert.Equal(32f, packed[at + 9]);
        Assert.Equal(-1f, packed[at + 10]);
        Assert.Equal(9f, packed[at + 11]);
        Assert.Equal((float)(int)PassEffect.MotionBlur, packed[at + 12]);
    }

    [Fact]
    public void AShutterLeftUnsetFollowsTheCameraAndSaysSoWithNought()
    {
        var blur = new MotionBlur();

        Assert.Equal(0, blur.Dials[0]);
        // Objects blur by default.
        Assert.Equal(1, blur.Dials[2]);
    }

    [Fact]
    public void AStreakIsSpeedTimesTheTimeTheShutterIsOpen()
    {
        // Something crossing at 600 pixels a second: a thousandth of a second barely moves it, a
        // thirtieth smears it twenty pixels, and the clamp stops a fast pan from turning the
        // picture into its own average.
        var blur = new MotionBlur();

        Assert.Equal(0.6, blur.Streak(600, cameraShutter: 1.0 / 1000), 1e-9);
        Assert.Equal(20, blur.Streak(600, cameraShutter: 1.0 / 30), 1e-9);
        Assert.Equal(40, blur.Streak(6000, cameraShutter: 1.0 / 30));
        // A shutter of its own overrides the camera.
        Assert.Equal(10, new MotionBlur { Shutter = 1.0 / 60 }.Streak(600, cameraShutter: 1), 1e-9);
    }

    [Fact]
    public void ATargetItReadsHasToKeepItsDepth()
    {
        var graph = new RenderGraph
        {
            Targets = [new RenderTarget { Name = "flat", Depth = false }],
            Passes = [new RenderPass { Name = "world", Into = "flat" }, new MotionBlur().Pass(reads: "flat")],
        };

        Assert.Contains("depth", string.Join(" ", graph.Problems.Select(problem => problem.What)));
    }

    [Fact]
    public void TheRendererNumbersTheEffectTheWayThisSideDoes()
    {
        // The effect crosses as its index, and the renderer switches on a constant of its own.
        // Appended rather than inserted, so every effect before it keeps its number, and checked
        // because an index that drifts runs a different shader rather than failing.
        var found = EffectConstant().Match(ReadNative("OrblitRendererTypes.h"));

        Assert.True(found.Success);
        Assert.Equal((int)PassEffect.MotionBlur, int.Parse(found.Groups[1].Value));
    }

    [Fact]
    public void TheBlurBindsItsSamplersBeforeItCanGiveUpOnAFrame()
    {
        // The resolve and tile passes keep a material instance each for as long as the effect is
        // wanted, and those instances name textures neither of them owns for long: the graph's
        // depth, which the renderer destroys the moment the graph changes, and the blur's own
        // targets, which go whenever the picture changes size. A frame with nothing moving leaves
        // prepare early, which is exactly the state just after a host switches scenes, so samplers
        // bound only beside their draws are left naming a texture that is already destroyed, and
        // the next frame that does draw them binds a freed handle. The renderer ends the process
        // for that.
        var native = ReadNative("OrblitMotionBlur.cpp");
        var at = native.IndexOf("void MotionBlur::prepare(", StringComparison.Ordinal);
        Assert.True(at > 0, "prepare is still where it was");
        var prepare = native[at..];

        // Where it decides there is nothing to blur and returns.
        var givesUp = prepare.IndexOf("if (cameraScale <= 0.0 && !drewObjects)", StringComparison.Ordinal);
        Assert.True(givesUp > 0, "prepare still returns early");

        foreach (var binding in new[] { "resolve.setParameter(\"objects\"", "tiles.setParameter(\"velocity\"" })
        {
            var bound = prepare.IndexOf(binding, StringComparison.Ordinal);
            Assert.True(bound > 0, $"{binding}) has to be there at all");
            Assert.True(
                bound < givesUp,
                $"{binding}) has to be bound before prepare can return, or the instance is left holding a destroyed texture");
        }
    }

    [GeneratedRegex(@"constexpr int kEffectMotionBlur\s*=\s*(\d+);")]
    private static partial Regex EffectConstant();
}
