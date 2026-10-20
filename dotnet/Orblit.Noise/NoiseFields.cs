namespace Orblit.Noise;

/// <summary>
/// A field of numbers that varies smoothly and never changes. The value at a
/// point depends only on the point and the seed, not on what was asked before,
/// so a world generated on one machine is the world generated on every other.
/// Nothing here allocates or holds state. A field is a function.
/// </summary>
public abstract record NoiseField
{
    /// <summary>The value at a point, between -1 and 1.</summary>
    public abstract double At(double x, double y = 0, double z = 0);

    /// <summary>The value scaled and shifted to run from 0 to 1.</summary>
    public double Unit(double x, double y = 0, double z = 0) => At(x, y, z) * 0.5 + 0.5;
}

/// <summary>
/// Smooth noise from a value at every whole coordinate. Cheap and slightly
/// blocky: the extremes sit on the lattice points.
/// </summary>
public sealed record ValueNoise(long Seed = 0) : NoiseField
{
    public override double At(double x, double y = 0, double z = 0)
    {
        var xi = Lattice.Floor(x);
        var yi = Lattice.Floor(y);
        var zi = Lattice.Floor(z);
        var xf = Lattice.Fade(x - xi);
        var yf = Lattice.Fade(y - yi);
        var zf = Lattice.Fade(z - zi);

        double Corner(int dx, int dy, int dz) => Lattice.ValueAt(Seed, xi + dx, yi + dy, zi + dz);

        var x00 = Lattice.Mix(Corner(0, 0, 0), Corner(1, 0, 0), xf);
        var x10 = Lattice.Mix(Corner(0, 1, 0), Corner(1, 1, 0), xf);
        var x01 = Lattice.Mix(Corner(0, 0, 1), Corner(1, 0, 1), xf);
        var x11 = Lattice.Mix(Corner(0, 1, 1), Corner(1, 1, 1), xf);

        return Lattice.Mix(Lattice.Mix(x00, x10, yf), Lattice.Mix(x01, x11, yf), zf);
    }
}

/// <summary>
/// Smooth noise from a direction at every whole coordinate. The value is nought
/// on every lattice point and the variation happens between them, so the grid
/// stops being visible.
/// </summary>
public sealed record GradientNoise(long Seed = 0) : NoiseField
{
    public override double At(double x, double y = 0, double z = 0)
    {
        var xi = Lattice.Floor(x);
        var yi = Lattice.Floor(y);
        var zi = Lattice.Floor(z);
        var xd = x - xi;
        var yd = y - yi;
        var zd = z - zi;
        var u = Lattice.Fade(xd);
        var v = Lattice.Fade(yd);
        var w = Lattice.Fade(zd);

        double Corner(int dx, int dy, int dz) => Lattice.Dot(
            Lattice.GradientAt(Seed, xi + dx, yi + dy, zi + dz),
            xd - dx,
            yd - dy,
            zd - dz);

        var x00 = Lattice.Mix(Corner(0, 0, 0), Corner(1, 0, 0), u);
        var x10 = Lattice.Mix(Corner(0, 1, 0), Corner(1, 1, 0), u);
        var x01 = Lattice.Mix(Corner(0, 0, 1), Corner(1, 0, 1), u);
        var x11 = Lattice.Mix(Corner(0, 1, 1), Corner(1, 1, 1), u);

        // The dot products run to about 0.866 at their widest, so this brings the
        // field back to the -1..1 the contract promises.
        return Lattice.Mix(Lattice.Mix(x00, x10, v), Lattice.Mix(x01, x11, v), w) * 1.1547;
    }
}

/// <summary>
/// Noise that joins up with itself, worked out by wrapping the lattice rather
/// than by fading two copies together, which leaves a seam. The period is in
/// whole numbers, because the lattice is at whole coordinates.
/// </summary>
public sealed record TilingNoise(NoiseField Inner, long Period = 16) : NoiseField
{
    public override double At(double x, double y = 0, double z = 0)
    {
        var p = Period <= 0 ? 1 : Period;
        return Inner.At(Wrap(x, p), Wrap(y, p), Wrap(z, p));
    }

    // C#'s % keeps the sign of the dividend and Dart's does not, so the
    // negative case is corrected here.
    private static double Wrap(double value, long period)
    {
        var wrapped = value % period;
        return wrapped < 0 ? wrapped + period : wrapped;
    }
}

/// <summary>
/// Several octaves of a field added together. Each is Lacunarity times the
/// frequency and Gain times the amplitude of the one before. Past about eight
/// octaves the detail is finer than anything being sampled.
/// </summary>
public sealed record FractalNoise(
    NoiseField Inner,
    long Octaves = 4,
    double Gain = 0.5,
    double Lacunarity = 2.0) : NoiseField
{
    public override double At(double x, double y = 0, double z = 0)
    {
        var total = 0.0;
        var amplitude = 1.0;
        var frequency = 1.0;
        var most = 0.0;

        for (long i = 0; i < Octaves; i++)
        {
            total += Inner.At(x * frequency, y * frequency, z * frequency) * amplitude;
            most += amplitude;
            amplitude *= Gain;
            frequency *= Lacunarity;
        }

        // Divided by what it could have reached rather than by the octave count,
        // so the field still runs to the edges of -1..1 whatever the gain is.
        return most == 0 ? 0 : total / most;
    }
}

/// <summary>
/// The absolute value of a field, turned upside down. Where a smooth field
/// crosses nought this has a crease, so a fractal built on it has ridges.
/// </summary>
public sealed record RidgedNoise(NoiseField Inner) : NoiseField
{
    public override double At(double x, double y = 0, double z = 0) =>
        1 - 2 * Math.Abs(Inner.At(x, y, z));
}

/// <summary>
/// Distance to the nearest of a scattering of points. Gives cracked mud,
/// scales, stone and foam: shapes that come from packing, not from smoothness.
/// </summary>
public sealed record CellNoise(long Seed = 0) : NoiseField
{
    public override double At(double x, double y = 0, double z = 0)
    {
        var xi = Lattice.Floor(x);
        var yi = Lattice.Floor(y);
        var zi = Lattice.Floor(z);
        var nearest = double.PositiveInfinity;

        // The twenty-six cells around this one, and this one. A point in a
        // further cell cannot be nearer than one in a neighbour, so this is the
        // whole search and not an approximation of it.
        for (var dz = -1; dz <= 1; dz++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var cx = xi + dx;
                    var cy = yi + dy;
                    var cz = zi + dz;
                    var px = cx + Lattice.UnitAt(Seed, cx, cy, cz, 0);
                    var py = cy + Lattice.UnitAt(Seed, cx, cy, cz, 1);
                    var pz = cz + Lattice.UnitAt(Seed, cx, cy, cz, 2);
                    var distance = (px - x) * (px - x) + (py - y) * (py - y) + (pz - z) * (pz - z);
                    if (distance < nearest)
                    {
                        nearest = distance;
                    }
                }
            }
        }

        // Nought at a point and rising away from it, brought into -1..1. It does
        // not need clamping in practice, but a caller that scaled the field
        // would notice, so it is clamped.
        var away = Math.Sqrt(nearest);
        return Math.Clamp(away * 2 - 1, -1.0, 1.0);
    }
}
