namespace Orblit.Noise;

// The arithmetic every field shares. Written out here rather than taken from a
// library, because it has to give the same answer on every machine and for
// ever: a field that changed with the runtime would be a world that
// regenerated differently after an upgrade.
internal static class Lattice
{
    // Dart's double.floor() throws on NaN and infinity. A cast would give long.MinValue.
    public static long Floor(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "A coordinate must be finite.");
        }

        return (long)Math.Floor(value);
    }

    // Smoothstep's bigger brother, with zero slope and zero curvature at both ends.
    // The curvature is what matters: plain smoothstep leaves a discontinuity in
    // the second derivative at every lattice point, which shows in anything
    // derived from the slope, such as a normal map or a flow.
    public static double Fade(double t) => t * t * t * (t * (t * 6 - 15) + 10);

    public static double Mix(double a, double b, double t) => a + (b - a) * t;

    // Twelve directions to the edge midpoints of a cube. Not the axes: axis
    // gradients line every extreme up with the lattice and put a visible cross
    // through the field.
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Maintainability", "CA1502:Avoid excessive complexity",
        Justification = "F2: a list of cases is data.")]
    public static double Dot(long gradient, double x, double y, double z) =>
        (gradient % 12) switch
        {
            0 => x + y,
            1 => -x + y,
            2 => x - y,
            3 => -x - y,
            4 => x + z,
            5 => -x + z,
            6 => x - z,
            7 => -x - z,
            8 => y + z,
            9 => -y + z,
            10 => y - z,
            _ => -y - z,
        };

    // One lattice point to one number between -1 and 1, always the same one.
    public static double ValueAt(long seed, long x, long y, long z) =>
        UnitAt(seed, x, y, z, 0) * 2 - 1;

    public static long GradientAt(long seed, long x, long y, long z) => Hash(seed, x, y, z, 7) % 12;

    // One lattice point and a channel to a number between 0 and 1.
    // The divisor is a double. Integer division here would give 0 every time.
    public static double UnitAt(long seed, long x, long y, long z, long channel) =>
        (Hash(seed, x, y, z, channel) & 0xFFFFFF) / 16777216.0;

    // A hash of four integers. The constants are large odd primes, and the
    // shifts mix the high bits back down where the low bits can see them.
    // Dart's int wraps at 64 bits. A large coordinate overflows the products,
    // so this stays unchecked whatever the project setting is.
    public static long Hash(long seed, long x, long y, long z, long channel)
    {
        unchecked
        {
            var h = seed & 0x7FFFFFFF;
            h = (h * 374761393 + x * 668265263) & 0x7FFFFFFF;
            h = (h * 2246822519 + y * 3266489917) & 0x7FFFFFFF;
            h = (h * 668265263 + z * 374761393) & 0x7FFFFFFF;
            h = (h * 2654435761 + channel * 40503) & 0x7FFFFFFF;
            h ^= h >> 15;
            h = (h * 2246822519) & 0x7FFFFFFF;
            h ^= h >> 13;
            return h & 0x7FFFFFFF;
        }
    }
}
