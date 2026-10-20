namespace Orblit.Camera;

/// <summary>
/// Damping stated in seconds, roughly how long a camera takes to close most of a gap.
/// A per-frame factor depends on the framerate, and a camera that drifts when the
/// machine is busy is a bug nobody can reproduce. The exponential form settles the
/// same way at any timestep.
/// </summary>
public static class Smoothing
{
    /// <summary>How much of the way to a target one frame should travel.</summary>
    public static double Factor(double damping, double delta)
    {
        if (damping <= 0)
        {
            return 1;
        }

        if (delta <= 0)
        {
            return 0;
        }

        return 1 - Math.Exp(-delta / damping);
    }

    /// <summary>Moves <paramref name="current"/> towards <paramref name="target"/> with <paramref name="damping"/> seconds of lag.</summary>
    public static double Damp(double current, double target, double damping, double delta) =>
        current + ((target - current) * Factor(damping, delta));
}
