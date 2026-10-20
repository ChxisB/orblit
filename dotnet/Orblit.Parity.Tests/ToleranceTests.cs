namespace Orblit.Parity.Tests;

public class ToleranceTests
{
    [Theory]
    [InlineData(1.0, 1.0)]
    [InlineData(1.0, 1.0 + 0.9e-9)]
    [InlineData(1000.0, 1000.0 * (1 + 0.9e-9))]
    [InlineData(0.0, 0.9e-9)]
    [InlineData(0.0, -0.0)]
    [InlineData(double.NaN, double.NaN)]
    [InlineData(double.PositiveInfinity, double.PositiveInfinity)]
    public void Agrees(double expected, double actual) =>
        Assert.True(Tolerance.Close(expected, actual));

    [Theory]
    [InlineData(1.0, 1.0 + 1.1e-9)]
    [InlineData(1000.0, 1000.0 * (1 + 1.1e-9))]
    [InlineData(0.0, 1.1e-9)]
    [InlineData(double.NaN, 1.0)]
    [InlineData(double.PositiveInfinity, double.NegativeInfinity)]
    [InlineData(double.PositiveInfinity, 1.0)]
    public void Differs(double expected, double actual) =>
        Assert.False(Tolerance.Close(expected, actual));
}
