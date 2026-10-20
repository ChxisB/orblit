using Orblit.VectorMath;

namespace Orblit.Particles.Tests;

public class TrailTests
{
    private readonly Trail _trail = new(life: 1, rate: 4, width: [0.4, 0]);
    private readonly TrailStrip _strip;
    private readonly TrailRing _ring;

    public TrailTests()
    {
        _strip = new TrailStrip(_trail.Points);
        _ring = new TrailRing(_trail);
    }

    // a trail

    [Fact]
    public void HoldsAPointForEachSampleInItsLifeAndOneToStart()
    {
        Assert.Equal(6, new Trail(life: 0.5, rate: 10).Points);
        Assert.Equal(4, new Trail(life: 1, rate: 2.5).Points);
    }

    [Fact]
    public void BadSettingsAreRefusedWhereTheTrailIsMade()
    {
        var bad = new Dictionary<string, Func<Trail>>
        {
            ["life"] = () => new Trail(life: 0),
            ["nan life"] = () => new Trail(life: double.NaN),
            ["rate"] = () => new Trail(rate: 0),
            ["infinite rate"] = () => new Trail(rate: double.PositiveInfinity),
            ["no width"] = () => new Trail(width: []),
            ["negative width"] = () => new Trail(width: [0.1, -1]),
            ["too many points"] = () => new Trail(life: 10, rate: 60),
            ["too many to count"] = () => new Trail(life: 1e30, rate: 1),
            ["overflowing"] = () => new Trail(life: 1e200, rate: 1e200),
        };
        foreach (var entry in bad)
        {
            Assert.True(Record.Exception(() => entry.Value()) is ArgumentException, entry.Key);
        }
    }

    [Fact]
    public void AStripWithRoomForFewerThanTwoPointsIsRefused()
    {
        Assert.ThrowsAny<ArgumentException>(() => new TrailStrip(1));
        Assert.ThrowsAny<ArgumentException>(() => new TrailStrip(-1));
    }

    [Fact]
    public void AListHandedInCannotBeChangedFromOutsideAfterwards()
    {
        var width = new List<double> { 0.2, 0 };
        var trail = new Trail(width: width);

        width.Clear();

        Assert.Equal([0.2, 0], trail.Width);
    }

    // a trail along a known path

    [Fact]
    public void RunsFromTheHeadBackAlongWhereThePointHasBeen()
    {
        _trail.SampleAlong(3, Along, _strip);

        Assert.Equal([6, 5.5, 5, 4.5, 4], Xs(_strip));
    }

    [Fact]
    public void TapersFromTheFirstWidthToTheLastAcrossTheLife()
    {
        _trail.SampleAlong(3, Along, _strip);

        Assert.Equal(0.4, _strip.Widths[0], 1e-6);
        Assert.Equal(0.2, _strip.Widths[2], 1e-6);
        Assert.Equal(0, _strip.Widths[4], 1e-6);
    }

    [Fact]
    public void HasNoPointsFromBeforeTimeBegan()
    {
        _trail.SampleAlong(0.6, Along, _strip);

        double[] expected = [1.2, 0.7, 0.2];
        var actual = Xs(_strip);
        Assert.Equal(expected.Length, actual.Length);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i], 1e-6);
        }

        Assert.Equal(3, _strip.Count);
    }

    [Fact]
    public void ASeekGivesTheSameStripWithNothingKeptSoNoJump()
    {
        _trail.SampleAlong(3, Along, _strip);
        var first = Xs(_strip);

        _trail.SampleAlong(40, Along, _strip);
        _trail.SampleAlong(0.2, Along, _strip);
        _trail.SampleAlong(3, Along, _strip);

        Assert.Equal(first, Xs(_strip));
    }

    [Fact]
    public void ATimeThatIsNotATimeGivesAnEmptyStrip()
    {
        _trail.SampleAlong(-1, Along, _strip);
        Assert.Equal(0, _strip.Count);
        _trail.SampleAlong(double.NaN, Along, _strip);
        Assert.Equal(0, _strip.Count);
    }

    // The same name is in both Dart groups, so the C# names say which one.
    [Fact]
    public void KnownPathAStripTooSmallGetsTheNewestPoints()
    {
        var small = new TrailStrip(2);

        _trail.SampleAlong(3, Along, small);

        Assert.Equal([6, 5.5], Xs(small));
    }

    // a trail that remembers

    [Fact]
    public void ReachesThePointAsItIsNowAndKeepsOlderSamplesApart()
    {
        Run(0, 1);
        _ring.Fill(_strip);

        Assert.Equal(2, _strip.Positions[0], 1e-6);
        Assert.True(_strip.Count > 2);

        // The head is the point as it is. The ones kept behind it are a sample
        // period apart.
        for (var i = 2; i < _strip.Count; i++)
        {
            Assert.True(
                _strip.Positions[(i - 1) * 3] - _strip.Positions[i * 3] >= 0.45,
                "samples are at least a sample period apart");
        }
    }

    [Fact]
    public void KeepsEveryFrameWhenTheClockRunsAtTheTrailsOwnRate()
    {
        var steady = new Trail(life: 0.5, rate: 60);
        var every = new TrailRing(steady);
        var all = new TrailStrip(steady.Points);

        for (var frame = 0; frame <= 120; frame++)
        {
            every.Record(frame / 60.0, Along(frame / 60.0));
        }

        every.Fill(all);

        Assert.Equal(steady.Points, all.Count);
    }

    [Fact]
    public void SpacesSamplesAsTheKnownPathDoesWhenRateDoesNotDivide()
    {
        var odd = new Trail(life: 1, rate: 2.5);
        var remembered = new TrailRing(odd);
        var fromRing = new TrailStrip(odd.Points);
        var fromPath = new TrailStrip(odd.Points);

        for (var frame = 0; frame <= 180; frame++)
        {
            remembered.Record(frame / 60.0, Along(frame / 60.0));
        }

        remembered.Fill(fromRing);
        odd.SampleAlong(3, Along, fromPath);

        Assert.Equal(fromPath.Count, fromRing.Count);
    }

    [Fact]
    public void LetsGoOfSamplesOlderThanTheLife()
    {
        Run(0, 5);
        _ring.Fill(_strip);

        Assert.True(_strip.Positions[(_strip.Count - 1) * 3] > 10 - 2.01);
    }

    [Fact]
    public void ASeekBackClearsIt()
    {
        Run(0, 3);
        _ring.Record(1, Along(1));
        _ring.Fill(_strip);

        Assert.Equal(1, _strip.Count);
        Assert.Equal(2, _strip.Positions[0]);
    }

    [Fact]
    public void AJumpPastTheWholeLifeClearsIt()
    {
        Run(0, 1);
        _ring.Record(9, Along(9));
        _ring.Fill(_strip);

        Assert.Equal(1, _strip.Count);
        Assert.Equal(18, _strip.Positions[0]);
    }

    [Fact]
    public void ClearEmptiesIt()
    {
        Run(0, 1);
        _ring.Clear();
        _ring.Fill(_strip);

        Assert.Equal(0, _strip.Count);
    }

    [Fact]
    public void ATimeThatIsNotATimeIsLeftOut()
    {
        Run(0, 1);
        _ring.Record(double.NaN, new Vector3(99, 0, 0));
        _ring.Record(-1, new Vector3(99, 0, 0));
        _ring.Fill(_strip);

        Assert.Equal(2, _strip.Positions[0], 1e-6);
    }

    [Fact]
    public void RememberingAStripTooSmallGetsTheNewestPoints()
    {
        Run(0, 1);
        var small = new TrailStrip(2);

        _ring.Fill(small);

        Assert.Equal(2, small.Count);
        Assert.Equal(2, small.Positions[0], 1e-6);
    }

    /// <summary>A point moving along x at two metres a second.</summary>
    private static Vector3 Along(double time) => new(2 * time, 0, 0);

    private static double[] Xs(TrailStrip strip) =>
        [.. Enumerable.Range(0, strip.Count).Select(i => strip.Positions[i * 3])];

    private void Run(double from, double to, double dt = 0.05)
    {
        for (var t = from; t <= to + 1e-9; t += dt)
        {
            _ring.Record(t, Along(t));
        }
    }
}
