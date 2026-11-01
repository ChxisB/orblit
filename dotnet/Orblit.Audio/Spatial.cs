using System.Diagnostics;
using Orblit.VectorMath;

namespace Orblit.Audio;

public static class Acoustics
{
    /// <summary>Metres a second, in air.</summary>
    public const double SpeedOfSound = 343;
}

/// <summary>
/// A position that knows how fast it is moving. Velocity comes from the last
/// two positions, because the physics engine is not always there and is not
/// the only thing that moves a camera.
/// </summary>
public abstract class Motion
{
    private protected Motion()
    {
    }

    public Vector3 Position { get; set; } = Vector3.Zero();

    public Vector3 Velocity { get; set; } = Vector3.Zero();

    /// <summary>Moves to <paramref name="to"/> over <paramref name="seconds"/>. A frame of no time gives no velocity.</summary>
    public void MoveTo(Vector3 to, double seconds)
    {
        Velocity = seconds > 0 ? (to - Position) / seconds : Vector3.Zero();
        Position = to.Clone();
    }

    /// <summary>Jumps to <paramref name="to"/>. A cut is not a speed, so Doppler hears none.</summary>
    public void Teleport(Vector3 to)
    {
        Position = to.Clone();
        Velocity = Vector3.Zero();
    }

    /// <summary>
    /// <see cref="AudioPlayer.Advance"/> calls this after every frame. A velocity is
    /// good for the frame its move was made in, so something that is no longer
    /// moved is no longer heard moving.
    /// </summary>
    public void Settle() => Velocity = Vector3.Zero();
}

/// <summary>Where the sound is heard from. It faces down -Z with +Y up until told otherwise.</summary>
public sealed class Listener : Motion
{
    public Vector3 Forward { get; set; } = new(0, 0, -1);

    public Vector3 Up { get; set; } = new(0, 1, 0);
}

public enum FalloffCurve
{
    Linear,
    Inverse,
}

/// <summary>
/// How a sound fades with distance. Full gain up to <see cref="Near"/>, silent
/// from <see cref="Far"/>, and <see cref="Curve"/> between. Both curves reach
/// exactly 0 at <see cref="Far"/>, so a backend can stop mixing a voice that is
/// out of range.
/// </summary>
public sealed class Falloff
{
    public Falloff(double near = 1, double far = 50, FalloffCurve curve = FalloffCurve.Inverse)
    {
        if (!(near > 0 && far > near && double.IsFinite(far)))
        {
            throw new ArgumentException("near must be above 0 and far above near, finite.");
        }

        Near = near;
        Far = far;
        Curve = curve;
    }

    public double Near { get; }

    public double Far { get; }

    public FalloffCurve Curve { get; }

    public double GainAt(double distance)
    {
        if (distance <= Near)
        {
            return 1;
        }

        if (distance >= Far)
        {
            return 0;
        }

        return Curve switch
        {
            FalloffCurve.Linear => (Far - distance) / (Far - Near),
            FalloffCurve.Inverse => ((Near / distance) - (Near / Far)) / (1 - (Near / Far)),
            _ => throw new UnreachableException(),
        };
    }
}

/// <summary>
/// A sound that is louder in front. Angles are radians either side of the way
/// the emitter faces. Full gain inside <see cref="Inner"/>,
/// <see cref="OuterGain"/> past <see cref="Outer"/> and a straight line between.
/// </summary>
public sealed class Cone
{
    public Cone(double inner, double outer, double outerGain = 0)
    {
        if (!(inner >= 0 && outer >= inner && outer <= Math.PI))
        {
            throw new ArgumentException("Need 0 <= inner <= outer <= pi.");
        }

        if (!(outerGain >= 0 && outerGain <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(outerGain), outerGain, "must be from 0 to 1");
        }

        Inner = inner;
        Outer = outer;
        OuterGain = outerGain;
    }

    public double Inner { get; }

    public double Outer { get; }

    public double OuterGain { get; }

    public double GainAt(double angle)
    {
        if (angle <= Inner)
        {
            return 1;
        }

        if (angle >= Outer)
        {
            return OuterGain;
        }

        return 1 + ((OuterGain - 1) * (angle - Inner) / (Outer - Inner));
    }
}

/// <summary>A place a sound comes from. It owns how that sound is shaped by where the <see cref="Listener"/> is.</summary>
public sealed class Emitter : Motion
{
    public Emitter(
        Vector3 position,
        Falloff? falloff = null,
        Cone? cone = null,
        Vector3? forward = null,
        double doppler = 1)
    {
        if (!double.IsFinite(doppler) || doppler < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(doppler), doppler, "must be zero or more");
        }

        Falloff = falloff ?? new Falloff();
        Cone = cone;
        Forward = forward?.Clone() ?? new Vector3(0, 0, -1);
        Doppler = doppler;
        Position = position.Clone();
    }

    public Falloff Falloff { get; }

    public Cone? Cone { get; }

    /// <summary>The way the emitter faces, for its <see cref="Cone"/>.</summary>
    public Vector3 Forward { get; set; }

    /// <summary>How much of the real pitch shift is heard. 0 is none and 1 is real.</summary>
    public double Doppler { get; }

    /// <summary>
    /// How a listener hears this emitter: the gain from distance and cone, the
    /// pitch from Doppler and the pan from direction.
    /// </summary>
    public Level HeardBy(Listener listener)
    {
        var toEmitter = Position - listener.Position;
        var distance = toEmitter.Length;
        var gain = Falloff.GainAt(distance);

        // No direction when they coincide, so no cone, pan or Doppler either.
        if (distance == 0)
        {
            return new Level(gain: gain);
        }

        var direction = toEmitter / distance;
        return new Level(
            gain: gain * ConeGain(direction),
            pitch: Pitch(listener, direction),
            pan: Pan(listener, direction, distance));
    }

    private double ConeGain(Vector3 direction)
    {
        if (Cone is null)
        {
            return 1;
        }

        var facing = Forward.Normalized().Dot(-direction);
        return Cone.GainAt(Math.Acos(Doubles.Clamp(facing, -1.0, 1.0)));
    }

    /// <summary>
    /// A speed towards the other side is clamped to half the speed of sound,
    /// which keeps the ratio between 1/3 and 3 for any <see cref="Doppler"/>.
    /// </summary>
    private double Pitch(Listener listener, Vector3 direction)
    {
        var limit = Acoustics.SpeedOfSound / 2 / Doppler;
        var listenerToward = Math.Min(limit, Math.Max(-limit, listener.Velocity.Dot(direction)));
        var emitterToward = Math.Min(limit, Math.Max(-limit, -Velocity.Dot(direction)));
        return (Acoustics.SpeedOfSound + (Doppler * listenerToward)) / (Acoustics.SpeedOfSound - (Doppler * emitterToward));
    }

    /// <summary>
    /// Inside <see cref="Falloff.Near"/> the sound is taken to be at the listener,
    /// so the pan eases to the middle instead of swinging across as it passes through.
    /// </summary>
    private double Pan(Listener listener, Vector3 direction, double distance)
    {
        var right = listener.Forward.Cross(listener.Up).Normalized();
        return direction.Dot(right) * Math.Min(1.0, distance / Falloff.Near);
    }
}
