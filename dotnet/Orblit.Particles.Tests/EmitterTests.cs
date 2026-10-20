using Orblit.VectorMath;

namespace Orblit.Particles.Tests;

public class EmitterTests
{
    [Fact]
    public void APlainEmitterIsValid() => Assert.Equal(1000, new Emitter().MaxParticles);

    [Fact]
    public void BadNumbersAreRefusedWhereTheEmitterIsMade()
    {
        var bad = new Dictionary<string, Func<Emitter>>
        {
            ["rate"] = () => new Emitter(rate: -1),
            ["nan rate"] = () => new Emitter(rate: double.NaN),
            ["duration"] = () => new Emitter(duration: -1),
            ["lifetime zero"] = () => new Emitter(lifetime: new Range(0, 1)),
            ["lifetime backwards"] = () => new Emitter(lifetime: new Range(2, 1)),
            ["speed"] = () => new Emitter(speed: new Range(2, 1)),
            ["no size"] = () => new Emitter(size: []),
            ["negative size"] = () => new Emitter(size: [-1]),
            ["colour pair"] = () => new Emitter(colour: [1, 1]),
            ["no colour"] = () => new Emitter(colour: []),
            ["drag"] = () => new Emitter(drag: -1),
            ["noise strength"] = () => new Emitter(noiseStrength: -1),
            ["noise scale"] = () => new Emitter(noiseScale: 0),
            ["gravity"] = () => new Emitter(gravity: new Vector3(0, double.NaN, 0)),
            ["cap"] = () => new Emitter(maxParticles: 0),
        };
        foreach (var entry in bad)
        {
            Assert.True(Record.Exception(() => entry.Value()) is ArgumentException, entry.Key);
        }
    }

    [Fact]
    public void AListHandedInCannotBeChangedFromOutsideAfterwards()
    {
        var sizes = new List<double> { 0.1 };
        var colours = new List<double> { 1, 1, 1 };
        var bursts = new List<Burst> { new(0, 2) };
        var emitter = new Emitter(size: sizes, colour: colours, bursts: bursts);

        sizes.Clear();
        colours.Clear();
        bursts.Clear();

        var output = new ParticleBuffer(10);
        emitter.Sample(0.5, output);
        Assert.Equal([0.1], emitter.Size);
        Assert.Equal([1.0, 1.0, 1.0], emitter.Colour);
        Assert.Single(emitter.Bursts);

        // Dart's list says it cannot change. Here the read-only view hides Add, so reach it through IList.
        Assert.Throws<NotSupportedException>(() => ((IList<double>)emitter.Size).Add(1));
    }

    [Fact]
    public void ABurstNeedsATimeAndACountItCanCarry()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Burst(-1, 5));
        Assert.ThrowsAny<ArgumentException>(() => new Burst(0, 0));
        Assert.ThrowsAny<ArgumentException>(() => new Burst(0, 65536));
        Assert.Equal(65535, new Burst(0, 65535).Count);
    }

    [Fact]
    public void ShapesRefuseBadSizes()
    {
        Assert.ThrowsAny<ArgumentException>(() => new SphereShape(radius: -1));
        Assert.ThrowsAny<ArgumentException>(() => new ConeShape(angle: 4));
        Assert.ThrowsAny<ArgumentException>(() => new ConeShape(radius: -1));
        Assert.ThrowsAny<ArgumentException>(() => new BoxShape(new Vector3(1, -1, 1)));
    }

    [Fact]
    public void CopyWithChangesOneThingAndKeepsTheRest()
    {
        var a = new Emitter(seed: 3, rate: 5, drag: 0.5);

        var b = a.CopyWith(rate: 20);

        Assert.Equal(20, b.Rate);
        Assert.Equal(3, b.Seed);
        Assert.Equal(0.5, b.Drag);
        Assert.ThrowsAny<ArgumentException>(() => a.CopyWith(rate: -1));
    }

    [Fact]
    public void GravityIsCopiedSoALaterChangeOutsideDoesNotReachIt()
    {
        var g = new Vector3(0, -9.8, 0);
        var emitter = new Emitter(gravity: g);

        g.Y = 0;

        Assert.Equal(-9.8, emitter.Gravity.Y);
    }
}
