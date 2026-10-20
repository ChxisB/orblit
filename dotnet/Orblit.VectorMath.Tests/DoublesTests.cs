namespace Orblit.VectorMath.Tests;

public class DoublesTests
{
    [Fact]
    public void NotANumberIsAboveEveryNumberAndEqualToItself()
    {
        Assert.Equal(1, Doubles.Compare(double.NaN, double.PositiveInfinity));
        Assert.Equal(-1, Doubles.Compare(double.NegativeInfinity, double.NaN));
        Assert.Equal(0, Doubles.Compare(double.NaN, double.NaN));
    }

    [Fact]
    public void NegativeZeroIsBelowPositiveZero()
    {
        Assert.Equal(-1, Doubles.Compare(-0.0, 0.0));
        Assert.Equal(1, Doubles.Compare(0.0, -0.0));
        Assert.Equal(0, Doubles.Compare(0.0, 0.0));
        Assert.Equal(0, Doubles.Compare(-0.0, -0.0));
    }

    [Fact]
    public void ANumberInsideTheLimitsComesBackAsItIs()
    {
        Assert.Equal(0.25, Doubles.Clamp(0.25, 0, 1));
        Assert.Equal(0, Doubles.Clamp(-3, 0, 1));
        Assert.Equal(1, Doubles.Clamp(3, 0, 1));
    }

    [Fact]
    public void NotANumberClampsToTheUpperLimit() => Assert.Equal(1, Doubles.Clamp(double.NaN, 0, 1));

    [Fact]
    public void NegativeZeroClampsUpToPositiveZero() =>
        Assert.True(double.IsPositive(Doubles.Clamp(-0.0, 0, 1)), "-0.0 is below 0.0 in Dart's order");

    [Fact]
    public void ALowerLimitAboveTheUpperOneIsRefused() =>
        Assert.Throws<ArgumentException>(() => Doubles.Clamp(0.5, 1, 0));
}
