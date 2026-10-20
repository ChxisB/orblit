namespace Orblit.VectorMath.Tests;

public class Vector3Tests
{
    [Fact]
    public void AComponentCanBeWrittenThroughTheVector()
    {
        var v = new Vector3(1, 2, 3);
        v.X = 9;
        Assert.Equal(9, v.X);
        Assert.Equal(2, v.Y);
        Assert.Equal(3, v.Z);
    }

    [Fact]
    public void ACloneDoesNotShareItsComponents()
    {
        var original = new Vector3(1, 0.5, 0.2);
        var copy = original.Clone();
        copy.X = 0;
        Assert.Equal(1, original.X);
        Assert.Equal(original.Y, copy.Y);
    }

    [Fact]
    public void EqualityIsByComponentAndNotANumberIsNotEqualToItself()
    {
        Assert.Equal(new Vector3(1, 2, 3), new Vector3(1, 2, 3));
        Assert.NotEqual(new Vector3(1, 2, 3), new Vector3(1, 2, 4));
        var odd = new Vector3(double.NaN, 0, 0);
        Assert.False(odd.Equals(odd.Clone()));
    }

    [Fact]
    public void ItAddsSubtractsAndScalesIntoANewVector()
    {
        var a = new Vector3(1, 2, 3);
        var b = new Vector3(10, 20, 30);
        Assert.Equal(new Vector3(11, 22, 33), a + b);
        Assert.Equal(new Vector3(9, 18, 27), b - a);
        Assert.Equal(new Vector3(2, 4, 6), a * 2);
        Assert.Equal(new Vector3(1, 2, 3), a);
    }

    [Fact]
    public void ItsLengthIsTheRootOfTheSummedSquares()
    {
        Assert.Equal(5, new Vector3(3, 4, 0).Length);
        Assert.Equal(25, new Vector3(0, 3, 4).Length2);
        Assert.Equal(0, Vector3.Zero().Length);
        Assert.Equal(new Vector3(1, 1, 1), Vector3.All(1));
    }

    [Fact]
    public void NormalisedIsACopyOfLengthOne()
    {
        var v = new Vector3(0, 3, 4);
        var unit = v.Normalized();
        Assert.Equal(1, unit.Length, 1e-15);
        Assert.Equal(0.6, unit.Y, 1e-15);
        Assert.Equal(new Vector3(0, 3, 4), v);
    }

    [Fact]
    public void NormalisingNothingGivesNothingAndNotNotANumber()
    {
        var zero = Vector3.Zero();
        var same = zero.Normalized();
        Assert.Equal(zero, same);
        Assert.NotSame(zero, same);
    }

    [Fact]
    public void DividingScalesByTheReciprocalAndNegatingFlipsEveryComponent()
    {
        var v = new Vector3(2, -4, 6);
        Assert.Equal(new Vector3(1, -2, 3), v / 2);
        Assert.Equal(new Vector3(-2, 4, -6), -v);
        Assert.Equal(new Vector3(2, -4, 6), v);
        var byZero = new Vector3(1, 0, -1) / 0;
        Assert.Equal(double.PositiveInfinity, byZero.X);
        Assert.True(double.IsNaN(byZero.Y));
        Assert.Equal(double.NegativeInfinity, byZero.Z);
    }

    [Fact]
    public void DotAndCrossFollowTheRightHandRule()
    {
        var x = new Vector3(1, 0, 0);
        var y = new Vector3(0, 1, 0);
        Assert.Equal(0, x.Dot(y));
        Assert.Equal(32, new Vector3(1, 2, 3).Dot(new Vector3(4, 5, 6)));
        Assert.Equal(new Vector3(0, 0, 1), x.Cross(y));
        Assert.Equal(new Vector3(0, 0, -1), y.Cross(x));
        Assert.Equal(new Vector3(-3, 6, -3), new Vector3(1, 2, 3).Cross(new Vector3(4, 5, 6)));
    }

    [Fact]
    public void SetValuesWritesAllThreeComponents()
    {
        var v = new Vector3(1, 2, 3);
        v.SetValues(7, 8, 9);
        Assert.Equal(new Vector3(7, 8, 9), v);
    }
}
