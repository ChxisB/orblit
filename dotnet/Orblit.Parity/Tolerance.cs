namespace Orblit.Parity;

/// <summary>
/// The rule a port is held to. Integers and bytes are compared with plain
/// equality, so this covers only doubles.
/// </summary>
public static class Tolerance
{
    public const double Relative = 1e-9;

    /// <summary>
    /// True when two doubles agree to <see cref="Relative"/>. Near zero a purely
    /// relative test would fail on 1e-17 against 0, so the scale never drops
    /// below one. Two NaNs agree, and so do two infinities of the same sign.
    /// </summary>
    public static bool Close(double expected, double actual)
    {
        if (expected.Equals(actual))
        {
            return true;
        }

        if (!double.IsFinite(expected) || !double.IsFinite(actual))
        {
            return false;
        }

        var scale = Math.Max(1.0, Math.Max(Math.Abs(expected), Math.Abs(actual)));
        return Math.Abs(expected - actual) <= Relative * scale;
    }
}
