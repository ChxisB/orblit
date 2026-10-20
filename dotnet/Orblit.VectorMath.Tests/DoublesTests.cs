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

    [Theory]
    [InlineData(-1, 3, 2)]
    [InlineData(7, 3, 1)]
    [InlineData(-6, 3, 0)]
    [InlineData(5.5, -2, 1.5)]
    [InlineData(-5.5, -2, 0.5)]
    public void ModIsNeverNegative(double value, double divisor, double expected) =>
        Assert.Equal(expected, Doubles.Mod(value, divisor), 1e-12);

    [Fact]
    public void AModThatComesToNothingIsPositiveZero() =>
        Assert.True(double.IsPositive(Doubles.Mod(-6, 3)), "Dart never answers -0.0 from %");

    [Fact]
    public void ModOfNotANumberOrAnInfinityIsNotANumber()
    {
        Assert.True(double.IsNaN(Doubles.Mod(double.NaN, 3)));
        Assert.True(double.IsNaN(Doubles.Mod(double.PositiveInfinity, 3)));
        Assert.True(double.IsNaN(Doubles.Mod(1, 0)));
    }

    [Theory]
    [InlineData(2.7, 2)]
    [InlineData(-2.1, -3)]
    [InlineData(-0.0, 0)]
    public void FloorGoesDown(double value, long expected) => Assert.Equal(expected, Doubles.Floor(value));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void FloorOfSomethingNotFiniteIsRefused(double value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Doubles.Floor(value));

    [Theory]
    [InlineData(2.5, 1)]
    [InlineData(-2.5, -1)]
    [InlineData(double.PositiveInfinity, 1)]
    [InlineData(double.NegativeInfinity, -1)]
    public void SignIsPlusOrMinusOne(double value, double expected) => Assert.Equal(expected, Doubles.Sign(value));

    [Fact]
    public void SignOfEitherZeroOrNotANumberIsTheValueItself()
    {
        Assert.True(double.IsNegative(Doubles.Sign(-0.0)));
        Assert.False(double.IsNegative(Doubles.Sign(0.0)));
        Assert.True(double.IsNaN(Doubles.Sign(double.NaN)));
    }
}
