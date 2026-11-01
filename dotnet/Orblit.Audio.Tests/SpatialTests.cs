using Orblit.VectorMath;

namespace Orblit.Audio.Tests;

public class SpatialTests
{
    private static Emitter At(double x, double z = 0, Falloff? falloff = null) =>
        new(new Vector3(x, 0, z), falloff: falloff);

    // Falloff

    [Fact]
    public void LinearIsFullToNearAStraightLineAndSilentFromFar()
    {
        var falloff = new Falloff(near: 2, far: 12, curve: FalloffCurve.Linear);

        Assert.Equal(1, falloff.GainAt(0));
        Assert.Equal(1, falloff.GainAt(2));
        Assert.Equal(0.5, falloff.GainAt(7), 1e-9);
        Assert.Equal(0, falloff.GainAt(12));
        Assert.Equal(0, falloff.GainAt(40));
    }

    [Fact]
    public void InverseDropsFastAtFirstAndStillReachesZeroAtFar()
    {
        var falloff = new Falloff(near: 1, far: 11);

        Assert.Equal(1, falloff.GainAt(1));
        Assert.Equal((0.5 - (1.0 / 11)) / (1 - (1.0 / 11)), falloff.GainAt(2), 1e-9);
        Assert.True(falloff.GainAt(2) < 0.5);
        Assert.Equal(0, falloff.GainAt(11));
        Assert.Equal(0, falloff.GainAt(10.999), 1e-3);
    }

    [Fact]
    public void NearMustBeAboveZeroAndFarAboveNear()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Falloff(near: 0));
        Assert.ThrowsAny<ArgumentException>(() => new Falloff(near: 5, far: 5));
        Assert.ThrowsAny<ArgumentException>(() => new Falloff(far: double.PositiveInfinity));
    }

    // Cone

    private static Cone TheCone() => new(inner: 0.4, outer: 1.2, outerGain: 0.2);

    [Fact]
    public void IsFullInsideALineBetweenAndOuterGainPastTheEdge()
    {
        var cone = TheCone();

        Assert.Equal(1, cone.GainAt(0));
        Assert.Equal(1, cone.GainAt(0.4));
        Assert.Equal(0.6, cone.GainAt(0.8), 1e-9);
        Assert.Equal(0.2, cone.GainAt(1.2));
        Assert.Equal(0.2, cone.GainAt(3));
    }

    [Fact]
    public void AnEdgeWithNoWidthIsAStepNotADivisionByZero()
    {
        var step = new Cone(inner: 1, outer: 1);

        Assert.Equal(1, step.GainAt(1));
        Assert.Equal(0, step.GainAt(1.01));
    }

    [Fact]
    public void TheAnglesAndTheGainAreChecked()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Cone(inner: 2, outer: 1));
        Assert.ThrowsAny<ArgumentException>(() => new Cone(inner: 0, outer: 4));
        Assert.ThrowsAny<ArgumentException>(() => new Cone(inner: 0, outer: 1, outerGain: 2));
    }

    // Motion

    [Fact]
    public void VelocityIsTheMoveOverTheTimeItTook()
    {
        var listener = new Listener();
        listener.Teleport(new Vector3(1, 0, 0));

        listener.MoveTo(new Vector3(4, 0, 0), 0.5);

        Assert.Equal(new Vector3(6, 0, 0), listener.Velocity);
        Assert.Equal(new Vector3(4, 0, 0), listener.Position);
    }

    [Fact]
    public void ATeleportIsNotASpeed()
    {
        var listener = new Listener();
        listener.MoveTo(new Vector3(10, 0, 0), 1);

        listener.Teleport(new Vector3(500, 0, 0));

        Assert.Equal(Vector3.Zero(), listener.Velocity);
    }

    [Fact]
    public void AFrameOfNoTimeLeavesNoVelocity()
    {
        var listener = new Listener();
        listener.MoveTo(new Vector3(10, 0, 0), 1);

        listener.MoveTo(new Vector3(12, 0, 0), 0);

        Assert.Equal(Vector3.Zero(), listener.Velocity);
    }

    [Fact]
    public void AMoveKeepsThePositionByValueNotByReference()
    {
        var spot = new Vector3(1, 2, 3);
        var listener = new Listener();
        listener.MoveTo(spot, 1);

        spot.X = 99;

        Assert.Equal(1, listener.Position.X);
    }

    [Fact]
    public void TheStartingPositionIsKeptByValueNotByReference()
    {
        var spot = new Vector3(1, 2, 3);
        var emitter = new Emitter(spot);

        spot.X = 99;

        Assert.Equal(1, emitter.Position.X);
    }

    // Emitter

    [Fact]
    public void GainFollowsTheFalloffByDistance()
    {
        var listener = new Listener();
        var falloff = new Falloff(near: 1, far: 11, curve: FalloffCurve.Linear);

        Assert.Equal(0.5, At(6, 0, falloff).HeardBy(listener).Gain, 1e-9);
        Assert.Equal(0, At(20, 0, falloff).HeardBy(listener).Gain);
    }

    [Fact]
    public void ASoundOnTheRightPansRightAndOnTheLeftPansLeft()
    {
        var listener = new Listener();

        Assert.Equal(1, At(5).HeardBy(listener).Pan, 1e-9);
        Assert.Equal(-1, At(-5).HeardBy(listener).Pan, 1e-9);
        Assert.Equal(0, At(0, -5).HeardBy(listener).Pan, 1e-9);
        Assert.Equal(0, At(0, 5).HeardBy(listener).Pan, 1e-9);
    }

    [Fact]
    public void ThePanEasesToTheMiddleInsideNear()
    {
        var listener = new Listener();

        Assert.Equal(0.5, At(0.5).HeardBy(listener).Pan, 1e-9);
        Assert.Equal(-0.25, At(-0.25).HeardBy(listener).Pan, 1e-9);
        Assert.Equal(0, At(0).HeardBy(listener).Pan);
        Assert.Equal(0.5, At(1, 0, new Falloff(near: 2)).HeardBy(listener).Pan, 1e-9);
    }

    [Fact]
    public void AnEmitterKeepsItsOwnCopyOfTheDirectionItFaces()
    {
        var facing = new Vector3(0, 0, 1);
        var emitter = new Emitter(Vector3.Zero(), forward: facing);

        facing.SetValues(1, 0, 0);

        Assert.Equal(new Vector3(0, 0, 1), emitter.Forward);
    }

    [Fact]
    public void TurningTheListenerTurnsThePan()
    {
        var listener = new Listener { Forward = new Vector3(1, 0, 0) };

        // Facing +X with +Y up puts +Z on the right.
        Assert.Equal(1, At(0, 5).HeardBy(listener).Pan, 1e-9);
        Assert.Equal(0, At(5).HeardBy(listener).Pan, 1e-9);
    }

    [Fact]
    public void AListenerWhoseUpIsNotAUnitVectorStillPansWithinOne()
    {
        var listener = new Listener { Up = new Vector3(0, 3, 0) };

        Assert.Equal(1, At(5).HeardBy(listener).Pan, 1e-9);
    }

    [Fact]
    public void AListenerFacingAlongUpHasNoRightSoNoPan()
    {
        var listener = new Listener { Forward = new Vector3(0, 1, 0) };

        Assert.Equal(0, At(5).HeardBy(listener).Pan);
    }

    [Fact]
    public void AConeIsLoudInFrontAndQuietBehind()
    {
        var cone = new Cone(inner: 0.3, outer: 1.0, outerGain: 0.1);
        var listener = new Listener();

        Emitter Facing(Vector3 forward) =>
            new(new Vector3(0, 0, -3), falloff: new Falloff(near: 5, far: 50), cone: cone, forward: forward);

        // The emitter is ahead of the listener. Facing +Z points it at them.
        Assert.Equal(1, Facing(new Vector3(0, 0, 1)).HeardBy(listener).Gain);
        Assert.Equal(0.1, Facing(new Vector3(0, 0, -1)).HeardBy(listener).Gain);
    }

    [Fact]
    public void AConeReadsAnEmitterFacingInAnyLengthOfVector()
    {
        static Emitter Facing(double length) =>
            new(
                new Vector3(0, 0, -3),
                falloff: new Falloff(near: 5, far: 50),
                cone: new Cone(inner: 0.1, outer: 0.2),
                forward: new Vector3(0, 0, length));

        Assert.Equal(1, Facing(40).HeardBy(new Listener()).Gain);
        Assert.Equal(1, Facing(0.5).HeardBy(new Listener()).Gain);
    }

    [Fact]
    public void ASoundOnTopOfTheListenerIsFullGainWithNoDirection()
    {
        var level = At(0).HeardBy(new Listener());

        Assert.Equal(1, level.Gain);
        Assert.Equal(1, level.Pitch);
        Assert.Equal(0, level.Pan);
    }

    // Doppler

    private static Level Heard(double emitterSpeed = 0, double listenerSpeed = 0, double doppler = 1)
    {
        // The emitter is 20 m ahead, down -Z. Positive speed is towards the other one.
        var listener = new Listener { Velocity = new Vector3(0, 0, -listenerSpeed) };
        var emitter = new Emitter(new Vector3(0, 0, -20), falloff: new Falloff(near: 1, far: 100), doppler: doppler)
        {
            Velocity = new Vector3(0, 0, emitterSpeed),
        };
        return emitter.HeardBy(listener);
    }

    [Fact]
    public void ASoundComingCloserRisesAndOneGoingAwayFalls()
    {
        Assert.Equal(1 / 0.9, Heard(emitterSpeed: 34.3).Pitch, 1e-9);
        Assert.Equal(1 / 1.1, Heard(emitterSpeed: -34.3).Pitch, 1e-9);
    }

    [Fact]
    public void AListenerMovingAtASoundHearsWhatTheSoundMovingDoes()
    {
        Assert.Equal(1.1, Heard(listenerSpeed: 34.3).Pitch, 1e-9);
        Assert.Equal(0.9, Heard(listenerSpeed: -34.3).Pitch, 1e-9);
    }

    [Fact]
    public void TheAmountScalesTheShiftAndZeroTurnsItOff()
    {
        var real = Heard(emitterSpeed: 34.3).Pitch;
        var half = Heard(emitterSpeed: 34.3, doppler: 0.5).Pitch;

        Assert.Equal(1 / 0.95, half, 1e-9);
        Assert.True(half < real);
        Assert.Equal(1, Heard(emitterSpeed: 34.3, doppler: 0).Pitch);
    }

    [Fact]
    public void ASpeedPastTheSpeedOfSoundStaysInsideAFactorOfThree()
    {
        Assert.Equal(2, Heard(emitterSpeed: 5000).Pitch, 1e-9);
        Assert.Equal(2.0 / 3, Heard(emitterSpeed: -5000).Pitch, 1e-9);
        var both = Heard(emitterSpeed: 5000, listenerSpeed: 5000);
        Assert.Equal(3, both.Pitch, 1e-9);
        var apart = Heard(emitterSpeed: -5000, listenerSpeed: -5000);
        Assert.Equal(1.0 / 3, apart.Pitch, 1e-9);
        var scaled = Heard(emitterSpeed: 5000, listenerSpeed: 5000, doppler: 4);
        Assert.Equal(3, scaled.Pitch, 1e-9);
    }

    [Fact]
    public void SpeedAcrossTheLineToTheListenerMakesNoShift()
    {
        var listener = new Listener();
        var emitter = new Emitter(new Vector3(0, 0, -20)) { Velocity = new Vector3(30, 0, 0) };

        Assert.Equal(1, emitter.HeardBy(listener).Pitch, 1e-9);
    }

    [Fact]
    public void TheAmountMustBeZeroOrMore()
    {
        Assert.ThrowsAny<ArgumentException>(() => new Emitter(Vector3.Zero(), doppler: -1));
    }
}
