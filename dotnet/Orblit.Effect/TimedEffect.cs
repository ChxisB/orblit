using Orblit.VectorMath;

namespace Orblit.Effect;

/// <summary>
/// A change stated as a function of time.
/// <b>Sampled, not stepped.</b> An effect is asked what it looks like at a
/// moment, rather than being advanced by a frame's worth. That is the same
/// choice the sequencer makes, and for the same reasons: it can be scrubbed
/// backwards, it gives the same answer at the same moment however the frames
/// fell, and a test can ask about the middle without running the beginning.
/// The cost is that an effect cannot depend on where the thing currently is.
/// <c>MoveBy</c> is expressible and "move towards whatever is nearest" is not.
/// That is steering, and it lives in a different package on purpose.
/// </summary>
public abstract class TimedEffect
{
    /// <summary>How long it lasts, in seconds. Infinite for one that never ends.</summary>
    public abstract double Duration { get; }

    /// <summary>
    /// What it has done by <paramref name="seconds"/> in. Before it starts and
    /// after it ends the answer is still defined: an effect holds its end state
    /// rather than snapping back, because a sequence asks its earlier members
    /// what they finished as while a later one runs.
    /// </summary>
    public abstract Change At(double seconds);

    /// <summary>Whether it has finished by <paramref name="seconds"/>.</summary>
    public virtual bool DoneBy(double seconds) => seconds >= Duration;
}

/// <summary>
/// Nothing, for a while. The join in a sequence, and the only way to say
/// "then, after a pause".
/// </summary>
public sealed class Wait(double duration) : TimedEffect
{
    public override double Duration { get; } = duration;

    public override Change At(double seconds) => Change.None;
}

/// <summary>
/// A straight change from nothing to something. The base every concrete
/// effect below is: a curve over a fraction, and a quantity scaled by it.
/// Public because the effects that extend it are, and closed to other
/// assemblies by its constructor.
/// </summary>
public abstract class Tween : TimedEffect
{
    internal Tween(double duration, Ease ease)
    {
        Duration = duration;
        Ease = ease;
    }

    public override double Duration { get; }

    public Ease Ease { get; }

    /// <summary>How far through, curved, clamped at both ends.</summary>
    public double Progress(double seconds)
    {
        if (Duration <= 0)
        {
            return 1;
        }

        return Ease(Doubles.Clamp(seconds / Duration, 0.0, 1.0));
    }
}

/// <summary>Moved by an offset.</summary>
public sealed class MoveBy(Vector3 offset, double duration, Ease? ease = null)
    : Tween(duration, ease ?? Eases.OutQuad)
{
    public Vector3 Offset { get; } = offset;

    public override Change At(double seconds) => new(move: Offset * Progress(seconds));
}

/// <summary>Turned by an angle about an axis, in radians.</summary>
public sealed class TurnBy(double angle, double duration, Vector3? axis = null, Ease? ease = null)
    : Tween(duration, ease ?? Eases.InOutQuad)
{
    public double Angle { get; } = angle;

    public Vector3 Axis { get; } = axis ?? new Vector3(0, 1, 0);

    public override Change At(double seconds) =>
        new(turn: Quaternion.AxisAngle(Axis.Normalized(), Angle * Progress(seconds)));
}

/// <summary>Grown or shrunk towards a multiplier.</summary>
public sealed class GrowTo(Vector3 factor, double duration, Ease? ease = null)
    : Tween(duration, ease ?? Eases.OutBack)
{
    public Vector3 Factor { get; } = factor;

    public override Change At(double seconds)
    {
        var t = Progress(seconds);

        // From one towards the factor, so a half-finished grow is halfway there
        // rather than half the size.
        return new Change(
            grow: new Vector3(
                1 + ((Factor.X - 1) * t),
                1 + ((Factor.Y - 1) * t),
                1 + ((Factor.Z - 1) * t)));
    }
}

/// <summary>Faded towards an opacity.</summary>
public sealed class FadeTo(double opacity, double duration, Ease? ease = null)
    : Tween(duration, ease ?? Eases.Linear)
{
    public double Opacity { get; } = opacity;

    public override Change At(double seconds) => new(fade: 1 + ((Opacity - 1) * Progress(seconds)));
}

/// <summary>Tinted towards a colour.</summary>
public sealed class TintTo(Vector4 colour, double duration, Ease? ease = null)
    : Tween(duration, ease ?? Eases.Linear)
{
    public Vector4 Colour { get; } = colour;

    public override Change At(double seconds)
    {
        var t = Progress(seconds);
        return new Change(
            tint: new Vector4(
                1 + ((Colour.X - 1) * t),
                1 + ((Colour.Y - 1) * t),
                1 + ((Colour.Z - 1) * t),
                1 + ((Colour.W - 1) * t)));
    }
}

/// <summary>
/// One after another. Each member's end is held while the ones after it run,
/// so the whole is the sum of everything that has happened rather than only of
/// what is happening. Without that, a move followed by a turn would snap back
/// to the start of the move the moment the turn began.
/// </summary>
public sealed class Then(IReadOnlyList<TimedEffect> steps) : TimedEffect
{
    public IReadOnlyList<TimedEffect> Steps { get; } = steps;

    public override double Duration
    {
        get
        {
            var total = 0.0;
            foreach (var step in Steps)
            {
                total += step.Duration;
            }

            return total;
        }
    }

    public override Change At(double seconds)
    {
        var change = Change.None;
        var spent = 0.0;

        foreach (var step in Steps)
        {
            // Every member that has finished contributes its end; the one running
            // contributes where it is; the ones after contribute nothing.
            if (seconds >= spent + step.Duration)
            {
                change = change.And(step.At(step.Duration));
            }
            else if (seconds > spent)
            {
                return change.And(step.At(seconds - spent));
            }
            else
            {
                return change;
            }

            spent += step.Duration;
        }

        return change;
    }
}

/// <summary>All at once.</summary>
public sealed class Both(IReadOnlyList<TimedEffect> parts) : TimedEffect
{
    public IReadOnlyList<TimedEffect> Parts { get; } = parts;

    public override double Duration
    {
        get
        {
            var longest = 0.0;
            foreach (var part in Parts)
            {
                longest = part.Duration > longest ? part.Duration : longest;
            }

            return longest;
        }
    }

    public override Change At(double seconds)
    {
        var change = Change.None;
        foreach (var part in Parts)
        {
            change = change.And(part.At(seconds));
        }

        return change;
    }
}

/// <summary>The same thing again, a number of times or for ever. A null <paramref name="times"/> never stops.</summary>
public sealed class Again(TimedEffect inner, long? times = null) : TimedEffect
{
    public TimedEffect Inner { get; } = inner;

    public long? Times { get; } = times;

    public override double Duration => Times is null ? double.PositiveInfinity : Inner.Duration * Times.Value;

    public override Change At(double seconds)
    {
        if (Inner.Duration <= 0)
        {
            return Inner.At(0);
        }

        if (Times is not null && seconds >= Duration)
        {
            return Inner.At(Inner.Duration);
        }

        // The modulo is what makes a repeat cost nothing to scrub to: the
        // thousandth run is as cheap to ask about as the first.
        return Inner.At(Doubles.Mod(seconds, Inner.Duration));
    }
}

/// <summary>Out, and back the way it came.</summary>
public sealed class OutAndBack(TimedEffect inner) : TimedEffect
{
    public TimedEffect Inner { get; } = inner;

    public override double Duration => Inner.Duration * 2;

    public override Change At(double seconds)
    {
        var half = Inner.Duration;
        if (seconds <= half)
        {
            return Inner.At(seconds);
        }

        if (seconds >= Duration)
        {
            return Inner.At(0);
        }

        return Inner.At(half - (seconds - half));
    }
}

/// <summary>Nothing until it starts.</summary>
public sealed class After(double delay, TimedEffect inner) : TimedEffect
{
    public double Delay { get; } = delay;

    public TimedEffect Inner { get; } = inner;

    public override double Duration => Delay + Inner.Duration;

    public override Change At(double seconds) => seconds <= Delay ? Change.None : Inner.At(seconds - Delay);
}

/// <summary>
/// A shake, as a function of time. Worked out from the clock rather than
/// rolled, so the same second of the same shake looks the same twice, which is
/// what makes a replay a replay and lets a test assert on it.
/// </summary>
public sealed class Shake(double strength, double duration, double rate = 24, long seed = 0, Ease? ease = null)
    : Tween(duration, ease ?? Eases.Linear)
{
    /// <summary>How far it moves, in metres.</summary>
    public double Strength { get; } = strength;

    /// <summary>How many times a second.</summary>
    public double Rate { get; } = rate;

    public long Seed { get; } = seed;

    public override Change At(double seconds)
    {
        if (seconds >= Duration)
        {
            return Change.None;
        }

        // Fading out over its life, so a shake ends rather than stops.
        var left = 1 - Progress(seconds);
        var step = seconds * Rate;
        return new Change(
            move: new Vector3(
                Wobble(Seed, step) * Strength * left,
                Wobble(unchecked(Seed + 977), step) * Strength * left,
                Wobble(unchecked(Seed + 1699), step) * Strength * left));
    }

    private static double Wobble(long seed, double t)
    {
        var floor = Doubles.Floor(t);
        var f = t - floor;
        var a = Point(seed, floor);
        var b = Point(seed, unchecked(floor + 1));
        var blend = f * f * (3 - (2 * f));
        return a + ((b - a) * blend);
    }

    private static double Point(long seed, long at)
    {
        // The multiplies wrap in 64 bits, and the mask keeps the result positive.
        var h = unchecked((at * 374761393) + (seed * 668265263)) & 0x7FFFFFFF;
        h = unchecked((h ^ (h >> 13)) * 1274126177) & 0x7FFFFFFF;
        h ^= h >> 16;
        return ((h % 20001) / 10000.0) - 1.0;
    }
}
