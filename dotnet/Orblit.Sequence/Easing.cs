using System.Diagnostics;
using Orblit.VectorMath;

namespace Orblit.Sequence;

/// <summary>
/// The shape of a change over its own span. Nought to one in, nought to one
/// out. Everything that eases in this package goes through one of these.
/// </summary>
public enum Easing
{
    /// <summary>No shaping at all.</summary>
    Linear,

    /// <summary>Starts slowly. Something getting under way.</summary>
    In,

    /// <summary>Ends slowly. Something arriving.</summary>
    Out,

    /// <summary>Both. The default.</summary>
    InOut,

    /// <summary>Smoothstep, gentler than <see cref="InOut"/> at the ends and quicker through the middle.</summary>
    Smooth,

    /// <summary>Overshoots and comes back. A snap, a pop, a lid closing.</summary>
    Back,

    /// <summary>Runs past and settles. Weight arriving.</summary>
    Bounce,
}

/// <summary>The top level <c>ease</c>.</summary>
public static class Easings
{
    // The usual constants: enough overshoot to read as a snap without looking like a mistake.
    private const double Overshoot = 1.70158;
    private const double Scaled = Overshoot + 1;
    private const double BounceN = 7.5625;
    private const double BounceD = 2.75;

    /// <summary>Applies an easing to a fraction. One outside 0 to 1 is clamped, and NaN counts as 1.</summary>
    public static double Ease(Easing shape, double at)
    {
        var t = Doubles.Clamp(at, 0.0, 1.0);
        return shape switch
        {
            Easing.Linear => t,
            Easing.In => t * t,
            Easing.Out => 1 - ((1 - t) * (1 - t)),
            Easing.InOut => InOut(t),
            Easing.Smooth => t * t * (3 - (2 * t)),
            Easing.Back => 1 + (Scaled * Math.Pow(t - 1, 3)) + (Overshoot * Math.Pow(t - 1, 2)),
            Easing.Bounce => Bounce(t),
            _ => throw new UnreachableException(),
        };
    }

    private static double InOut(double t) => t < 0.5 ? 2 * t * t : 1 - (Math.Pow((-2 * t) + 2, 2) / 2);

    private static double Bounce(double t)
    {
        if (t < 1 / BounceD)
        {
            return BounceN * t * t;
        }

        if (t < 2 / BounceD)
        {
            var x = t - (1.5 / BounceD);
            return (BounceN * x * x) + 0.75;
        }

        if (t < 2.5 / BounceD)
        {
            var x = t - (2.25 / BounceD);
            return (BounceN * x * x) + 0.9375;
        }

        var last = t - (2.625 / BounceD);
        return (BounceN * last * last) + 0.984375;
    }
}
