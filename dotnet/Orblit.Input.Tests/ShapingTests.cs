using Orblit.VectorMath;

namespace Orblit.Input.Tests;

public class ShapingTests
{
    private const double Tolerance = 1e-9;

    private static readonly Shaping Zone = new(inner: 0.2, outer: 0.9);

    private static readonly Shaping Round = new(inner: 0.2, outer: 1.0);

    [Fact]
    public void AStickAtRestIsExactlyNothing()
    {
        Assert.Equal(0, Zone.Stick(Vector2.Zero()).Length, Tolerance);
        Assert.Equal(0, Zone.Scalar(0), Tolerance);
    }

    [Fact]
    public void JitterInsideTheZoneIsNothing()
    {
        Assert.Equal(0, Zone.Stick(new Vector2(0.11, -0.07)).Length, Tolerance);
    }

    [Fact]
    public void DeflectionIsContinuousWhereTheZoneEnds()
    {
        var justOut = Zone.Stick(new Vector2(0.2001, 0));
        Assert.True(justOut.X < 0.01);
        Assert.True(justOut.X > 0);
    }

    [Fact]
    public void TheOutsideSaturatesBeforeTheCorner()
    {
        Assert.Equal(1, Zone.Stick(new Vector2(0.9, 0)).X, Tolerance);
        Assert.Equal(1, Zone.Stick(new Vector2(1.0, 0)).X, Tolerance);
    }

    [Fact]
    public void AScalarKeepsItsSign()
    {
        Assert.Equal(-1, Zone.Scalar(-0.9), Tolerance);
        Assert.Equal(1, Zone.Scalar(0.9), Tolerance);
    }

    [Fact]
    public void ASlowDiagonalSurvives()
    {
        var gentle = new Vector2(0.16, 0.16);
        Assert.True(gentle.X < 0.2, "neither axis alone clears it");
        Assert.True(Round.Stick(gentle).Length > 0);
    }

    [Theory]
    [InlineData(0.3)]
    [InlineData(0.6)]
    [InlineData(0.95)]
    public void DirectionIsNeverChangedByHowHardTheStickIsPushed(double magnitude)
    {
        var pushed = new Vector2(1, 2).Normalized() * magnitude;
        var shaped = Round.Stick(pushed);
        Assert.Equal(pushed.X / pushed.Y, shaped.X / shaped.Y, Tolerance);
    }

    [Fact]
    public void ADiagonalIsNotAllowedToOutrunAStraightPush()
    {
        var corner = Round.Stick(new Vector2(1, 1));
        Assert.True(corner.Length <= 1.0 + Tolerance);
    }

    [Fact]
    public void ACurveAboveOneGivesFinerControlNearTheMiddle()
    {
        var linear = new Shaping(inner: 0, outer: 1);
        var squared = new Shaping(inner: 0, outer: 1, curve: 2);
        Assert.True(squared.Scalar(0.5) < linear.Scalar(0.5));
        Assert.Equal(1, squared.Scalar(1), Tolerance);
        Assert.Equal(0, squared.Scalar(0), Tolerance);
    }

    [Fact]
    public void TheCurveIsAppliedAfterTheRescaleNotBefore()
    {
        var shaping = new Shaping(inner: 0.5, outer: 1, curve: 2);
        Assert.Equal(0, shaping.Scalar(0.5), Tolerance);
        Assert.Equal(1, shaping.Scalar(1.0), Tolerance);
    }

    [Fact]
    public void AStickOfNotANumberReadsAsAtRest()
    {
        var shaping = new Shaping();
        Assert.Equal(0, shaping.Stick(new Vector2(double.NaN, 0)).Length, Tolerance);
        Assert.Equal(0, shaping.Scalar(double.NaN), Tolerance);
        Assert.Equal(0, shaping.Scalar(double.PositiveInfinity), Tolerance);
    }

    [Fact]
    public void AZoneWithNoRoomBetweenItsEndsStillAnswers()
    {
        var pinched = new Shaping(inner: 0.5, outer: 0.5);
        Assert.Equal(1, pinched.Scalar(0.9), Tolerance);
        Assert.Equal(0, pinched.Scalar(0.1), Tolerance);
    }
}
