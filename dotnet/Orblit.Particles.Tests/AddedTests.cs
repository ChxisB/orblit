namespace Orblit.Particles.Tests;

// What C# does that Dart cannot be asked: a record compares by value, and a bad size is refused before it allocates.
public class AddedTests
{
    [Fact]
    public void ARangeComparesByValue() => Assert.Equal(new Range(1, 2), Range.Of(1) with { Max = 2 });

    [Fact]
    public void AZeroCapacityBufferIsRefusedBeforeItAllocates()
    {
        var error = Assert.Throws<ArgumentException>(() => new ParticleBuffer(0));
        Assert.Equal("capacity", error.ParamName);
    }

    [Fact]
    public void ANegativeCapacityBufferIsRefusedBeforeItAllocates() =>
        Assert.Throws<ArgumentException>(() => new ParticleBuffer(-1));

    [Fact]
    public void ASmallStripIsRefusedBeforeItAllocates()
    {
        var error = Assert.Throws<ArgumentException>(() => new TrailStrip(1));
        Assert.Equal("capacity", error.ParamName);
    }

    [Fact]
    public void ARefusalNamesTheArgument()
    {
        var error = Assert.Throws<ArgumentException>(() => new Emitter(rate: -1));
        Assert.Equal("Rate", error.ParamName);
        Assert.Contains("Rate must be 0 or more. Got -1.", error.Message);
    }

    [Fact]
    public void ACopyCannotClearTheDuration()
    {
        var timed = new Emitter(duration: 3);
        Assert.Equal(3, timed.CopyWith(rate: 5).Duration);
    }

    [Fact]
    public void ACopyChecksItsNewValues() =>
        Assert.Throws<ArgumentException>(() => new Emitter().CopyWith(maxParticles: 0));
}
