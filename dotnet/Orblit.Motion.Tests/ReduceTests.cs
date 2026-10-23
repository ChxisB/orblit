namespace Orblit.Motion.Tests;

public class ReduceTests
{
    [Fact]
    public void ThinsADenseCurveToWithinTheDistanceAskedFor()
    {
        var dense = ClipSamples.Moves(
            "hips",
            [.. Enumerable.Range(0, 201).Select(i => (i / 100.0, new Vector3(Math.Sin(i / 100.0 * Math.PI), 0, 0.2 * i / 100.0)))]);
        var reduced = ClipSamples.Only<Vector3>(KeyReducer.Reduce(ClipOf(dense)));

        // Half a millimetre of a sine one metre tall lets about every other key go.
        Assert.True(reduced.Keys.Count < dense.Keys.Count * 0.7);
        Assert.Equal(0, reduced.Keys[0].At);
        Assert.Equal(2, reduced.Keys[^1].At);

        for (var i = 0; i < 200; i++)
        {
            foreach (var at in new[] { i / 100.0, (i + 0.5) / 100.0 })
            {
                var off = (reduced.Curve.At(at) - dense.Curve.At(at)).Length;
                Assert.True(off <= 5e-4 + 1e-12, $"off by {off} at {at}");
            }
        }
    }

    [Fact]
    public void ALooserDistanceKeepsFewerKeysThanATighterOne()
    {
        var dense = ClipSamples.Moves(
            "hips",
            [.. Enumerable.Range(0, 101).Select(i => (i / 50.0, new Vector3(Math.Sin(i / 50.0 * Math.PI), 0, 0)))]);

        int Kept(double distance) =>
            ClipSamples.Only<Vector3>(KeyReducer.Reduce(ClipOf(dense), distance: distance)).Keys.Count;

        Assert.True(Kept(0.05) < Kept(0.001));
    }

    [Fact]
    public void KeepsARotationInsideTheAngleAskedFor()
    {
        var dense = ClipSamples.Turns(
            "spine",
            [.. Enumerable.Range(0, 121).Select(i => (i / 60.0, ClipSamples.About(0.2, 1, 0, 40 * Math.Sin(i / 60.0 * Math.PI))))]);
        var reduced = ClipSamples.Only<Quaternion>(KeyReducer.Reduce(ClipOf(dense), angle: 1e-3));

        Assert.True(reduced.Keys.Count < dense.Keys.Count);
        for (var i = 0; i < 120; i++)
        {
            var at = (i + 0.5) / 60;
            var off = ClipSamples.TurnBetween(reduced.Curve.At(at), dense.Curve.At(at));
            Assert.True(off <= 1e-3 + 1e-9, $"off by {off} at {at}");
        }
    }

    [Fact]
    public void KeepsAStraightRunAsItsTwoEnds()
    {
        var line = ClipSamples.Moves(
            "hips",
            [.. Enumerable.Range(0, 11).Select(i => (i / 5.0, new Vector3(i * 0.1, 0, 0)))]);
        Assert.Equal(2, ClipSamples.Only<Vector3>(KeyReducer.Reduce(ClipOf(line))).Keys.Count);
    }

    [Fact]
    public void LeavesAnEasedChannelAsItWas()
    {
        var eased = new ClipChannel<Vector3>(
            string.Empty,
            "position",
            ChannelKind.Vector,
            [
                new Key<Vector3>(0, Vector3.Zero(), Hold.Smooth),
                new Key<Vector3>(1, new Vector3(0.5, 0, 0), Hold.Smooth),
                new Key<Vector3>(2, new Vector3(1, 0, 0), Hold.Smooth),
            ],
            "hips");
        Assert.Equal(3, ClipSamples.Only<Vector3>(KeyReducer.Reduce(ClipOf(eased))).Keys.Count);
    }

    [Fact]
    public void LeavesAChannelWithACutAsItWas()
    {
        var cut = ClipSamples.Moves(
            "hips",
            (0, Vector3.Zero()),
            (1, new Vector3(1, 0, 0)),
            (1, new Vector3(1, 1, 0)),
            (2, new Vector3(1, 2, 0)));
        Assert.Equal(4, ClipSamples.Only<Vector3>(KeyReducer.Reduce(ClipOf(cut))).Keys.Count);
    }

    [Fact]
    public void LeavesEveryOtherPartOfTheClipAlone()
    {
        var clip = ClipOf(ClipSamples.Moves(
            "hips",
            [.. Enumerable.Range(0, 9).Select(i => (i / 4.0, new Vector3(i * 0.1, 0, 0)))]))
            .CopyWith(marks: [new Mark(0.5, "step")]);
        var reduced = KeyReducer.Reduce(clip);
        Assert.Single(reduced.Marks);
        Assert.Equal(clip.Duration, reduced.Duration);
        Assert.Equal(clip.Name, reduced.Name);
    }

    private static ClipDocument ClipOf(ClipChannel channel) => new("Reduced", 2, channels: [channel]);
}
