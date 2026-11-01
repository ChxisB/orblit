using Orblit.VectorMath;

namespace Orblit.Terrain.Tests;

public class DistancePolicyTests
{
    /// <summary>Costs are frame times in milliseconds: over 16 is too slow and under 12 has room.</summary>
    private static DistancePolicy Policy(int ceiling = 4, int floor = 1, double settle = 2) =>
        new(ceiling: ceiling, floor: floor, high: 16, low: 12, settle: settle);

    /// <summary>Feeds <paramref name="cost"/> every tenth of a second from <paramref name="from"/> up to and including <paramref name="to"/>.</summary>
    private static void Feed(DistancePolicy policy, double cost, double from, double to)
    {
        for (var tenth = Doubles.Round(from * 10); tenth <= Doubles.Round(to * 10); tenth++)
        {
            policy.Update(cost, tenth / 10.0);
        }
    }

    // a distance policy

    [Fact]
    public void StartsAtTheFullDistance()
    {
        var p = Policy();

        Assert.Equal(4, p.Steps);
        Assert.Equal(1, p.Fraction);
    }

    [Fact]
    public void StepsDownOneAtATimeOnceEverySettle()
    {
        var p = Policy();

        Feed(p, 30, 0, 1.9);
        Assert.True(p.Steps == 4, "nothing inside the first interval");
        Feed(p, 30, 2, 3.9);
        Assert.Equal(3, p.Steps);
        Feed(p, 30, 4, 4);
        Assert.Equal(2, p.Steps);
    }

    [Fact]
    public void DecidesWhenSettleSecondsHavePassedExactly()
    {
        var p = Policy();

        p.Update(30, 0);
        p.Update(30, 1.99);
        Assert.Equal(4, p.Steps);
        p.Update(30, 2);
        Assert.Equal(3, p.Steps);
    }

    [Fact]
    public void NeverGoesBelowTheFloor()
    {
        var p = Policy(floor: 2);

        Feed(p, 30, 0, 30);

        Assert.Equal(2, p.Steps);
        Assert.Equal(0.5, p.Fraction);
    }

    [Fact]
    public void StepsUpWhenThereIsRoomToTheCeilingAndNoFurther()
    {
        var p = Policy();
        Feed(p, 30, 0, 6);
        Assert.Equal(1, p.Steps);

        Feed(p, 5, 6.1, 8);
        Assert.Equal(2, p.Steps);
        Feed(p, 5, 8.1, 40);
        Assert.Equal(4, p.Steps);
    }

    [Fact]
    public void HoldsWhenTheCostIsBetweenTheTwoThresholds()
    {
        var p = Policy();
        Feed(p, 14, 0, 20);

        Assert.Equal(4, p.Steps);
    }

    [Fact]
    public void ACostExactlyOnAThresholdHolds()
    {
        var p = Policy();
        Feed(p, 16, 0, 20);
        Assert.True(p.Steps == 4, "high is the most that is still fine");

        var q = Policy();
        Feed(q, 30, 0, 6);
        Assert.Equal(1, q.Steps);
        Feed(q, 12, 6.1, 30);
        Assert.True(q.Steps == 1, "low is the least that has no room");
    }

    [Fact]
    public void JudgesTheMeanOfTheIntervalNotItsLastFrame()
    {
        var p = Policy();

        Feed(p, 14, 0, 1.9);
        p.Update(30, 2);

        Assert.Equal(4, p.Steps);
    }

    [Fact]
    public void DoesNotStepBackUpOnTheCostItsOwnStepBrought()
    {
        // A cost of 14 is under 16, so it is no longer too slow, and over 12, so
        // there is still no room. One threshold would flip here.
        var p = Policy();
        Feed(p, 30, 0, 2);
        Assert.Equal(3, p.Steps);

        Feed(p, 14, 2.1, 30);

        Assert.Equal(3, p.Steps);
    }

    [Fact]
    public void OneSlowFrameInAnIntervalMovesNothing()
    {
        var p = Policy();

        Feed(p, 10, 0, 1.9);
        p.Update(100, 1.95);
        Feed(p, 10, 2, 2);

        Assert.Equal(4, p.Steps);
    }

    [Fact]
    public void AnIntervalIsJudgedOnItsOwnFrames()
    {
        // If the first interval's cost were carried over, the second would not
        // be low enough to step back up.
        var p = Policy();
        Feed(p, 30, 0, 2);
        Assert.Equal(3, p.Steps);

        Feed(p, 5, 2.1, 4);

        Assert.Equal(4, p.Steps);
    }

    [Fact]
    public void StartsCountingAtItsFirstFrameWhateverTheClockSays()
    {
        var p = Policy();

        p.Update(30, 100);
        Assert.Equal(4, p.Steps);
        p.Update(30, 102);
        Assert.Equal(3, p.Steps);
    }

    [Fact]
    public void RefusesWhatCannotWork()
    {
        static DistancePolicy Make(int floor = 1, double low = 12, double s = 2) =>
            new(ceiling: 4, floor: floor, high: 16, low: low, settle: s);

        Assert.ThrowsAny<ArgumentException>(() => Make(floor: 0));
        Assert.ThrowsAny<ArgumentException>(() => Make(floor: 5));
        Assert.ThrowsAny<ArgumentException>(() => Make(low: 16));
        Assert.ThrowsAny<ArgumentException>(() => Make(low: 20));
        Assert.ThrowsAny<ArgumentException>(() => Make(s: 0));
        Assert.ThrowsAny<ArgumentException>(() => Make(s: double.NaN));
        Assert.ThrowsAny<ArgumentException>(() => Make(low: double.NaN));
    }
}
