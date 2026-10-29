using Orblit.Filament;

namespace Orblit.Examples.Tests;

/// <summary>
/// The benchmark's three loads, each on its own dial. The fixture matches
/// the population on shape only, so these say what it holds and when it is sent.
/// </summary>
public sealed class BenchmarkTests
{
    private static readonly RenderCamera Camera = new ViewPoint().ToRenderCamera();

    [Fact]
    public void StandingStillSendsNothingAgainAndMovingSendsEveryFrame()
    {
        var example = new BenchmarkExample();
        var first = Assert.Single(example.Scene(Camera, 0).Populations);
        var still = Assert.Single(example.Scene(Camera, 1).Populations);

        example.Control<Toggle>("Moving").Change(true);
        var moving = Assert.Single(example.Scene(Camera, 2).Populations);
        var next = Assert.Single(example.Scene(Camera, 2).Populations);

        Assert.Equal(50000, first.Count);
        Assert.Equal(first.Revision, still.Revision);
        Assert.Equal(first.Revision + 1, moving.Revision);
        Assert.Equal(moving.Revision + 1, next.Revision);
        Assert.Same(first.Transforms, next.Transforms);
    }

    [Fact]
    public void TheSeedGivesTheSameFieldEveryRun()
    {
        var first = Assert.Single(new BenchmarkExample().Scene(Camera, 0).Populations);
        var second = Assert.Single(new BenchmarkExample().Scene(Camera, 0).Populations);

        Assert.Equal(first.Transforms, second.Transforms);
        Assert.Equal(first.Colours, second.Colours);
        // Pinned from this port's own run. The Dart field came from another generator.
        Assert.Equal("a0ec9c347745f2eb", SceneDigest.Raw(first.Transforms));
        Assert.Equal("f8c71c72244b7d14", SceneDigest.Raw(first.Colours));
    }

    [Fact]
    public void TheSlidersOnlyChangeTheLoadOnceTheyAreLetGo()
    {
        var example = new BenchmarkExample();
        var members = example.Control<Setting>("Members");
        var objects = example.Control<Setting>("Objects");

        members.Change(0);
        objects.Change(3000);
        var dragging = example.Scene(Camera, 0);
        members.Settle?.Invoke(0);
        objects.Settle?.Invoke(3000);
        var settled = example.Scene(Camera, 0);
        members.Settle?.Invoke(1000);
        var fewer = Assert.Single(example.Scene(Camera, 0).Populations);

        Assert.Single(dragging.Populations);
        Assert.Equal(201, dragging.Objects.Count);
        Assert.Empty(settled.Populations);
        Assert.Equal(3001, settled.Objects.Count);
        Assert.Equal(1990, settled.Objects[^1].Key);
        Assert.Equal(1000, fewer.Count);
    }

    /// <summary>The readout's last line names the load drawn, not the one a slider is still on.</summary>
    [Fact]
    public void TheLoadLineMovesWhenASliderIsLetGo()
    {
        var example = new BenchmarkExample();
        var before = example.Load;

        example.Control<Setting>("Members").Change(1234.5);
        var dragging = example.Load;
        example.Control<Setting>("Members").Settle?.Invoke(1234.5);
        example.Control<Setting>("Objects").Settle?.Invoke(7.4);
        example.Control<Choice>("Sky").Select?.Invoke("Full");

        Assert.Equal("50000 members · 200 objects · sky fair", before);
        Assert.Equal(before, dragging);
        Assert.Equal("1235 members · 7 objects · sky full", example.Load);
    }

    [Fact]
    public void ShadowsOffTakesThemFromEveryLoad()
    {
        var example = new BenchmarkExample();
        var on = example.Scene(Camera, 0);

        example.Control<Toggle>("Shadows").Change(false);
        var off = example.Scene(Camera, 0);

        Assert.True(Assert.Single(on.Populations).CastShadows);
        Assert.True(Assert.Single(on.Lights).CastShadows);
        Assert.True(on.Objects[0].CastShadows);
        Assert.False(on.Objects[^1].CastShadows);
        Assert.False(Assert.Single(off.Populations).CastShadows);
        Assert.False(Assert.Single(off.Lights).CastShadows);
        Assert.All(off.Objects, item => Assert.False(item.CastShadows));
    }

    [Theory]
    [InlineData("Lean")]
    [InlineData("Full")]
    public void TheSkyIsDrawnAtTheQualityChosen(string option)
    {
        var example = new BenchmarkExample();

        example.Control<Choice>("Sky").Select?.Invoke(option);

        Assert.Equal(option == "Lean" ? SkyQuality.Lean : SkyQuality.Full, example.Scene(Camera, 0).Sky?.Quality);
        Assert.Equal(option, example.Control<Choice>("Sky").Selected);
    }

    [Fact]
    public void BoxesOnlyBobWhileMoving()
    {
        var example = new BenchmarkExample();
        var still = example.Scene(Camera, 1).Objects[3].Transform.GetTranslation().Y;

        example.Control<Toggle>("Moving").Change(true);
        var moving = example.Scene(Camera, 1).Objects[3].Transform.GetTranslation().Y;

        Assert.Equal(0.6, still, 12);
        Assert.Equal(0.6 + (Math.Sin(0.9 + 3) * 0.5), moving, 12);
    }
}
