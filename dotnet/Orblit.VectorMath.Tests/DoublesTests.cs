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
    [InlineData(2.1, 3)]
    [InlineData(-2.9, -2)]
    [InlineData(-0.5, 0)]
    [InlineData(4.0, 4)]
    public void CeilGoesUp(double value, long expected) => Assert.Equal(expected, Doubles.Ceil(value));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void CeilOfSomethingNotFiniteIsRefused(double value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Doubles.Ceil(value));

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

    [Theory]
    [InlineData(2.5, 3)]
    [InlineData(-2.5, -3)]
    [InlineData(0.49999999999999994, 0)]
    [InlineData(-0.4, 0)]
    [InlineData(1.5, 2)]
    public void RoundGoesAwayFromZeroOnAHalf(double value, long expected) => Assert.Equal(expected, Doubles.Round(value));

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void RoundOfSomethingNotFiniteIsRefused(double value) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Doubles.Round(value));

    [Theory]
    [InlineData(0.5, 0, "1")]
    [InlineData(-0.5, 0, "-1")]
    [InlineData(2.5, 0, "3")]
    [InlineData(-2.5, 0, "-3")]
    [InlineData(115.25, 1, "115.3")]
    [InlineData(-0.125, 0, "-0")]
    [InlineData(-0.125, 1, "-0.1")]
    [InlineData(-0.0, 3, "-0.000")]
    [InlineData(-0.0001, 3, "-0.000")]
    [InlineData(0.0004, 3, "0.000")]
    [InlineData(5e-324, 3, "0.000")]
    [InlineData(1.005, 1, "1.0")]
    [InlineData(4.35, 1, "4.3")]
    [InlineData(1234567.8915, 3, "1234567.891")]
    [InlineData(1e20, 1, "100000000000000000000.0")]
    [InlineData(123456789012345680000.0, 0, "123456789012345683968")]
    public void FixedPointRoundsTheExactValueAndTiesGoAwayFromZero(double value, int digits, string expected) =>
        Assert.Equal(expected, Doubles.ToStringAsFixed(value, digits));

    [Theory]
    [InlineData(1e21, "1e+21")]
    [InlineData(-1e21, "-1e+21")]
    [InlineData(1.5e22, "1.5e+22")]
    [InlineData(1e300, "1e+300")]
    [InlineData(double.NaN, "NaN")]
    [InlineData(double.PositiveInfinity, "Infinity")]
    [InlineData(double.NegativeInfinity, "-Infinity")]
    public void FixedPointGivesTheShortestFormWhereDartDoes(double value, string expected)
    {
        Assert.Equal(expected, Doubles.ToStringAsFixed(value, 0));
        Assert.Equal(expected, Doubles.ToStringAsFixed(value, 3));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(21)]
    public void FixedPointRefusesDigitsOutsideZeroToTwenty(int digits) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Doubles.ToStringAsFixed(1.0, digits));
}
