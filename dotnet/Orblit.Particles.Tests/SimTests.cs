using Orblit.VectorMath;

namespace Orblit.Particles.Tests;

public class SimTests
{
    [Fact]
    public void AStepThatIsNotASpanOfTimeIsRefused()
    {
        var sim = new ParticleSim(new Emitter());

        Assert.ThrowsAny<ArgumentException>(() => sim.Step(-0.1));
        Assert.ThrowsAny<ArgumentException>(() => sim.Step(double.NaN));
        Assert.ThrowsAny<ArgumentException>(() => sim.Step(double.PositiveInfinity));
    }

    [Fact]
    public void ALongPauseBirthsOnlyTheParticlesThatCouldBeAlive()
    {
        var sim = new ParticleSim(new Emitter(rate: 1000, lifetime: Range.Of(1), maxParticles: 5000));

        sim.Step(1e5);

        Assert.True(sim.Count <= 1001);
        Assert.True(sim.Count > 0);
    }

    [Fact]
    public void AStreamHoldsAboutRateTimesLifetimeOnceItHasSettled()
    {
        var sim = new ParticleSim(new Emitter(rate: 10, lifetime: Range.Of(1)));

        for (var i = 0; i < 50; i++)
        {
            sim.Step(0.1);
        }

        Assert.InRange(sim.Count, 9, 11);
    }

    [Fact]
    public void ABurstIsBornOnce()
    {
        var sim = new ParticleSim(new Emitter(rate: 0, bursts: [new Burst(0.5, 4)], lifetime: Range.Of(9)));

        sim.Step(0.4);
        Assert.Equal(0, sim.Count);
        sim.Step(0.2);
        Assert.Equal(4, sim.Count);
        sim.Step(0.2);
        Assert.Equal(4, sim.Count);
    }

    [Fact]
    public void WithoutAHitTestAParticleFallsThroughTheFloor()
    {
        var low = Heights(new ParticleSim(Thrown()), 200).Min();

        Assert.True(low < -1);
    }

    [Fact]
    public void AFloorStopsAParticleGoingBelowIt()
    {
        var seen = Heights(new ParticleSim(Thrown(), hit: Floor, bounce: 0.5), 400);

        Assert.True(seen.All(y => y >= -1e-9));
    }

    [Fact]
    public void ABounceKeepsTheShareOfSpeedItWasGiven()
    {
        var seen = Heights(new ParticleSim(Thrown(), hit: Floor, bounce: 0.5), 400);
        var apex = seen.Take(120).Max();
        var landed = seen.FindIndex(60, y => y < 0.02);
        var again = seen.Skip(landed + 5).Take(100).Max();

        // Heights go with speed squared, so half the speed is a quarter the height.
        Assert.Equal(1.25, apex, 0.1);
        Assert.Equal(0.25, again / apex, 0.05);
    }

    [Fact]
    public void WithNoBounceAParticleDiesOnTheFloor()
    {
        var sim = new ParticleSim(Thrown(), hit: Floor);

        for (var i = 0; i < 400; i++)
        {
            sim.Step(0.01);
        }

        Assert.Equal(0, sim.Count);
    }

    [Fact]
    public void TurbulenceMovesAParticleOffItsPath()
    {
        var output = new ParticleBuffer(4);
        var calm = new ParticleSim(Thrown());
        var shaken = new ParticleSim(Thrown(), turbulence: 8);
        for (var i = 0; i < 50; i++)
        {
            calm.Step(0.01);
            shaken.Step(0.01);
        }

        var other = new ParticleBuffer(4);

        calm.Fill(output);
        shaken.Fill(other);

        Assert.NotEqual(output.Positions[0], other.Positions[0]);
    }

    [Fact]
    public void WhenOverTheCapTheOldestDie()
    {
        var sim = new ParticleSim(new Emitter(rate: 100, lifetime: Range.Of(5), maxParticles: 5));

        sim.Step(1);

        Assert.Equal(5, sim.Count);
    }

    [Fact]
    public void ALocalParticleFollowsItsEmitterWhenDrawn()
    {
        var sim = new ParticleSim(
            new Emitter(
                rate: 0,
                bursts: [new Burst(0, 1)],
                lifetime: Range.Of(9),
                speed: Range.Of(0),
                space: ParticleSpace.Local));
        var output = new ParticleBuffer(4);

        sim.Step(1);
        sim.Fill(output, place: t => Matrix4.TranslationValues(t * 3, 0, 0));

        Assert.Equal(3, output.Positions[0], 1e-6);
    }

    [Fact]
    public void ABufferTooSmallGetsWhatFits()
    {
        var sim = new ParticleSim(new Emitter(rate: 0, bursts: [new Burst(0, 10)], lifetime: Range.Of(9)));
        var output = new ParticleBuffer(3);

        sim.Step(0.1);
        sim.Fill(output);

        Assert.Equal(3, output.Count);
    }

    [Fact]
    public void BadSettingsAreRefused()
    {
        Assert.ThrowsAny<ArgumentException>(() => new ParticleSim(new Emitter(), bounce: 1.5));
        Assert.ThrowsAny<ArgumentException>(() => new ParticleSim(new Emitter(), bounce: -0.1));
        Assert.ThrowsAny<ArgumentException>(() => new ParticleSim(new Emitter(), turbulence: -1));
    }

    /// <summary>A floor at y = 0 that faces up.</summary>
    private static Hit? Floor(Vector3 from, Vector3 to) =>
        to.Y < 0 ? new Hit(new Vector3(to.X, 0, to.Z), new Vector3(0, 1, 0)) : null;

    /// <summary>One particle thrown straight up from the origin at 5 m/s.</summary>
    private static Emitter Thrown() => new(
        rate: 0,
        bursts: [new Burst(0, 1)],
        lifetime: Range.Of(20),
        speed: Range.Of(5),
        shape: new ConeShape(angle: 0),
        gravity: new Vector3(0, -10, 0));

    /// <summary>Height after each of <paramref name="steps"/> steps of 0.01 seconds.</summary>
    private static List<double> Heights(ParticleSim sim, int steps)
    {
        var output = new ParticleBuffer(4);
        var seen = new List<double>();
        for (var i = 0; i < steps; i++)
        {
            sim.Step(0.01);
            sim.Fill(output);
            seen.Add(output.Count == 0 ? double.NaN : output.Positions[1]);
        }

        return seen;
    }
}
