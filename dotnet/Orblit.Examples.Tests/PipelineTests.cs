using Orblit.Filament;

namespace Orblit.Examples.Tests;

/// <summary>
/// What the environment tells the pipeline example. The fixture ran with
/// <c>ORBLIT_DETAIL</c> unset, so these cover the tier picked from outside.
/// </summary>
public sealed class PipelineTests
{
    private static readonly RenderCamera Camera = new ViewPoint().ToRenderCamera();

    [Theory]
    [InlineData("ultra", Detail.Ultra)]
    [InlineData("LOW", Detail.Low)]
    [InlineData("highest", Detail.Medium)]
    [InlineData(null, Detail.Medium)]
    public void TheDetailNamedInTheEnvironmentIsTheTierDrawn(string? named, Detail drawn)
    {
        var example = new PipelineExample(name => name == "ORBLIT_DETAIL" ? named : null);

        Assert.Equal(Pipeline.At(drawn), example.Scene(Camera, 0).Pipeline);
        Assert.Equal(drawn.ToString(), Assert.IsType<Choice>(example.Settings()[0]).Selected);
    }

    // The three below follow handlers that the fixture's moves never
    // reach: they start at Medium and go only to the ends of a range.
    [Fact]
    public void ShadowsOffHidesTheirControls()
    {
        var example = new PipelineExample(_ => null);
        example.Control<Toggle>("Shadows").Change(false);

        Assert.DoesNotContain(example.Settings(), control => control.Label == "Edge");
    }

    [Fact]
    public void ChoosingARenderScaleStopsItShrinking()
    {
        var example = new PipelineExample(name => name == "ORBLIT_DETAIL" ? "low" : null);
        example.Control<Setting>("Render scale").Change(0.6);

        Assert.False(example.Scene(Camera, 0).Pipeline.Resolution.Adaptive);
    }

    [Fact]
    public void CascadesRoundToTheNearestWhole()
    {
        var example = new PipelineExample(_ => null);
        example.Control<Setting>("Cascades").Change(2.6);

        Assert.Equal(3, example.Scene(Camera, 0).Pipeline.Shadows.Cascades);
    }
}
