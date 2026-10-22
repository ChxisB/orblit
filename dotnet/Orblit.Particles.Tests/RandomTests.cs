namespace Orblit.Particles.Tests;

public class RandomTests
{
    [Fact]
    public void TheSameThreeNumbersGiveTheSameAnswer()
    {
        Assert.Equal(UnitRandom.At(7, 123456, 9), UnitRandom.At(7, 123456, 9));
        Assert.NotEqual(UnitRandom.At(7, 123456, 9), UnitRandom.At(7, 123456, 10));
        Assert.NotEqual(UnitRandom.At(7, 123456, 9), UnitRandom.At(8, 123456, 9));
    }

    // These were recorded from the first implementation. A web run must give the same digits, which
    // is what the 32-bit multiply is for.
    [Fact]
    public void KnownValuesDoNotMove()
    {
        Assert.Equal(0.12107708724215627, UnitRandom.At(0, 0, 0));
        Assert.Equal(0.16923378384672105, UnitRandom.At(1, 2, 3));
        Assert.Equal(0.8150795300025493, UnitRandom.At(7, 123456, 9));
        Assert.Equal(0.7470536117907614, UnitRandom.At(-5, 0x7fffffff, 4));
        Assert.Equal(0.5054033196065575, UnitRandom.At(99, 0x40000000 + (65536 * 3) + 7, 1));
    }

    [Fact]
    public void EveryValueIsFrom0UpToNotIncluding1()
    {
        for (var i = 0; i < 5000; i++)
        {
            var v = UnitRandom.At(3, i, i % 10);
            Assert.InRange(v, 0, 1);
            Assert.True(v < 1);
        }
    }
}
