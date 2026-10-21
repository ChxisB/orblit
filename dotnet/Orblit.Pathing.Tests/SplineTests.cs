using Orblit.VectorMath;

namespace Orblit.Pathing.Tests;

public class SplineTests
{
    private static readonly Spline Path = Spline.Bezier(
        [
            new PathKnot(new Vector3(0, 0, 0), tilt: 0.25, handleOut: new Vector3(1, 0, 0)),
            new PathKnot(new Vector3(5, 1, 2), handleIn: new Vector3(-1, 0, 0)),
            new PathKnot(new Vector3(9, 0, 0)),
        ],
        closed: true);

    // length and position

    [Fact]
    public void ALineBetweenTwoPointsIsAsLongAsTheGap()
    {
        var line = Spline.Line([new Vector3(0, 0, 0), new Vector3(10, 0, 0)]);
        Assert.Equal(10, line.Length, 1e-9);
        Assert.Equal(5, line.PositionAt(5).X, 1e-9);
    }

    [Fact]
    public void AClosedSquareIsFourSidesLongAndWraps()
    {
        var square = Spline.Line(
            [new Vector3(0, 0, 0), new Vector3(2, 0, 0), new Vector3(2, 0, 2), new Vector3(0, 0, 2)],
            closed: true);
        Assert.Equal(8, square.Length, 1e-9);
        Assert.True((square.PositionAt(9) - square.PositionAt(1)).Length < 1e-9);
        Assert.True((square.PositionAt(-1) - square.PositionAt(7)).Length < 1e-9);
    }

    [Fact]
    public void AnOpenCurveHoldsAtItsEnds()
    {
        var line = Spline.Line([new Vector3(0, 0, 0), new Vector3(4, 0, 0)]);
        Assert.Equal(0, line.PositionAt(-3).X);
        Assert.Equal(4, line.PositionAt(40).X, 1e-9);
    }

    [Fact]
    public void ACurveThroughPointsPassesThroughThem()
    {
        Vector3[] points = [new(0, 0, 0), new(3, 1, 0), new(5, 0, 4), new(1, 2, 6)];
        var curve = Spline.Through(points);
        foreach (var point in points)
        {
            Assert.True(curve.Closest(point).Offset < 0.02);
        }
    }

    [Fact]
    public void DistanceIsMetresHoweverThePointsAreSpaced()
    {
        var curve = Spline.Through(
            [new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 0, 1), new Vector3(20, 0, 1), new Vector3(21, 0, 5)]);
        const int steps = 120;
        var step = curve.Length / steps;
        for (var i = 0; i < steps; i++)
        {
            var gap = (curve.PositionAt((i + 1) * step) - curve.PositionAt(i * step)).Length;
            Assert.Equal(step, gap, step * 0.05);
        }
    }

    [Fact]
    public void ABezierWithHandlesIsLongerThanItsChord()
    {
        var bent = Spline.Bezier(
        [
            new PathKnot(new Vector3(0, 0, 0), handleOut: new Vector3(0, 0, 4)),
            new PathKnot(new Vector3(10, 0, 0), handleIn: new Vector3(0, 0, 4)),
        ]);
        Assert.True(bent.Length > 10.5);
        Assert.True(bent.PositionAt(bent.Length / 2).Z > 2);
    }

    [Fact]
    public void NeedsTwoKnots()
    {
        Assert.ThrowsAny<ArgumentException>(() => Spline.Line([Vector3.Zero()]));
    }

    [Fact]
    public void RefusesAKnotThatIsNotFinite()
    {
        Assert.ThrowsAny<ArgumentException>(() => new PathKnot(new Vector3(double.NaN, 0, 0)));
        Assert.ThrowsAny<ArgumentException>(() => new PathKnot(Vector3.Zero(), tilt: double.PositiveInfinity));
    }

    [Fact]
    public void NeedsATiltForEachPointOrNone()
    {
        Assert.ThrowsAny<ArgumentException>(
            () => Spline.Line([Vector3.Zero(), new Vector3(1, 0, 0)], tilts: [0]));
    }

    // frames

    [Fact]
    public void ForwardFollowsTheLineAndUpIsWorldUp()
    {
        var line = Spline.Line([new Vector3(0, 0, 0), new Vector3(0, 0, -10)]);
        var frame = line.FrameAt(4);
        Assert.Equal(-1, frame.Forward.Z, 1e-9);
        Assert.Equal(1, frame.Up.Y, 1e-9);
        Assert.Equal(1, frame.Right.X, 1e-9);
    }

    [Fact]
    public void UpStaysSquareToForwardOnAClimb()
    {
        var climb = Spline.Line([new Vector3(0, 0, 0), new Vector3(10, 10, 0)]);
        var frame = climb.FrameAt(3);
        Assert.Equal(0, frame.Forward.Dot(frame.Up), 1e-9);
        Assert.True(frame.Up.Y > 0.7);
    }

    [Fact]
    public void TiltRollsUpTowardsTheRightAndEasesBetweenKnots()
    {
        var bank = Spline.Line(
            [new Vector3(0, 0, 0), new Vector3(0, 0, -10)],
            tilts: [0, Math.PI / 2]);
        Assert.Equal(1, bank.FrameAt(0).Up.Y, 1e-9);
        var end = bank.FrameAt(10);
        Assert.Equal(0, end.Up.Y, 1e-9);
        Assert.Equal(1, end.Up.X, 1e-9);
        var half = bank.FrameAt(5);
        Assert.Equal(Math.Cos(Math.PI / 4), half.Up.Y, 1e-3);
        Assert.Equal(1, half.Up.Length, 1e-9);
    }

    [Fact]
    public void APathStraightUpStillHasAnUp()
    {
        var rise = Spline.Line([new Vector3(0, 0, 0), new Vector3(0, 8, 0)]);
        var frame = rise.FrameAt(4);
        Assert.Equal(1, frame.Up.Length, 1e-9);
        Assert.Equal(0, frame.Forward.Dot(frame.Up), 1e-9);
    }

    // closest

    [Fact]
    public void FindsTheFootOfThePerpendicularAndTheWayOff()
    {
        var line = Spline.Line([new Vector3(0, 0, 0), new Vector3(10, 0, 0)]);
        var near = line.Closest(new Vector3(6, 3, 0));
        Assert.Equal(6, near.Distance, 1e-6);
        Assert.Equal(3, near.Offset, 1e-6);
        Assert.Equal(6, near.Position.X, 1e-6);
    }

    [Fact]
    public void ClampsToTheEndOfAnOpenPath()
    {
        var line = Spline.Line([new Vector3(0, 0, 0), new Vector3(10, 0, 0)]);
        Assert.Equal(10, line.Closest(new Vector3(40, 0, 0)).Distance, 1e-9);
        Assert.Equal(0, line.Closest(new Vector3(-4, 0, 0)).Distance, 1e-9);
    }

    // the file

    [Fact]
    public void RoundTrips()
    {
        var back = Spline.Decode(Path.Encode());
        Assert.True(back.Closed);
        Assert.Equal(3, back.Knots.Count);
        Assert.Equal(0.25, back.Knots[0].Tilt);
        Assert.Equal(1, back.Knots[0].HandleOut.X);
        Assert.Equal(-1, back.Knots[1].HandleIn.X);
        Assert.Equal(Path.Length, back.Length, 1e-9);
    }

    [Fact]
    public void PutsAKnotOnEachLine()
    {
        var lines = Path.Encode().Split('\n');
        Assert.Equal(3, lines.Count(line => line.Contains("\"at\"", StringComparison.Ordinal)));
    }

    [Fact]
    public void NamesItsKindAndVersion()
    {
        Assert.Equal(Spline.Marker, Path.ToJson()["kind"]);
        Assert.Equal(Spline.FormatVersion, Path.ToJson()["formatVersion"]);
    }

    [Fact]
    public void RefusesWhatIsNotAPath()
    {
        Assert.Throws<PathFormatException>(() => Spline.Decode("{\"kind\":\"orblit.terrain\"}"));
        Assert.Throws<PathFormatException>(() => Spline.Decode("nope"));
    }

    [Fact]
    public void RefusesANewerVersion()
    {
        Assert.Throws<PathFormatException>(
            () => Spline.Decode("{\"kind\":\"orblit.path\",\"formatVersion\":99,\"knots\":[]}"));
    }

    [Fact]
    public void RefusesAKnotThatIsNotNumbers()
    {
        Assert.Throws<PathFormatException>(() => Spline.Decode(
            "{\"kind\":\"orblit.path\",\"formatVersion\":1,"
            + "\"knots\":[{\"at\":[0,0,0]},{\"at\":[1,\"x\",0]}]}"));
    }

    [Fact]
    public void UsesTheOpathExtension()
    {
        Assert.Equal(".opath", PathNames.Extension);
    }
}
