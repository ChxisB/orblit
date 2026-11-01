namespace Orblit.Motion.Tests;

public class PlayerTests
{
    [Fact]
    public void StartsPausedAndAPausedPlayerGoesNowhere()
    {
        var player = new ClipPlayer(Marked());
        var step = player.Advance(0.5);
        Assert.False(player.Playing);
        Assert.Equal(0, player.At);
        Assert.Empty(step.Marks);
        Assert.True(step.Moved.IsNone);
    }

    [Fact]
    public void PlaysToTheClipsOwnEndingUnlessToldAnother()
    {
        Assert.Equal(WhenDone.Loop, new ClipPlayer(Marked(WhenDone.Loop)).WhenDone);
        Assert.Equal(WhenDone.Bounce, new ClipPlayer(Marked(), WhenDone.Bounce).WhenDone);
    }

    [Fact]
    public void SamplesTheClipWhereThePlayheadIs()
    {
        var player = Playing(Marked());
        var step = player.Advance(0.25);
        Assert.Equal(0.25, step.Frame.At);
        Assert.Equal(0.25, Assert.IsType<double>(step.Frame.ValueOf(string.Empty, "light.power")), 1e-12);
        Assert.Equal(0.25, player.Sample().At);
    }

    [Fact]
    public void HoldingFiresEachMarkOnceTheFirstIncludedAndStopsAtTheEnd()
    {
        var player = Playing(Marked());
        Assert.Equal(["start"], Names(player.Advance(0.25)));
        Assert.Equal(["middle"], Names(player.Advance(0.5)));
        var last = player.Advance(0.5);
        Assert.Equal(["end"], Names(last));
        Assert.Equal(1, player.At);
        Assert.Equal(1, last.Frame.At);
        Assert.False(player.Playing);
        Assert.True(player.Finished);
        Assert.False(player.Released);

        Assert.Empty(player.Advance(1).Marks);
        Assert.Equal(1, player.At);
    }

    [Fact]
    public void HoldingPlayingAgainAfterTheEndClearsFinished()
    {
        var player = Playing(Marked());
        player.Advance(2);
        player.Play();
        Assert.False(player.Finished);
        Assert.True(player.Playing);
    }

    [Fact]
    public void ReleasingLetsGoAtTheEnd()
    {
        var player = new ClipPlayer(Marked(), WhenDone.Release);
        player.Play();
        player.Advance(2);
        Assert.True(player.Finished);
        Assert.True(player.Released);
    }

    [Fact]
    public void LoopingFiresTheMarksOfEveryPassTheStartOfEachIncluded()
    {
        var player = Playing(Marked(WhenDone.Loop));
        Assert.Equal(["start"], Names(player.Advance(0.25)));
        Assert.Equal(["middle", "end", "start"], Names(player.Advance(1)));
        Assert.Equal(0.25, player.At, 1e-12);
        Assert.True(player.Playing);
        Assert.False(player.Finished);
    }

    [Fact]
    public void LoopingShowsTheLastFrameWhenAStepLandsExactlyOnIt()
    {
        var player = Playing(Marked(WhenDone.Loop));
        Assert.Equal(["start", "middle", "end"], Names(player.Advance(1)));
        Assert.Equal(1, player.At);
        Assert.Equal(["start"], Names(player.Advance(0.25)));
        Assert.Equal(0.25, player.At, 1e-12);
    }

    [Fact]
    public void LoopingLosesNothingToAStepManyLapsLong()
    {
        var player = Playing(Marked(WhenDone.Loop));
        var all = Names(player.Advance(3.5));
        Assert.Equal(4, all.Count(name => name == "start"));
        Assert.Equal(4, all.Count(name => name == "middle"));
        Assert.Equal(3, all.Count(name => name == "end"));
        Assert.Equal(0.5, player.At, 1e-9);
    }

    [Fact]
    public void LoopingStopsCountingMarksPastAFewHundredEndsInOneStep()
    {
        var player = Playing(Marked(WhenDone.Loop, duration: 0.01));
        var step = player.Advance(1000);

        // A hundred thousand laps: a few hundred are played, the rest skipped.
        Assert.InRange(step.Marks.Count, 256, 300);
        Assert.InRange(player.At, 0, 0.01);
    }

    [Fact]
    public void LoopingALoopOfNoLengthDoesNotHang()
    {
        var player = Playing(Marked(WhenDone.Loop, duration: 0));
        player.Advance(1);
        Assert.Equal(0, player.At);
    }

    [Fact]
    public void BouncingTurnsAtEachEndWithMarksOnlyOnTheWayForwards()
    {
        var player = Playing(Marked(WhenDone.Bounce));
        Assert.Equal(["start", "middle", "end"], Names(player.Advance(1.25)));
        Assert.Equal(0.75, player.At, 1e-12);
        Assert.Equal(["start"], Names(player.Advance(1)));
        Assert.Equal(0.25, player.At, 1e-12);
        Assert.True(player.Playing);
    }

    [Fact]
    public void PlayingBackwardsFiresNoMarksAndFinishesAtTheStart()
    {
        var player = new ClipPlayer(Marked(), speed: -1);
        player.Seek(1);
        player.Play();
        Assert.Empty(player.Advance(0.75).Marks);
        Assert.Equal(0.25, player.At, 1e-12);
        Assert.Empty(player.Advance(1).Marks);
        Assert.Equal(0, player.At);
        Assert.True(player.Finished);
    }

    [Fact]
    public void SpeedScalesHowFarAStepGoes()
    {
        var player = Playing(Marked(), speed: 2);
        Assert.Equal(["start", "middle"], Names(player.Advance(0.25)));
        Assert.Equal(0.5, player.At);
    }

    [Fact]
    public void SeekingFiresNothingAndIsKeptWithinTheClip()
    {
        var player = new ClipPlayer(Marked());
        player.Seek(0.75);
        Assert.Equal(0.75, player.At);
        player.Play();
        Assert.Empty(player.Advance(0.1).Marks);

        player.Seek(5);
        Assert.Equal(1, player.At);
        player.Seek(-1);
        Assert.Equal(0, player.At);
    }

    [Fact]
    public void SeekingStoppingGoesBackToTheStartPaused()
    {
        var player = Playing(Marked(WhenDone.Bounce));
        player.Advance(1.5);
        player.Stop();
        Assert.Equal(0, player.At);
        Assert.False(player.Playing);

        // Forwards again, whichever way the bounce was going.
        player.Play();
        Assert.Equal(["start", "middle"], Names(player.Advance(0.5)));
    }

    /// <summary>A second of a light coming up, with a mark at each end and the middle.</summary>
    private static ClipDocument Marked(WhenDone whenDone = WhenDone.Hold, double duration = 1) => new(
        "Steps",
        duration,
        whenDone,
        channels:
        [
            new ClipChannel<double>(
                string.Empty,
                "light.power",
                ChannelKind.Number,
                [new Key<double>(0, 0, Hold.Linear), new Key<double>(1, 1)]),
        ],
        marks: [new Mark(0, "start"), new Mark(0.5, "middle"), new Mark(1, "end")]);

    private static List<string> Names(ClipStep step) => [.. step.Marks.Select(mark => mark.Name)];

    private static ClipPlayer Playing(ClipDocument clip, double speed = 1)
    {
        var player = new ClipPlayer(clip, speed: speed);
        player.Play();
        return player;
    }
}
