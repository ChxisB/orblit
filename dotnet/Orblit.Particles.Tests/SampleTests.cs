using Orblit.VectorMath;

namespace Orblit.Particles.Tests;

public class SampleTests
{
    private readonly ParticleBuffer _output = new(2000);

    // time

    [Fact]
    public void TheSameTimeGivesTheSameFrameWithNothingKept()
    {
        var emitter = new Emitter(
            seed: 4,
            shape: new SphereShape(radius: 2),
            speed: new Range(1, 3),
            gravity: new Vector3(0, -9.8, 0),
            drag: 0.3,
            noiseStrength: 0.2);
        emitter.Sample(1.3, _output);
        var first = PositionsOf(_output);
        var count = _output.Count;

        emitter.Sample(7.9, _output);
        emitter.Sample(0.2, _output);
        emitter.Sample(1.3, _output);

        Assert.Equal(count, _output.Count);
        Assert.Equal(first, PositionsOf(_output));
    }

    [Fact]
    public void TwoEmittersMadeAlikeGiveTheSameFrame()
    {
        static Emitter Make() => new(seed: 9, shape: new ConeShape(radius: 0.3));
        var other = new ParticleBuffer(2000);

        Make().Sample(2.5, _output);
        Make().Sample(2.5, other);

        Assert.Equal(PositionsOf(_output), PositionsOf(other));
    }

    [Fact]
    public void AnotherSeedGivesAnotherFrame()
    {
        var other = new ParticleBuffer(2000);

        new Emitter(seed: 1).Sample(2.5, _output);
        new Emitter(seed: 2).Sample(2.5, other);

        Assert.NotEqual(PositionsOf(_output), PositionsOf(other));
    }

    [Fact]
    public void BeforeTime0OrAtATimeThatIsNotANumberIsEmpty()
    {
        var emitter = new Emitter();
        foreach (var t in new[] { -1.0, double.NaN, double.PositiveInfinity })
        {
            _output.Count = 99;
            emitter.Sample(t, _output);
            Assert.True(_output.Count == 0, $"{t}");
        }
    }

    // births

    [Fact]
    public void ASteadyStreamHoldsRateTimesLifetime()
    {
        var emitter = new Emitter(rate: 10, lifetime: Range.Of(1));

        emitter.Sample(5.05, _output);

        Assert.Equal(10, _output.Count);
    }

    [Fact]
    public void TheFirstParticleIsThereAtTime0()
    {
        new Emitter(rate: 10).Sample(0, _output);

        Assert.Equal(1, _output.Count);
    }

    [Fact]
    public void BirthsStopAtTheDuration()
    {
        var emitter = new Emitter(rate: 10, duration: 1, lifetime: Range.Of(0.5));

        emitter.Sample(1.25, _output);
        Assert.Equal(3, _output.Count);
        emitter.Sample(3, _output);
        Assert.Equal(0, _output.Count);
    }

    [Fact]
    public void ABurstAppearsAtItsTimeAndIsGoneAfterItsLifetime()
    {
        var emitter = new Emitter(rate: 0, bursts: [new Burst(1, 5)], lifetime: Range.Of(2));
        var counts = new[] { 0.5, 1.0, 2.9, 3.0 }
            .Select(t =>
            {
                emitter.Sample(t, _output);
                return _output.Count;
            })
            .ToArray();

        Assert.Equal([0, 5, 5, 0], counts);
    }

    [Fact]
    public void ABurstAndAStreamDoNotShareIds()
    {
        var emitter = new Emitter(rate: 5, bursts: [new Burst(0, 3)], lifetime: Range.Of(1));

        emitter.Sample(0.5, _output);

        Assert.Equal(3 + 3, _output.Count);
    }

    // caps

    [Fact]
    public void WhenOverTheCapTheOldestDie()
    {
        var uncapped = new Emitter(rate: 100, lifetime: Range.Of(1));
        var all = new ParticleBuffer(2000);
        uncapped.Sample(0.995, all);
        Assert.Equal(100, all.Count);

        uncapped.CopyWith(maxParticles: 10).Sample(0.995, _output);

        Assert.Equal(10, _output.Count);
        Assert.Equal(PositionsOf(all)[(90 * 3)..], PositionsOf(_output));
    }

    [Fact]
    public void ACrowdBornAtOneMomentIsCutToTheCapExactly()
    {
        var emitter = new Emitter(rate: 0, bursts: [new Burst(0, 20)], maxParticles: 8);

        emitter.Sample(0.5, _output);

        Assert.Equal(8, _output.Count);
    }

    [Fact]
    public void ALimitLowersTheCapForOneCall()
    {
        var emitter = new Emitter(rate: 100, lifetime: Range.Of(1));

        emitter.Sample(0.995, _output, limit: 7);
        Assert.Equal(7, _output.Count);
        emitter.Sample(0.995, _output);
        Assert.Equal(100, _output.Count);
    }

    [Fact]
    public void TheBufferIsNeverOverrun()
    {
        var small = new ParticleBuffer(4);

        new Emitter(rate: 100).Sample(1, small);

        Assert.Equal(4, small.Count);
    }

    // motion

    [Fact]
    public void WithoutDragAParticleFollowsAParabola()
    {
        var emitter = OneParticle(
            speed: Range.Of(3),
            shape: new ConeShape(angle: 0),
            gravity: new Vector3(0, -10, 0));

        emitter.Sample(0.8, _output);

        Assert.Equal(0, _output.Positions[0], 1e-6);
        Assert.Equal((3 * 0.8) - (5 * 0.64), _output.Positions[1], 1e-5);
        Assert.Equal(0, _output.Positions[2], 1e-6);
    }

    [Fact]
    public void WithDragTheClosedFormAgreesWithAFineNumericStep()
    {
        const double drag = 0.8;
        var gravity = new Vector3(0, -9.8, 0);
        var emitter = OneParticle(
            speed: Range.Of(6),
            shape: new ConeShape(angle: 0.5),
            gravity: gravity,
            drag: drag);
        var p = Vector3.Zero();
        var v = Vector3.Zero();
        emitter.Birth(Emitter.BurstId(0, 0), p, v);
        const double dt = 1e-5;
        for (var i = 0; i < 70000; i++)
        {
            v.AddScaled(gravity - (v * drag), dt);
            p.AddScaled(v, dt);
        }

        emitter.Sample(0.7, _output);

        Assert.Equal(p.X, _output.Positions[0], 1e-3);
        Assert.Equal(p.Y, _output.Positions[1], 1e-3);
        Assert.Equal(p.Z, _output.Positions[2], 1e-3);
    }

    [Fact]
    public void ADragTooSmallToMatterActsAsNoDrag()
    {
        var thrown = OneParticle(
            speed: Range.Of(6),
            shape: new ConeShape(angle: 0.5),
            gravity: new Vector3(0, -9.8, 0));
        var other = new ParticleBuffer(10);

        thrown.Sample(3, _output);
        thrown.CopyWith(drag: 1e-12).Sample(3, other);

        for (var axis = 0; axis < 3; axis++)
        {
            Assert.Equal(_output.Positions[axis], other.Positions[axis], 1e-6);
        }
    }

    [Fact]
    public void DragSlowsAParticle()
    {
        var baseline = OneParticle(speed: Range.Of(6), shape: new ConeShape(angle: 0));
        var other = new ParticleBuffer(10);

        baseline.Sample(1, _output);
        baseline.CopyWith(drag: 2).Sample(1, other);

        Assert.True(other.Positions[1] < _output.Positions[1]);
    }

    [Fact]
    public void NoiseMovesAParticleByAtMostTwiceItsStrengthPerAxis()
    {
        var other = new ParticleBuffer(10);
        var calm = OneParticle();

        calm.Sample(1, _output);
        calm.CopyWith(noiseStrength: 0.5).Sample(1, other);

        var moved = Math.Sqrt(
            Math.Pow((double)other.Positions[0] - _output.Positions[0], 2)
            + Math.Pow((double)other.Positions[1] - _output.Positions[1], 2)
            + Math.Pow((double)other.Positions[2] - _output.Positions[2], 2));
        Assert.True(moved > 0);
        Assert.True(moved <= (2 * 0.5 * Math.Sqrt(3)) + 1e-6);
    }

    // noise

    [Fact]
    public void LeavesANewbornWhereItWasBorn()
    {
        var shaken = OneParticle(noiseStrength: 1, speed: Range.Of(2));
        var still = shaken.CopyWith(noiseStrength: 0);
        var other = new ParticleBuffer(10);

        shaken.Sample(0, _output);
        still.Sample(0, other);

        Assert.Equal(PositionsOf(_output), PositionsOf(other));
    }

    // look

    [Fact]
    public void SizeAndColourFollowTheirRampsOverALife()
    {
        var emitter = OneParticle(life: 2, size: [0, 1, 0], colour: [1, 0, 0, 0, 0, 1]);
        var seen = new List<double[]>();
        foreach (var t in new[] { 0.5, 1.0, 1.5 })
        {
            emitter.Sample(t, _output);
            seen.Add([_output.Sizes[0], _output.Colours[0], _output.Colours[2]]);
        }

        Assert.Equal(0.5, seen[0][0], 1e-6);
        Assert.Equal(1, seen[1][0], 1e-6);
        Assert.Equal(0.5, seen[2][0], 1e-6);
        Assert.Equal(0.5, seen[1][1], 1e-6);
        Assert.Equal(0.5, seen[1][2], 1e-6);
        Assert.Equal(0.75, seen[0][1], 1e-6);
    }

    [Fact]
    public void ARampOfOneStopIsAConstant()
    {
        OneParticle(size: [0.3]).Sample(4, _output);

        Assert.Equal(0.3, _output.Sizes[0], 1e-6);
    }

    [Fact]
    public void SpinTurnsAParticleWithItsAge()
    {
        var spinning = OneParticle().CopyWith(spin: Range.Of(2));
        var other = new ParticleBuffer(10);

        spinning.Sample(1, _output);
        spinning.Sample(2, other);

        Assert.Equal(2, (double)other.Angles[0] - _output.Angles[0], 1e-5);
    }

    // space

    [Fact]
    public void AWorldParticleIsLeftWhereItsEmitterWasWhenItWasBorn()
    {
        OneParticle().Sample(2, _output, place: Slide);

        Assert.Equal(0, _output.Positions[0], 1e-6);
    }

    [Fact]
    public void ALocalParticleGoesWithItsEmitter()
    {
        OneParticle(space: ParticleSpace.Local).Sample(2, _output, place: Slide);

        Assert.Equal(2, _output.Positions[0], 1e-6);
    }

    [Fact]
    public void ATurnedEmitterTurnsTheWayItsParticlesHead()
    {
        var up = OneParticle(speed: Range.Of(1), shape: new ConeShape(angle: 0));

        up.Sample(1, _output, place: _ => Matrix4.RotationZ(Math.PI / 2));

        Assert.Equal(-1, _output.Positions[0], 1e-6);
        Assert.Equal(0, _output.Positions[1], 1e-6);
    }

    [Fact]
    public void WithNoPlacementTheEmitterSitsAtTheOrigin()
    {
        OneParticle(space: ParticleSpace.Local).Sample(2, _output);

        Assert.Equal(0f, _output.Positions[0]);
    }

    // shapes

    [Theory]
    [InlineData("point")]
    [InlineData("sphere")]
    [InlineData("cone")]
    [InlineData("box")]
    public void GivesUnitDirections(string kind)
    {
        var shape = ShapeOf(kind);
        var p = Vector3.Zero();
        var d = Vector3.Zero();
        for (var i = 0; i < 200; i++)
        {
            shape.Pick(1, i, p, d);
            Assert.Equal(1, d.Length, 1e-9);
        }
    }

    [Fact]
    public void ASphereStaysInsideItsRadiusAndABoxInsideItsBox()
    {
        var p = Vector3.Zero();
        var d = Vector3.Zero();
        for (var i = 0; i < 500; i++)
        {
            new SphereShape(radius: 2).Pick(1, i, p, d);
            Assert.True(p.Length <= 2 + 1e-9);
            new BoxShape(new Vector3(1, 2, 3)).Pick(1, i, p, d);
            Assert.True(Math.Abs(p.X) <= 1);
            Assert.True(Math.Abs(p.Y) <= 2);
            Assert.True(Math.Abs(p.Z) <= 3);
        }
    }

    [Fact]
    public void AConeHeadsWithinItsAngleOfUp()
    {
        var p = Vector3.Zero();
        var d = Vector3.Zero();
        for (var i = 0; i < 500; i++)
        {
            new ConeShape(angle: 0.5).Pick(1, i, p, d);
            Assert.True(d.Y >= Math.Cos(0.5) - 1e-9);
        }
    }

    private static float[] PositionsOf(ParticleBuffer b) => b.Positions[..(b.Count * 3)];

    private static Matrix4 Slide(double t) => Matrix4.TranslationValues(t, 0, 0);

    private static Shape ShapeOf(string kind) => kind switch
    {
        "point" => new PointShape(),
        "sphere" => new SphereShape(radius: 2),
        "cone" => new ConeShape(angle: 0.5, radius: 1),
        _ => new BoxShape(new Vector3(1, 2, 3)),
    };

    /// <summary>
    /// One particle at time 0 that lives for <paramref name="life"/>, so a test
    /// can ask for it at any age.
    /// </summary>
    // F2: nine settings, all optional and called by name, as in Dart.
    private static Emitter OneParticle(
        double life = 10,
        Range? speed = null,
        Shape? shape = null,
        Vector3? gravity = null,
        double drag = 0,
        IEnumerable<double>? size = null,
        IEnumerable<double>? colour = null,
        ParticleSpace space = ParticleSpace.World,
        double noiseStrength = 0) => new(
            rate: 0,
            bursts: [new Burst(0, 1)],
            lifetime: Range.Of(life),
            speed: speed ?? Range.Of(0),
            shape: shape ?? new PointShape(),
            gravity: gravity,
            drag: drag,
            size: size ?? [0.1],
            colour: colour ?? [1, 1, 1],
            space: space,
            noiseStrength: noiseStrength);
}
