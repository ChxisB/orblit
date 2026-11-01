using Orblit.VectorMath;

namespace Orblit.Sequence.Tests;

public class CurveTests
{
    // a curve

    [Fact]
    public void PassesThroughItsKeys()
    {
        var channel = new Channel<double>(
            [
                new Key<double>(0, 0.0, Hold.Curve),
                new Key<double>(1, 4.0, Hold.Curve),
                new Key<double>(3, 2.0),
            ],
            Mixers.OfDouble);
        Assert.Equal(0, channel.At(0));
        Assert.Equal(4, channel.At(1), 1e-12);
        Assert.Equal(2, channel.At(3));
    }

    [Fact]
    public void WithNamedSlopesIsTheCubicTheyDescribe()
    {
        // Leaving 0 rising at 3 a second and arriving at 1 flat: the Hermite
        // weights for the start slope and the end value, and nothing else.
        var channel = new Channel<double>(
            [
                new Key<double>(0, 0.0, Hold.Curve, slopeOut: 3),
                new Key<double>(1, 1.0, slopeIn: 0),
            ],
            Mixers.OfDouble);
        foreach (var t in new[] { 0.25, 0.5, 0.75 })
        {
            var expected = (3 * ((t * t * t) - (2 * t * t) + t)) + ((-2 * t * t * t) + (3 * t * t));
            Assert.Equal(expected, channel.At(t), 1e-12);
        }
    }

    [Fact]
    public void FlowsThroughAMiddleKeyInsteadOfStoppingAtIt()
    {
        var channel = new Channel<double>(
            [
                new Key<double>(0, 0.0, Hold.Curve),
                new Key<double>(1, 1.0, Hold.Curve),
                new Key<double>(2, 2.0),
            ],
            Mixers.OfDouble);
        Assert.Equal(1, channel.SlopeAt(1), 1e-12);
        // Either side of the middle key the value is still moving, where a
        // smooth hold would have come to rest there.
        var before = channel.At(0.99);
        var after = channel.At(1.01);
        Assert.Equal(0.02, after - before, 1e-3);
    }

    [Fact]
    public void LeavesAPeakFlatSoItNeverOvershootsIt()
    {
        var channel = new Channel<double>(
            [
                new Key<double>(0, 0.0, Hold.Curve),
                new Key<double>(1, 5.0, Hold.Curve),
                new Key<double>(2, 0.0),
            ],
            Mixers.OfDouble);
        Assert.Equal(0, channel.SlopeAt(1));
        for (var t = 0.0; t <= 2; t += 0.05)
        {
            Assert.True(channel.At(t) <= 5 + 1e-9, $"at {t}");
        }
    }

    [Fact]
    public void StartsAndFinishesAtRest()
    {
        var channel = new Channel<double>(
            [new Key<double>(0, 0.0, Hold.Curve), new Key<double>(1, 1.0)],
            Mixers.OfDouble);
        Assert.Equal(0, channel.SlopeAt(0));
        Assert.Equal(0, channel.SlopeAt(1));
        // At rest at both ends, the curve is a smoothstep.
        Assert.Equal((3 * 0.09) - (2 * 0.027), channel.At(0.3), 1e-12);
    }

    [Fact]
    public void FlattensOneAxisOfAVectorWithoutStoppingTheOthers()
    {
        var channel = new Channel<Vector3>(
            [
                new Key<Vector3>(0, new Vector3(0, 0, 0), Hold.Curve),
                new Key<Vector3>(1, new Vector3(1, 3, 0), Hold.Curve),
                new Key<Vector3>(2, new Vector3(2, 0, 0)),
            ],
            Mixers.OfVector3);
        var slope = channel.SlopeAt(1);
        Assert.Equal(1, slope.X, 1e-12);
        Assert.Equal(0, slope.Y);
    }

    [Fact]
    public void KeepsARotationARotation()
    {
        var a = Quaternion.AxisAngle(new Vector3(0, 1, 0), 0);
        var b = Quaternion.AxisAngle(new Vector3(0, 1, 0), 1);
        var c = Quaternion.AxisAngle(new Vector3(0, 1, 0), 2);
        var channel = new Channel<Quaternion>(
            [
                new Key<Quaternion>(0, a, Hold.Curve),
                new Key<Quaternion>(1, b, Hold.Curve),
                new Key<Quaternion>(2, c),
            ],
            Mixers.OfQuaternion);
        for (var t = 0.0; t <= 2; t += 0.1)
        {
            Assert.Equal(1, channel.At(t).Length, 1e-9);
        }

        Assert.Equal(b.W, channel.At(1).W, 1e-9);
    }

    [Fact]
    public void GoesTheShortWayRoundARotationWrittenTheOtherWay()
    {
        var a = Quaternion.AxisAngle(new Vector3(0, 1, 0), 0.1);
        var b = Quaternion.AxisAngle(new Vector3(0, 1, 0), -0.1);
        var negated = new Quaternion(-b.X, -b.Y, -b.Z, -b.W);
        var channel = new Channel<Quaternion>(
            [new Key<Quaternion>(0, a, Hold.Curve), new Key<Quaternion>(1, negated)],
            Mixers.OfQuaternion);
        var middle = channel.At(0.5);
        // Halfway between a small turn one way and a small turn the other is no
        // turn at all, not a half turn.
        Assert.Equal(1, Math.Abs(middle.W), 1e-6);
    }

    [Fact]
    public void TurnsSteadilyThroughKeysExactlyHalfATurnApart()
    {
        // Neither way round is shorter. Written out rather than turned from an
        // angle, because cos(pi / 2) is a hair above zero and the sign check
        // would not sit on its edge.
        var channel = new Channel<Quaternion>(
            [new Key<Quaternion>(0, new Quaternion(0, 0, 0, 1), Hold.Curve), new Key<Quaternion>(1, new Quaternion(0, 1, 0, 0))],
            Mixers.OfQuaternion);

        double Turned(double t)
        {
            var q = channel.At(t);
            Assert.Equal(1, q.Length, 1e-9);
            Assert.True(q.Y >= 0, "always the same way");
            return 2 * Math.Atan2(q.Y, q.W);
        }

        var before = Turned(0);
        for (var step = 1; step <= 20; step++)
        {
            var angle = Turned(step / 20.0);
            Assert.True(angle > before, $"at {step / 20.0}");
            Assert.Equal(Math.PI, Turned(1 - (step / 20.0)) + angle, 1e-9);
            before = angle;
        }

        Assert.Equal(Math.PI / 2, Turned(0.5), 1e-9);
    }

    [Fact]
    public void MovesAFlagTheWayALineDoes()
    {
        var channel = new Channel<bool>(
            [new Key<bool>(0, false, Hold.Curve), new Key<bool>(1, true)],
            Mixers.OfBool);
        Assert.False(channel.At(0.4));
        Assert.True(channel.At(0.6));
        Assert.Throws<InvalidOperationException>(() => channel.SlopeAt(0));
    }
}
