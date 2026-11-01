namespace Orblit.VectorMath.Tests;

public class Vector2Tests
{
    [Fact]
    public void AComponentCanBeWrittenThroughTheVector()
    {
        var v = new Vector2(1, 2);
        v.X = 9;
        Assert.Equal(new Vector2(9, 2), v);
    }

    [Fact]
    public void ZeroIsANewVectorEachTime()
    {
        var first = Vector2.Zero();
        first.X = 1;
        Assert.Equal(new Vector2(0, 0), Vector2.Zero());
    }

    [Fact]
    public void DotMultipliesComponentsAndAddsThem()
    {
        Assert.Equal(14, new Vector2(2, 3).Dot(new Vector2(4, 2)));
        Assert.Equal(0, new Vector2(1, 0).Dot(new Vector2(0, 5)));
        Assert.Equal(-6, new Vector2(2, 3).Dot(new Vector2(-3, 0)));
    }

    [Fact]
    public void DistanceIsTheLengthOfTheGapBetweenTwoPoints()
    {
        var from = new Vector2(1, 2);
        var to = new Vector2(4, 6);
        Assert.Equal(25, from.DistanceToSquared(to));
        Assert.Equal(5, from.DistanceTo(to));
        Assert.Equal(5, to.DistanceTo(from));
        Assert.Equal(0, from.DistanceTo(from));
    }

    [Fact]
    public void NormalizeScalesToLengthOneAndSaysTheOldLength()
    {
        var vector = new Vector2(3, 4);
        Assert.Equal(5, vector.Normalize());
        Assert.Equal(0.6, vector.X, 12);
        Assert.Equal(0.8, vector.Y, 12);

        var kept = new Vector2(3, 4);
        Assert.Equal(0.6, kept.Normalized().X, 12);
        Assert.Equal(3, kept.X);
    }

    [Fact]
    public void ANullVectorStaysAsItIsWhenNormalized()
    {
        var vector = Vector2.Zero();
        Assert.Equal(0, vector.Normalize());
        Assert.Equal(0, vector.X);
        Assert.Equal(0, vector.Y);
    }

    [Fact]
    public void ACloneDoesNotShareItsComponents()
    {
        var original = new Vector2(1, 0.5);
        var copy = original.Clone();
        copy.X = 0;
        Assert.Equal(1, original.X);
    }

    [Fact]
    public void ArithmeticGivesNewVectors()
    {
        var a = new Vector2(1, 2);
        var b = new Vector2(4, 8);
        Assert.Equal(new Vector2(5, 10), a + b);
        Assert.Equal(new Vector2(3, 6), b - a);
        Assert.Equal(new Vector2(-1, -2), -a);
        Assert.Equal(new Vector2(2, 4), a * 2);
        Assert.Equal(new Vector2(2, 4), b / 2);
        Assert.Equal(new Vector2(3, 3), Vector2.All(3));
        Assert.Equal(new Vector2(1, 2), a);
    }

    [Fact]
    public void LengthIsThePythagoreanOne()
    {
        Assert.Equal(25, new Vector2(3, 4).Length2);
        Assert.Equal(5, new Vector2(3, 4).Length);
    }

    [Fact]
    public void EqualityIsByComponentAndNotANumberIsNotEqualToItself()
    {
        Assert.NotEqual(new Vector2(1, 2), new Vector2(1, 3));
        var odd = new Vector2(double.NaN, 0);
        Assert.False(odd.Equals(odd.Clone()));
        Assert.Equal("[1.5,-2]", new Vector2(1.5, -2).ToString());
    }
}
