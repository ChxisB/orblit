namespace Orblit.Effect;

/// <summary>
/// How a value gets from one end of a change to the other.
/// A curve takes a fraction of the way through and returns a fraction of the
/// way there. Linear is a machine moving something; everything else is the
/// difference between an interface that feels made and one that feels
/// generated.
/// </summary>
public delegate double Ease(double t);

/// <summary>The named curves, so a call site says what it wants rather than a formula.</summary>
public static class Eases
{
    /// <summary>Straight through. For anything mechanical, and for a value that has to meet another one exactly.</summary>
    public static double Linear(double t) => t;

    /// <summary>Slow away, fast arrival. Something falling, or being dropped.</summary>
    public static double InQuad(double t) => t * t;

    public static double InCubic(double t) => t * t * t;

    /// <summary>
    /// Fast away, slow arrival. The default for anything a person asked for:
    /// it answers immediately and settles rather than stopping dead.
    /// </summary>
    public static double OutQuad(double t) => 1 - ((1 - t) * (1 - t));

    public static double OutCubic(double t) => 1 - Math.Pow(1 - t, 3);

    /// <summary>Slow at both ends. For something moving of its own accord.</summary>
    public static double InOutQuad(double t) => t < 0.5 ? 2 * t * t : 1 - (Math.Pow((-2 * t) + 2, 2) / 2);

    public static double InOutCubic(double t) => t < 0.5 ? 4 * t * t * t : 1 - (Math.Pow((-2 * t) + 2, 3) / 2);

    /// <summary>Overshoots and comes back. A small exaggeration reads as weight.</summary>
    public static double OutBack(double t)
    {
        const double overshoot = 1.70158;
        var u = t - 1;
        return 1 + ((overshoot + 1) * u * u * u) + (overshoot * u * u);
    }

    /// <summary>Settles with a wobble. Sparingly: it is charming once and tiring twice.</summary>
    public static double OutElastic(double t)
    {
        if (t <= 0)
        {
            return 0;
        }

        if (t >= 1)
        {
            return 1;
        }

        const double period = 2 * Math.PI / 3;
        return (Math.Pow(2, -10 * t) * Math.Sin(((t * 10) - 0.75) * period)) + 1;
    }

    /// <summary>Lands, bounces, lands.</summary>
    public static double OutBounce(double t)
    {
        const double n = 7.5625;
        const double d = 2.75;
        if (t < 1 / d)
        {
            return n * t * t;
        }

        if (t < 2 / d)
        {
            var u = t - (1.5 / d);
            return (n * u * u) + 0.75;
        }

        if (t < 2.5 / d)
        {
            var u = t - (2.25 / d);
            return (n * u * u) + 0.9375;
        }

        var last = t - (2.625 / d);
        return (n * last * last) + 0.984375;
    }

    /// <summary>Any curve, run backwards.</summary>
    public static Ease Reversed(Ease ease) => t => 1 - ease(1 - t);

    /// <summary>
    /// Any curve, out and back within one run.
    /// For a pulse, a flash, a nudge, where the value has to end where it
    /// started and writing that as two effects is twice the work and twice the
    /// chance of them disagreeing about the middle.
    /// </summary>
    public static Ease ThereAndBack(Ease ease) => t => t < 0.5 ? ease(t * 2) : ease((1 - t) * 2);
}
