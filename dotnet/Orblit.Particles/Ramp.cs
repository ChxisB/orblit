using Orblit.VectorMath;

namespace Orblit.Particles;

internal static class Ramp
{
    /// <summary>
    /// <paramref name="values"/> read as <paramref name="stride"/> numbers per
    /// stop, spread evenly over 0 to 1.
    /// </summary>
    public static double At(IReadOnlyList<double> values, int stride, int channel, double fraction)
    {
        var last = (values.Count / stride) - 1;
        if (last == 0)
        {
            return values[channel];
        }

        var at = Doubles.Clamp(fraction, 0.0, 1.0) * last;
        var stop = (int)Math.Min(Doubles.Floor(at), last - 1);
        var across = at - stop;
        return (values[(stop * stride) + channel] * (1 - across)) + (values[((stop + 1) * stride) + channel] * across);
    }
}
