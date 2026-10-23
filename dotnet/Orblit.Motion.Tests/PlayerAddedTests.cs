namespace Orblit.Motion.Tests;

/// <summary>
/// Checks the C# adds to the ported player tests: the kind of failure, counts past an int, and the parts of
/// a player the ported tests never look at.
/// </summary>
public class PlayerAddedTests
{
    [Fact]
    public void AStepOfForeverOnAClipThatGoesOnIsRefusedRatherThanSpunOn()
    {
        foreach (var whenDone in new[] { WhenDone.Loop, WhenDone.Bounce })
        {
            var player = new ClipPlayer(new ClipDocument("Short", 1, whenDone: whenDone));
            player.Play();

            Assert.Throws<ArgumentOutOfRangeException>(() => player.Advance(double.PositiveInfinity));
        }
    }

    [Fact]
    public void AStepRepeatedPastWhatAnIntHoldsGoesTheWholeWay()
    {
        var far = RootStep.Repeated(new RootStep(new Vector3(1, 0, 0)), 3_000_000_000);

        Assert.Equal(3e9, far.Position.X, 1e-6);
        Assert.Equal(0, far.Position.Y, 1e-6);
    }

    [Fact]
    public void ASkeletonThatCannotBeBuiltIsRefusedAsABadArgument()
    {
        Matrix4 At(double x) => Matrix4.Compose(new Vector3(x, 0, 0), Quaternion.Identity(), Vector3.All(1));

        Assert.Throws<ArgumentException>(() => new RestSkeleton(["a", "a"], [-1, 0], [At(0), At(1)]));
        Assert.Throws<ArgumentException>(() => new RestSkeleton(["a", "b"], [1, -1], [At(0), At(1)]));
        Assert.Throws<ArgumentException>(() => new RestSkeleton(["a", "b"], [-1, 1], [At(0), At(1)]));
        Assert.Throws<ArgumentException>(() => new RestSkeleton(["a", "b", "c"], [-1, 0], [At(0), At(1), At(2)]));
    }

    [Fact]
    public void APausedPlayerStaysWhereItIsUntilItPlaysAgain()
    {
        var player = new ClipPlayer(new ClipDocument("Long", 4));
        player.Play();
        player.Advance(1);
        player.Pause();

        player.Advance(1);

        Assert.False(player.Playing);
        Assert.Equal(1, player.At, 1e-9);
    }

    [Fact]
    public void StoppingOrSeekingAFinishedPlayerTakesItOutOfFinished()
    {
        foreach (var move in new Action<ClipPlayer>[] { target => target.Stop(), target => target.Seek(0.5) })
        {
            var player = new ClipPlayer(new ClipDocument("Short", 1));
            player.Play();
            player.Advance(2);
            Assert.True(player.Finished);

            move(player);

            Assert.False(player.Finished);
        }
    }

    [Fact]
    public void PassingOverAStretchTakesTheMarksUnlessToldNot()
    {
        var clip = new ClipDocument("Marked", 1, marks: [new Mark(0.5, "middle")]);

        var passed = ClipPass.Over(clip, WhenDone.Loop, 0, 1);

        Assert.Equal(["middle"], passed.Marks.Select(mark => mark.Name));
        Assert.Empty(ClipPass.Over(clip, WhenDone.Loop, 0, 1, marks: false).Marks);
    }
}
