using Orblit.VectorMath;

namespace Orblit.Filament.Tests;

/// <summary>What the sky, post and graph port does at its edges, beyond the original's own tests.</summary>
public sealed class PictureAddedTests
{
    private static RenderScene Scene() => SceneBuilders.Of([], SceneBuilders.CameraAt(0, 0, 5));

    [Fact]
    public void SplitsPastTheThirdAreNotSent()
    {
        var packed = new Pipeline { Shadows = new ShadowSettings { Cascades = 4, Splits = [0.1, 0.2, 0.3, 0.9] } }.Packed;

        Assert.Equal(Pipeline.Stride, packed.Length);
        SceneBuilders.NearAll(packed[18..21], 0.1, 0.2, 0.3);
    }

    [Fact]
    public void ADetailPastTheFourNamedOnesIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Pipeline.At((Detail)9));
    }

    [Fact]
    public void ADisplayOfExactlyAMegapixelAndAHalfIsStillSmall()
    {
        var named = Pipeline.At(Detail.High);

        Assert.Equal(1024, Pipeline.ForDisplay(new Display(1500, 1000, Detail.High)).Shadows.MapSize);
        Assert.Equal(named.Shadows.MapSize, Pipeline.ForDisplay(new Display(1501, 1000, Detail.High)).Shadows.MapSize);
    }

    [Fact]
    public void ADisplayWithNoHeightHasAnAspectOfOne()
    {
        Assert.Equal(1.0, new Display(1280, 0).Aspect);
    }

    [Fact]
    public void ACaptureStopsAtTheShorterOfTheNamesAndTheNumbers()
    {
        var fewerNames = FrameCapture.From([1f, 10, 2, 20, 3, 30], ["a", "b"]);
        var fewerNumbers = FrameCapture.From([1f, 10, 2, 20], ["a", "b", "c"]);
        var half = FrameCapture.From([1f, 10, 2], ["a", "b"]);

        Assert.Equal(2, fewerNames.Passes.Count);
        Assert.Equal(2, fewerNumbers.Passes.Count);
        Assert.Single(half.Passes);
    }

    [Fact]
    public void ACaptureKeepsTheFirstOfTwoEqualSlowestPasses()
    {
        var capture = FrameCapture.From([2f, 1, 2, 1], ["first", "second"]);

        Assert.Equal("first", capture.Slowest!.Name);
    }

    [Fact]
    public void ASceneWithAGraphThatHasProblemsSendsNoPasses()
    {
        var broken = Scene() with { Graph = new RenderGraph { Passes = [new RenderPass { Name = "one" }, new RenderPass { Name = "two" }] } };
        var message = broken.ToMessage();

        Assert.Empty(SceneBuilders.Read<float[]>(message, "graphPasses"));
        Assert.Empty(broken.PassNames);
    }

    [Fact]
    public void ScreenEffectsAreOnlyAddedWhenTheSceneHasSomethingToDrawThem()
    {
        var wave = Distortion.Shockwave(Vector3.Zero(), 2);
        var spent = Distortion.Expanding(Vector3.Zero(), 5, lifetime: 2);

        Assert.Equal(["scene", "distortion"], (Scene() with { Distortions = [wave] }).PassNames);
        Assert.Equal(["scene"], (Scene() with { Distortions = [spent] }).PassNames);
    }

    [Fact]
    public void AnEnvironmentIsNotSetByItsBrightnessAlone()
    {
        Assert.False(new RenderEnvironment { Intensity = 90000 }.IsSet);
    }
}
