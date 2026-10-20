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
}
