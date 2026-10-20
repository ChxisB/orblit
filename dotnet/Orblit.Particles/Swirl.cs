using Orblit.Noise;
using Orblit.VectorMath;

namespace Orblit.Particles;

internal static class Swirl
{
    /// <summary>
    /// Writes a smooth vector for the point to <paramref name="output"/>. Three
    /// offsets of one field give three components that are not the same number.
    /// The pure and the stepped form both read it, so they push a particle the
    /// same way.
    /// </summary>
    // F2: the point is three numbers, and both callers pass them as they are.
    public static void Write(GradientNoise noise, double x, double y, double z, Vector3 output) =>
        output.SetValues(noise.At(x, y, z), noise.At(x + 31.7, y, z), noise.At(x + 67.3, y, z));
}
