using Orblit.Filament;

namespace Orblit.Examples.Tests;

/// <summary>
/// When the crowd's buffer is written and sent. The fixture matches it on
/// shape only, so these say the rest: what it holds and when it changes.
/// </summary>
public sealed class CrowdTests
{
    private static readonly RenderCamera Camera = new ViewPoint().ToRenderCamera();

    [Fact]
    public void TheCrowdIsBuiltOnceAndThenOnlyLookedAt()
    {
        var example = new CrowdExample();

        var first = Crowd(example, 0);
        var later = Crowd(example, 5);

        Assert.Equal(100000, first.Count);
        Assert.Equal(100000 * 3, first.Colours.Length);
        Assert.Equal(first.Revision, later.Revision);
        Assert.Same(first.Transforms, later.Transforms);
        Assert.Equal(300, first.Range);
    }

    [Fact]
    public void TheSeedGivesTheSameCrowdEveryRun()
    {
        var first = Crowd(new CrowdExample(), 0);
        var second = Crowd(new CrowdExample(), 0);

        Assert.Equal(first.Transforms, second.Transforms);
        Assert.Equal(first.Colours, second.Colours);
        // Pinned from this port's own run. The Dart crowd came from another generator.
        Assert.Equal("32b5478fc350aadd", SceneDigest.Raw(first.Transforms));
        Assert.Equal("024087d05eb68090", SceneDigest.Raw(first.Colours));
    }

    [Fact]
    public void AMemberStandsOnTheGroundTurnedAndScaled()
    {
        var transforms = Crowd(new CrowdExample(), 0).Transforms;

        for (var i = 0; i < 1000; i++)
        {
            var at = i * 16;
            var height = transforms[at + 5] * 2;
            var width = Math.Sqrt((transforms[at] * transforms[at]) + (transforms[at + 2] * transforms[at + 2]));
            Assert.InRange(height, 0.6, 4.0);
            Assert.InRange(width, 0.3 - 1e-6, 0.8 + 1e-6);
            Assert.Equal((height * 0.5) - 2.2, transforms[at + 13], tolerance: 1e-5);
            Assert.InRange(transforms[at + 12], -700, 700);
            Assert.Equal(transforms[at], transforms[at + 10]);
            Assert.Equal(-transforms[at + 2], transforms[at + 8]);
            Assert.Equal(1, transforms[at + 15]);
        }
    }

    [Fact]
    public void ASliderOnlyRebuildsTheCrowdOnceItIsLetGo()
    {
        var example = new CrowdExample();
        var before = Crowd(example, 0);
        var members = example.Control<Setting>("Members");
        var spread = example.Control<Setting>("Spread");

        members.Change(1000);
        spread.Change(40);
        var dragging = Crowd(example, 0);
        members.Settle?.Invoke(1000);
        spread.Settle?.Invoke(40);
        var scene = example.Scene(Camera, 0);
        var settled = Assert.Single(scene.Populations);

        Assert.Equal(before.Revision, dragging.Revision);
        Assert.Equal(100000, dragging.Count);
        Assert.Equal(1000, example.Control<Setting>("Members").Value);
        Assert.Equal(before.Revision + 1, settled.Revision);
        Assert.Equal(1000, settled.Count);
        Assert.Equal(-40, settled.Bounds.Minimum.X);
        Assert.Equal(40, settled.Bounds.Maximum.Z);
        Assert.Equal(48, scene.Objects[0].Transform[0], 12);
    }

    [Fact]
    public void DriftingRewritesTheBufferInPlaceTenTimesASecond()
    {
        var example = new CrowdExample();
        var still = Crowd(example, 0).Transforms[12];
        example.Control<Toggle>("Drifting").Change(true);

        var first = Crowd(example, 0.0);
        var drifted = first.Transforms[12];
        var driftedZ = first.Transforms[14];
        var sameBeat = Crowd(example, 0.05);
        var nextBeat = Crowd(example, 0.1);

        Assert.NotEqual(still, drifted);
        Assert.Equal(first.Revision, sameBeat.Revision);
        Assert.Equal(first.Revision + 1, nextBeat.Revision);
        Assert.Same(first.Transforms, nextBeat.Transforms);
        Assert.NotEqual(drifted, nextBeat.Transforms[12]);
        Assert.NotEqual(driftedZ, nextBeat.Transforms[14]);
    }

    private static Population Crowd(Example example, double seconds) =>
        Assert.Single(example.Scene(Camera, seconds).Populations);
}
