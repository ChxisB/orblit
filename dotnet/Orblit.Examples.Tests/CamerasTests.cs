namespace Orblit.Examples.Tests;

/// <summary>
/// The cameras example away from what the fixture reaches. The fixture never
/// changes shot part way through a run, so it never sees a blend, and it reads
/// one frame after each move. The digests come from a one-off run of the previous
/// example over the same frames.
/// </summary>
public sealed class CamerasTests
{
    [Fact]
    public void ALinearBlendToTheOrbitMatchesTheFixture()
    {
        var example = new CamerasExample();

        var digest = Run(example, 2, atOneSecond: () =>
        {
            Pick(example, "Blend", "Linear");
            example.Control<Setting>("Blend time").Change(2);
            Pick(example, "", "Orbit");
        });

        Assert.Equal("de82f1b42e0d1461", digest);
    }

    /// <summary>
    /// A dead zone narrow enough for the subject to leave it upwards. The
    /// fixture's widest one holds the subject anywhere the chase puts it.
    /// </summary>
    [Fact]
    public void ANarrowDeadZoneMatchesTheFixture()
    {
        var example = new CamerasExample();
        example.Control<Setting>("Dead zone").Change(0.05);

        Assert.Equal("30afc9f799115266", Run(example, 5, atOneSecond: () => { }));
    }

    /// <summary>
    /// The subject nears the level's edge at three and a half seconds, and the
    /// flat shot stops at it rather than following.
    /// </summary>
    [Fact]
    public void TheFlatShotStopsAtTheLevelsEdgeAsInTheFixture()
    {
        var example = new CamerasExample();
        Pick(example, "Live", "Flat");

        Assert.Equal("a60fb410c9060d8b", Run(example, 4, atOneSecond: () => { }));
    }

    [Fact]
    public void ACutHasNoBlendTime()
    {
        var example = new CamerasExample();

        Pick(example, "Blend", "Cut");

        Assert.DoesNotContain(example.Settings(), setting => setting.Label == "Blend time");
    }

    [Fact]
    public void EachRowShowsOnlyTheShotsItHolds()
    {
        var example = new CamerasExample();
        Assert.Equal("", example.Control<Choice>("").Selected);

        Pick(example, "", "Orbit");

        Assert.Equal("", example.Control<Choice>("Live").Selected);
        Assert.Equal("Orbit", example.Control<Choice>("").Selected);
    }

    /// <summary>
    /// What the gallery draws over the frame. The previous code worked this out inside a
    /// painter, so no test reached it.
    /// </summary>
    [Fact]
    public void TheFramingFollowsTheLiveShotAndHidesWhenAsked()
    {
        var example = new CamerasExample { Aspect = 2 };
        Run(example, 1, atOneSecond: () => { });

        var chase = example.Framing;
        Assert.NotNull(chase);
        Assert.Equal(("Chase", false, true), (chase.Label, chase.Blending, chase.Subject.InFront));
        Assert.Equal(0.45, chase.Guides.ScreenY);
        // The chase shot keeps its subject inside the soft zone.
        var soft = chase.Guides.Soft;
        Assert.InRange((chase.Subject.X + 1) / 2, soft.Left, soft.Left + soft.Width);
        Assert.InRange((1 - chase.Subject.Y) / 2, soft.Top, soft.Top + soft.Height);

        Pick(example, "", "Watchtower");
        example.Scene(example.ViewPoint.ToRenderCamera(), 61 / 60.0);
        Assert.Equal(("Watchtower", true), (example.Framing?.Label, example.Framing?.Blending ?? false));

        Pick(example, "", "Orbit");
        example.Scene(example.ViewPoint.ToRenderCamera(), 62 / 60.0);
        Assert.Null(example.Framing);

        Pick(example, "", "Watchtower");
        example.Scene(example.ViewPoint.ToRenderCamera(), 63 / 60.0);
        example.Control<Toggle>("Show framing").Change(false);
        Assert.Null(example.Framing);
    }

    /// <summary>
    /// A dead zone is a fraction of the frame, so the subject's place across it
    /// depends on how wide the frame is. A surface not laid out yet is no ratio.
    /// </summary>
    [Fact]
    public void TheAspectIsTheFramesAndOnlyARealRatioIsTaken()
    {
        var example = new CamerasExample { Aspect = 2 };
        Run(example, 1, atOneSecond: () => { });
        var wide = example.Framing!.Subject.X; // the chase shot has guides, asserted above

        example.Aspect = 0;
        example.Aspect = double.NaN;
        Assert.Equal(2, example.Aspect);
        example.Aspect = 1;

        Assert.Equal(wide * 2, example.Framing!.Subject.X, 9); // the same shot, a squarer frame
    }

    private static void Pick(Example example, string label, string option)
    {
        var select = example.Control<Choice>(label).Select;
        Assert.NotNull(select);
        select(option);
    }

    /// <summary>
    /// Draws every frame at 60 a second up to <paramref name="seconds"/>, as the
    /// gallery would, and digests the last. The camera carries its state from
    /// one frame to the next, so the frames before it count.
    /// </summary>
    private static string Run(Example example, int seconds, Action atOneSecond)
    {
        for (var frame = 0; frame < seconds * 60; frame++)
        {
            if (frame == 60)
            {
                atOneSecond();
            }

            example.Scene(example.ViewPoint.ToRenderCamera(), frame / 60.0);
        }

        return SceneDigest.Of(SceneDigest.Message(example, seconds));
    }
}
