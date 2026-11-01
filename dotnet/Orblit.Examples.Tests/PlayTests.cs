using Orblit.Filament;
using Orblit.Test;

namespace Orblit.Examples.Tests;

/// <summary>
/// Examples played through a <see cref="TestStage"/>, which steps whole frames
/// with no window and no wall clock. The Runner's two play tests are with the
/// Runner.
/// </summary>
public sealed class PlayTests
{
    private static readonly RenderCamera Camera = new ViewPoint().ToRenderCamera();

    [Fact]
    public void ACrateLiftedOverTwoSecondsCostsOneOperationAFrame()
    {
        var example = new SceneFilesExample();
        var captions = new List<string>();
        var stage = new TestStage(frame =>
        {
            example.Control<Setting>("Lift the crate").Change(Math.Min(2, frame.Now.TotalSeconds));
            captions.Add(example.Settings().OfType<Caption>().Single().Label);
        });

        stage.Step(TimeSpan.FromSeconds(2));

        Assert.Equal(120, captions.Count);
        Assert.All(captions, caption => Assert.Equal("Last edit: 1 operation, and only the entity it named was rebuilt.", caption));
        Assert.Contains(example.Scene(Camera, 2).Objects, item => Math.Abs(item.Transform.GetTranslation().Y - 2) < 0.001);
    }

    [Fact]
    public void TheSunPeaksAtNoonAndTheLuxFallsToTheMoonAfterDark()
    {
        var example = new DayAndNightExample();
        var lux = 0.0;
        var stage = new TestStage(frame => lux = Assert.Single(example.Scene(Camera, frame.Now.TotalSeconds).Lights).Intensity);

        // Ten in the morning, at 0.6 hours a second: noon is a little over
        // three seconds in.
        stage.Step(TimeSpan.FromMilliseconds(3340));
        Assert.True(lux > 70000, $"{lux} lux at noon.");

        stage.StepUntil(() => lux <= 1, TimeSpan.FromSeconds(20));
        Assert.True(stage.Now > TimeSpan.FromSeconds(6), $"Dark at {stage.Now}.");
    }
}
